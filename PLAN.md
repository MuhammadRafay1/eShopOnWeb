# PLAN — PayPal payments & saved cards for eShopOnWeb

This is the build plan for adding **PayPal card payments** and **saved cards (vault)** to
eShopOnWeb, exposed as JWT-authenticated endpoints on `src/PublicApi`. It is additive: the
existing catalog/basket/order/checkout flow is untouched.

The plan has three parts:

- **Part 1 — Integration design** (eShopOnWeb-side: domain, persistence, endpoints, config,
  idempotency, reconciliation, security, error mapping).
- **Part 2 — PayPal SDK Contract Sheet** (the *sole* PayPal reference: exact signatures, wire
  names, envelope shapes, error accessors, enums — grounded in the bundled SDK map). Treat its
  contracts as authoritative; do **not** re-derive PayPal facts from memory or the web.
- **Part 3 — Build order, DI wiring, and verification guide.**

### How to use this plan (read first)

1. This repo uses the **paypal-sdk plugin** as the only source of PayPal knowledge. Before
   writing any integration code, **re-run the `integrate-paypal` flow**: spawn the `paypal-sdk`
   agent once, and **load every `dotnet-*` companion skill** named in Part 2 §7 REQUIRED
   READING. This plan carries the *contract facts*; the companion skills carry the *usage
   hazards* (HttpClient lifetime, retry semantics, the JSON error-boundary traps) that are not
   reproduced here.
2. Every PayPal signature / wire name / enum / error accessor you write must come from Part 2
   (or a fresh follow-up to the warm `paypal-sdk` agent) — never from memory.
3. Any SDK compile error (`CS1061`/`CS0117`/`CS0234`/`CS1503`/… on `PayPalServerSdk.*`) →
   hand it to the warm `paypal-sdk` agent, do not guess-fix.

### Environment facts (verified on this machine)

- SDKs installed: .NET 8.0.423 **and** .NET 10 (10.0.301/302). ASP.NET Core runtime **8.0.28 /
  8.0.29 present** (and 10.x). `global.json` pins `8.0.x` with `rollForward: latestFeature`.
  The task warns the target machine may have only .NET 10; **run with
  `DOTNET_ROLL_FORWARD=Major`** so it works either way (harmless when 8.0 is present).
- No SQL LocalDB: **always run with `UseOnlyInMemoryDatabase=true`**. Consequence: data is
  per-process and reset on restart, and EF migrations are ignored by the in-memory provider.
  Create → pay → fulfil → refund within the **same** run.
- **In-memory stores are per host.** PublicApi has its own store, isolated from Web. The whole
  payment flow must be drivable through PublicApi alone (hence `POST /api/orders`).
- PublicApi listens on **`https://localhost:37663`** (http 37664). Port block: **37660–37679**
  (`APP_PORT_BLOCK_BASE=37660`, size 20). `launchSettings.json` already targets these.
- Seeded identity (from `AppIdentityDbContextSeed`): shopper **`demouser@microsoft.com`** and
  admin/operator **`admin@microsoft.com`**, both password **`Pass@word1`** (constant
  `AuthorizationConstants.DEFAULT_PASSWORD`). Admin is in role `Administrators`.
- Central Package Management is ON (`Directory.Packages.props`, `ManagePackageVersionsCentrally=true`).

---
---

# PART 1 — Integration design

## 1.1 Layering & where code goes

Respect the existing clean-architecture layering:

| Layer | Project | What we add | May reference the PayPal SDK? |
|---|---|---|---|
| Domain | `src/ApplicationCore` | Entities (Order changes, `Payment`, `PaymentRefund`, `SavedCard`), enums, `IPaymentGateway` + its **app-level DTOs**, app-service interfaces + implementations, specifications, domain exceptions | **No** — SDK-free |
| Infrastructure | `src/Infrastructure` | `PayPalPaymentGateway` (implements `IPaymentGateway` with the SDK), `AmountFormatter`, EF `IEntityTypeConfiguration`s + `DbSet`s, `AddPayPalIntegration` DI extension | **Yes** — the only project that references `PayPalServerSdk` |
| API | `src/PublicApi` | `IEndpoint` classes + request/response DTOs, env→config bridge, options + DI wiring, `ExceptionMiddleware` additions | No direct SDK use (calls app services) |

Rationale: the SDK dependency is quarantined in Infrastructure behind the `IPaymentGateway`
abstraction (which returns our own DTOs, never SDK types). ApplicationCore stays pure and
unit-testable; PublicApi stays a thin HTTP surface. This mirrors how `IOrderService` /
`OrderService` are already split (interface in ApplicationCore, no SDK anywhere).

**PublicApi does NOT reference the SDK** — it references ApplicationCore + Infrastructure and
resolves `IPaymentGateway` / the app services from DI.

## 1.2 Authorization matrix (from the task — encode exactly)

The task states: *"Fulfil, cancel and reconciliation are operator actions: restrict them to
the administrator role … Every other endpoint is shopper-scoped and acts only on the caller's
own data."*

| Endpoint | Verb + route | Role | Scope |
|---|---|---|---|
| Place order | `POST /api/orders` | any authenticated | creates for caller (buyerId = token) |
| Pay (authorize) | `POST /api/orders/{orderId}/pay` | any authenticated | caller's own order only |
| **Fulfil (capture)** | `POST /api/orders/{orderId}/fulfil` | **Administrators** | any order |
| **Cancel (void)** | `POST /api/orders/{orderId}/cancel` | **Administrators** | any order |
| Refund | `POST /api/orders/{orderId}/refunds` | any authenticated | caller's own order only |
| My orders | `GET /api/my-orders` | any authenticated | caller's own orders |
| **Reconciliation** | `GET /api/reconciliation` | **Administrators** | all |
| Save card | `POST /api/payment-methods` | any authenticated | caller |
| List cards | `GET /api/payment-methods` | any authenticated | caller's own cards |
| Delete card | `DELETE /api/payment-methods/{paymentMethodId}` | any authenticated | caller's own card |

- Admin role constant: **`BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS`** (`"Administrators"`).
- Shopper (any-role) endpoints:
  `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` (no `Roles`).
- Admin endpoints:
  `[Authorize(Roles = Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
- **Ownership rule:** for shopper-scoped order/card access, filter the DB query by
  `BuyerId == caller` (and card by owner). If a row exists but is not owned → return **404 Not
  Found** (do not leak existence with 403). buyerId = `ClaimsPrincipal.Identity.Name` (the JWT
  sets `ClaimTypes.Name` to the username/email; see `IdentityTokenClaimService`).

## 1.3 Endpoint conventions (match the project)

Use the **`MinimalApi.Endpoint` `IEndpoint<...>`** pattern used by every CatalogItem endpoint
(exemplar to copy: `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`). Do **not**
use MediatR (PublicApi doesn't) or FluentValidation (PublicApi doesn't). Endpoints are
auto-discovered by `builder.Services.AddEndpoints()` + `app.MapEndpoints()` in `Program.cs` —
no manual registration per endpoint.

Pattern:
```csharp
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PlaceOrderRequest request, ClaimsPrincipal user, IOrderPaymentService svc) =>
                await HandleAsync(request, user, svc))
            .Produces<PlaceOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }
    public async Task<IResult> HandleAsync(PlaceOrderRequest request, ClaimsPrincipal user, IOrderPaymentService svc)
    { ... }
}
```
- Inject the caller identity by adding **`ClaimsPrincipal user`** as a handler lambda parameter
  (PublicApi does NOT register `IHttpContextAccessor`). buyerId = `user.Identity!.Name`.
- Route params bind as lambda params: `(int orderId, ...)` for `{orderId}`,
  `(int paymentMethodId, ...)` for `{paymentMethodId}`.
- Return `IResult` via `Results.Ok/Created/NotFound/Conflict/UnprocessableEntity/...`.
- Colocate DTOs next to the endpoint (e.g. `PlaceOrderEndpoint.PlaceOrderRequest.cs`).
- Response bodies must carry the required **top-level identifiers**: `orderId` (place),
  `paymentMethodId` (save card), `refundId` (refund). Other fields are our choice.

## 1.4 Domain model changes (`src/ApplicationCore`)

Reuse the existing `Order`/`OrderItem` aggregate (`Entities/OrderAggregate/`). Add payment &
fulfilment state to it plus two new entities in the Order aggregate and one new aggregate root
for saved cards.

### 1.4.1 Enum `OrderPaymentStatus` (`Entities/OrderAggregate/OrderPaymentStatus.cs`)
```
AwaitingPayment   // initial, after POST /api/orders
Authorized        // after POST /pay (funds held, not taken)
Fulfilled         // after POST /fulfil (funds captured)
Cancelled         // after POST /cancel (authorization voided, nothing taken)
PartiallyRefunded // after a partial refund of a captured payment
Refunded          // captured amount fully refunded
```
(No separate "Failed" persisted state: a failed authorize leaves the order `AwaitingPayment`
and returns an error; the order remains payable/retryable.)

### 1.4.2 `Order` additions (edit `Entities/OrderAggregate/Order.cs`)
Keep the existing public ctor `Order(string buyerId, Address shipToAddress, List<OrderItem> items)`
and `Total()`. Add:
- `public OrderPaymentStatus PaymentStatus { get; private set; } = OrderPaymentStatus.AwaitingPayment;`
- `public Payment? Payment { get; private set; }` (1:1 child; the money/PayPal state).
- State-transition methods that also guard legality and keep `PaymentStatus` in sync, e.g.:
  - `void AttachAuthorization(Payment payment)` — sets `Payment`, `PaymentStatus = Authorized`.
    Guard: current status is `AwaitingPayment`.
  - `void MarkFulfilled()` — guard `Authorized` → `Fulfilled`.
  - `void MarkCancelled()` — guard `AwaitingPayment` or `Authorized` → `Cancelled`.
  - `void ApplyRefund(...)` — after refund, set `Refunded` or `PartiallyRefunded` based on
    `Payment.TotalRefunded()` vs `Payment.CapturedAmount`.
- Do **not** remove or reorder existing members (EF config + Web checkout depend on them).

### 1.4.3 `Payment` entity (`Entities/OrderAggregate/Payment.cs`, child of Order aggregate)
`Payment : BaseEntity` (int Id). Holds the state PayPal owns so later requests can act on it:
```
int      OrderId
string   Provider              // "PayPal"
string   CurrencyCode          // from config at authorize time
decimal  AuthorizedAmount      // order total held
string   InvoiceReference      // run-unique reconciliation key (see 1.7); stamped into PayPal invoice_id
string?  PayPalOrderId         // from CreateOrder
string?  AuthorizationId       // from AuthorizeOrder
string?  AuthorizationStatus   // last known PayPal AuthorizationStatus (string)
DateTimeOffset? AuthorizationExpiresAt  // parsed from ExpirationTime
string?  CaptureId             // from CaptureAuthorizedPayment
string?  CaptureStatus
decimal? CapturedAmount        // seller_receivable_breakdown.gross_amount
decimal? PayPalFee             // seller_receivable_breakdown.paypal_fee
decimal? NetAmount             // seller_receivable_breakdown.net_amount
private readonly List<PaymentRefund> _refunds = new();
IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();
```
Helpers: `decimal TotalRefunded()` (sum of refund amounts), `decimal RefundableRemaining()`
(`(CapturedAmount ?? 0) - TotalRefunded()`), `void AddRefund(PaymentRefund r)`. Follow the
encapsulated-collection style used by `Order._orderItems`.

### 1.4.4 `PaymentRefund` entity (`Entities/OrderAggregate/PaymentRefund.cs`, child of Payment)
`PaymentRefund : BaseEntity`:
```
int      PaymentId
string   PayPalRefundId    // PayPal's refund id → returned to caller as refundId
decimal  Amount
string   Status
string   IdempotencyKey    // caller-supplied; unique per Payment
DateTimeOffset CreatedAt
```

### 1.4.5 `SavedCard` aggregate root (`Entities/SavedCardAggregate/SavedCard.cs`)
`SavedCard : BaseEntity, IAggregateRoot` (root so `EfRepository<SavedCard>` accepts it):
```
string   BuyerId          // owner (ClaimTypes.Name); scope every query by this
string   PayPalVaultId    // vault payment-token id (PaymentTokenResponse.Id)
string   Brand            // e.g. "VISA" (safe descriptor)
string   LastFourDigits   // "last_digits" from PayPal — NEVER the PAN
string   Expiry           // "YYYY-MM"
string?  CardholderName
DateTimeOffset CreatedAt
```
`paymentMethodId` returned to the caller = `SavedCard.Id` (int). Ownership is enforced by
always querying `BuyerId == caller && Id == paymentMethodId`. **Never** persist PAN or CVC.

> **PAN/CVC handling (hard rule).** Card number and CVC exist only transiently in the request
> DTO and the `IPaymentGateway` card-input DTO, are handed straight to the SDK, and are never
> written to the DB and never logged. Do not add them to any entity, log statement, or
> `ToString()`. Consider marking card-input DTO fields so they can't be accidentally serialized
> into logs.

## 1.5 Persistence (`src/Infrastructure/Data`)

- `CatalogContext` (`Data/CatalogContext.cs`): add `DbSet<Payment> Payments`,
  `DbSet<SavedCard> SavedCards`. (`PaymentRefund` reached via `Payment.Refunds` navigation;
  a `DbSet` is optional.) `OnModelCreating` already calls
  `ApplyConfigurationsFromAssembly(...)` so new `IEntityTypeConfiguration`s are auto-applied.
- Add configs in `Data/Config/`:
  - `PaymentConfiguration : IEntityTypeConfiguration<Payment>` — 1:1 `Order`↔`Payment` (FK
    `OrderId`), `decimal(18,2)` for money columns, `HasMany(_refunds)` with
    `SetPropertyAccessMode(PropertyAccessMode.Field)` (mirror `OrderConfiguration`'s handling
    of `_orderItems`). Map string columns with sane max lengths.
  - `PaymentRefundConfiguration` — FK `PaymentId`, `decimal(18,2)` amount.
  - `SavedCardConfiguration` — index on `BuyerId`; string lengths.
- **In-memory caveats:** the in-memory provider ignores migrations and does **not** enforce
  unique indexes. So idempotency/uniqueness must be enforced in application code
  (see 1.6), not relied on at the DB. Still add a unique index on
  `PaymentRefund(PaymentId, IdempotencyKey)` and on `SavedCard(BuyerId, PayPalVaultId)` as
  correct intent for a real DB. **Do add EF migrations** for the new tables (harmless under
  in-memory; correct for SQL Server) — mirror the existing `Data/Migrations/` catalog-migration
  style. Migrations are optional for the in-memory demo but expected for "production-grade".
- Access via the generic `IRepository<T>` / `IReadRepository<T>` (`EfRepository<T>`), already
  registered in PublicApi `Program.cs`. Add Ardalis specifications in
  `ApplicationCore/Specifications/`:
  - `OrderWithPaymentByIdSpec(int orderId)` and `...ForBuyerSpec(int orderId, string buyerId)`
    — `Include(o => o.Payment).ThenInclude(p => p.Refunds)` and `Include(o => o.OrderItems)`.
  - `CustomerOrdersWithPaymentSpec(string buyerId)` — for `GET /api/my-orders`.
  - `SavedCardsByBuyerSpec(string buyerId)`, `SavedCardByIdForBuyerSpec(int id, string buyerId)`.

## 1.6 Idempotency design (required: "a double-click never authorizes or captures twice")

Two layers, applied together:

1. **Application state guard** (primary). Each mutating op checks the persisted
   `OrderPaymentStatus` / stored PayPal ids first and no-ops (returns the existing result) if
   the target state is already reached:
   - **Pay**: if already `Authorized` (Payment has `AuthorizationId`) → return the existing
     authorization, do not call PayPal again. Only proceed from `AwaitingPayment`.
   - **Fulfil**: if already `Fulfilled` (Payment has `CaptureId`) → return the existing
     capture. Only proceed from `Authorized`.
   - **Cancel**: if already `Cancelled` → no-op. Only proceed from `AwaitingPayment`/`Authorized`.
   - **Refund**: look up `PaymentRefund` by `IdempotencyKey`; if present → return that refund
     (same `refundId`), do not call PayPal again.
2. **PayPal `PayPal-Request-Id`** (defense in depth, dedupes at PayPal even under a race). Pass
   the `payPalRequestId` parameter on every write op (Part 2 confirms which ops accept it):
   - CreateOrder → `payPalRequestId = $"order-{orderId}-create"`
   - AuthorizeOrder → `$"order-{orderId}-authorize"`
   - CaptureAuthorizedPayment → `$"order-{orderId}-capture"` (stable across a reauthorize retry;
     if a reauthorize replaced the authorization, use `$"order-{orderId}-capture-{authId}"` so
     the new capture isn't blocked by the previous request-id)
   - ReauthorizePayment → `$"order-{orderId}-reauth-{n}"`
   - RefundCapturedPayment → the **caller-supplied idempotency key** (so repeats dedupe both in
     our DB and at PayPal; two *different* keys = two legitimate partial refunds)
   - CreatePaymentToken → `$"vault-{buyerId}-{guid}"`

> **Why both:** the app-state guard prevents the second click from ever calling PayPal; the
> `PayPal-Request-Id` covers the window where two requests race past the guard concurrently.
> This is essential because a retried non-idempotent POST could otherwise double-charge (see
> Part 2 §6 resilience trap).

**Refund partial-refund safety** ("never refundable beyond what was captured"): before calling
PayPal, compute `remaining = Payment.RefundableRemaining()`. If requested `amount > remaining`
→ 422. If `amount` omitted → refund `remaining` (full remaining). After success, recompute and
set `Refunded` (remaining == 0) or `PartiallyRefunded`.

## 1.7 Reconciliation design

- **Join key = PayPal `invoice_id`** (Part 2 §8: same wire name on both the purchase unit and
  the transaction record; `custom_id`→`custom_field` is not guaranteed, `reference_id` isn't
  returned). At `CreateOrder`, stamp `PurchaseUnitRequest.InvoiceId = Payment.InvoiceReference`.
- **`InvoiceReference` must be run-unique.** PayPal enforces `invoice_id` uniqueness per
  merchant to prevent duplicate payments, but the **in-memory DB resets order ids to 1 every
  run**, so `"eshop-{orderId}"` would collide with a prior run's transactions at PayPal. Use
  `InvoiceReference = $"eshop-{orderId}-{Guid.NewGuid():N}"` (or a per-process run-id prefix +
  orderId), generate it once at pay time, **persist it on `Payment`**, and reconcile by matching
  `TransactionInformation.InvoiceId == Payment.InvoiceReference`. This guarantees a stable,
  collision-free join.
- `GET /api/reconciliation?from=&to=` (admin):
  1. Parse `from`/`to` as `DateTimeOffset` (ISO-8601). 400 on parse failure.
  2. `gateway.SearchTransactionsAsync(from, to)` — **paged over the whole range** (loop page
     1..`TotalPages`; see Part 2 §3.5). Returns our `GatewayTransaction` DTOs
     (`TransactionId`, `Status`, `Amount`, `Currency`, `InvoiceId`, `InitiationDate`).
  3. Load eShop `Payment`s whose `InvoiceReference` is set (i.e. orders that reached authorize),
     optionally filtered by order date in range.
  4. Produce three buckets by joining on `InvoiceReference == PayPal InvoiceId` (and secondarily
     `CaptureId == TransactionId`):
     - `matched` — present both sides (include eShop orderId + PayPal transactionId + amounts).
     - `unmatchedInPayPal` — PayPal transactions with no eShop payment.
     - `unmatchedInEshop` — eShop payments with no PayPal transaction in the window.
  5. Response: `{ from, to, paypalTransactionCount, matched:[...], unmatchedInPayPal:[...],
     unmatchedInEshop:[...] }`.
- **Reporting lag:** PayPal reporting lags up to ~3 hours; a range covering just-created
  payments may return empty. That is expected — the report must be *correct over a range that
  has data*; an empty recent range is **not** a bug and must not be reported as a gap.

## 1.8 `IPaymentGateway` abstraction (ApplicationCore interface, Infrastructure impl)

Define in `ApplicationCore/Interfaces/IPaymentGateway.cs`, returning **our own DTOs** (declared
in `ApplicationCore`, e.g. `ApplicationCore/Payments/`), never SDK types. Suggested surface
(async, all take `CancellationToken`):

```
Task<GatewayCreatedOrder>   CreateAuthorizeOrderAsync(decimal amount, string currency, string invoiceReference, string idempotencyKey, ct)
Task<GatewayAuthorization>  AuthorizeWithCardAsync(string payPalOrderId, CardInput card, string idempotencyKey, ct)
Task<GatewayAuthorization>  AuthorizeWithVaultAsync(string payPalOrderId, string vaultId, string idempotencyKey, ct)
Task<GatewayCapture>        CaptureAsync(string authorizationId, string idempotencyKey, ct)
Task<GatewayAuthorization>  ReauthorizeAsync(string authorizationId, decimal amount, string currency, string idempotencyKey, ct)
Task                        VoidAsync(string authorizationId, ct)
Task<GatewayRefund>         RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, ct)
Task<GatewayAuthorization>  GetAuthorizationAsync(string authorizationId, ct)
Task<GatewaySavedCard>      CreateVaultCardAsync(CardInput card, string merchantCustomerId, string idempotencyKey, ct)
Task                        DeleteVaultCardAsync(string vaultId, ct)
Task<IReadOnlyList<GatewayTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, ct)
```

DTOs (fields map to Part 2 accessor paths):
- `CardInput { Number, Expiry("YYYY-MM"), SecurityCode, CardholderName, BillingAddress(Line1, Line2?, City, State, PostalCode, CountryCode) }` — transient, never persisted/logged.
- `GatewayCreatedOrder { PayPalOrderId, Status }`
- `GatewayAuthorization { AuthorizationId, Status, ExpiresAt (DateTimeOffset?) }`
- `GatewayCapture { CaptureId, Status, GrossAmount, PayPalFee?, NetAmount?, Currency }`
- `GatewayRefund { RefundId, Status, Amount }`
- `GatewaySavedCard { VaultId, Brand, LastDigits, Expiry, CardholderName }`
- `GatewayTransaction { TransactionId, Status, Amount, Currency, InvoiceId, InitiationDate }`

The Infrastructure impl `PayPalPaymentGateway` (Infrastructure, references the SDK) maps SDK
models ↔ these DTOs, does the challenge-detection & error translation (1.9), formats amounts
(1.10), and performs paged transaction search. It is the **only** place `PayPalServerSdk.*`
types appear.

## 1.9 Error handling & mapping

Define domain exceptions in `ApplicationCore` (`ApplicationCore/Exceptions/`):
- `PaymentChallengeRequiredException` — PayPal returned `PAYER_ACTION_REQUIRED` / a payer-action
  approval link (browser/3DS). Per the task, this is a **STOP-and-report** condition; in the
  running API surface it as **409 Conflict** with a clear message
  (`"Card requires buyer approval in a browser (3DS/PAYER_ACTION_REQUIRED); server-side card
  payment cannot complete."`). Detect per Part 2 §3.1 (status + link `Rel` scan, fail-closed).
- `AuthorizationNotRenewableException` — capture failed on a stale authorization and reauthorize
  is no longer permitted (Part 2 §3.2). Carry an **operator-actionable** message built from the
  PayPal error `Details[0].Description`/`Issue`/`Message`. Surface as **409 Conflict**.
- `PaymentGatewayException` — any other PayPal failure; carry HTTP status (from `RawError`/the
  `TryGet…` bucket), PayPal `DebugId`, and message. Surface as **502 Bad Gateway** (upstream
  failure) — never 500, and never a blanket 5xx for a deterministic 4xx rejection (Part 2 §7
  JSON-boundary trap: a 4xx whose body doesn't match the typed error shape can arrive as a
  `JsonException` — handle it so a permanent rejection isn't reported as a retryable outage).

Translate SDK exceptions inside `PayPalPaymentGateway` (load `dotnet-error-handling` first —
`SdkException<TError>` has **no** `.StatusCode`; read status via `RawError`/`TryGetRawError`;
vault ops use `TryGetError1`/`Error1`; `SearchTransactions` is the lone `SdkException<RawError>`
Case-B op; guard the two `System.Text.Json.JsonException` boundary hazards).

Map domain → HTTP centrally by **extending `PublicApi/Middleware/ExceptionMiddleware.cs`**
(it already maps `DuplicateException`→409, else→500). Add cases:
`PaymentChallengeRequiredException`→409, `AuthorizationNotRenewableException`→409,
`PaymentGatewayException`→502, and a validation/argument exception→400/422. Keep writing the
existing `BlazorShared.Models.ErrorDetails` JSON body. Endpoints may also return explicit
`Results.NotFound()` / `Results.Conflict()` / `Results.UnprocessableEntity()` for
state/ownership checks they detect directly (preferred for expected control flow).

## 1.10 Currency & amount formatting

- Currency code comes from config `PayPal:Currency`. Every PayPal amount `Value` is a **string**
  the SDK does not format.
- Add `Infrastructure/Payments/AmountFormatter`:
  - `string ToPayPalValue(decimal amount, string currency)` — format with the currency's
    minor-unit digit count using `CultureInfo.InvariantCulture`. Default **2** decimals; **0**
    for zero-decimal currencies (JPY, KRW, VND, HUF, CLP, ISK, …); **3** for three-decimal
    (BHD, KWD, OMR, TND, …). Provide a small lookup table with a 2-decimal default. `.ToString("F2",
    Invariant)` is correct **only** for 2-decimal currencies — do not hard-code F2.
  - `decimal FromPayPalValue(string value)` — parse PayPal amount strings back with
    `InvariantCulture` (for storing `CapturedAmount`/`PayPalFee`/`NetAmount`/refund amounts).
- The held amount must equal the order total **to the cent**: format `Order.Total()` via
  `ToPayPalValue(total, currency)`; on any 4xx amount complaint, surface PayPal's error body
  verbatim.

## 1.11 Endpoint specifications

For each: request shape, behavior, response, status codes. All under `/api/`. buyerId =
`user.Identity!.Name`.

### `POST /api/orders` — place order (shopper)
- Request: `{ items: [{ catalogItemId:int, quantity:int }], shipToAddress?: { street, city, state, country, zipCode } }`.
- Load each `CatalogItem` by id via `IReadRepository<CatalogItem>`; 400 if any id unknown or
  quantity < 1. Snapshot price/name/pictureUri into `CatalogItemOrdered` + `OrderItem(itemOrdered,
  catalogItem.Price, quantity)` (unit price from catalog). Build `Order(buyerId, address, items)`.
- `shipToAddress` optional (this task is about payment, not shipping). If omitted, use a default
  non-empty `Address` (the `Address` value object guards against null/empty), e.g.
  `new Address("N/A","N/A","N/A","N/A","00000")`. Document this default.
- `PaymentStatus = AwaitingPayment`. Persist via `IRepository<Order>`.
- Response **201**: `{ orderId, status:"AwaitingPayment", total, currency }` (currency from config).

### `POST /api/orders/{orderId}/pay` — authorize (shopper, own order)
- Request (exactly one of): `{ card: { number, expiryMonth:int, expiryYear:int, securityCode, cardholderName, billingAddress:{ line1, line2?, city, state, postalCode, countryCode } } }` **or** `{ savedPaymentMethodId: int }`. 400 if both/neither.
- Load order for buyer (`OrderWithPaymentByIdForBuyerSpec`); **404** if missing/not owned.
- Idempotency (1.6): if already `Authorized` → **200** with existing authorization; if
  `Fulfilled`/`Cancelled`/`Refunded`/`PartiallyRefunded` → **409**.
- Build `CardInput` from raw card (expiry → `"YYYY-MM"`, zero-padded month) OR resolve
  `SavedCard` by `(savedPaymentMethodId, buyerId)` (**404** if missing/not owned) → use its
  `PayPalVaultId`.
- Orchestrate (2-step, Part 2 §8): `CreateAuthorizeOrderAsync(total, currency, invoiceReference,
  idem)` → store `PayPalOrderId` + `InvoiceReference` immediately (so a retry can resume via the
  request-id); then `AuthorizeWithCardAsync(payPalOrderId, card, idem)` **or**
  `AuthorizeWithVaultAsync(payPalOrderId, vaultId, idem)`.
  - `PaymentChallengeRequiredException` → **409** (STOP-and-report message).
  - other gateway failure → mapped (502/…); order stays `AwaitingPayment` (retryable).
- On success: create/attach `Payment` (AuthorizedAmount=total, currency, AuthorizationId, status,
  ExpiresAt), `Order.AttachAuthorization(payment)` → `Authorized`. Persist.
- Response **200**: `{ orderId, paymentStatus:"Authorized", authorizationId, amountHeld, currency }`.

### `POST /api/orders/{orderId}/fulfil` — capture (ADMIN, any order)
- Load order + payment by id (no buyer scope); **404** if missing. Guard `Authorized` else **409**.
- Idempotency: if already `Fulfilled` (CaptureId set) → **200** existing capture.
- **Stale-authorization handling** (task requirement; Part 2 GAP-3 — no `EXPIRED` enum):
  1. If `Payment.AuthorizationExpiresAt` is in the past (or null-unknown), or the first capture
     attempt throws a 422 → attempt `ReauthorizeAsync(authorizationId, total, currency, idem)`.
  2. Reauthorize success → update `Payment.AuthorizationId`/`ExpiresAt`, then retry
     `CaptureAsync` once (with a request-id keyed to the new auth id).
  3. Reauthorize fails (422 not-renewable) → throw `AuthorizationNotRenewableException`
     → **409** with an operator-actionable message (e.g. *"Authorization expired and can no
     longer be renewed — ask the shopper to pay the order again."*).
- On capture success: store `CaptureId`, `CaptureStatus`, `CapturedAmount` (gross),
  `PayPalFee`, `NetAmount` from `SellerReceivableBreakdown` (Part 2 §3.2). `Order.MarkFulfilled()`.
  Persist.
- Response **200**: `{ orderId, paymentStatus:"Fulfilled", captureId, capturedAmount, paypalFee, netAmount, currency }`.

### `POST /api/orders/{orderId}/cancel` — void (ADMIN, any order)
- Load order + payment; **404** if missing. If already `Cancelled` → **200** no-op. Guard state
  is `AwaitingPayment` or `Authorized`; if `Fulfilled`/refunded → **409** (*"use refunds after
  fulfilment"*).
- If `Authorized` (has `AuthorizationId`): `VoidAsync(authorizationId)` (releases the hold — no
  money moved). If `AwaitingPayment` (no auth): nothing at PayPal.
- `Order.MarkCancelled()`. Persist. Response **200**: `{ orderId, paymentStatus:"Cancelled" }`.

### `POST /api/orders/{orderId}/refunds` — refund (shopper, own order)
- Request: `{ amount?: decimal, idempotencyKey: string }` (idempotencyKey **required**).
- Load order + payment for buyer; **404** if missing/not owned. Guard state ∈
  {`Fulfilled`, `PartiallyRefunded`} (must be captured) else **409**.
- Idempotency (1.6): existing `PaymentRefund` with same `IdempotencyKey` → **200** with that
  `refundId` (no second PayPal call).
- Validate: `remaining = Payment.RefundableRemaining()`; if `amount > remaining` → **422**; if
  `amount` omitted → refund `remaining`.
- `RefundAsync(captureId, amount, currency, idempotencyKey)` (pass the caller key as
  `payPalRequestId`). Persist `PaymentRefund`. Update status → `Refunded`/`PartiallyRefunded`.
- Response **201**: `{ refundId, orderId, amount, paymentStatus, totalRefunded, refundableRemaining }`.

### `GET /api/my-orders` — (shopper)
- List orders for buyer (`CustomerOrdersWithPaymentSpec`). Response **200**: array of
  `{ orderId, orderDate, total, currency, paymentStatus, authorizationId?, captureId?,
  capturedAmount?, paypalFee?, netAmount?, refunds:[{ refundId, amount, status }],
  items:[{ catalogItemId, productName, unitPrice, units }] }`.

### `GET /api/reconciliation?from=&to=` — (ADMIN)
- Per 1.7. Response **200** with the three buckets. Empty over a recent range is valid.

### `POST /api/payment-methods` — save card (shopper)
- Request: `{ card: { number, expiryMonth, expiryYear, securityCode, cardholderName, billingAddress } }`.
- `merchantCustomerId` = a stable per-buyer id derived from buyerId (e.g.
  `$"eshop-{Sanitize(buyerId)}"`) so a shopper's cards share one PayPal customer (reuse
  supported, Part 2 §8). `CreateVaultCardAsync(card, merchantCustomerId, idem)`.
- Persist `SavedCard(buyerId, vaultId, brand, lastDigits, expiry, cardholderName)`. **Never**
  store PAN/CVC.
- Response **201**: `{ paymentMethodId, brand, lastFourDigits, expiry, cardholderName }`.

### `GET /api/payment-methods` — list cards (shopper)
- `SavedCardsByBuyerSpec(buyerId)`. Response **200**: array of `{ paymentMethodId, brand,
  lastFourDigits, expiry, cardholderName }`. No PAN, ever.

### `DELETE /api/payment-methods/{paymentMethodId}` — delete card (shopper)
- Load `SavedCard` by `(paymentMethodId, buyerId)`; **404** if missing/not owned.
- `DeleteVaultCardAsync(vaultId)` then delete the row (`IRepository<SavedCard>.DeleteAsync`).
- After: absent from the list, and `pay` with that id → **404** (no longer usable). Response
  **204**.

## 1.12 Configuration & credentials

- Bind a `PayPalOptions` POCO to the **`PayPal`** section with **exactly** these keys:
  `ClientId`, `ClientSecret`, `Environment`, `Currency`, `BaseUrl`
  (`PayPal:ClientId`, `PayPal:ClientSecret`, `PayPal:Environment`, `PayPal:Currency`,
  `PayPal:BaseUrl`). **Hard-code no values.**
- **Env-var → config bridge (required).** The credentials arrive as flat env vars
  `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`, `PAYPAL_CURRENCY`.
  ASP.NET's `AddEnvironmentVariables()` maps them to config keys named `PAYPAL_CLIENT_ID` (flat)
  — **not** `PayPal:ClientId`. So in PublicApi `Program.cs`, after the existing config setup,
  add an explicit mapping source that reads the flat env vars and exposes them under the
  `PayPal:` keys, only when present:
  ```csharp
  var paypalEnv = new Dictionary<string, string?>();
  void Map(string env, string key) {
      var v = Environment.GetEnvironmentVariable(env);
      if (!string.IsNullOrEmpty(v)) paypalEnv[key] = v;
  }
  Map("PAYPAL_CLIENT_ID",     "PayPal:ClientId");
  Map("PAYPAL_CLIENT_SECRET", "PayPal:ClientSecret");
  Map("PAYPAL_ENVIRONMENT",   "PayPal:Environment");
  Map("PAYPAL_CURRENCY",      "PayPal:Currency");
  builder.Configuration.AddInMemoryCollection(paypalEnv);
  ```
  This keeps values out of the repo (they come from the environment), lets a *different* PayPal
  account be supplied purely via env vars, and still allows `appsettings`/user-secrets to
  provide `PayPal:*` (notably `PayPal:BaseUrl`) directly. `PayPal:BaseUrl` has **no** env var —
  it is an optional config-only override.
- `builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection("PayPal"));`
- **Do not** write credential values into `appsettings.json` or any repo file. (`appsettings`
  may contain an empty `"PayPal": { "BaseUrl": "" }` placeholder at most.)

### Client construction / environment / BaseUrl (Part 2 §4)
- Register the SDK client once (singleton) in Infrastructure's `AddPayPalIntegration` extension
  (load `dotnet-client-initialization` for HttpClient lifetime first). Auth = OAuth2 client
  credentials (`OAuth2ClientCredentials { ClientId, ClientSecret }`); token acquisition is
  automatic.
- Environment/BaseUrl resolution (GAP-1: the SDK's `ServerEnvironment` only has `Sandbox`):
  - If `PayPal:BaseUrl` is set → use it **verbatim** as `options.Server.Default.Sandbox.BaseUrl`
    (this is honored for the OAuth token request **and** every API call — Part 2 §4/§5, so the
    "must be used for EVERY call including token" requirement is met).
  - Else map `PayPal:Environment`: `sandbox` (default) → leave default sandbox host;
    `live`/`production` → set `BaseUrl = "https://api-m.paypal.com"`.
  - Target is **sandbox** for all dev/testing here.

---
---

# PART 2 — PayPal SDK Contract Sheet (authoritative)

> Produced by the `paypal-sdk` agent, grounded in the bundled SDK map (release tag `v1.0.1`,
> source stamp `9653d18`); items marked *source-confirmed* were resolved from SDK source. This
> is the **sole** PayPal reference — do not web-search or use general knowledge. Take every
> signature, wire name, enum and error accessor from here. If something needed is missing, ask
> the warm `paypal-sdk` agent; do not invent it.

SDK: NuGet `AsadAli.Checkout.Sdk` (install version-less under Central Package Management) · root
namespace `PayPalServerSdk` · client `PayPalServerSdkClient` · target `netstandard2.0`.

## 2.1 Namespace legend (add each `using` separately — C# does not import child namespaces transitively)

| Type(s) | Namespace |
|---|---|
| `PayPalServerSdkClient`, `PayPalServerSdkClientOptions`, `ServerOptions`, `Server` | `PayPalServerSdk` |
| `ServerEnvironment`, `DefaultOptions` (+ `DefaultOptions.SandboxOptions`) | `PayPalServerSdk.Servers` |
| Controllers `Orders`/`Payments`/`Vault`/`TransactionSearch` | `PayPalServerSdk.Api` |
| Record models (`OrderRequest`, `Money`, `CardRequest`, `CapturedPayment`, …) | `PayPalServerSdk.Models` |
| Enums (`CheckoutPaymentIntent`, `OrderStatus`, `CardBrand`, …) | `PayPalServerSdk.Models.Enums` |
| Typed error classes (`CreateOrderError`, …) | `PayPalServerSdk.Errors` |
| `SdkException<TError>` | `PayPalServerSdk.Core.Exceptions` (source-confirmed) |
| `RawError` | `PayPalServerSdk.Core.ErrorResponse` |
| `RequestOptions` | `PayPalServerSdk.Core` (source-confirmed) |
| `RetryOptions`, `RetryAttempt` | `PayPalServerSdk.Core.Configuration` |
| `OAuth2ClientCredentials` | `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials` (source-confirmed) |

## 2.2 Universal call/error facts (read once)

- **Every op is throw-based; no `…Result` no-throw variants exist.**
- **`SdkException<TError>` exposes ONLY `.Error` — there is NO `.StatusCode` on the exception**
  (source `Core/Exceptions/SdkException.cs`). Read numeric HTTP status via `RawError.StatusCode`
  (Case B directly; Case A via the `TryGetRawError(out RawError)` fallback). For Case-A typed
  shapes the status bucket is implied by *which* `TryGet…` returned `true`. The typed
  `Error`/`Error1` payloads carry `Name`, `Message`, `DebugId`, `Details[]` — no numeric status.
- **Call every op with named arguments.** Most ops have a run of nullable-no-default header
  params that must be passed explicitly (pass `null` to skip); positional calls mis-bind. The
  cancellation-token param is literally named **`ct`** (write `ct:`).
- **Idempotency `PayPal-Request-Id` is a dedicated `string? payPalRequestId` PARAMETER**, not a
  `RequestOptions` field. `RequestOptions` carries ONLY `LogLevel` (source
  `Core/RequestOptions.cs`) — there is **no** general per-request custom-header hook (GAP-2).

## 2.3 Orders controller — `client.Orders`

- **CreateOrder** —
  `CreateOrder(string? payPalMockResponse, string? payPalRequestId, string? payPalPartnerAttributionId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderRequest body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`.
  First 5 params nullable-no-default (pass `null`); pass `payPalRequestId` for idempotency.
  - `OrderRequest`: `Intent (intent): CheckoutPaymentIntent !req`, `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnitRequest> !req`, `Payer?`, `PaymentSource (payment_source): PaymentSource?` (**optional** — omit for the two-step raw-card flow), `ApplicationContext?`.
  - `PurchaseUnitRequest`: `Amount (amount): AmountWithBreakdown !req`, `ReferenceId (reference_id)?`, `CustomId (custom_id)?`, `InvoiceId (invoice_id)?`, `Items?`.
  - `AmountWithBreakdown`: `CurrencyCode (currency_code): string !req`, `Value (value): string !req`, `Breakdown?`.
  - Returns **`Order`**: `Id (id): string?`, `Status (status): OrderStatus?`, `Links?`, `PurchaseUnits?`, `PaymentSource?`. Persist `Order.Id`.
  - Error **Case A** `SdkException<CreateOrderError>` · `TryGetError(out Error)` [400,401,422] · `TryGetRawError(out RawError)` [fallback].
- **AuthorizeOrder** —
  `AuthorizeOrder(string id, string? payPalMockResponse, string? payPalRequestId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderAuthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`.
  `id` = order id; 5 params (`payPalMockResponse`…`body`) nullable-no-default.
  - `OrderAuthorizeRequest`: `PaymentSource (payment_source): OrderAuthorizeRequestPaymentSource?`.
    `OrderAuthorizeRequestPaymentSource`: `Card (card): CardRequest?`, `Token?`, `Paypal?`, `ApplePay?`, `GooglePay?`, `Venmo?`.
  - **Direct raw card** → `CardRequest`: `Number (number): string?`, `Expiry (expiry): string?`, `SecurityCode (security_code): string?`, `Name (name): string?`, `BillingAddress (billing_address): Address?`, **`VaultId (vault_id): string?`** (vaulted-card path).
  - `Address`: `AddressLine1 (address_line_1)?`, `AddressLine2 (address_line_2)?`, `AdminArea1 (admin_area_1)?` (state), `AdminArea2 (admin_area_2)?` (city), `PostalCode (postal_code)?`, `CountryCode (country_code): string !req`.
  - Returns **`OrderAuthorizeResponse`**: `Id?`, `Status (status): OrderStatus?`, `Links?`, `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnit>?`, `PaymentSource?`. Authorization lives at `PurchaseUnits[].Payments.Authorizations[]` (see 2.5).
  - Error **Case A** `SdkException<AuthorizeOrderError>` · `TryGetError(out Error)` [400,401,403,404,422,500] · `TryGetRawError` [fallback].
- **GetOrder** —
  `GetOrder(string id, string? fields, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)`.
  Returns **`Order`**. Error **Case A** `SdkException<GetOrderError>` · `TryGetError` [401,404] · `TryGetRawError`.

**`CheckoutPaymentIntent`** (enums): `Capture (CAPTURE)`, `Authorize (AUTHORIZE)`. **Use
`CheckoutPaymentIntent.Authorize`** (holds funds, does not capture).

**`OrderStatus`** (enums): `Created (CREATED)`, `Saved (SAVED)`, `Approved (APPROVED)`,
`Voided (VOIDED)`, `Completed (COMPLETED)`, `PayerActionRequired (PAYER_ACTION_REQUIRED)`.

**Challenge / redirect detection (STOP-and-report).** After `AuthorizeOrder`: a browser/3DS
approval requirement is NOT a completed authorization. Detect from
`OrderAuthorizeResponse.Status == OrderStatus.PayerActionRequired` **first**, and additionally
scan `Links` (`LinkDescription { Href !req, Rel !req, Method? }`) treating any `Rel` containing
`payer-action`/`approve`/`3ds`/`authenticate` (case-insensitive) as a challenge. A successful
server-side card auth yields `Status = Approved`/`Completed` with a populated
`Authorizations[]`. If authorization data is absent and status isn't Approved/Completed,
**fail closed to "approval required" → STOP and report** (do not build an approval round-trip).
(*UNVERIFIED live-wire:* exact `rel` token not encoded in any SDK type — hence the defensive scan.)

## 2.4 Payments controller — `client.Payments`

- **CaptureAuthorizedPayment** —
  `CaptureAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, CaptureRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`.
  4 params (`payPalMockResponse`…`body`) nullable-no-default; pass `payPalRequestId`; **`body = null` = full capture**.
  - `CaptureRequest`: `Amount (amount): Money?` (partial; omit = full), `InvoiceId?`, `FinalCapture (final_capture): bool? = false`, `NoteToPayer?`, `SoftDescriptor?`.
  - Returns **`CapturedPayment`**: `Id?`, `Status (status): CaptureStatus?`, `Amount (amount): Money?`, `SellerReceivableBreakdown (seller_receivable_breakdown): SellerReceivableBreakdown?`, `Links?`.
  - **Reconciliation reads:** `resp.SellerReceivableBreakdown.GrossAmount.Value` (+`.CurrencyCode`) = captured gross (`!req`); `.SellerReceivableBreakdown.PaypalFee?.Value` = fee; `.SellerReceivableBreakdown.NetAmount?.Value` = merchant net.
  - Error **Case A** `SdkException<CaptureAuthorizedPaymentError>` · `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback].
- **ReauthorizePayment** —
  `ReauthorizePayment(string authorizationId, string? payPalRequestId, string? payPalAuthAssertion, ReauthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`.
  `payPalRequestId`,`payPalAuthAssertion`,`body` nullable-no-default.
  - `ReauthorizeRequest`: **`Amount (amount): Money?` only**.
  - Returns **`PaymentAuthorization`**: `Id?`, `Status (status): AuthorizationStatus?`, `Amount?`, `ExpirationTime (expiration_time): string?`, `Links?`.
  - Error **Case A** `SdkException<ReauthorizePaymentError>` · `TryGetError` [400,401,403,404,422] · `TryGetNoContent` [500] · `TryGetRawError`.
- **VoidPayment** —
  `VoidPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, string? payPalRequestId, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`.
  **Note the differing order: `payPalMockResponse`, `payPalAuthAssertion`, `payPalRequestId`.** No body.
  - Returns **`PaymentAuthorization`** (status → `Voided`).
  - Error **Case A** `SdkException<VoidPaymentError>` · `TryGetError` [401,403,404,409,422] · `TryGetNoContent` [500] · `TryGetRawError`.
- **RefundCapturedPayment** —
  `RefundCapturedPayment(string captureId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, RefundRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`.
  4 params nullable-no-default; pass `payPalRequestId`; **full refund ⇒ `body = null`**, partial ⇒ set `Amount`.
  - `RefundRequest`: `Amount (amount): Money?` (omit = full), `CustomId?`, `InvoiceId?`, `NoteToPayer?`, `PaymentInstruction?`.
  - Returns **`Refund`**: `Id?`, `Status (status): RefundStatus?`, `Amount (amount): Money?`, `SellerPayableBreakdown?`, `Links?`.
  - Error **Case A** `SdkException<RefundCapturedPaymentError>` · `TryGetError` [400,401,403,404,409,422] · `TryGetNoContent` [500] · `TryGetRawError`.
- **GetAuthorizedPayment** — `GetAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` → **`PaymentAuthorization`** (`Id`, `Status`, `ExpirationTime`, `Amount`). Case A `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError`.
- **GetCapturedPayment** — `GetCapturedPayment(string captureId, string? payPalMockResponse, RequestOptions? requestOptions = null, CancellationToken ct = default)` → **`CapturedPayment`**. Case A `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError`.
- **GetRefund** — `GetRefund(string refundId, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` → **`Refund`**. Case A `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError`.

**`SellerReceivableBreakdown`**: `GrossAmount (gross_amount): Money !req`, `PaypalFee (paypal_fee): Money?`,
`PaypalFeeInReceivableCurrency?`, `NetAmount (net_amount): Money?`, `ReceivableAmount?`, `ExchangeRate?`, `PlatformFees?`.
Accessors: `capturedPayment.SellerReceivableBreakdown.GrossAmount.Value` (+`.CurrencyCode`),
`....PaypalFee?.Value`, `....NetAmount?.Value`.

**`Money`**: `CurrencyCode (currency_code): string !req`, `Value (value): string !req`. Both required, both string.
Same shape for `CaptureRequest.Amount`, `RefundRequest.Amount`, `ReauthorizeRequest.Amount`.

**Enums:**
- `AuthorizationStatus`: `Created (CREATED)`, `Captured (CAPTURED)`, `Denied (DENIED)`, `PartiallyCaptured (PARTIALLY_CAPTURED)`, `Voided (VOIDED)`, `Pending (PENDING)` — **no `Expired`** (GAP-3).
- `CaptureStatus`: `Completed`, `Declined`, `PartiallyRefunded`, `Pending`, `Refunded`, `Failed`.
- `RefundStatus`: `Cancelled`, `Failed`, `Pending`, `Completed`.

**Not-reauthorizable detection (actionable message).** On `ReauthorizePayment` a 4xx (typically
422) → `ex.Error.TryGetError(out var err)` → `err` (`Error`) has `Name`, `Message`, `DebugId`,
`Details: IReadOnlyList<ErrorDetails>?`. `ErrorDetails`: `Issue (issue): string !req`,
`Description (description): string?`, `Field?`, `Value?`. Build the operator message best-effort
from `err.Details[0].Description` → `.Issue` → `err.Message`; if `TryGetError` is false, fall
back to `TryGetRawError(out var raw)` → `raw.StatusCode` + `raw.ReadAsString()`. Do **not** branch
on a hard-coded issue string; treat any 422 on this op as "reauthorization not permitted — create
a new authorization instead."

## 2.5 Reading the authorization out of the AuthorizeOrder response

`OrderAuthorizeResponse.PurchaseUnits` (`IReadOnlyList<PurchaseUnit>`) → `PurchaseUnit.Payments`
(`PaymentCollection?`) → `PaymentCollection.Authorizations` (`IReadOnlyList<AuthorizationWithAdditionalData>?`).
Each **`AuthorizationWithAdditionalData`**: `Id (id): string?`, `Status (status): AuthorizationStatus?`,
`ExpirationTime (expiration_time): string?`, `Amount (amount): Money?`, `ProcessorResponse?`.
Path to persist: `resp.PurchaseUnits[0].Payments.Authorizations[0].Id` / `.Status` / `.ExpirationTime`.
**Null-guard the whole chain**; if no authorization element is present treat as "not authorized"
(pair with the challenge-detection rule in 2.3).

## 2.6 Vault controller — `client.Vault`

- **CreatePaymentToken** —
  `CreatePaymentToken(string? payPalRequestId, PaymentTokenRequest body, RequestOptions? requestOptions = null, CancellationToken ct = default)`.
  - `PaymentTokenRequest`: `Customer (customer): Customer?`, `PaymentSource (payment_source): PaymentTokenRequestPaymentSource !req`.
    `PaymentTokenRequestPaymentSource`: `Card (card): PaymentTokenRequestCard?`, `Token (token): VaultTokenRequest?`.
  - **Raw card** → `PaymentTokenRequestCard`: `Number (number): string?`, `Expiry (expiry): string?`, `SecurityCode (security_code): string?`, `Name?`, `Brand (brand): CardBrand?`, `BillingAddress (billing_address): Address?`.
  - `Customer`: `Id (id): string?`, `MerchantCustomerId (merchant_customer_id): string?`.
  - Returns **`PaymentTokenResponse`**: `Id (id): string?` = **vault/token id**, `Customer?`, `PaymentSource?`, `Links?`. Safe descriptor: `PaymentSource.Card` = `CardPaymentTokenEntity` → `Brand (brand): CardBrand?`, `LastDigits (last_digits): string?`, `Expiry (expiry): string?`, `Name?`, `BillingAddress?`. **Full PAN is NOT returned — only `LastDigits`** (`CardPaymentTokenEntity` has no `Number`).
  - Error **Case A** `SdkException<CreatePaymentTokenError>` · **`TryGetError1(out Error1)`** [400,403,404,422,500] · `TryGetRawError` [fallback].
- **GetPaymentToken** — `GetPaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` → **`PaymentTokenResponse`**. Case A `TryGetError1` [403,404,422,500] · `TryGetRawError`.
- **DeletePaymentToken** — `DeletePaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` → **`void`/Task** (success = no throw). Case A `TryGetError1` [400,403,500] · `TryGetRawError`.

Vault ops use **`TryGetError1(out Error1)`** (not `TryGetError`). `Error1`: `Name !req`, `Message !req`,
`DebugId !req`, `Details: IReadOnlyList<ErrorDetails1>?`, `Links?`. `ErrorDetails1` mirrors `ErrorDetails`
(`Issue !req`, `Description?`).

**Pay with a vaulted card (2-step):** set `CardRequest.VaultId (vault_id) = "<PaymentTokenResponse.Id>"`
on `OrderAuthorizeRequest.PaymentSource.Card` (do NOT resend raw PAN when `VaultId` is set). (Also
settable at create via `OrderRequest.PaymentSource.Card`.)

**`CardBrand`** (enums): `Visa (VISA)`, `Mastercard (MASTERCARD)`, `Discover`, `Amex`, `Jcb`, `Diners`,
`Elo`, `Maestro`, `ChinaUnionPay`, … `Unknown (UNKNOWN)` (30 members). (Sandbox test card `4111...` = Visa.)

## 2.7 TransactionSearch controller — `client.TransactionSearch`

- **SearchTransactions** —
  `SearchTransactions(string startDate, string endDate, string? transactionId, string? transactionType, string? transactionStatus, string? transactionAmount, string? transactionCurrency, string? paymentInstrumentType, string? storeId, string? terminalId, string? fields = "transaction_info", string? balanceAffectingRecordsOnly = "Y", int? pageSize = 100, int? page = 1, RequestOptions? requestOptions = null, CancellationToken ct = default)`.
  **`startDate`/`endDate` REQUIRED non-null strings**; 8 params (`transactionId`…`terminalId`) nullable-no-default.
  - Wire: `start_date`←`startDate`, `end_date`←`endDate`, `page_size`←`pageSize` (default 100), `page`←`page` (default 1), `fields`←`fields` (default `"transaction_info"`).
  - Returns **`SearchResponse`**: `TransactionDetails (transaction_details): IReadOnlyList<TransactionDetails>?`, `Page (page): int?`, `TotalItems (total_items): int?`, `TotalPages (total_pages): int?`, `Links?`.
  - **Error is the ONLY Case B in the SDK:** `SdkException<RawError>` → `ex.Error.StatusCode`, `ex.Error.ReadAsString()`, `ex.Error.ReadAsJson<T>()`.
- **Date format:** plain strings, ISO-8601 date-time (`yyyy-MM-ddTHH:mm:sszzz`, InvariantCulture). On a 400, surface `RawError.ReadAsString()` verbatim.
- **Pagination (cover the whole range):** request `page=1`, read `resp.TotalPages`, loop `page=2..TotalPages` (same `pageSize`/dates), accumulate `TransactionDetails`. No auto-pager exists. Do NOT stop at page 1.
- **Per-transaction fields:** `TransactionDetails.TransactionInfo (transaction_info): TransactionInformation?`.
  `TransactionInformation`: `TransactionId (transaction_id): string?`, `TransactionStatus (transaction_status): string?`
  (e.g. `S`/`P`/`V`/`D`), `TransactionAmount (transaction_amount): Money?`, `FeeAmount?`, `TransactionInitiationDate?`,
  `TransactionUpdatedDate?`, `InvoiceId (invoice_id): string?`, `CustomField (custom_field): string?`.
- **Join key = `invoice_id`** (same wire name both sides). `custom_id`→`custom_field` echo is NOT
  guaranteed (different wire names); `reference_id` is not returned. **Stamp the eShop reference into
  `InvoiceId`.**
- **Reporting lag:** executed transactions take **up to ~3 hours** to appear; covers the previous 3
  years. A recent window may return empty — expected, not a gap.

## 2.8 Client construction, auth, environment & base URL

- **Construction (source-confirmed):** `PayPalServerSdkClient(HttpClient httpClient, PayPalServerSdkClientOptions options)`, or DI `services.AddPayPalServerSdkClient(o => { … })`. `PayPalServerSdkClientOptions`: `Environment (ServerEnvironment)`, `Retry (RetryOptions)`, `Logging (LoggingOptions)`, `Server (ServerOptions)`, `Oauth2 (OAuth2ClientCredentials?)`, `Oauth2TokenStrategy?`.
- **Auth = OAuth2 client credentials, token automatic.** `options.Oauth2 = new OAuth2ClientCredentials { ClientId = <cfg>, ClientSecret = <cfg>, Scope = null }` (`ClientId`/`ClientSecret` are `required`). When set, the SDK fetches the bearer token from `/v1/oauth2/token` and attaches it to every call — you never call the token endpoint yourself.
- **Environment:** `ServerEnvironment` has **only `Sandbox`** (GAP-1). No live member.
- **Base-URL override — applies to BOTH the OAuth token request AND all API calls (source-confirmed):**
  ```csharp
  options.Server = new ServerOptions {
      Default = new DefaultOptions {
          Sandbox = new DefaultOptions.SandboxOptions { BaseUrl = cfg["PayPal:BaseUrl"] } } };
  ```
  Default host `https://api-m.sandbox.paypal.com`. Setting one `BaseUrl` redirects token + API. To
  target live, set `PayPal:BaseUrl = "https://api-m.paypal.com"` (only way, given GAP-1). The
  "must be used for EVERY call incl. token" requirement is satisfied — **no gap**.
- **Amounts:** `Money.Value` / `AmountWithBreakdown.Value` are strings the SDK does not format —
  format yourself with InvariantCulture and currency-correct decimals (Part 1 §1.10).

## 2.9 GAPS & assumptions

- **GAP-1** — No live/production `ServerEnvironment` member. Reach live only via `BaseUrl` override
  (works; source-confirmed). Not a blocker.
- **GAP-2** — No general per-request custom-header hook. `PayPal-Request-Id` idempotency is the only
  one, via the `payPalRequestId` parameter on write ops (`CreateOrder`, `AuthorizeOrder`,
  `CaptureOrder`, `CaptureAuthorizedPayment`, `ReauthorizePayment`, `RefundCapturedPayment`,
  `VoidPayment`, `CreatePaymentToken`, `CreateSetupToken`). It is **not** on reads or
  `SearchTransactions`. Acceptable — idempotency is only needed on the writes.
- **GAP-3** — No `EXPIRED` in `AuthorizationStatus`. Detect staleness via `ExpirationTime` timestamps
  and/or the 422 on capture/reauthorize — not an enum check.
- **Assumption** — currency from config; the integration owns amount decimal formatting.
- **Assumption** — reconciliation key stamped into `invoice_id` (Part 1 §1.7 uses a run-unique
  `InvoiceReference`).
- **No blockers.** Every capability this task needs is exposed by the SDK.

## 2.10 REQUIRED READING — load BEFORE writing the code for each step

The build session must load these `dotnet-*` companion skills (they carry usage hazards this sheet
deliberately omits):

| Skill | Governs |
|---|---|
| `dotnet-client-initialization` | client construction, HttpClient lifetime, DI registration |
| `dotnet-authentication` | setting `Oauth2` credentials, secret sourcing |
| `dotnet-configuration-resilience` | retries/backoff/timeouts, base-URL selection, manual pagination |
| `dotnet-calling-endpoints` | named-argument calls, async/cancellation |
| `dotnet-models` | building requests, `StringEnum<T>`, required members, wire names |
| `dotnet-error-handling` | the exception boundary (mandatory) |
| `dotnet-testing` | the HttpClient test seam |

**Two mandatory `System.Text.Json.JsonException` boundary hazards** (`dotnet-error-handling`):
- A drifted/malformed **2xx** body (e.g. a missing `required` member) throws `JsonException` on
  deserialization, **not** `SdkException` — an SDK-exception-only catch ladder lets it escape.
- A **non-2xx** body that doesn't match its `{Operation}Error`/`Error1` shape throws `JsonException`
  *while the error object is constructed*, **replacing** the `SdkException` and destroying the HTTP
  status — so mapping every `JsonException` to 5xx turns a deterministic 4xx rejection into a
  reported outage that a caller then retries forever. Handle both explicitly.

Also relevant to idempotency: **a retried non-idempotent POST without a `payPalRequestId` can
double-charge** — always pass the request-id (Part 1 §1.6) and read `dotnet-configuration-resilience`
before tuning retries.

---
---

# PART 3 — Build order, DI wiring, verification

## 3.1 Package reference

Central Package Management is on. Add the SDK version in `Directory.Packages.props`
(`<PackageVersion Include="AsadAli.Checkout.Sdk" Version="…" />` — pin the version the
`paypal-sdk` agent / plugin install step specifies), and a version-less
`<PackageReference Include="AsadAli.Checkout.Sdk" />` in **`src/Infrastructure/Infrastructure.csproj`**
only. Do **not** add it to PublicApi or ApplicationCore.

## 3.2 Build order (sequential; build after each layer)

1. **Baseline.** `dotnet restore` + `dotnet build` the untouched solution (`eShopOnWeb.sln`) with
   `DOTNET_ROLL_FORWARD=Major` so later failures are attributable to your changes.
2. **Package.** Add the SDK package refs (3.1). Restore; build (confirms the package resolves).
3. **ApplicationCore.** Enums; `Order` additions; `Payment`, `PaymentRefund`, `SavedCard`; domain
   exceptions; `IPaymentGateway` + DTOs; `CardInput`; app-service interfaces
   (`IOrderPaymentService`, `ISavedCardService`, `IReconciliationService` or fold into one);
   specifications. Build (SDK-free — should compile without the package).
4. **Infrastructure.** EF configs + `DbSet`s; EF migrations (optional under in-memory; do for
   correctness); `AmountFormatter`; `PayPalPaymentGateway` (SDK calls, mapping, error translation,
   challenge detection, paged search); `AddPayPalIntegration(IServiceCollection, IConfiguration)`
   registering the SDK client (singleton) + `IPaymentGateway`. **Load the `dotnet-*` skills first.**
   Build; route any `PayPalServerSdk.*` compile error to the warm `paypal-sdk` agent.
5. **App services.** Implement orchestration services in ApplicationCore (or Infrastructure if they
   need the gateway only) using `IPaymentGateway` + repositories. Build.
6. **PublicApi.** Env→config bridge + `PayPalOptions` binding; call `AddPayPalIntegration(...)` and
   register the app services in `Program.cs`; add the 11 `IEndpoint` classes + DTOs; extend
   `ExceptionMiddleware`. Build.
7. **Run & self-verify** (3.4). Fix. Re-run.

## 3.3 DI registration (PublicApi `Program.cs`, additive)

- Add the env→config bridge and `Configure<PayPalOptions>(GetSection("PayPal"))` (Part 1 §1.12).
- `Infrastructure.Dependencies.ConfigureServices` already registers `CatalogContext` (incl.
  in-memory) — the new `DbSet`s ride along automatically.
- Call a new `builder.Services.AddPayPalIntegration(builder.Configuration);` (Infrastructure
  extension) to register the SDK client + `IPaymentGateway`.
- Register app services: `AddScoped<IOrderPaymentService, OrderPaymentService>();`
  `AddScoped<ISavedCardService, SavedCardService>();` (+ reconciliation).
- `IRepository<>`/`IReadRepository<>` are already registered. Endpoints are auto-discovered.
- Keep all of this **PublicApi-only** — do not modify the Web project's DI (it must stay untouched).

## 3.4 Self-verification (build session runs this; no browser needed)

Run PublicApi with: `DOTNET_ROLL_FORWARD=Major`, `UseOnlyInMemoryDatabase=true`, the four
`PAYPAL_*` env vars set (sandbox business account), bound to `https://localhost:37663` (its
`launchSettings` profile). Ensure the dev cert is trusted (`dotnet dev-certs https --check`; use
`-k` with curl if needed). **Do everything in one run** (in-memory resets on restart).

Verify each with real sandbox calls using test card **Visa `4111 1111 1111 1111`**, any future
expiry (`YYYY-MM`), any CVC, any name/billing address:

1. **Token (shopper):** `POST /api/authenticate` with `demouser@microsoft.com` / `Pass@word1` → bearer.
2. **Token (admin):** same with `admin@microsoft.com` / `Pass@word1` → admin bearer.
3. **Place order** (shopper) `POST /api/orders` with catalog item ids/qty → capture `orderId`.
4. **Pay** (shopper) `POST /api/orders/{orderId}/pay` with the raw card → status `Authorized`,
   `authorizationId` present. (Re-POST once → same result, not a second authorization.)
5. **Fulfil** (admin) `POST /api/orders/{orderId}/fulfil` → status `Fulfilled`, response shows
   `capturedAmount`, `paypalFee`, `netAmount`.
6. **Refund** (shopper) `POST /api/orders/{orderId}/refunds` with an `idempotencyKey` and a partial
   `amount` → `refundId`; repeat same key → same `refundId` (no double refund); a second refund with
   a *different* key for the remainder succeeds; an amount beyond captured → 422.
7. **Cancel path** (admin): place+pay a *separate* order, then `POST /cancel` → `Cancelled` (funds
   released; no capture).
8. **Save card** (shopper) `POST /api/payment-methods` with the card → `paymentMethodId`, brand,
   `lastFourDigits` (`1111`), expiry. `GET /api/payment-methods` lists it.
9. **Reuse saved card:** place a *second* order, `POST /pay` with `{ savedPaymentMethodId }` →
   `Authorized`. Fulfil it (admin) → captured.
10. **Delete card** (shopper) `DELETE /api/payment-methods/{id}` → 204; gone from the list; paying a
    new order with that id → 404.
11. **My orders** (shopper) `GET /api/my-orders` → all the caller's orders with payment state.
12. **Reconciliation** (admin) `GET /api/reconciliation?from=&to=` over a wide past window → report
    with the three buckets. (A window covering only just-created payments may be empty — expected due
    to ~3h reporting lag; not a failure.)
13. **Ownership:** a second shopper cannot see/pay/refund the first shopper's orders or see/delete
    their cards (404).

Also confirm: `dotnet build` clean; PAN/CVC never appear in logs or the DB; amounts held/captured
equal the order total to the cent.

## 3.5 User verification guide (hand to the user at the end)

Provide a concise, copy-pasteable curl sequence mirroring 3.4 steps 1–12: authenticate (shopper +
admin), place order, pay (raw card), fulfil (admin), refund, save card, place+pay second order with
the saved card, fulfil it, and run reconciliation over a past window — each with the exact route,
required headers (`Authorization: Bearer …`, `Content-Type: application/json`), and a sample body.
State the prerequisites (env vars set, `UseOnlyInMemoryDatabase=true`, `DOTNET_ROLL_FORWARD=Major`,
trusted dev cert, all in a single run) and note that Swagger is available at
`https://localhost:37663/swagger`.

## 3.6 Guardrails recap

- PayPal facts come only from Part 2 / the warm `paypal-sdk` agent — never memory or the web.
- If a needed capability is genuinely absent from the SDK → **STOP and report the gap**; don't
  invent a workaround. (None is expected — see 2.9.)
- If a card payment returns a browser-approval challenge → **STOP and report**; do not build an
  approval round-trip (2.3).
- Secrets never enter the repo. Credential values come from env vars only.
- Additive only: do not change the Web storefront, the existing checkout, or unrelated projects.
