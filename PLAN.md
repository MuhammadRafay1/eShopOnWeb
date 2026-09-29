# eShopOnWeb + PayPal — Implementation Plan

This is the plan for a build session that has the same task brief as this session, plus this
file, and nothing else from this conversation. It is organized so that session can work
top-to-bottom: architecture and data model first, then the API surface endpoint-by-endpoint,
then cross-cutting rules (idempotency, errors, security), then a build sequence, then testing,
then the self-verification script it must run and hand back to the user. **Appendix A** (below,
produced by the `paypal-sdk` agent against the full feature list in this plan) is the grounded,
no-open-lookups PayPal contract — treat every signature/model/error fact in this plan as sourced
from there; do not re-derive a PayPal fact from memory.

## 0. Repo facts this plan relies on

(Gathered by read-only exploration for this planning session — re-verify paths still match if
the repo has moved on, but nothing here should have changed structurally.)

- Solution is `net8.0` throughout, central package versions in `Directory.Packages.props`
  (individual `.csproj` `PackageReference`s carry no `Version`).
- `src/ApplicationCore` — domain entities/interfaces/services/specifications, **no EF or 3rd-party
  SDK dependency**. `src/Infrastructure` — EF Core (`CatalogContext`, `AppIdentityDbContext`),
  `EfRepository<T>`, Identity, JWT issuance, logging adapter. `src/PublicApi` — the REST API,
  own `Program.cs`/`appsettings*.json`. `src/Web` — MVC/Razor storefront (not touched by this
  work; no storefront UI required).
- PublicApi endpoints follow the `MinimalApi.Endpoint` package convention: one class per endpoint
  implementing `IEndpoint<TResult, TRequest, TDependency...>` with `AddRoute(IEndpointRouteBuilder)`
  + `HandleAsync(...)`, auto-registered via `builder.Services.AddEndpoints()` /
  `app.MapEndpoints()` (`src/PublicApi/Program.cs:28,176`). Reference exemplar:
  `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs` (request/response/DTO split
  across sibling files, admin route via
  `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`
  directly on the minimal-API delegate, domain exceptions like `DuplicateException` thrown from
  `HandleAsync` and caught centrally).
- Central error handling: `app.UseMiddleware<ExceptionMiddleware>()`
  (`src/PublicApi/Program.cs:155`) → `src/PublicApi/Middleware/ExceptionMiddleware.cs` — currently
  a two-branch `if (exception is DuplicateException) → 409 else → 500`, serialized as
  `BlazorShared.Models.ErrorDetails`. **This plan extends that same middleware** with more
  `is`-branches rather than introducing a new pipeline.
- Admin role constant: `BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS = "Administrators"`.
  Seeded admin user: `admin@microsoft.com` (`src/Infrastructure/Identity/AppIdentityDbContextSeed.cs`).
- JWT claims contain **only** `ClaimTypes.Name` (username) + one `ClaimTypes.Role` per role — no
  `NameIdentifier`/`sub` claim (`src/Infrastructure/Identity/IdentityTokenClaimService.cs`). The
  existing convention app-wide is: **`BuyerId` == the JWT username claim**, e.g.
  `Order.BuyerId`, `Basket.BuyerId` (see `src/Web/Pages/Basket/Checkout.cshtml.cs`,
  `src/ApplicationCore/Services/OrderService.cs`). This plan's new code follows the same
  convention rather than inventing a GUID buyer id. No PublicApi endpoint today resolves "current
  user" from the token — every new shopper-scoped endpoint in this plan must bind a
  `ClaimsPrincipal` parameter (ASP.NET Core minimal APIs bind this special type automatically,
  no extra wiring needed) and use `user.Identity!.Name!` as the buyer id.
- `Order` (`src/ApplicationCore/Entities/OrderAggregate/Order.cs`) today has **no status/payment
  field at all** — `BuyerId`, `OrderDate`, `ShipToAddress` (owned value object), private
  `List<OrderItem>` exposed read-only, `Total()` summed from items. Constructor requires
  `(buyerId, shipToAddress, items)`. `OrderItem.UnitPrice` is a **snapshot** taken at order-
  creation time (via `CatalogItemOrdered`), independent of later catalog price changes — this is
  exactly the stability the payment amount needs, and this plan relies on it rather than
  re-deriving prices at pay-time.
- The existing checkout path (`IOrderService.CreateOrderAsync(basketId, address)` →
  `src/ApplicationCore/Services/OrderService.cs`) starts from a `Basket`. PublicApi's new
  order-creation endpoint must build an `Order` directly from catalog item ids/quantities — it
  cannot go through `Basket` (env gotcha: Web and PublicApi have separate in-memory stores
  anyway, so a basket-based path would be untestable through PublicApi alone). This plan adds a
  **new method to the same `IOrderService`/`OrderService`**, mirroring the existing
  lookup-catalog-items-then-build-`OrderItem`s logic, rather than a parallel order model.
  `Order`/`OrderItem` gain a couple of new fields (below); no parallel order type is introduced.
- `Buyer`/`PaymentMethod` (`src/ApplicationCore/Entities/BuyerAggregate/*`) are dead stub code —
  `PaymentMethod` has no public constructor, neither type is an EF `DbSet` or has an
  `IEntityTypeConfiguration`. **Do not resurrect or touch these** — they're unwired and
  half-built, and `BuyerId`-as-username (not `Buyer.IdentityGuid`) is this app's actual identity
  model. This plan adds a fresh, independent entity for saved cards instead (below).
- Config binding conventions in use: a plain `Get<T>()` against configuration root
  (`CatalogSettings`, `src/PublicApi/Program.cs:42-43`) and the `IOptions<T>` named-section
  pattern (`BaseUrlConfiguration.CONFIG_NAME = "baseUrls"`,
  `Configure<BaseUrlConfiguration>(GetRequiredSection(...))`, `src/PublicApi/Program.cs:48-50`).
  This plan uses the **named-section `IOptions<T>`** pattern for `PayPal:` (a `PayPalOptions`
  class with `public const string CONFIG_NAME = "PayPal";`).
- `builder.Configuration.AddEnvironmentVariables()` is called late in `Program.cs` (line 86,
  after several options are already bound from an earlier configuration snapshot) — and even
  where it isn't too late, ASP.NET Core's default env-var provider maps `PAYPAL_CLIENT_ID` to
  config key `PAYPAL_CLIENT_ID`, **not** `PayPal:ClientId` (the default section-delimiter is
  `__`, double underscore, not `_`). The task's env vars (`PAYPAL_CLIENT_ID`,
  `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`, `PAYPAL_CURRENCY`) will **not** auto-bind to
  `PayPal:*` config keys without explicit bridging code — this is called out as a required step
  in §6 below, not an assumption to skip.
- No existing `HttpClient`/`IHttpClientFactory`-based external integration exists anywhere in the
  codebase today — the PayPal SDK client is the first. The closest DI analogue is
  `IEmailSender`/`EmailSender` (interface in `ApplicationCore`, trivial implementation in
  `Infrastructure`, registered only in `Web`'s `ConfigureCoreServices` — **not** shared via
  `Infrastructure.Dependencies.ConfigureServices`). This plan follows the same interface-in-Core /
  implementation-in-Infrastructure split, but registers the PayPal-specific services from
  **PublicApi's `Program.cs` only** (Web is out of scope; no need to touch `Web`'s DI at all).
- Test conventions: `tests/PublicApiIntegrationTests` (MSTest,
  `WebApplicationFactory<Program>` + `ApiTokenHelper.GetAdminUserToken()`/`GetNormalUserToken()`,
  one test class per endpoint named `<Verb><Noun>EndpointTest`, in-memory DB via
  `appsettings.test.json`) is the project to extend for the new endpoints. `tests/UnitTests`
  (xUnit + NSubstitute) is where to unit-test the new domain services against a faked PayPal
  gateway interface.

---

## 1. Architecture — layering rule

**`ApplicationCore` must end this project with zero reference to the PayPal SDK package.**
Define a PayPal-agnostic gateway interface in `ApplicationCore` whose methods take/return only
primitives and small plain DTOs owned by `ApplicationCore` itself (never a `PayPalServerSdk.*`
type in the signature). Implement it in `Infrastructure`, mapping SDK request/response models to
those DTOs. This mirrors the existing `IEmailSender`/`EmailSender` split and keeps:
- domain/orchestration code (state machine, idempotency, ownership checks) unit-testable by
  faking one interface, with no SDK/network dependency in those tests (see §11, and
  `dotnet-testing` guidance on which seam to fake);
- the SDK dependency confined to one project, so a future SDK swap or version bump touches only
  `Infrastructure`.

New interface — `ApplicationCore/Interfaces/IPayPalPaymentGateway.cs`:

```csharp
public interface IPayPalPaymentGateway
{
    Task<AuthorizationResult> AuthorizeWithCardAsync(string idempotencyKey, decimal amount, string currency, CardDetails card, CancellationToken ct);
    Task<AuthorizationResult> AuthorizeWithVaultedCardAsync(string idempotencyKey, decimal amount, string currency, string vaultId, CancellationToken ct);
    Task<AuthorizationResult> ReauthorizeAsync(string idempotencyKey, string authorizationId, decimal amount, string currency, CancellationToken ct);
    Task<AuthorizationResult> GetAuthorizationAsync(string authorizationId, CancellationToken ct);
    Task VoidAuthorizationAsync(string idempotencyKey, string authorizationId, CancellationToken ct);
    Task<CaptureResult> CaptureAuthorizationAsync(string idempotencyKey, string authorizationId, CancellationToken ct);
    Task<CaptureResult> GetCaptureAsync(string captureId, CancellationToken ct);
    Task<RefundResult> RefundCaptureAsync(string idempotencyKey, string captureId, decimal? amount, string currency, CancellationToken ct);
    Task<RefundResult> GetRefundAsync(string refundId, CancellationToken ct);
    Task<VaultedCardResult> CreateVaultedCardAsync(string idempotencyKey, string buyerId, CardDetails card, CancellationToken ct);
    Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct);
    Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
```

DTOs (`ApplicationCore/Interfaces/PaymentGateway/*.cs`, all plain records/classes, no SDK types):
`CardDetails { Number, ExpiryMonth, ExpiryYear, SecurityCode, CardholderName, BillingAddressLine1, BillingAddressLine2?, City, State?, PostalCode, CountryCode }`;
`AuthorizationResult { PayPalOrderId, PayPalAuthorizationId, StatusRaw, Amount, ExpiresAt, RequiresPayerAction (bool) }`;
`CaptureResult { PayPalCaptureId, StatusRaw, CapturedAmount, PayPalFeeAmount, NetAmount }`;
`RefundResult { PayPalRefundId, StatusRaw, Amount, TotalRefundedOnCapture }`;
`VaultedCardResult { VaultId, Brand, LastFour, Expiry }`;
`PayPalTransactionRecord { TransactionId, Amount, CurrencyCode, StatusRaw, InitiatedAt, InvoiceId?, CustomField? }`.
`StatusRaw` fields carry the SDK enum's string value (`.ToString()`/wire value) — `ApplicationCore`
does not take a dependency on the SDK's enum types either; keep a small `ApplicationCore`-owned
enum only where the domain state machine needs to branch (see §2).

`RequiresPayerAction` on `AuthorizationResult` is how `PayerActionRequired` (§Appendix A, Order
row) crosses the gateway boundary — the orchestration service in §4 must check this flag
immediately after every authorize call and **stop** (return a clear "requires manual approval,
not supported" domain error) rather than following any `Links`. Per the task: if this is ever
observed against the sandbox Visa test card during self-verification, stop and report it — do
not build an approval round-trip.

Implementation — `Infrastructure/Services/PayPalPaymentGateway.cs` — constructs SDK requests and
maps SDK responses per **every row of Appendix A**; every call there passes
`prefer: "return=representation"` (Appendix A rule R1) and a `payPalRequestId` (rule R2). Money
values cross the boundary as `decimal` (`ApplicationCore`) and are formatted to the SDK's string
`Value` field only inside this class (`amount.ToString("F2", CultureInfo.InvariantCulture)` — R4).

## 2. Domain model changes (all additive)

`src/ApplicationCore/Entities/OrderAggregate/`:
- **`Order`** gains: `OrderStatus Status { get; private set; }` (default `AwaitingPayment`),
  `string Currency { get; private set; }` (snapshotted at creation from `PayPal:Currency`, so a
  later config change never retroactively changes what an existing order is priced in). Add
  domain methods `MarkAuthorized()`, `MarkCancelled()`, `MarkFulfilled()`,
  `MarkPartiallyRefunded()`, `MarkRefunded()`, `MarkAwaitingPayment()` (the last one is the
  revert-for-retry path used when an authorization becomes unrecoverably stale — see §4.3) that
  each validate the current state before transitioning (`Ardalis.GuardClauses`, matching this
  entity's existing style) — keep the state machine's legal transitions inside the entity, not
  scattered across endpoint handlers.
- **`OrderStatus`** (new enum, own file `OrderStatus.cs`): `AwaitingPayment`, `Authorized`,
  `Cancelled`, `Fulfilled`, `PartiallyRefunded`, `Refunded`.
- **`OrderPayment`** (new aggregate root, own file, 1:1 with `Order` via a unique `OrderId`):
  `Id`, `OrderId`, `Currency`, `PayPalOrderId`, `PayPalAuthorizationId` (current/latest — replaced
  in place on reauthorization), `AuthorizedAmount`, `AuthorizationExpiresAt` (`DateTimeOffset`),
  `PayPalCaptureId?`, `CapturedAmount?`, `PayPalFeeAmount?`, `NetAmount?`,
  `SavedPaymentMethodId?` (FK, set only when paid via a saved card), `TotalRefundedAmount`
  (`decimal`, starts at 0, updated by `RecordRefund`), `CreatedAt`, `UpdatedAt`, a private
  `List<Refund> _refunds` exposed read-only as `Refunds` (same private-field/read-only-wrapper
  pattern as `Order.OrderItems`). Domain methods: `RecordAuthorization(...)`,
  `RecordReauthorization(newAuthorizationId, newExpiry)`, `RecordCapture(captureId, captured,
  fee, net)`, `RecordVoid()`, `RecordRefund(refund)` (appends to `_refunds`, updates
  `TotalRefundedAmount`), `decimal RemainingRefundable()` computed as
  `CapturedAmount - TotalRefundedAmount` — used for the over-refund guard in §4.4.
- **`Refund`** (new entity, child of `OrderPayment`, own file): `Id`, `OrderPaymentId`,
  `PayPalRefundId`, `Amount`, `IdempotencyKey`, `CreatedAt`.

`src/ApplicationCore/Entities/PaymentMethodAggregate/`  *(new folder — deliberately not
`BuyerAggregate`, to avoid any coupling to the disused `Buyer`/`PaymentMethod` stub)*:
- **`SavedPaymentMethod`** (new aggregate root): `Id`, `BuyerId` (string, the JWT username — same
  convention as `Order.BuyerId`), `PayPalVaultId`, `CardBrand?`, `LastFour?`, `Expiry?`,
  `CardholderName?`, `CreatedAt`. No raw PAN/CVV field exists anywhere on this type — enforce that
  at review time, not just by convention.

`IOrderService` (`src/ApplicationCore/Interfaces/IOrderService.cs` /
`src/ApplicationCore/Services/OrderService.cs`) gains a new method (existing
`CreateOrderAsync(basketId, address)` is untouched, so `Web` needs no change):
```csharp
Task<Order> CreateOrderFromItemsAsync(string buyerId, Address shippingAddress, IEnumerable<(int CatalogItemId, int Quantity)> items);
```
Implementation mirrors the existing `CreateOrderAsync` body: load the referenced `CatalogItem`s
(one query via a new `CatalogItemsByIdsSpecification`, echoing the existing
`CatalogItemsSpecification` used for the basket path), build one `CatalogItemOrdered` +
`OrderItem` per requested line using the **current catalog price** (snapshotted into
`OrderItem.UnitPrice`, same as today), construct `new Order(buyerId, shippingAddress, items)`,
set `Currency` from `PayPal:Currency`, persist via `IRepository<Order>.AddAsync`, return the
persisted `Order` (needed immediately for the `orderId` response field — unlike the existing
`Task`-returning method, this one must return the entity).

## 3. Persistence changes

`src/Infrastructure/Data/Config/` gains `OrderPaymentConfiguration`, `RefundConfiguration` (or a
nested config inside `OrderPaymentConfiguration` for the child `Refunds` navigation — mirror
`OrderConfiguration`'s exact pattern for the private-field navigation:
`builder.Metadata.FindNavigation(nameof(OrderPayment.Refunds))?.SetPropertyAccessMode(PropertyAccessMode.Field);`),
and `SavedPaymentMethodConfiguration`. Also update `OrderConfiguration` for the two new `Order`
properties (`Status` as a stored enum — `HasConversion<string>()` or the default int mapping,
either is fine, pick one and be consistent; `Currency` as a required, short `HasMaxLength`
string).

Add to `CatalogContext` (`src/Infrastructure/Data/CatalogContext.cs`): `DbSet<OrderPayment>
OrderPayments`, `DbSet<Refund> Refunds`, `DbSet<SavedPaymentMethod> SavedPaymentMethods`.
Constraints to add explicitly (don't rely on defaults):
- Unique index on `OrderPayment.OrderId` (enforces the 1:1 and gives the create-payment race a
  DB-level guard — see §4.2).
- Unique **composite** index on `(Refund.OrderPaymentId, Refund.IdempotencyKey)` — this is the
  hard backstop for refund idempotency (§4.4), independent of the app-level pre-check and
  independent of PayPal's own ~45-day dedup window.
- `SavedPaymentMethod.BuyerId` indexed (non-unique — one buyer has many cards) for the
  list-my-cards query.

Add an EF Core migration on `CatalogContext` for all of the above (`Order.Status`/`Currency`
columns + three new tables) — **do this even though local self-verification runs against
`UseOnlyInMemoryDatabase=true`, which ignores migrations.** The task asks for a production-grade
integration; a real deployment uses `UseSqlServer`, and without a migration the new columns/tables
simply won't exist there. Generate it with
`dotnet ef migrations add AddPaymentsAndSavedCards --project src/Infrastructure --startup-project src/PublicApi`.

## 4. Payment orchestration service (ApplicationCore)

New interfaces + implementations in `ApplicationCore/Interfaces` and `ApplicationCore/Services`
(constructor-injected `IPayPalPaymentGateway`, `IRepository<Order>`, `IRepository<OrderPayment>`,
`IRepository<SavedPaymentMethod>`, `IAppLogger<T>` — same DI shapes already registered in
`Program.cs`):

- **`IOrderPaymentService`** — `AuthorizeAsync`, `FulfilAsync`, `CancelAsync`,
  `RefundAsync(orderId, buyerId-for-ownership-check, amount?, idempotencyKey)`. One method per
  caller-invocable action (matches the task's "each action a caller can take stays separately
  invocable" requirement) — this service is the thing each of the four endpoint handlers in §7
  calls, so route-level separation and service-level separation match 1:1.
- **`ISavedPaymentMethodService`** — `SaveCardAsync(buyerId, card)`, `ListAsync(buyerId)`,
  `DeleteAsync(buyerId, paymentMethodId)`.
- **`IReconciliationService`** — `BuildReportAsync(from, to)` (chunking + matching logic, §9) —
  kept separate from `IOrderPaymentService` since it's a report, not a lifecycle transition.

### 4.1 Idempotency keys — deterministic, namespaced

None of `pay`/`fulfil`/`cancel` take a caller-supplied idempotency key (only `refunds` does, per
the task). Idempotency for those three is achieved by **(a)** an app-level state check before
ever calling PayPal, **plus (b)** a deterministic `payPalRequestId` as defense-in-depth against a
race that slips past (a) (Appendix A rule R2 — same key = safe retry of the *same* logical write).
Use a stable prefix so these never collide with a caller-supplied refund key:
`"eshop-authorize-{orderId}"`, `"eshop-reauthorize-{orderId}-{currentAuthorizationId}"`,
`"eshop-capture-{orderId}-{authorizationId}"`, `"eshop-void-{orderId}-{authorizationId}"`. For
refunds, send `"eshop-refund-{orderId}-{callerSuppliedKey}"` — namespaced so a shopper's raw key
can never accidentally collide with these deterministic ones for a different order.

### 4.2 `AuthorizeAsync` (called by `POST /api/orders/{orderId}/pay`)

1. Load `Order` by id; 404 if missing or `order.BuyerId != callerBuyerId` (ownership — see §8 for
   the 404-not-403 rationale).
2. If an `OrderPayment` row already exists for this order: its presence **is** the idempotency
   check — return its current state as a success response without calling PayPal again (handles
   the double-click case directly). If `Order.Status` is `Cancelled`, return a conflict instead
   (paying a cancelled order is not a retry, it's invalid).
3. Otherwise: validate exactly one of `Card`/`PaymentMethodId` is supplied (400 if both or
   neither). If `PaymentMethodId`: load `SavedPaymentMethod`, 404 if missing or not owned by the
   caller (same ownership rule as saved cards generally — §8).
4. Compute `amount = order.Total()`, `currency = order.Currency`. Call
   `AuthorizeWithCardAsync`/`AuthorizeWithVaultedCardAsync` with the §4.1 key.
5. If `AuthorizationResult.RequiresPayerAction` — stop, return the domain error described in §1,
   do not persist an `OrderPayment` row.
6. Otherwise persist a new `OrderPayment` (insert). **Concurrency note:** two concurrent first-time
   `pay` calls can both pass step 2's check before either commits; rely on the §3 unique index on
   `OrderPayment.OrderId` to make the second insert fail, catch that specific failure, and treat
   it exactly like step 2 (fetch and return the winner's row) rather than surfacing a 500 — this
   is what actually makes step 2 race-safe, not just "check-then-act".
7. `order.MarkAuthorized()`, save.

### 4.3 `FulfilAsync` (called by `POST /api/orders/{orderId}/fulfil`, admin-only)

1. Load `Order` + its `OrderPayment`. If `Order.Status` is already `Fulfilled` — idempotent
   no-op, return the existing capture info. If `Order.Status` is not `Authorized` — 409 (nothing
   to fulfil, or already cancelled/refunded).
2. **Staleness check before capture, not after:** if `DateTimeOffset.UtcNow` is at or past
   `OrderPayment.AuthorizationExpiresAt` (small safety buffer, e.g. 60s), call `ReauthorizeAsync`
   first; on success, `OrderPayment.RecordReauthorization(newId, newExpiry)` and proceed with the
   *new* authorization id.
3. Call `CaptureAuthorizationAsync`. **Fallback for the case the proactive check missed** (the
   exact validity window PayPal grants a fresh authorization is not a fact Appendix A states —
   don't hard-code an assumed number of days; trust `ExpirationTime`/`AuthorizationExpiresAt` as
   the only source of truth, and treat a capture failure as *possibly* expiry-related): if the
   capture call throws, attempt exactly one `ReauthorizeAsync` + retry-capture cycle. If that also
   fails, this is the "cannot be renewed" case the task calls out — per Appendix A's staleness
   note, treat it as unrecoverable, **do not** leave the order stuck: call `order.MarkAwaitingPayment()`
   (reverts to a state where the shopper can call `pay` again — same order, no need to create a
   new one) and return a 409 whose message says exactly that ("this order's payment hold could
   not be renewed and must be paid again before it can be fulfilled") — that is the "operator can
   act on it" language the task asks for.
4. On successful capture: `OrderPayment.RecordCapture(...)`, `order.MarkFulfilled()`, save.

### 4.4 `CancelAsync` (called by `POST /api/orders/{orderId}/cancel`, admin-only)

1. Load `Order`. If `Status == Cancelled` — idempotent no-op. If `Status == Fulfilled` or beyond —
   409 ("already fulfilled; use refund instead"). If `Status == AwaitingPayment` (never paid) —
   just `order.MarkCancelled()`, no PayPal call needed (nothing to release). If
   `Status == Authorized` — call `VoidAuthorizationAsync`, then `OrderPayment.RecordVoid()` (or
   simply leave `OrderPayment` as-is and rely on `Order.Status`; either is fine, pick one and be
   consistent — this plan's entity list above doesn't add a separate status enum on
   `OrderPayment`, so `Order.Status` is the single source of truth for lifecycle state; `RecordVoid()`
   can just be a no-op/marker if there's nothing else to record), `order.MarkCancelled()`.

### 4.5 `RefundAsync` (called by `POST /api/orders/{orderId}/refunds`, **shopper-scoped, not
admin** — see §8 on why, this is intentional per the task, not an oversight)

1. Load `Order` + `OrderPayment`; 404 if missing or not owned by caller. 409 if
   `Order.Status` is not `Fulfilled`/`PartiallyRefunded` (nothing captured yet, or already
   cancelled).
2. **Idempotency (caller-supplied key, per the task):** look up an existing `Refund` row by
   `(OrderPaymentId, IdempotencyKey)`. If found, return it as-is — no new PayPal call. This is a
   real DB-level guarantee (§3's unique composite index), not just a pre-check.
3. If not found: compute `remaining = OrderPayment.RemainingRefundable()`. If the request supplies
   an explicit amount, it must be `<= remaining` (400 otherwise — this is the app-level guard that
   "a partly-refunded order must never become refundable beyond what was captured", enforced
   *before* ever calling PayPal, in addition to whatever PayPal itself would separately reject).
   If no amount given, refund exactly `remaining` (full/remaining refund).
4. Call `RefundCaptureAsync` with the §4.1 namespaced key. On success: persist the new `Refund`
   row, `OrderPayment.RecordRefund(...)`, and set `Order.Status` to `Refunded` if
   `RemainingRefundable() == 0` afterward, else `PartiallyRefunded`.
5. Two distinct partial refunds of the same capture are two different `idempotencyKey` values
   from the caller — nothing here prevents that; only a *repeated* key on the *same* logical
   refund is deduplicated, which is exactly the required behavior.

## 5. Saved cards (`ISavedPaymentMethodService`)

- **`SaveCardAsync(buyerId, card)`** → `CreateVaultedCardAsync` (a fresh idempotency key per
  call — saving a card is not something a caller retries with the same key; a genuine double
  submission just creates two vault tokens, which is acceptable and matches how e.g. adding a
  card twice behaves on most real checkout pages). Persist a `SavedPaymentMethod` row with the
  returned `VaultId`/`Brand`/`LastFour`/`Expiry`. Never persist `card.Number`/`SecurityCode`
  anywhere — those two fields exist only on the transient `CardDetails` request DTO, are read
  once to build the SDK call, and must never appear in a log statement or exception message (see
  §8's logging rule).
- **`ListAsync(buyerId)`** → `IRepository<SavedPaymentMethod>` filtered by `BuyerId` (a
  `SavedPaymentMethodsByBuyerIdSpecification`, same `Ardalis.Specification` style as existing
  specs).
- **`DeleteAsync(buyerId, paymentMethodId)`** → load the row, 404 if missing or
  `BuyerId != buyerId` (never leak whether a card id owned by someone else exists). Call
  `DeleteVaultedCardAsync(vaultId)` **first** (revokes it PayPal-side — this operation exists on
  the SDK's `Vault` controller, confirmed as a follow-up to Appendix A; see the addendum in
  Appendix A §2.2), then delete the local row. If the PayPal delete call throws, do **not** delete
  the local row (fail the whole operation) — otherwise a card that failed to revoke PayPal-side
  would still show as "removed" locally while remaining chargeable state on PayPal's side, which
  would violate "must no longer be usable to pay" in spirit even if not in practice through this
  app's own API.

## 6. Configuration & DI wiring

New file `src/PublicApi/PayPalOptions.cs` (or `Infrastructure` — either is fine; PublicApi is
simplest since only PublicApi consumes it):
```csharp
public class PayPalOptions
{
    public const string CONFIG_NAME = "PayPal";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Environment { get; set; } = "Sandbox";
    public string Currency { get; set; } = "USD";
    public string? BaseUrl { get; set; }
}
```

Add a `"PayPal"` section to `src/PublicApi/appsettings.json` (and `.Development.json`/
`.Docker.json`) with **empty placeholder values only** — never a real secret:
```json
"PayPal": { "ClientId": "", "ClientSecret": "", "Environment": "Sandbox", "Currency": "USD", "BaseUrl": "" }
```

**Required env-var bridge** (per §0's finding — this is not optional). In `src/PublicApi/Program.cs`,
after `builder.Configuration.AddEnvironmentVariables();` (currently line 86), add:
```csharp
static void BridgeEnvVar(ConfigurationManager config, string envVarName, string configKey)
{
    var value = System.Environment.GetEnvironmentVariable(envVarName);
    if (!string.IsNullOrWhiteSpace(value)) config[configKey] = value;
}
BridgeEnvVar(builder.Configuration, "PAYPAL_CLIENT_ID", "PayPal:ClientId");
BridgeEnvVar(builder.Configuration, "PAYPAL_CLIENT_SECRET", "PayPal:ClientSecret");
BridgeEnvVar(builder.Configuration, "PAYPAL_ENVIRONMENT", "PayPal:Environment");
BridgeEnvVar(builder.Configuration, "PAYPAL_CURRENCY", "PayPal:Currency");
BridgeEnvVar(builder.Configuration, "PAYPAL_BASE_URL", "PayPal:BaseUrl"); // optional override; task doesn't name an env var for this one — only bridge it if actually set, config-file/appsettings value otherwise governs
```
then bind and register (mirrors the `BaseUrlConfiguration` pattern at `Program.cs:48-50`):
```csharp
var payPalSection = builder.Configuration.GetSection(PayPalOptions.CONFIG_NAME);
builder.Services.Configure<PayPalOptions>(payPalSection);
var payPalOptions = payPalSection.Get<PayPalOptions>() ?? new PayPalOptions();
```

Register the SDK client and gateway (per Appendix A §2.1 — use named arguments, not positional,
per the trap notes) and the new services, all in `Program.cs` (PublicApi-only, per §0):
```csharp
builder.Services.AddPayPalServerSdkClient(options =>
{
    options.Environment = PayPalServerSdk.Servers.ServerEnvironment.Sandbox; // only member this SDK ships — see Appendix A Gaps
    options.Oauth2 = new PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials
    {
        ClientId = payPalOptions.ClientId,
        ClientSecret = payPalOptions.ClientSecret
    };
    if (!string.IsNullOrWhiteSpace(payPalOptions.BaseUrl))
        options.Server.Default.Sandbox.BaseUrl = payPalOptions.BaseUrl;
});
builder.Services.AddScoped<IPayPalPaymentGateway, PayPalPaymentGateway>();
builder.Services.AddScoped<IOrderPaymentService, OrderPaymentService>();
builder.Services.AddScoped<ISavedPaymentMethodService, SavedPaymentMethodService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
```
Log (at startup, `Information` level, never at `Debug`/anywhere card data could later be added
nearby) `payPalOptions.Environment` and whether `BaseUrl` is overridden — never log `ClientSecret`.

NuGet: add `AsadAli.Checkout.Sdk` to `Directory.Packages.props` (`<PackageVersion Include="AsadAli.Checkout.Sdk" Version="1.0.1" />`
— pin explicitly; verify against the currently-published version at implementation time) and a
version-less `<PackageReference Include="AsadAli.Checkout.Sdk" />` to
`src/Infrastructure/Infrastructure.csproj` (the gateway implementation lives there; PublicApi
only needs the DI extension method, which is exposed from the same package's root namespace —
add the `PackageReference` to `src/PublicApi/PublicApi.csproj` too if `AddPayPalServerSdkClient`
isn't reachable transitively).

## 7. API surface (all under `/api/`, all in `src/PublicApi`, `MinimalApi.Endpoint` convention,
one class per endpoint per §0)

Every endpoint below binds `ClaimsPrincipal user` for the caller's identity (`user.Identity!.Name!`
= buyer id) unless marked **admin**, in which case it also uses the
`[Authorize(Roles = ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`
attribute exactly as `CreateCatalogItemEndpoint` does; every other endpoint uses plain
`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` (any authenticated
user, no role requirement). Response bodies are this plan's own design (task leaves the shape
open beyond the three named top-level identifier fields) — keep them consistent internally.

| Endpoint | Route | Auth | Request | Response (top-level fields shown) |
|---|---|---|---|---|
| Create order | `POST api/orders` | shopper | `{ shippingAddress: {street,city,state,country,zipCode}, items: [{catalogItemId, quantity}] }` | `{ orderId, status, total, currency, items:[...] }` |
| Pay for order | `POST api/orders/{orderId}/pay` | shopper, own order only | `{ card: {number,expiryMonth,expiryYear,securityCode,cardholderName,billingAddress:{...}} }` **or** `{ paymentMethodId }` — exactly one | `{ orderId, status, authorizedAmount, currency, authorizationExpiresAt }` |
| Fulfil order | `POST api/orders/{orderId}/fulfil` | **admin** | *(none)* | `{ orderId, status, capturedAmount, payPalFee, netAmount, currency }` |
| Cancel order | `POST api/orders/{orderId}/cancel` | **admin** | *(none)* | `{ orderId, status }` |
| Refund order | `POST api/orders/{orderId}/refunds` | shopper, own order only | `{ amount?: decimal, idempotencyKey: string }` | `{ refundId, payPalRefundId, orderId, amount, status, remainingRefundable }` |
| My orders | `GET api/my-orders` | shopper | — | `{ orders: [{ orderId, status, total, currency, capturedAmount?, netAmount?, createdAt }] }` |
| Reconciliation | `GET api/reconciliation?from={iso}&to={iso}` | **admin** | — | `{ from, to, matched:[...], payPalOnly:[...], localOnly:[...], summary:{...} }` (§9) |
| Save card | `POST api/payment-methods` | shopper | `{ card: {number,expiryMonth,expiryYear,securityCode,cardholderName,billingAddress:{...}} }` | `{ paymentMethodId, brand, lastFour, expiry }` |
| List cards | `GET api/payment-methods` | shopper | — | `{ paymentMethods: [{ paymentMethodId, brand, lastFour, expiry, createdAt }] }` |
| Delete card | `DELETE api/payment-methods/{paymentMethodId}` | shopper, own card only | — | `204 No Content` |

`orderId`/`paymentMethodId`/`refundId` are the **internal** entity ids (`int`, from `BaseEntity.Id`)
— matches how the task frames them ("its identifier") and is consistent across all three; PayPal's
own id for the same resource (e.g. `payPalRefundId`) is included alongside for traceability but is
never the top-level field the task names.

Each endpoint's `HandleAsync` stays thin (mirrors `CreateCatalogItemEndpoint`'s style): resolve
buyer id from `ClaimsPrincipal`, call exactly one method on `IOrderPaymentService` /
`ISavedPaymentMethodService` / `IReconciliationService` / the extended `IOrderService`, map the
result to the response DTO, return `Results.Ok/Created/NoContent/Conflict/NotFound` — all the
state-machine and idempotency logic lives in the services from §4-5, not in the endpoint classes.

## 8. Cross-cutting rules

- **Ownership → 404, not 403.** For every shopper-scoped endpoint that takes an `orderId` or
  `paymentMethodId`, a resource that exists but belongs to a different buyer returns the same 404
  as a resource that doesn't exist at all — never reveal that another user's resource exists.
  Admin endpoints (`fulfil`, `cancel`, `reconciliation`) act on any order; no ownership check
  there by design.
- **Exception → HTTP status mapping.** Extend `ExceptionMiddleware`'s `if`-chain (keep the
  existing `DuplicateException` branch) with: `OrderNotFoundException`/`PaymentMethodNotFoundException`
  → 404; `InvalidPaymentRequestException` (both/neither card+paymentMethodId, refund amount
  validation) → 400; `PaymentDeclinedException` (wraps the PayPal `Error.Name`/`Message`, never
  raw card data) → 402; `OrderStateConflictException` (wrong-state transition attempts) → 409;
  `AuthorizationCannotBeRenewedException` (§4.3) → 409 with the actionable message; any other
  `SdkException<T>`/`JsonException` surfacing from the gateway → 502 (a PayPal-side problem, not
  this app's fault, and — per the `dotnet-error-handling` hazard already flagged in Appendix A —
  must **not** be silently folded into the generic 500 branch, since a 500 invites a caller retry
  that a deterministic rejection will never satisfy).
- **Card data never persisted, never logged.** `CardDetails.Number`/`SecurityCode` exist only on
  request DTOs, live only for the duration of one request, and are read exactly once (to build the
  SDK call). No log statement, exception message, or Swagger example may include them. Optionally
  extend `CustomSchemaFilters` (`src/PublicApi/CustomSchemaFilters.cs`, which already hides
  `CorrelationId` from Swagger schemas) to also scrub these two fields from generated examples —
  polish, not required.
- **Currency.** Single app-wide currency from `PayPal:Currency`, snapshotted onto each `Order` at
  creation (§2) — there is no per-item or multi-currency handling in this app, consistent with the
  task's framing ("the currency comes from configuration").
- **Amount-to-the-cent.** `Order.Total()` is a `decimal` sum of snapshotted `OrderItem.UnitPrice *
  Units` — stable between order creation and payment. Format as `"F2"` (Appendix A rule R4) when
  building the SDK's `Money.Value` string; never round differently in two places.

## 9. Reconciliation report (`IReconciliationService.BuildReportAsync`)

1. Validate `from <= to` (both ISO-8601 `DateTimeOffset`, 400 on unparseable input).
2. Chunk `[from, to]` into ≤31-day windows (Appendix A: `SearchTransactions`'s per-call max
   range) and call `SearchTransactionsAsync` once per chunk; within each chunk, page until
   exhausted (Appendix A: page-number pagination, no auto-pager) — the report must cover the
   *entire* requested range across every chunk and every page, not just the first page of the
   first chunk.
3. Load local `OrderPayment`s (+ their `Refund`s) whose `PayPalAuthorizationId`/`PayPalCaptureId`/
   `Refund.PayPalRefundId` could plausibly fall in range (simplest: load all `OrderPayment`s
   updated within `[from, to]` plus a small lookback/lookahead buffer, rather than trying to
   pre-filter by a PayPal-side timestamp this app doesn't itself store precisely).
4. **Matching key — a design decision, not a gap:** attempt to match each PayPal
   `TransactionRecord.TransactionId` against any of the three PayPal ids stored locally
   (`PayPalAuthorizationId`, `PayPalCaptureId`, any `Refund.PayPalRefundId`) for that
   `OrderPayment`. PayPal's transaction-search `transaction_id` for a card capture/refund is
   generally the same id as the capture/refund resource id, but Appendix A does not guarantee
   this — **the build session must verify this empirically against real sandbox search results
   during self-verification** and adjust the matching field if a different correlation turns out
   to be more reliable (e.g. `InvoiceId`/`CustomField`, if this app starts stamping
   `PurchaseUnitRequest.InvoiceId`/`CustomId` with the local `orderId` at authorize/capture time —
   worth doing regardless, as a second correlation path independent of id-equality).
5. Response: `matched` (paired local + PayPal records), `payPalOnly` (PayPal knows about it,
   eShop has no matching local record — the task's explicit "eShop doesn't know" case),
   `localOnly` (eShop has a captured/refunded payment with no matching PayPal transaction in this
   range — the reverse case), plus a `summary` of counts. **An empty result for a range that was
   just created is an expected sandbox artifact** (PayPal's transaction reporting lags live
   activity) — do not treat it as a bug; verify the report's correctness against an older
   date range that has had time to appear in PayPal's reporting.

## 10. Build sequence (for the build session)

This follows the `integrate-paypal` skill's plan-first workflow, which that session must load
before writing any code — this file is *not* a substitute for that skill's process, it's the
plan the skill's Step 1 tells that session to work from once its own `paypal-plan.md`/contract
sheet exists (here, folded into Appendix A). Suggested order once past that gate:

1. `Directory.Packages.props` + `Infrastructure.csproj`/`PublicApi.csproj` — add the SDK package
   reference (§6). Confirm `dotnet restore` succeeds before writing any code against the SDK.
2. Domain entities/enums (§2) → EF configs/DbSets/migration (§3). Build.
3. `IPayPalPaymentGateway` + DTOs (§1) → `PayPalPaymentGateway` implementation against every
   Appendix A row, including the Vault-delete addendum. Build.
4. `PayPalOptions` + env-var bridge + DI registration (§6). Smoke-test client construction alone
   (e.g. a throwaway console line or a unit test that resolves `PayPalServerSdkClient` from the
   container) before wiring any endpoint to it.
5. `IOrderService.CreateOrderFromItemsAsync` (§2) → `POST /api/orders` endpoint. Verify this one
   works end-to-end (real HTTP call, real JWT) before touching payment code — it has no PayPal
   dependency and isolates basic wiring/auth problems from PayPal-specific ones.
6. `IOrderPaymentService.AuthorizeAsync` (§4.2) → `POST /api/orders/{orderId}/pay`. Verify a real
   sandbox authorization with the Visa test card before proceeding.
7. `ISavedPaymentMethodService` (§5) → the three `payment-methods` endpoints. Verify save → list →
   pay-a-second-order-with-the-saved-card → delete → confirm it's gone and no longer payable.
8. `IOrderPaymentService.FulfilAsync`/`CancelAsync` (§4.3-4.4) → the two admin endpoints. Verify a
   real capture (fee/net populated) and, on a separate order, a real cancel-before-fulfilment.
9. `IOrderPaymentService.RefundAsync` (§4.5) → the refunds endpoint. Verify a real partial refund,
   then repeat the exact same request (same idempotency key) and confirm no second PayPal refund
   occurs, then a second *distinct* partial refund with a new key.
10. `GET /api/my-orders`.
11. `IReconciliationService` (§9) → `GET /api/reconciliation`. Verify against an older date range.
12. `ExceptionMiddleware` extensions (§8) — do this incrementally as each new exception type is
    introduced above, not as one final pass.

## 11. Testing

- **Unit tests** (`tests/UnitTests`, xUnit + NSubstitute, existing convention): fake
  `IPayPalPaymentGateway` (per `dotnet-testing` — fake at this seam, not inside the SDK client) to
  cover, without any network dependency: the `AuthorizeAsync` double-click short-circuit and the
  unique-index race fallback, the `FulfilAsync` proactive-reauth and unrecoverable-reauth →
  revert-to-`AwaitingPayment` paths, the `RefundAsync` idempotency-key replay and over-refund
  guard, and ownership checks (404 for a non-owned order/card) on every shopper-scoped service
  method. Add test-data builders (`tests/UnitTests/Builders/`) for `OrderPayment`/`SavedPaymentMethod`
  mirroring the existing `OrderBuilder`/`AddressBuilder`.
- **Endpoint integration tests** (`tests/PublicApiIntegrationTests`, MSTest, existing convention):
  one `<Verb><Noun>EndpointTest` class per endpoint in §7, using `ProgramTest.NewClient` +
  `ApiTokenHelper`. These run against the in-memory DB (no real PayPal call) — inject a fake
  `IPayPalPaymentGateway` via the test host's service overrides (same technique
  `TestApiApplication` in `tests/FunctionalTests/PublicApi/ApiTestFixture.cs` already uses to swap
  `DbContext` registrations) so these tests are deterministic and network-free. Cover at minimum:
  401/403 on missing/wrong-role token for every admin endpoint; 404 for another buyer's
  order/card; the full happy-path status-code/shape for each row in §7's table.
- **A small opt-in real-sandbox smoke suite** (either a few `[Fact]`s gated on
  `Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID") is not null` so default `dotnet test`
  runs skip them, or just the manual script in §12 — either satisfies the task's "self-verify...
  a real authorization... a real capture... a real refund... a saved card reused" requirement,
  but at least one of the two must actually execute against sandbox before this task is called
  done, per the task's Rules of Engagement).

## 12. Self-verification (build session must run this, then hand a trimmed version to the user)

Run with `DOTNET_ROLL_FORWARD=Major`, `UseOnlyInMemoryDatabase=true`, the four `PAYPAL_*` env vars
set, PublicApi bound to its assigned port block. All calls below are through **PublicApi only**
(per the env gotcha — Web and PublicApi don't share state under the in-memory provider).

1. `POST api/authenticate` as `demouser@microsoft.com` / `Pass@word1` → bearer token (shopper).
   Same for `admin@microsoft.com` (admin token).
2. `POST api/orders` (shopper token) with 1-2 real catalog item ids/quantities + a shipping
   address → note `orderId`, `total`.
3. `POST api/orders/{orderId}/pay` (shopper token) with the sandbox Visa
   `4111 1111 1111 1111`, any future expiry, any CVC/name/address → expect `Authorized`, an
   `authorizedAmount` equal to step 2's `total` to the cent. **If the response instead indicates a
   payer-action-required/challenge outcome, stop here and report it — do not attempt to build or
   drive an approval redirect.**
4. `GET api/my-orders` (shopper token) → confirm the order and its `Authorized` status appear, and
   that it belongs only to this shopper (repeat with a second shopper account and confirm the
   first order is absent from the second shopper's list).
5. `POST api/orders/{orderId}/fulfil` (admin token) → expect `Fulfilled`, with `capturedAmount`
   equal to step 2's total, plus a real, non-zero `payPalFee` and `netAmount` from PayPal.
6. `POST api/orders/{orderId}/refunds` (shopper token, owner) with a partial `amount` and an
   `idempotencyKey` → expect success, `remainingRefundable` reduced accordingly. Repeat the exact
   same request body → expect the identical `refundId` back, and confirm (e.g. via
   `GET api/reconciliation` or direct inspection) that PayPal was not charged/refunded twice.
   Then submit a second, distinct partial refund with a new `idempotencyKey` → expect it to
   succeed independently.
7. Separately: `POST api/orders` a second order → `POST api/payment-methods` (shopper token) with
   the same Visa test card → note `paymentMethodId` → `POST` the second order's `/pay` with
   `{ paymentMethodId }` instead of raw card fields → expect `Authorized` using the saved card,
   with no card number sent on this call. `GET api/payment-methods` → confirm it's listed.
   `DELETE api/payment-methods/{paymentMethodId}` → `GET api/payment-methods` again → confirm it's
   gone → attempt to `pay` a third order with that same `paymentMethodId` → expect it to be
   rejected (404/400), confirming it's no longer usable.
8. Separately, on a **freshly created, unpaid** order: `POST api/orders/{orderId}/cancel` (admin
   token) → expect `Cancelled` with no PayPal call made (never authorized). On a **paid,
   unfulfilled** order: `pay` it, then `cancel` it (admin token) → expect `Cancelled` and confirm
   (via PayPal's sandbox dashboard or a `GetAuthorizedPayment` check) the hold was voided, not
   captured.
9. `GET api/reconciliation?from=...&to=...` (admin token) over a range covering the activity just
   generated *and* over an older range with no fresh activity → confirm the report returns
   sensible `matched`/`payPalOnly`/`localOnly` sets for the older range; an empty result for the
   just-created range is expected (reporting lag) and should be explained as such, not reported
   as a failure.
10. Attempt every admin-only endpoint (`fulfil`, `cancel`, `reconciliation`) with a shopper token →
    expect 403. Attempt every shopper endpoint with no token → expect 401.

Turn the above into the concise numbered guide handed back to the user, with actual `curl`/HTTP
examples substituted for the placeholders once real ids/tokens are known from the run.

---

# Appendix A: PayPal .NET SDK Contract Sheet
Target: ASP.NET Core 8 `PublicApi` project, eShopOnWeb, Sandbox. Config keys: `PayPal:ClientId`,
`PayPal:ClientSecret`, `PayPal:Environment`, `PayPal:Currency`, optional `PayPal:BaseUrl`.

*(Produced by the `paypal-sdk` agent against this plan's full feature list — treat every fact
below as grounded and authoritative; do not re-derive a PayPal fact from memory or the web.)*

SDK identity: NuGet `AsadAli.Checkout.Sdk` (install version-less), root namespace `PayPalServerSdk`,
source tag `v1.0.1` / commit `9653d18`.

---

## 1. Scope & sequence

1. **Client construction & DI** — register `PayPalServerSdkClient` via `AddPayPalServerSdkClient`;
   wire `Oauth2` credentials, `Environment`, and the `PayPal:BaseUrl` override.
2. **Direct card AUTHORIZE, no redirect** — `client.Orders.CreateOrder` single-step, `Intent =
   CheckoutPaymentIntent.Authorize`, raw `CardRequest` in `payment_source`.
3. **Vault a card directly** — `client.Vault.CreatePaymentToken` with raw card fields. Deleting a
   vaulted card (for the app's saved-card removal endpoint) is `client.Vault.DeletePaymentToken`
   — see the addendum at §2.2a, added as a same-session follow-up lookup.
4. **Pay with a vaulted card** — `client.Orders.CreateOrder`, same intent, `CardRequest.VaultId`
   set instead of PAN/expiry/CVV.
5. **Idempotency** — `payPalRequestId` param (→ `PayPal-Request-Id` header) on every write op.
6. **Capture an authorization** — `client.Payments.CaptureAuthorizedPayment`.
7. **Reauthorize** — `client.Payments.ReauthorizePayment`.
8. **Void an authorization** — `client.Payments.VoidPayment`.
9. **Refund a capture** — `client.Payments.RefundCapturedPayment`.
10. **Fetch current status by id** — `Orders.GetOrder` / `Payments.GetAuthorizedPayment` /
    `Payments.GetCapturedPayment` / `Payments.GetRefund` / `Vault.GetPaymentToken`.
11. **Transaction search / reconciliation** — `client.TransactionSearch.SearchTransactions`,
    chunked into ≤31-day windows, hand-paginated.
12. **Error-handling boundary** — wraps every call above.
13. **Currency/amount formatting** — `Money`/`AmountWithBreakdown` string values, applies to
    every request/response above.

---

## 2. CONTRACT SHEET

> **Signatures are generated code, verbatim — every parameter name is the literal
> C# identifier. The cancellation-token parameter really is named `ct`: in named
> arguments write `ct:`, never `cancellationToken:`.**
>
> **Every SDK type is written fully-qualified with the namespace the map gives it** — take
> each one from that type's own map row, never from where a neighbouring type sits. A members
> table names the namespace outright; otherwise the row's source path implies it
> (`Core/Configuration/…` ⇒ `…Core.Configuration`; a file at the repo root ⇒ the root
> namespace). Enums, unions, auth, server and client-config types are spread across different
> child namespaces, and two types configured side by side in the same options object routinely
> live in different ones. Dropping a type to the root or to `.Models` makes the implementer
> guess the wrong `using`, and the build breaks.

### 2.0 SDK-wide rules — read before any per-operation row

**R1 — `prefer` header controls whether nested response fields exist at all.** Every write
operation below defaults `prefer` to `"return=minimal"` — that response contains **only** `id`,
`status`, and `links`; nested objects (`purchase_units`, `authorizations`, `captures`, fee
breakdowns) are simply absent. Every row in this sheet that reads a nested field **requires**
passing `prefer: "return=representation"` explicitly (source: XML doc on the `prefer` parameter,
identical wording on every op in `Api/Orders.cs` / `Api/Payments.cs`).

**R2 — idempotency key.** `payPalRequestId` (nullable `string`, no default → pass explicitly)
maps 1:1 to the `PayPal-Request-Id` HTTP header (verified in `Api/Orders.cs` / `Api/Payments.cs`
/ `Api/Vault.cs` — `new HeaderParam("PayPal-Request-Id", payPalRequestId)`). The XML doc on
`CreateOrder`'s `payPalRequestId` states it **"is mandatory for all single-step create order
calls (e.g. Create Order Request with payment source information like Card, PayPal.vault_id,
…)"** — i.e. mandatory for both step 2 and step 4 below. Server retains keys ~45 hours
(`Orders`) / 45 days (`Payments`/`Vault`) per the per-op doc comments. Caller contract: reuse the
**same** key only to retry the identical logical write; use a **new** key per distinct logical
write (e.g. two separate partial refunds of the same capture need two different keys, or the
second is treated as a duplicate of the first).

**R3 — typed (Case A) errors do not carry the HTTP status code for their enumerated statuses.**
Verified directly in source (`Errors/RefundCapturedPaymentError.cs`, `Errors/AuthorizeOrderError.cs`,
same generated pattern in every `Errors/*.cs`): the `Create(HttpResponseMessage, ct)` factory
`switch`es on `(int)response.StatusCode`, and **every status the map lists for `TryGetError(…)`
(or `TryGetError1`/`TryGetDefaultError`) is routed to the same branch with no status stored** —
e.g. `RefundCapturedPaymentError`: `400 or 401 or 403 or 404 or 409 or 422 => FromJson<Error>(…)`.
`SdkException<TError>` itself (`Core/Exceptions/SdkException.cs`) has only an `Error` property —
no status code anywhere. Only when the live status is **not** one of the enumerated ones does
`TryGetRawError(out RawError raw)` succeed, and only *that* `raw.StatusCode` is a real int.
**Consequence:** you cannot distinguish "insufficient funds" from "instrument declined" from
"over-refund" by status code once the exception is typed — the only signal is the string content
of `Error.Name` / `Error.Details[].Issue` (plain `string`, not an enum — the SDK does not compile
in a list of literal values). Treat every such distinction as **UNVERIFIED**: log
`Error.Name`/`Error.Details[].Issue` verbatim, map to a generic domain error, and do not branch
on an assumed literal you have not seen in an actual sandbox response.

**R4 — amounts are strings.** Every `Money`/`AmountWithBreakdown` field pair is
`CurrencyCode (currency_code): string !req` + `Value (value): string !req` — `Value` is a
decimal-formatted **string**, not `decimal`/`double`; the SDK performs no rounding or
minor-unit formatting. `PurchaseUnitRequest.Amount.Value` must equal the exact sum of its
`Breakdown` (if supplied) to the cent — PayPal validates this server-side and rejects mismatches
via the same Case-A `Error`/`ErrorDetails` shape (R3).

**R5 — namespaces (from each type's own source path).**

| Contents | Namespace |
|---|---|
| Client, options, `Server`, `ServerOptions` (repo root files) | `PayPalServerSdk` |
| `ServerEnvironment` | `PayPalServerSdk.Servers` |
| `OAuth2ClientCredentials`, `IOAuth2TokenStrategy<T>` | `PayPalServerSdk.Core.Authentication.OAuth2` / `...OAuth2.ClientCredentials` (credentials class itself) |
| Controllers (`client.Orders` etc.) | `PayPalServerSdk.Api` |
| Records (`OrderRequest`, `Money`, `CardRequest`, …) | `PayPalServerSdk.Models` |
| Enums (`OrderStatus`, `CheckoutPaymentIntent`, …) | `PayPalServerSdk.Models.Enums` |
| Error classes (`CreateOrderError`, `Error`, `Error1`, …) — `Error`/`Error1`/etc. payload records live in `Models`; the `{Op}Error` wrapper classes live in `Errors` | `PayPalServerSdk.Errors` (wrapper) / `PayPalServerSdk.Models` (payload) |
| `RawError`, `SdkException<T>` | `PayPalServerSdk.Core.ErrorResponse` / `PayPalServerSdk.Core.Exceptions` |

---

### 2.1 Client construction & auth (Step 1)

```csharp
using PayPalServerSdk;
using PayPalServerSdk.Servers;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;

services.AddPayPalServerSdkClient(options =>
{
    options.Environment = ServerEnvironment.Sandbox;               // the only member this SDK ships (see Gaps)
    options.Oauth2 = new OAuth2ClientCredentials
    {
        ClientId = config["PayPal:ClientId"]!,
        ClientSecret = config["PayPal:ClientSecret"]!
    };
    var baseUrl = config["PayPal:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
        options.Server.Default.Sandbox.BaseUrl = baseUrl;           // default "https://api-m.sandbox.paypal.com"
});
```

- `AddPayPalServerSdkClient(Action<PayPalServerSdkClientOptions>? configure = null)` —
  `PayPalServerSdk.ServiceCollectionExtensions`, registers `PayPalServerSdkClient` as a
  **singleton** via `services.AddHttpClient()` + `IHttpClientFactory.CreateClient()` (unnamed
  default client), built once inside the singleton factory delegate.
- **Base-URL override reaches the token endpoint too** (satisfies "verbatim for every call
  including token/credential request"): `AuthSchemes.cs` builds the OAuth2 strategy as
  `OAuth2ClientCredentialsStrategy.ForBasicAuthRequest(server.Default("/v1/oauth2/token"), rawClient)`
  — `server` is the **same** `Server` instance (built from `options.Server`) that every API
  controller uses, so `options.Server.Default.Sandbox.BaseUrl` governs both.
- **Auth/token refresh is automatic and requires no application code.** `OAuth2Scheme<T>.Apply`
  (`Core/Authentication/OAuth2/OAuth2Scheme.cs`) caches the fetched `OAuthToken`, checks
  `token.IsExpired(DateTimeOffset.UtcNow)` on every request, and re-fetches only when expired,
  under an `AsyncLock` (thread-safe across concurrent requests). There is no public
  invalidate/refresh hook on `PayPalServerSdkClient` itself — you cannot force an early refresh.
- Credentials: `OAuth2ClientCredentials { ClientId (required string), ClientSecret (required
  string), Scope (string?) }`.

---

### 2.2 Operations

| Step | Controller.Method | Request model (wire names) | Response / fields read | Error case + accessors | Notes |
|---|---|---|---|---|---|
| 2/4 | `client.Orders.CreateOrder(string? payPalMockResponse, string? payPalRequestId, string? payPalPartnerAttributionId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderRequest body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `OrderRequest{ Intent(intent):CheckoutPaymentIntent!req, PurchaseUnits(purchase_units):IReadOnlyList<PurchaseUnitRequest>!req, PaymentSource(payment_source):PaymentSource? }`. `PurchaseUnitRequest{ Amount(amount):AmountWithBreakdown!req{CurrencyCode!req,Value!req,Breakdown?}, ReferenceId?, InvoiceId?, CustomId?, ... }`. `PaymentSource{ Card(card):CardRequest? }`. Raw card (step 2): `CardRequest{ Name?, Number?, Expiry?, SecurityCode?, BillingAddress(billing_address):Address?{AddressLine1?,AddressLine2?,AdminArea2?,AdminArea1?,PostalCode?,CountryCode!req} }`. Vaulted card (step 4): `CardRequest{ VaultId(vault_id):string? }` = the `PaymentTokenResponse.Id` from step 3; leave Number/Expiry/SecurityCode null. | `Order{ Id, Status:OrderStatus, PurchaseUnits:IReadOnlyList<PurchaseUnit>, PaymentSource:PaymentSourceResponse?, Links }`. Authorization is nested: `order.PurchaseUnits[i].Payments`(`PaymentCollection`)`.Authorizations`(`IReadOnlyList<AuthorizationWithAdditionalData>`)`[j]{ Id, Status:AuthorizationStatus, Amount:Money, ExpirationTime:string }`. Requires `prefer:"return=representation"` (R1) or `PurchaseUnits`/`Payments` are absent. | `SdkException<CreateOrderError>` Case A. `TryGetError(out Error)`[400,401,422] (Error: `Name!req,Message!req,DebugId!req,Details:IReadOnlyList<ErrorDetails>?{Field?,Value?,Location?="body",Issue!req,Description?},Links?`). `TryGetRawError(out RawError)` fallback. | `payPalRequestId` mandatory (R2). 3DS/payer-action signal: `Order.Status == OrderStatus.PayerActionRequired` (enum member exists) — **stop** on this status rather than following `Links`; `LinkDescription.Rel` is a bare `string` (no compiled rel-value list) — UNVERIFIED beyond the Status check; log any `Links` entries, do not branch on an assumed literal rel. |
| 2 (alt., SDK-modeled fallback) | `client.Orders.AuthorizeOrder(string id, string? payPalMockResponse, string? payPalRequestId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderAuthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `OrderAuthorizeRequest{ PaymentSource(payment_source):OrderAuthorizeRequestPaymentSource?{ Card(card):CardRequest? } }` (same `CardRequest` shape as above) | `OrderAuthorizeResponse{ Id, Status:OrderStatus, PurchaseUnits, PaymentSource:OrderAuthorizeResponsePaymentSource?, Links }` — distinct C# type from `Order` despite identical shape (see trap note). | `SdkException<AuthorizeOrderError>` Case A. `TryGetError(out Error)`[400,401,403,404,422,500]. `TryGetRawError` fallback. | Only needed if an `Order` was created **without** `payment_source` and must be finished server-side without buyer redirect; primary flow is the single-step `CreateOrder` row above. |
| 3 | `client.Vault.CreatePaymentToken(string? payPalRequestId, PaymentTokenRequest body, RequestOptions? requestOptions = null, CancellationToken ct = default)` | `PaymentTokenRequest{ Customer(customer):Customer?{Id?,MerchantCustomerId?}, PaymentSource(payment_source):PaymentTokenRequestPaymentSource!req{ Card(card):PaymentTokenRequestCard?{Name?,Number?,Expiry?,SecurityCode?,Brand?:CardBrand,BillingAddress?:Address} } }` | `PaymentTokenResponse{ Id(id):string? — the vault/payment-token id to persist, Customer:CustomerResponse?, PaymentSource:PaymentTokenResponsePaymentSource?{ Card(card):CardPaymentTokenEntity?{Name?,LastDigits?,Brand?:CardBrand,Expiry?,BillingAddress?:CardResponseAddress,Type?:CardType,VerificationStatus?,BinDetails?} }, Links }`. Safe-to-display metadata (never PAN): `LastDigits`,`Brand`,`Expiry`,`Type`,`Name`. | `SdkException<CreatePaymentTokenError>` Case A. `TryGetError1(out Error1)`[400,403,404,422,500] (`Error1`: same shape as `Error` but `Details:IReadOnlyList<ErrorDetails1>?` with `Links:IReadOnlyList<ErrorLinkDescription>?`). `TryGetRawError` fallback. | `payPalRequestId` supported (R2). No browser approval: this is a direct `Vault` call, not the Setup-Token/PayPal-Wallet approval path. UNVERIFIED: `PaymentTokenResponse` has **no** `Status` field (unlike `SetupTokenResponse.Status:PaymentTokenStatus`) — if the card issuer needed SCA/3DS mid-vault this SDK has no modeled slot for it on this response; treat any thrown exception as vault failure, do not build a retry/approval loop. |
| 10 (vault) | `client.Vault.GetPaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `PaymentTokenResponse` (same shape as above) | `SdkException<GetPaymentTokenError>` Case A. `TryGetError1(out Error1)`[403,404,422,500]. `TryGetRawError` fallback. | Refresh vault-token state by id. |
| 6 | `client.Payments.CaptureAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, CaptureRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `CaptureRequest{ Amount(amount):Money?, InvoiceId?, FinalCapture(final_capture):bool?=false, PaymentInstruction?, NoteToPayer?, SoftDescriptor? }` — pass `body: null` (or `Amount` unset) to capture the full authorized amount; set `FinalCapture=true` to release any remaining hold when this is the last capture. | `CapturedPayment{ Id, Status:CaptureStatus, Amount:Money (captured amount), SellerReceivableBreakdown(seller_receivable_breakdown):SellerReceivableBreakdown?{ GrossAmount(gross_amount)!req, PaypalFee(paypal_fee)?, NetAmount(net_amount)? — net to merchant, ReceivableAmount(receivable_amount)?, PlatformFees? } }`. Requires `prefer:"return=representation"` (R1) or the breakdown is absent. | `SdkException<CaptureAuthorizedPaymentError>` Case A. `TryGetError(out Error)`[400,401,403,404,409,422]. `TryGetNoContent(out RawError)`[500] (this **does** carry a real `.StatusCode==500`, unlike `TryGetError`). `TryGetRawError` fallback. | `payPalRequestId` (R2). |
| 7 | `client.Payments.ReauthorizePayment(string authorizationId, string? payPalRequestId, string? payPalAuthAssertion, ReauthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `ReauthorizeRequest{ Amount(amount):Money? }` — "Supports only the `amount` request parameter" (op doc); PayPal enforces the up-to-115%/$75-US-max rule server-side, not modeled client-side. | `PaymentAuthorization{ Id — a **new** authorization id, use it in place of the old one for later capture/void, Status:AuthorizationStatus, ExpirationTime, Amount, ... }`. Requires `prefer:"return=representation"` (R1). | `SdkException<ReauthorizePaymentError>` Case A. `TryGetError(out Error)`[400,401,403,404,422]. `TryGetNoContent(out RawError)`[500]. `TryGetRawError` fallback. | `payPalRequestId` (R2). See staleness note below. |
| 8 | `client.Payments.VoidPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, string? payPalRequestId, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `PaymentAuthorization` with `Status == AuthorizationStatus.Voided` on success. | `SdkException<VoidPaymentError>` Case A. `TryGetError(out Error)`[401,403,404,409,422]. `TryGetNoContent(out RawError)`[500]. `TryGetRawError` fallback. | `payPalRequestId` (R2). Op doc: "You cannot void an authorized payment that has been fully captured" — plausible 409 cause, UNVERIFIED exact literal (see R3); log `Error.Name`/`Details[].Issue`. |
| 9 | `client.Payments.RefundCapturedPayment(string captureId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, RefundRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `RefundRequest{ Amount(amount):Money? (omit/null = full refund; set for partial), CustomId?, InvoiceId?, NoteToPayer?, PaymentInstruction? }` | `Refund{ Id, Status:RefundStatus, Amount:Money, SellerPayableBreakdown(seller_payable_breakdown):SellerPayableBreakdown?{GrossAmount?,PaypalFee?,NetAmount?,TotalRefundedAmount(total_refunded_amount)? — cumulative refunded on this capture, usable for a client-side proactive over-refund guard} }`. Requires `prefer:"return=representation"` (R1). | `SdkException<RefundCapturedPaymentError>` Case A. `TryGetError(out Error)`[400,401,403,404,409,422] — all collapse into one `Error`, no stored status (R3). `TryGetNoContent(out RawError)`[500]. `TryGetRawError` fallback. | `payPalRequestId` mandatory pattern (R2): same key = retry-of-same-refund (deduped server-side, ~45 days), new key required per distinct partial refund. Over-refund detection is UNVERIFIED (R3) — defensively: (a) compare requested `Amount` + prior `TotalRefundedAmount` against the original `CapturedPayment.Amount` **before** calling; (b) on exception, log `Error.Name`/`Details[].Issue` verbatim and surface a generic "refund rejected" error rather than assuming a literal. |
| 10 | `client.Orders.GetOrder(string id, string? fields, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `Order` (full representation; `prefer` not applicable to GET) | `SdkException<GetOrderError>` Case A. `TryGetError(out Error)`[401,404]. `TryGetRawError` fallback. | Refresh order state by id. |
| 10 | `client.Payments.GetAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `PaymentAuthorization` | `SdkException<GetAuthorizedPaymentError>` Case A. `TryGetError(out Error)`[401,403,404]. `TryGetNoContent(out RawError)`[500]. `TryGetRawError` fallback. | Refresh authorization state by id. |
| 10 | `client.Payments.GetCapturedPayment(string captureId, string? payPalMockResponse, RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `CapturedPayment` | `SdkException<GetCapturedPaymentError>` Case A. `TryGetError(out Error)`[401,403,404]. `TryGetNoContent(out RawError)`[500]. `TryGetRawError` fallback. | Refresh capture state by id. |
| 10 | `client.Payments.GetRefund(string refundId, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `Refund` | `SdkException<GetRefundError>` Case A. `TryGetError(out Error)`[401,403,404]. `TryGetNoContent(out RawError)`[500]. `TryGetRawError` fallback. | Refresh refund state by id. |
| 11 | `client.TransactionSearch.SearchTransactions(string startDate, string endDate, string? transactionId, string? transactionType, string? transactionStatus, string? transactionAmount, string? transactionCurrency, string? paymentInstrumentType, string? storeId, string? terminalId, string? fields = "transaction_info", string? balanceAffectingRecordsOnly = "Y", int? pageSize = 100, int? page = 1, RequestOptions? requestOptions = null, CancellationToken ct = default)` | Query params (wire←C#): `start_date`←`startDate`, `end_date`←`endDate`, `page_size`←`pageSize`, `page`←`page`, etc. `startDate`/`endDate` are RFC3339/ISO-8601 date-times, seconds required, fractional optional; **max supported range per call is 31 days** (op doc comment) — chunk any wider reconciliation window into ≤31-day sub-ranges, one `SearchTransactions` call per chunk. | `SearchResponse{ TransactionDetails(transaction_details):IReadOnlyList<TransactionDetails>?{ TransactionInfo(transaction_info):TransactionInformation?{ TransactionId(transaction_id):string?, TransactionAmount(transaction_amount):Money?, TransactionStatus(transaction_status):string? (bare string, not an enum), TransactionInitiationDate(transaction_initiation_date):string?, InvoiceId(invoice_id):string?, CustomField(custom_field):string? — both are candidate merchant-supplied correlation ids, both optional plain strings }, PayerInfo?, ShippingInfo?, CartInfo?, StoreInfo? }, Page(page):int?, TotalItems(total_items):int?, TotalPages(total_pages):int?, Links }` | `SdkException<RawError>` — **Case B, the only Case-B operation in the SDK.** `ex.Error.StatusCode` is a real `HttpStatusCode` here (unlike Case A). `ex.Error.ReadAsString()`. Best-effort structured read: `ex.Error.ReadAsJson<SearchError>()` (`Models/SearchError.cs`: `Name!req,Message!req,DebugId!req,Details?,TotalItems?,MaximumItems?`) wrapped in try/catch — this shape is **not** contractually guaranteed for a Case-B op; fall back to the raw string on deserialize failure. | Pagination is page-number based, hand-rolled: loop `page = page + 1` while `page <= TotalPages` (or until `TransactionDetails` is empty), **per date-range chunk**. No auto-pager/continuation-token exists in this SDK. |

### 2.2a Addendum — deleting a vaulted card (follow-up lookup, same session)

Needed for the `DELETE /api/payment-methods/{id}` endpoint (§5 of the plan above) to revoke the
saved card PayPal-side, not just remove the local DB row.

**`client.Vault.DeletePaymentToken(string id, PayPalServerSdk.Core.Models.RequestOptions?
requestOptions = null, CancellationToken ct = default)`** — `PayPalServerSdk.Api.Vault`, maps to
`DELETE /v3/vault/payment-tokens/{id}`. `id` = the `PaymentTokenResponse.Id` from step 3
(`CreatePaymentToken`) — required, no default, pass named as `id:`.

- **Success:** returns `Task` (no response body — HTTP 204). A non-throwing return is the success
  signal.
- **Failure:** `SdkException<PayPalServerSdk.Errors.DeletePaymentTokenError>`, Case A (typed).
  `TryGetError1(out Error1 value)` covers statuses `[400, 403, 500]` (same `Error1` shape as
  `CreatePaymentTokenError`/`GetPaymentTokenError`: `Name!req, Message!req, DebugId!req,
  Details:IReadOnlyList<ErrorDetails1>?, Links:IReadOnlyList<ErrorLinkDescription>?`).
  `TryGetRawError(out RawError value)` fallback for any other status. Per rule R3, all three
  listed statuses collapse into the one accessor with no stored status code — distinguish
  "already deleted"/"not found" from other failures only via `Error1.Name`/`Details[].Issue`
  string content (UNVERIFIED literal values; log and treat generically, same as every other Case
  A operation in this sheet).

### 2.3 Enum value tables needed

| Enum | Members (C# → wire) | Source |
|---|---|---|
| `CheckoutPaymentIntent` | `Capture`(CAPTURE), `Authorize`(AUTHORIZE) | `Models/Enums/CheckoutPaymentIntent.cs` |
| `OrderStatus` | `Created`(CREATED), `Saved`(SAVED), `Approved`(APPROVED), `Voided`(VOIDED), `Completed`(COMPLETED), `PayerActionRequired`(PAYER_ACTION_REQUIRED) | `Models/Enums/OrderStatus.cs` |
| `AuthorizationStatus` | `Created`(CREATED), `Captured`(CAPTURED), `Denied`(DENIED), `PartiallyCaptured`(PARTIALLY_CAPTURED), `Voided`(VOIDED), `Pending`(PENDING) — **no Expired/Stale member; see staleness note** | `Models/Enums/AuthorizationStatus.cs` |
| `CaptureStatus` | `Completed`(COMPLETED), `Declined`(DECLINED), `PartiallyRefunded`(PARTIALLY_REFUNDED), `Pending`(PENDING), `Refunded`(REFUNDED), `Failed`(FAILED) | `Models/Enums/CaptureStatus.cs` |
| `RefundStatus` | `Cancelled`(CANCELLED), `Failed`(FAILED), `Pending`(PENDING), `Completed`(COMPLETED) | `Models/Enums/RefundStatus.cs` |
| `PaymentTokenStatus` | `Created`(CREATED), `PayerActionRequired`(PAYER_ACTION_REQUIRED), `Approved`(APPROVED), `Vaulted`(VAULTED), `Tokenized`(TOKENIZED) — used by `SetupTokenResponse` only, **not** `PaymentTokenResponse` | `Models/Enums/PaymentTokenStatus.cs` |
| `CardBrand` | `Visa`,`Mastercard`,`Discover`,`Amex`,… (29 members) | `Models/Enums/CardBrand.cs` |
| `CardType` | `Credit`,`Debit`,`Prepaid`,`Store`,`Unknown` | `Models/Enums/CardType.cs` |

**Authorization staleness/expiry (grounded fact, feature #7):** `AuthorizationStatus` has no
`Expired`/`Stale` member — expiry is never surfaced as a status. Check
`Authorization`/`PaymentAuthorization.ExpirationTime` (`string`, ISO-8601) client-side; PayPal
signals an expired authorization only by **throwing** on the next Capture/Reauthorize attempt.
Op doc on `ReauthorizePayment` states verbatim: *"If 30 days have transpired since the date of
the original authorization, you must create an authorized payment instead of reauthorizing the
original authorized payment."* — treat any `SdkException<ReauthorizePaymentError>` there as
"cannot reauthorize, create a new order/authorization instead" (UNVERIFIED which literal
`Error.Name`/`Issue` confirms it — apply R3's defensive logging). On success, `PaymentAuthorization.Id`
is a **new** id and must replace the stored authorization id.

---

## 3. Trap notes

⚠ Step 1 (client & DI) — `AddPayPalServerSdkClient` builds the `HttpClient` once via
`IHttpClientFactory.CreateClient()` inside a singleton factory delegate; whether that matches
this app's intended `HttpClient` lifetime/handler-rotation strategy is not obvious from the
signature. **MUST load `dotnet-client-initialization`**.

⚠ Step 1 (auth) — where secrets should be loaded from and whether `Oauth2TokenStrategy` needs
setting for this flow (vs. leaving it null to get the built-in strategy) isn't visible from the
options shape alone. **MUST load `dotnet-authentication`**.

⚠ Steps 2/4/6/7/8/9 (calling endpoints) — most of these operations take 3-5 leading `string?`
parameters with **no default**, so a positional call silently mis-binds `payPalMockResponse` /
`payPalRequestId` / `payPalAuthAssertion` if you skip named arguments. **MUST load
`dotnet-calling-endpoints`**.

⚠ Steps 2-10 (models) — this SDK has multiple near-duplicate record families for the same
PayPal concept (`Authorization` / `AuthorizationWithAdditionalData` / `PaymentAuthorization`;
`CapturedPayment` / `PaymentsCapture` / `OrdersCapture`) that are structurally identical but are
**distinct C# types** — a mapper written against one will not compile against another, and it's
easy to reuse the wrong one across the Orders vs. Payments controllers. Also: enums here are
`StringEnum<T>`, not C# `enum`, and `Order` vs `OrderAuthorizeResponse` are two distinct types
with the same shape. **MUST load `dotnet-models`**.

⚠ Steps 2/6/9/11 (resilience) — whether `RetryOptions.HttpMethodsToRetry` will retry the
non-idempotent parts of `CreateOrder`/`CaptureAuthorizedPayment`/`RefundCapturedPayment` on a
transport failure (as opposed to a status-code trigger), and whether `Timeout` bounds one
attempt or the whole paginated `SearchTransactions` reconciliation loop, are not visible from the
option names. **MUST load `dotnet-configuration-resilience`**.

⚠ Testing the integration layer — which seam to fake for this SDK's `HttpClient`-based
construction, and how to exercise both Case-A and Case-B error paths without hitting the network,
isn't obvious from the client shape. **MUST load `dotnet-testing`**.

---

## 4. REQUIRED READING

Load every skill below **before implementation starts** — this sheet deliberately does not
carry their contents:

- `dotnet-client-initialization` — Step 1 (client & DI registration, `HttpClient` lifetime).
- `dotnet-authentication` — Step 1 (credentials wiring, secret loading).
- `dotnet-calling-endpoints` — Steps 2, 3, 4, 6, 7, 8, 9, 10, 11 (every call site; named-argument
  discipline for the many no-default nullable params).
- `dotnet-models` — Steps 2-10 (union/record shapes, `StringEnum<T>` construction, the
  near-duplicate record families).
- `dotnet-error-handling` — Step 12, the error boundary around every call above. Two mandatory
  hazards, verbatim:
  - a drifted or malformed **2xx** body (a missing `required` member) surfaces as a
    `System.Text.Json.JsonException` from deserialization, **not** as an `SdkException` — so an
    SDK-exception-only catch ladder lets it escape the integration boundary;
  - a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape
    throws `JsonException` *while the error object is being constructed*, so the `JsonException`
    **replaces** the `SdkException` and the HTTP status is destroyed with it — a boundary that
    maps every `JsonException` to a 5xx then reports a deterministic rejection as an outage, and
    a caller that retries 5xx retries something that can never succeed.
- `dotnet-configuration-resilience` — Steps 2, 6, 9, 11 (retry semantics on writes, per-attempt
  timeout across the paginated reconciliation loop, base-URL override already resolved above).
- `dotnet-testing` — testing the integration layer once written.

---

## 5. Gaps / Not Supported

- **No Production/Live environment member.** `Servers/ServerEnvironment.cs` defines exactly one
  member, `ServerEnvironment.Sandbox`; `Match()` throws `ArgumentOutOfRangeException` for
  anything else. `PayPal:Environment` therefore has nothing to *select* in this SDK version —
  going live later means manually overriding `options.Server.Default.Sandbox.BaseUrl` to the
  production host (the same mechanism as `PayPal:BaseUrl`) while `options.Environment` stays
  `Sandbox`; it is not a first-class environment switch.
- **No auto-pagination helper anywhere in the SDK.** `ListCustomerPaymentTokens` and
  `SearchTransactions` both require hand-rolled `page`/`pageSize` loops (see §2.2); there is no
  continuation-token or `IAsyncEnumerable` pager.
- **No no-throw (`…Result`) variant on any of the 40 operations** — every call in this SDK is
  throw-only (`sdk-map.md` `gen:op-stats`); there is no way to get a typed result without a
  try/catch on the hot path.
- **Typed (Case A) errors never expose the real HTTP status code for their enumerated statuses**
  (R3) — if the application needs the literal status for logging/telemetry on a "known" error
  (e.g. to tag a 409 vs a 422 in an APM dashboard), the SDK does not surface it; only the string
  content of `Error.Name`/`Error.Details[].Issue` is available.

---

## 6. Assumptions & Blockers

**Assumptions:**
- `PayPal:Environment` is read from config but, given the single-Sandbox-member SDK (see Gaps),
  its only legitimate value for this build is `"Sandbox"`; startup should validate/log it rather
  than branch on it to select an SDK environment.
- The vaulted-card payment-source reference is `CardRequest.VaultId` (`payment_source.card.vault_id`,
  `Models/CardRequest.cs`) — the SDK's separate `Token`/`TokenType` model exposes only
  `TokenType.BillingAgreement` and is therefore **not** the modeled route for a saved-card token
  in this SDK version; `VaultId` is.
- The primary no-redirect AUTHORIZE flow is the single-step `Orders.CreateOrder` call (§2.2, step
  2/4) with `payment_source` populated directly, per the `CreateOrder.payPalRequestId` XML doc
  ("mandatory for all single-step create order calls … with payment source information like
  Card"); `Orders.AuthorizeOrder`'s own `body.PaymentSource` parameter is documented in this sheet
  as the SDK-modeled fallback for an order already created without a payment source, not the
  primary path.

**Blockers:** none.

---

**Note on file location:** this content was requested at
`C:/claude-runs/t3zaid-task3-plugin-2p-opus48high-001/tmp/paypal-plan.md` (outside the repo), but
this session's Write tool is restricted to `PLAN.md` at the repository root ("This session is the
planning phase. The only file it may write is PLAN.md at the repository root."), so it was
written here instead. No clone path from this session's SDK-source lookups is referenced above.
