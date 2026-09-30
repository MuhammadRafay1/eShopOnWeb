# PLAN — PayPal payments & saved cards for eShopOnWeb

This is the build plan for adding **PayPal-backed payments** (authorize at checkout, capture at
fulfilment, void on cancel, refund on return) and **saved cards (vault)** to eShopOnWeb, exposed as
JWT-authenticated HTTP endpoints on **`src/PublicApi`**. It is an **additive** capability: the existing
catalog/basket/order/checkout flow is left intact.

A later session builds this from the plan alone (it does not see the investigation that produced it).
Everything it needs is here, including a fully grounded **PayPal SDK contract sheet** (Appendix A).

---

## 0. TL;DR of the key decisions

- **Reuse the existing `Order`/`OrderItem` aggregate unchanged.** All payment/fulfilment/PayPal state
  lives on a **new, separate `Payment` aggregate** (one per order, linked by `OrderId`) plus a `Refund`
  child entity. This keeps the change additive and avoids editing the core `Order` aggregate, its EF
  config, or the Web checkout flow. (`Order` reuse is a mandate; a separate payment aggregate is *not* a
  "parallel order model" — it is payment state that references the order.)
- **Saved cards** are a second new aggregate, `SavedPaymentMethod` (one per vaulted card, owned by a
  buyer). The app stores only a **safe descriptor** (brand + last-4 + expiry + the PayPal vault id) —
  **never the PAN/CVV**.
- **The PayPal SDK is touched only in `Infrastructure`**, behind a port interface `IPaymentGateway`
  declared in `ApplicationCore`. `ApplicationCore` never references any `PayPalServerSdk.*` type. This
  keeps the domain SDK-free and testable, and confines the SDK reference to one project.
- **Identity = the JWT `ClaimTypes.Name` claim = username = email = `Order.BuyerId`.** Every shopper
  endpoint scopes to that value; ownership is enforced on every read and write.
- **Roles:** `fulfil`, `cancel`, and `reconciliation` are **administrator-only**; every other endpoint is
  shopper-scoped and acts only on the caller's own data. (This is the task's explicit role split —
  note in particular that **refund is shopper-scoped**, not an operator action.)
- **Idempotency** is enforced in two layers: application state checks (don't re-authorize/re-capture if
  already done) + a stable **`PayPal-Request-Id`** per logical PayPal write (persisted so retries reuse
  it). Refunds additionally use the **caller-supplied idempotency key** as both the dedupe key in our DB
  and the `PayPal-Request-Id`.

---

## 1. Mandates checklist (every one is addressed below — do not drop any)

Flow 1 — pay for an order:
- [ ] `POST /api/orders` — place an order from catalog item ids + quantities, **reusing `Order`/`OrderItem`**; buyer = token identity; starts **awaiting payment**; returns top-level **`orderId`**. (§4, §7.1)
- [ ] `POST /api/orders/{orderId}/pay` — **authorize** (hold, do not capture) the order total **to the cent**; body carries **either** one-off card details **or** a saved-card id. (§6.2, §7.2)
- [ ] `POST /api/orders/{orderId}/fulfil` — **admin**; capture at fulfilment; persist captured amount, PayPal fee, net proceeds; **renew a stale authorization** before capture; if it can no longer be renewed, return an **operator-actionable** error. (§6.3, §7.3)
- [ ] `POST /api/orders/{orderId}/cancel` — **admin**; before fulfilment; **void** the hold so no money moved. (§6.4, §7.4)
- [ ] `POST /api/orders/{orderId}/refunds` — after fulfilment; full or partial; **never refundable beyond captured**; caller idempotency key; returns top-level **`refundId`**. (§6.5, §7.5)
- [ ] `GET /api/my-orders` — caller's orders with payment state. (§7.6)
- [ ] `GET /api/reconciliation?from=&to=` — **admin**; PayPal's transactions for a range lined up against eShop orders; **covers the whole range** (paginates); ISO-8601 `from`/`to`. (§6.6, §7.7)

Flow 2 — saved cards:
- [ ] `POST /api/payment-methods` — save a card; returns top-level **`paymentMethodId`** + safe descriptor (never full details). (§6.7, §7.8)
- [ ] `GET /api/payment-methods` — caller's saved cards. (§7.9)
- [ ] `DELETE /api/payment-methods/{paymentMethodId}` — remove; afterwards not listed and not usable to pay. (§7.10)

Cross-cutting mandates:
- [ ] All endpoints on `src/PublicApi`, JWT auth, `/api/…` routes, following its conventions; each action separately invocable (no do-everything route). (§7)
- [ ] Amounts from catalog prices; currency from `PayPal:Currency` config. (§4, §6.1)
- [ ] Idempotent in effect: a double-click never authorizes/captures twice; refund key repeat never refunds twice; two distinct partial refunds still allowed. (§6, Cross-cutting §10)
- [ ] Payment carries enough PayPal-owned state (ids + current status for hold, capture, refunds) to drive later requests. (§4)
- [ ] Saved card belongs to its shopper; one shopper never sees/uses/deletes another's; same for orders. (§5, §7)
- [ ] Full card details never stored in our DB and never logged. (§4, §10)
- [ ] Bind `PayPal:` settings from config with exact keys; hard-code no values; `PayPal:BaseUrl` optional verbatim override for **every** call incl. token request. (§8)
- [ ] Use the **paypal-sdk plugin** for **every** PayPal interaction; do not web-search PayPal; stop & report a genuine gap. (§9, Appendix A)

---

## 2. Repository facts the build relies on (verified this session)

- **Solution**: `eShopOnWeb.sln`. Projects: `ApplicationCore` (domain, no infra deps), `Infrastructure`
  (EF Core + identity + SDK integration), `PublicApi` (JWT minimal-API back end), `Web`, `BlazorAdmin`,
  `BlazorShared`. Dependency direction: `PublicApi → Infrastructure → ApplicationCore`.
- **Central package management**: `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`,
  `TargetFramework=net8.0`). Adding a package needs a `<PackageVersion Include=… Version=… />` there
  **and** a `<PackageReference Include=… />` (no `Version`) in the consuming `.csproj`.
- **`global.json`** pins SDK `8.0.x`, `rollForward: latestFeature`. On this machine only the .NET 10 SDK
  is installed and the ASP.NET Core 8.0 runtime is missing → run with **`DOTNET_ROLL_FORWARD=Major`**
  (or set `global.json` `rollForward: latestMajor`), or install the ASP.NET Core 8.0 runtime x64. (§12)
- **PublicApi endpoint pattern** = **`MinimalApi.Endpoint`** (the dominant style; the legacy
  `AuthenticateEndpoint` uses `Ardalis.ApiEndpoints`, but new endpoints follow MinimalApi). An endpoint is
  a class implementing `IEndpoint<IResult, TRequest, …deps>` with `AddRoute(IEndpointRouteBuilder app)`
  (maps the verb, `.Produces<TResponse>()`, `.WithTags("…")`) and `HandleAsync(...)`.
  `builder.Services.AddEndpoints()` + `app.MapEndpoints()` (already in `Program.cs`) auto-discover them —
  **no manual registration per endpoint**. Exemplar to imitate: `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`
  (and `DeleteCatalogItemEndpoint.cs` for the admin `[Authorize]` + route-param shape).
- **Request/response DTO convention**: one class per file, `…Endpoint.<Request>.cs` / `…Endpoint.<Response>.cs`,
  requests derive `BaseRequest`, responses derive `BaseResponse` (carry a `Guid CorrelationId()`; build
  responses with `new XxxResponse(request.CorrelationId())`). Plain DTOs are POCOs. Group per folder
  `src/PublicApi/<Area>Endpoints/`.
- **Auth**: JWT bearer, signing key = constant `AuthorizationConstants.JWT_SECRET_KEY`. Token from
  `POST api/authenticate` (`{ Username, Password }` → `{ Result, Token, … }`), built by
  `IdentityTokenClaimService` which adds `ClaimTypes.Name = userName` and one `ClaimTypes.Role` per role.
  Admin role constant: `BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS` (= "Administrators").
  Admin endpoints use
  `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`;
  shopper endpoints use `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
  **No existing PublicApi endpoint reads the caller identity** — new code injects `ClaimsPrincipal` into
  the handler lambda and reads `user.FindFirstValue(ClaimTypes.Name)` (= username = email = `BuyerId`).
- **Seeded users** (created at PublicApi startup): shopper `demouser@microsoft.com`, admin
  `admin@microsoft.com`; both password `Pass@word1` (constant `AuthorizationConstants.DEFAULT_PASSWORD`).
- **Domain**: `Order : BaseEntity, IAggregateRoot` — `Id`, `BuyerId` (string = email), `OrderDate`,
  `ShipToAddress` (owned `Address`), private `_orderItems` (read-only `OrderItems`), computed `Total()`.
  Constructor `Order(string buyerId, Address shipToAddress, List<OrderItem> items)`. `OrderItem` (child,
  not an aggregate root): `CatalogItemOrdered ItemOrdered` (owned snapshot: `CatalogItemId, ProductName,
  PictureUri`), `decimal UnitPrice`, `int Units`; ctor `OrderItem(CatalogItemOrdered, unitPrice, units)`.
  `CatalogItem : IAggregateRoot` has `decimal Price`, `Name`, `PictureUri`. `IUriComposer.ComposePicUri`
  builds the picture URI. `BaseEntity` has `int Id`. Marker `IAggregateRoot`. **No domain events, no
  unit-of-work** — each repo call saves independently (mirror this; optional UoW is a hardening note).
- **Persistence**: two `DbContext`s — `CatalogContext` (domain) and `AppIdentityDbContext` (identity),
  separate stores. `CatalogContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly(...)`, so any
  `IEntityTypeConfiguration<T>` dropped in `src/Infrastructure/Data/Config/` is **auto-discovered**. New
  aggregate roots get `DbSet<T>` on `CatalogContext`; only aggregate roots can have a repository.
- **Repositories**: generic `EfRepository<T> : RepositoryBase<T>, IReadRepository<T>, IRepository<T>`
  over `CatalogContext` (Ardalis.Specification). Registered open-generic in `PublicApi/Program.cs`
  (`AddScoped(typeof(IRepository<>), typeof(EfRepository<>))` + `IReadRepository<>`). New aggregate roots
  need **no extra registration**. Specifications live in `src/ApplicationCore/Specifications/`
  (e.g. `OrderWithItemsByIdSpec`, `CustomerOrdersWithItemsSpecification(string buyerId)`).
- **DI entry points**: `PublicApi/Program.cs` (top-level; JWT, repos, AutoMapper `MappingProfile`,
  Swagger, `Infrastructure.Dependencies.ConfigureServices(config, services)` which registers the
  DbContexts and honors `UseOnlyInMemoryDatabase`). PublicApi does **not** currently register
  `IOrderService` — new application services must be registered in `Program.cs` (or a small extension it
  calls). `builder.Configuration.AddEnvironmentVariables()` is already called (§8 caveat).
- **Config**: `PublicApi/appsettings.json` holds `baseUrls`, `ConnectionStrings` (LocalDB — absent on
  this machine), `Logging`. No `UseOnlyInMemoryDatabase` and no `PayPal` section exist yet. `appsettings.test.json`
  is force-loaded and is what tests use to set `UseOnlyInMemoryDatabase`.
- **Ports** (`PublicApi/Properties/launchSettings.json`): default Kestrel profile
  `https://localhost:37743;http://localhost:37744`, launchUrl `swagger`. **At run time bind only to the
  assigned block** (`APP_PORT_BLOCK_BASE … +APP_PORT_BLOCK_SIZE-1`); `launchSettings` already targets it.

---

## 3. Architecture & layering (where every new file goes)

```
src/ApplicationCore/
  Entities/PaymentAggregate/
    Payment.cs                 (aggregate root: order link + PayPal state + refunds)
    Refund.cs                  (child entity of Payment)
    PaymentStatus.cs           (enum: order/payment lifecycle)
  Entities/PaymentMethodAggregate/
    SavedPaymentMethod.cs      (aggregate root: one vaulted card, buyer-owned, safe descriptor only)
  Interfaces/
    IPaymentGateway.cs         (PORT: all PayPal ops the app needs, in domain terms — no SDK types)
    IPaymentService.cs         (order pay / fulfil / cancel / refund orchestration)
    IPaymentMethodService.cs   (vault save / list / delete)
    IReconciliationService.cs  (transaction-search report)
    IOrderPlacementService.cs  (build+persist an Order from catalog ids/quantities)  [see §6.1 note]
  Models/Payments/             (plain gateway DTOs: inputs & results — no SDK types)
    CardDetails.cs, BillingAddressInput.cs, MoneyAmount.cs,
    AuthorizationResult.cs, CaptureResult.cs, RefundResult.cs,
    VaultedCardResult.cs, GatewayTransaction.cs
  Services/
    PaymentService.cs, PaymentMethodService.cs, ReconciliationService.cs, OrderPlacementService.cs
  Specifications/
    PaymentByOrderIdSpec.cs
    SavedPaymentMethodsByBuyerSpec.cs
    SavedPaymentMethodByIdSpec.cs
    PaymentsCreatedBetweenSpec.cs

src/Infrastructure/
  Data/Config/
    PaymentConfiguration.cs, RefundConfiguration.cs, SavedPaymentMethodConfiguration.cs
  Data/CatalogContext.cs       (EDIT: add DbSet<Payment>, DbSet<Refund>, DbSet<SavedPaymentMethod>)
  Data/Migrations/             (optional: AddPaymentsAndSavedCards migration for the SQL Server path)
  PayPal/
    PayPalSettings.cs          (ClientId, ClientSecret, Environment, Currency, BaseUrl)
    PayPalPaymentGateway.cs    (implements IPaymentGateway using PayPalServerSdk — the ONLY SDK consumer)
    PayPalClientFactory.cs     (build/configure PayPalServerSdkClient from settings; per dotnet-client-initialization)
  Dependencies.cs              (EDIT: bind PayPalSettings, register SDK client + IPaymentGateway)

src/PublicApi/
  OrderPaymentEndpoints/
    CreateOrderEndpoint(.cs/.Request.cs/.Response.cs)
    PayOrderEndpoint(...)         RefundOrderEndpoint(...)
    FulfilOrderEndpoint(...)      CancelOrderEndpoint(...)
    MyOrdersEndpoint(...)         ReconciliationEndpoint(...)
    OrderPaymentDto.cs / RefundDto.cs / ReconciliationDto.cs (safe response shapes)
  PaymentMethodEndpoints/
    SavePaymentMethodEndpoint(...)  ListPaymentMethodsEndpoint(...)  DeletePaymentMethodEndpoint(...)
    PaymentMethodDto.cs
  Program.cs                   (EDIT: PayPal config bridging §8; register new application services)
  appsettings.json             (EDIT: add "PayPal" section with BaseUrl + non-secret defaults only)
```

Rationale for the port/adapter split: the SDK is throw-based and uses its own models/enums
(`StringEnum<T>`, string money, deep nested envelopes — Appendix A). Wrapping it behind `IPaymentGateway`
that speaks domain DTOs (a) keeps `ApplicationCore` SDK-free and unit-testable with a fake gateway, (b)
localizes the SDK error boundary to one class, and (c) means an SDK compile error only touches
`Infrastructure/PayPal/*` (handed to the paypal-sdk agent per §9).

---

## 4. Domain model (new)

### 4.1 `Payment` (aggregate root, `Entities/PaymentAggregate/Payment.cs`)

One `Payment` per `Order`. Holds the money-movement lifecycle **and** the PayPal-owned identifiers/statuses
needed to drive later requests. Follow the repo's DDD style (private setters, private backing collection,
EF `private Payment(){}` ctor, `Ardalis.GuardClauses`).

Fields:
- `int OrderId` — link to the reused `Order` (set after the order is persisted and has an Id).
- `string BuyerId` — copy of the order's buyer (email). Enables ownership checks and reconciliation
  display without loading `Order`.
- `decimal Amount` — snapshot of `Order.Total()` at placement = the amount to authorize (to the cent).
- `string CurrencyCode` — from `PayPal:Currency`.
- `PaymentStatus Status` — see 4.3. Starts `AwaitingPayment`.
- `DateTimeOffset CreatedAt` — for reconciliation range filtering (defaults to now).
- `string CorrelationReference` — unique per payment (e.g. `"ESHOP-{OrderId}-{8charGuid}"`), set as the
  PayPal order **`invoice_id`** (primary reconciliation key) and echoed via **`custom_id` = OrderId**.
- PayPal hold state: `string? PayPalOrderId`, `string? AuthorizationId`, `string? AuthorizationStatus`,
  `DateTimeOffset? AuthorizationExpiresAt`.
- PayPal capture state: `string? CaptureId`, `string? CaptureStatus`, `decimal? CapturedAmount`,
  `decimal? PayPalFee`, `decimal? NetAmount`.
- Idempotency keys (persisted so retries reuse the same `PayPal-Request-Id`): `string? PayRequestKey`
  (base key generated once on first `pay`), `string? CaptureRequestKey` (generated once on first `fulfil`).
- Refunds: `private readonly List<Refund> _refunds`; `IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly()`.

Behavior (domain methods — keep the gateway/PayPal calls in the service, only state transitions here):
- `Payment(int orderId, string buyerId, decimal amount, string currencyCode, string correlationReference)`.
- `void BeginAuthorization(string payRequestKey)` / setters used by the service to persist step results:
  `SetPayPalOrder(string id)`, `MarkAuthorized(string authorizationId, string status, decimal heldAmount, DateTimeOffset? expiresAt)`
  (guard: `heldAmount == Amount` to the cent), `UpdateAuthorization(string status, DateTimeOffset? expiresAt)` (for reauthorize).
- `void MarkCaptured(string captureId, string status, decimal capturedAmount, decimal? fee, decimal? net)` → `Status = Fulfilled`.
- `void MarkVoided()` → `Status = Canceled` (only from `AwaitingPayment`/`Authorized`).
- `decimal TotalRefunded()` = sum of `_refunds.Amount`.
- `Refund AddRefund(string refundId, decimal amount, string status, string idempotencyKey)` — **guards
  over-refund**: `TotalRefunded() + amount <= (CapturedAmount ?? 0)` else throw a domain exception;
  updates `Status` to `PartiallyRefunded` or `Refunded` when cumulative == captured.
- `Refund? FindRefundByKey(string idempotencyKey)` — for refund idempotency.

### 4.2 `Refund` (child entity, `Entities/PaymentAggregate/Refund.cs`)
`BaseEntity` (not an aggregate root — reached only through `Payment`): `int PaymentId`, `string RefundId`
(PayPal id), `decimal Amount`, `string Status`, `string IdempotencyKey`, `DateTimeOffset CreatedAt`.

### 4.3 `PaymentStatus` enum (`Entities/PaymentAggregate/PaymentStatus.cs`)
`AwaitingPayment, Authorized, Fulfilled, Canceled, PartiallyRefunded, Refunded`. (Store the raw PayPal
status strings separately in the `*Status` fields; this enum is the eShop-side lifecycle used by
`my-orders` and the state machine.)

### 4.4 `SavedPaymentMethod` (aggregate root, `Entities/PaymentMethodAggregate/SavedPaymentMethod.cs`)
`int Id`, `string BuyerId` (owner = email), `string VaultId` (PayPal payment-token id), `string Brand`,
`string LastDigits`, `string Expiry` (`YYYY-MM`), `string? CardholderName`, `string? PayPalCustomerId`,
`DateTimeOffset CreatedAt`. **No PAN, no CVV, ever.** Ctor takes only the safe descriptor + vault id.

> **PII rule (hard):** raw card number / CVV / expiry-as-entered must never be assigned to any persisted
> field and never logged. They flow only through method parameters into the gateway and are discarded.

---

## 5. Ownership & authorization model

- Shopper identity for every request = `ClaimsPrincipal.FindFirstValue(ClaimTypes.Name)` (email). Reject
  with 401 if absent (the `[Authorize]` attribute already guarantees authentication, but guard anyway).
- **Order/payment ownership:** shopper `pay`, `refunds`, and `my-orders` load the `Payment` by `OrderId`
  **and** require `Payment.BuyerId == caller`. A mismatch or missing payment returns **404 Not Found**
  (do not reveal that another buyer's order exists → no 403 that leaks existence).
- **Saved-card ownership:** `GET`/`DELETE`/pay-with-saved-card load `SavedPaymentMethod` by id and require
  `BuyerId == caller`; mismatch/missing → **404**. A deleted card is gone from the DB (so not listed) and
  its id can no longer resolve → not usable to pay.
- **Admin endpoints** (`fulfil`, `cancel`, `reconciliation`) use the role attribute; they may act on any
  order but still resolve the `Payment` by `OrderId`.

---

## 6. Application services — orchestration logic (the heart of the plan)

All money is computed from **catalog prices**; currency from `PayPal:Currency`. All PayPal calls go
through `IPaymentGateway`. Amounts are passed to the gateway as `decimal`; the gateway formats to the
exact 2-dp invariant string the SDK requires (Appendix A, D1). **Never** trust a client-supplied amount
for authorization — always authorize `Order.Total()`.

### 6.1 Place order — `OrderPlacementService` / `CreateOrderEndpoint`
Input: list of `{ catalogItemId, quantity }` + buyer (from token) + a ship-to address (accept from the
request; if not modeled, reuse the existing hard-coded-style default `Address` as the Web flow does —
prefer accepting it, but a default is acceptable since the task does not require address capture).
Steps:
1. Load the referenced `CatalogItem`s (`CatalogItemsSpecification(ids)`); validate all exist and
   quantities ≥ 1 (else 400).
2. Build `OrderItem`s: `new OrderItem(new CatalogItemOrdered(item.Id, item.Name, uriComposer.ComposePicUri(item.PictureUri)), item.Price, quantity)`
   — **UnitPrice = `CatalogItem.Price`** (task: amounts come from catalog prices).
3. `var order = new Order(buyerId, address, items); await _orderRepo.AddAsync(order);` → `order.Id`.
4. Create `Payment(order.Id, buyerId, order.Total(), currency, correlationReference)` (status
   `AwaitingPayment`); `await _paymentRepo.AddAsync(payment)`.
5. Return `order.Id` as top-level **`orderId`**.

> Reuse note: this deliberately does **not** go through a basket — the mandate is to reuse the
> `Order`/`OrderItem` *model*, which it does, constructing them directly. `IOrderService` (basket-based)
> is left untouched. If preferred, add a new method to `IOrderService` instead of a new service — either
> is fine; keep the basket overload intact.

### 6.2 Pay (authorize) — `PaymentService.AuthorizeAsync(orderId, buyer, payInput)` / `PayOrderEndpoint`
`payInput` = **exactly one of** `CardDetails` (one-off) **or** `PaymentMethodId` (saved card). Validate
exactly-one (else 400).
State machine (idempotent):
1. Load `Payment` by `orderId`, require `BuyerId == buyer` (else 404).
2. If `Status == Authorized` (AuthorizationId present) → **return the existing authorization**, no PayPal
   call (idempotent double-click). If `Status` is beyond authorized (Fulfilled/…): 409.
3. If `Status == AwaitingPayment`:
   a. If `PayRequestKey` is null, generate one (GUID) and persist **before** any PayPal call; reuse it on
      retry so PayPal dedupes.
   b. Resolve the card source:
      - one-off: use `CardDetails` (number/expiry/cvc/name/billing).
      - saved: load `SavedPaymentMethod` by `PaymentMethodId`, require `BuyerId == buyer` (else 404), use
        its `VaultId`.
   c. Call `IPaymentGateway.AuthorizeAsync(...)` which performs **CreateOrder(intent=AUTHORIZE, card OR
      card.vault_id, invoice_id=CorrelationReference, custom_id=OrderId, amount=Total())** then
      **AuthorizeOrder** (Appendix A, Feature 1). Uses derived stable request ids from `PayRequestKey`
      (`{key}:create`, `{key}:auth`). If `PayPalOrderId` is already persisted from a prior partial attempt,
      the gateway skips CreateOrder and goes straight to AuthorizeOrder.
   d. Persist `PayPalOrderId`, `AuthorizationId`, `AuthorizationStatus`, `AuthorizationExpiresAt`, held
      amount; guard held == `Amount`; `Status = Authorized`.
4. Return authorization summary (status, held amount) — **not** card data.

> **3DS/challenge:** the sandbox test card is expected to authorize directly. If PayPal returns a
> challenge / `PAYER_ACTION_REQUIRED` (an approval round-trip), **STOP and report it** — do not build an
> approval flow (task mandate). Surface it as a clear 4xx to the caller and a report to the operator.

### 6.3 Fulfil (capture) — `PaymentService.FulfilAsync(orderId)` / `FulfilOrderEndpoint` (**admin**)
1. Load `Payment` by `orderId`.
2. If `Status == Fulfilled` (CaptureId present) → return existing capture (idempotent).
3. Require `Status == Authorized` (else 409 "order is not awaiting fulfilment").
4. **Staleness / renewal:** if `AuthorizationExpiresAt` has passed (or a pre-capture
   `GetAuthorization` shows it is not capturable), call `IPaymentGateway.ReauthorizeAsync(authorizationId,
   Amount)`; on success update `AuthorizationId`/status/expiry and continue. If reauthorize fails (the
   window has closed — Appendix A GAP-2/D2: no `EXPIRED` enum, detect via 422/400 typed error), **do not**
   fail silently: return **409/422 with an operator-actionable message**, e.g. *"Authorization expired and
   can no longer be renewed; cancel this order and ask the shopper to pay again."*
5. Generate/reuse `CaptureRequestKey`; call `IPaymentGateway.CaptureAsync(authorizationId, requestKey)`
   (full capture). Read back captured amount, `paypal_fee`, `net_amount` from `seller_receivable_breakdown`
   (Appendix A, Feature 2).
6. Persist `CaptureId`, `CaptureStatus`, `CapturedAmount`, `PayPalFee`, `NetAmount`; `Status = Fulfilled`.

### 6.4 Cancel (void) — `PaymentService.CancelAsync(orderId)` / `CancelOrderEndpoint` (**admin**)
1. Load `Payment`. If already `Canceled` → idempotent OK.
2. If `Status == AwaitingPayment` (never authorized) → mark `Canceled` (no money moved).
3. If `Status == Authorized` → `IPaymentGateway.VoidAsync(authorizationId)` to release the hold →
   `MarkVoided()` (`Canceled`).
4. If `Status == Fulfilled`/refunded → 409 "already captured; use refund".

### 6.5 Refund — `PaymentService.RefundAsync(orderId, buyer, idempotencyKey, amount?)` / `RefundOrderEndpoint`
**Shopper-scoped** (own order). Request carries a **caller idempotency key** and an optional partial
`amount` (omitted = refund the remaining balance in full).
1. Load `Payment` by `orderId`, require `BuyerId == buyer` (else 404).
2. Require captured (`Fulfilled`/`PartiallyRefunded`) (else 409).
3. **Idempotency:** if `FindRefundByKey(idempotencyKey)` exists → return its `RefundId` with no PayPal
   call. (Repeat under same key never refunds twice.)
4. Determine amount: partial = given `amount`; full = `CapturedAmount - TotalRefunded()`.
5. **Over-refund guard** (Appendix A GAP-3 — no client-side cap; enforce ourselves): reject 422 if
   `TotalRefunded() + amount > CapturedAmount`. (A partly-refunded order is never refundable beyond
   captured.)
6. `IPaymentGateway.RefundAsync(captureId, amount?, payPalRequestId: idempotencyKey)` → read refund id +
   status (Appendix A, Feature 5). Pass the caller key as the `PayPal-Request-Id` so PayPal also dedupes.
7. `payment.AddRefund(refundId, amount, status, idempotencyKey)` (updates status to
   `PartiallyRefunded`/`Refunded`).
8. Return top-level **`refundId`**. Two **distinct** keys → two legitimate partial refunds (allowed).

### 6.6 Reconciliation — `ReconciliationService.BuildAsync(from, to)` / `ReconciliationEndpoint` (**admin**)
1. Validate `from`/`to` are ISO-8601 date-times and `from <= to` (else 400).
2. **PayPal side:** `IPaymentGateway.SearchTransactionsAsync(from, to)` which loops **all pages**
   (`page = 1..TotalPages`, `pageSize` explicit — Appendix A, Feature 8/D3) and returns every
   `GatewayTransaction { TransactionId, Status, Amount, Currency, InvoiceId, CustomField, InitiationDate }`
   in range. **Covers the whole range, not just page 1.**
3. **eShop side:** `_paymentRepo.ListAsync(new PaymentsCreatedBetweenSpec(from, to))` → each payment's
   `CorrelationReference`, `OrderId`, `Amount`, `Status`.
4. **Line up** by `InvoiceId == CorrelationReference` (primary; `CustomField`/`custom_id` is a best-effort
   secondary — D4 UNVERIFIED). Emit three buckets:
   - `matched` (present both sides; include amounts from each for eyeballing mismatches),
   - `paypalOnly` (PayPal knows, eShop doesn't),
   - `eShopOnly` (eShop knows, PayPal doesn't).
5. **Sandbox lag:** an empty PayPal result for a range covering just-created payments is **expected**, not
   a gap — the report is correct; do not treat empty as failure. State this in the response/notes.

### 6.7 Saved cards — `PaymentMethodService`
- **Save** (`SavePaymentMethodEndpoint`): take card details + buyer; call
  `IPaymentGateway.VaultCardAsync(card, buyer)` → `VaultedCardResult { VaultId, Brand, LastDigits, Expiry,
  CardholderName?, PayPalCustomerId? }` (Appendix A, Feature 7, `Vault.CreatePaymentToken`; the response
  returns only `last_digits` — no PAN). Persist a `SavedPaymentMethod`; return top-level
  **`paymentMethodId`** + safe descriptor.
- **List** (`ListPaymentMethodsEndpoint`): `SavedPaymentMethodsByBuyerSpec(buyer)` → safe descriptors.
- **Delete** (`DeletePaymentMethodEndpoint`): load by id, require ownership (404 else); call
  `IPaymentGateway.DeleteVaultedCardAsync(vaultId)` (`Vault.DeletePaymentToken`); then delete the local
  record. Afterwards not listed and not usable to pay.

---

## 7. PublicApi endpoints (MinimalApi.Endpoint style)

General rules: route under `/api/…`; `.Produces<TResponse>()` + `.WithTags(...)`; requests derive
`BaseRequest`, responses `BaseResponse`; inject repositories/services via the handler lambda or ctor;
inject `ClaimsPrincipal` for identity; wrap gateway/domain exceptions into problem responses via a small
mapper (or extend `Middleware/ExceptionMiddleware`) — map validation → 400, ownership/not-found → 404,
state conflicts → 409, over-refund/PayPal rejection → 422, unexpected → 500. **Return created-resource
identifiers as top-level fields.** Each action is a **separate** endpoint (no combined route).

| # | Method & route | Role | Request (key fields) | Success response (top-level id in **bold**) |
|---|---|---|---|---|
| 7.1 | `POST /api/orders` | shopper | `items: [{catalogItemId, quantity}]`, optional `shipToAddress` | 201 `{ **orderId**, status:"AwaitingPayment", total, currency }` |
| 7.2 | `POST /api/orders/{orderId}/pay` | shopper (own) | **one of** `card:{number,expiry"YYYY-MM",securityCode,cardholderName,billingAddress{...}}` **or** `paymentMethodId` | 200 `{ orderId, status:"Authorized", authorizationId, heldAmount, currency }` |
| 7.3 | `POST /api/orders/{orderId}/fulfil` | **admin** | — | 200 `{ orderId, status:"Fulfilled", captureId, capturedAmount, paypalFee, netAmount, currency }` |
| 7.4 | `POST /api/orders/{orderId}/cancel` | **admin** | — | 200 `{ orderId, status:"Canceled" }` |
| 7.5 | `POST /api/orders/{orderId}/refunds` | shopper (own) | `idempotencyKey` (required), `amount?` (partial) | 201 `{ **refundId**, orderId, amount, status, totalRefunded, capturedAmount }` |
| 7.6 | `GET /api/my-orders` | shopper | — | 200 `[{ orderId, orderDate, total, currency, status, authorizationId?, captureId?, capturedAmount?, paypalFee?, netAmount?, refunds:[{refundId,amount,status}] }]` |
| 7.7 | `GET /api/reconciliation?from=&to=` | **admin** | ISO-8601 `from`,`to` | 200 `{ from, to, matched:[...], paypalOnly:[...], eShopOnly:[...], note }` |
| 7.8 | `POST /api/payment-methods` | shopper | `card:{number,expiry"YYYY-MM",securityCode,cardholderName,billingAddress{...}}` | 201 `{ **paymentMethodId**, brand, lastDigits, expiry, cardholderName? }` |
| 7.9 | `GET /api/payment-methods` | shopper | — | 200 `[{ paymentMethodId, brand, lastDigits, expiry, cardholderName? }]` |
| 7.10 | `DELETE /api/payment-methods/{paymentMethodId}` | shopper (own) | — | 204 (or 200 `{ paymentMethodId, deleted:true }`) |

Notes:
- Shopper attribute: `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
  Admin attribute adds `Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS`.
- Response DTOs must never contain PAN/CVV. `card` in requests is bound, used, and discarded — never
  echoed back and never logged.
- Add AutoMapper `CreateMap`s in `MappingProfile.cs` only if convenient; hand-mapping is also fine (both
  patterns exist in the repo).
- Register the new application services (`IPaymentService`, `IPaymentMethodService`,
  `IReconciliationService`, `IOrderPlacementService`) as scoped in `Program.cs` (PublicApi does not
  register `IOrderService` today, so these must be added explicitly). `IUriComposer` is already registered.

---

## 8. Configuration & DI (PayPal settings)

### 8.1 The binding contract (exact keys)
Bind an options type `PayPalSettings` from the **`PayPal:`** section with **exactly** these keys — bind
them, hard-code none:
- `PayPal:ClientId` ← env `PAYPAL_CLIENT_ID`
- `PayPal:ClientSecret` ← env `PAYPAL_CLIENT_SECRET`
- `PayPal:Environment` ← env `PAYPAL_ENVIRONMENT`
- `PayPal:Currency` ← env `PAYPAL_CURRENCY`
- `PayPal:BaseUrl` (optional) — when set, use **verbatim** as the API base address for **every** PayPal
  call **including the OAuth token request** (Appendix A, Feature 9 confirms the SDK routes both API and
  token calls through `options.Server.Default.Sandbox.BaseUrl` under the default token strategy).

> **Secrets never enter the repository.** Do not write credential *values* into any file (appsettings,
> PLAN, tests). `appsettings.json`'s new `"PayPal"` section may carry only non-secret defaults such as
> `BaseUrl` (blank/omitted) and a `Currency` fallback — the real values come from env vars at run time.

### 8.2 The env-var bridge (a gotcha — must implement)
`Program.cs` already calls `builder.Configuration.AddEnvironmentVariables()` (no prefix). The .NET env
provider maps **double** underscores to the `:` section separator — so `PAYPAL_CLIENT_ID` (single
underscores) becomes a **flat** key `PAYPAL_CLIENT_ID`, **not** `PayPal:ClientId`. You must bridge
explicitly. In `Program.cs`, after the existing `AddEnvironmentVariables()` call, add the flat `PAYPAL_*`
env vars into the `PayPal:` section (only when present, so appsettings defaults survive), e.g.:

```csharp
var paypalEnv = new Dictionary<string, string?>
{
    ["PayPal:ClientId"]     = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"),
    ["PayPal:ClientSecret"] = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET"),
    ["PayPal:Environment"]  = Environment.GetEnvironmentVariable("PAYPAL_ENVIRONMENT"),
    ["PayPal:Currency"]     = Environment.GetEnvironmentVariable("PAYPAL_CURRENCY"),
    ["PayPal:BaseUrl"]      = Environment.GetEnvironmentVariable("PAYPAL_BASE_URL"), // optional convenience
};
builder.Configuration.AddInMemoryCollection(
    paypalEnv.Where(kv => !string.IsNullOrEmpty(kv.Value)));
```
(Added last → env values win when present; appsettings `PayPal:*` provide fallbacks. `PAYPAL_BASE_URL`
is also honored via config key `PayPal:BaseUrl` if the harness sets it as `PayPal__BaseUrl`.)

Then: `builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection("PayPal"));`

### 8.3 SDK client registration
In `Infrastructure/Dependencies.cs` (called by PublicApi), register the SDK client + gateway:
- Build `PayPalServerSdkClient` from `PayPalSettings` (client id/secret via `OAuth2ClientCredentials`;
  `Environment = ServerEnvironment.Sandbox`; if `BaseUrl` is set, apply
  `options.Server.Default.Sandbox.BaseUrl = settings.BaseUrl` verbatim). **Load
  `dotnet-client-initialization` first** to decide `HttpClient` ownership/lifetime and singleton-vs-scoped
  registration (Appendix A trap; the constructor takes the `HttpClient`). Prefer the SDK's own
  `AddPayPalServerSdkClient(...)` DI extension if it cleanly accepts these options.
- Register `IPaymentGateway → PayPalPaymentGateway` (scoped).
- Interpret `PayPal:Environment`: this SDK exposes only `ServerEnvironment.Sandbox` (Appendix A GAP-1).
  Treat `Environment=sandbox` as sandbox; to target live, set `BaseUrl=https://api-m.paypal.com` (there
  is no Live enum member — this is the documented workaround, not a gap). For this task, sandbox is the
  target.

---

## 9. PayPal integration workflow for the build session (mandatory)

The PayPal SDK is a plugin (**paypal-sdk**, apimatic marketplace) and is the **sole** reference for talking
to PayPal — **no web search, no external/general PayPal knowledge**. Follow the `integrate-paypal` skill's
gates exactly:

1. **Load the `integrate-paypal` skill first.** Then, as your first action, **spawn the `paypal-sdk`
   agent once** with the full feature set, dictating the plan output path
   `<repo root>/paypal-plan.md`. It returns a grounded contract sheet. **The equivalent contract sheet is
   already embedded here as Appendix A** (produced by that same agent during planning) — use it to seed
   `paypal-plan.md` and to implement, but still run the agent so it can (a) confirm the exact package
   id/version to install, (b) resolve the UNVERIFIED items (D2 issue strings, D4 `custom_field` echo), and
   (c) fix any SDK compile error in place.
2. **Do not create/edit any project file while that agent runs.** During the wait, do only read-only prep:
   `dotnet restore`, a baseline `dotnet build`/`dotnet test` of the untouched solution, and confirm the
   `PAYPAL_*` env vars are present.
3. **HARD GATE:** before writing any integration code, confirm `paypal-plan.md` exists and read it.
4. **Load every `dotnet-*` companion skill** the sheet's REQUIRED READING names (Appendix A §4):
   `dotnet-client-initialization`, `dotnet-authentication`, `dotnet-calling-endpoints`, `dotnet-models`,
   `dotnet-error-handling`, `dotnet-configuration-resilience`, and `dotnet-testing` for tests. These are
   API-agnostic usage skills — load them; take contract *facts* only from the sheet/agent.
5. **Package:** add the SDK to `Directory.Packages.props` (`<PackageVersion>`) + a `<PackageReference>` in
   **`Infrastructure.csproj`** only (the sole SDK consumer). Appendix A identifies the SDK as
   `AsadAli.Checkout.Sdk` (root namespace `PayPalServerSdk`, map tag `v1.0.1`) — **confirm the exact
   package id and version with the paypal-sdk agent / plugin before pinning it.**
6. Implement `PayPalPaymentGateway` strictly from the sheet: signatures verbatim (the cancellation-token
   param is named `ct`; the first several nullable params have **no defaults** and must be passed
   explicitly — pass `null` to skip); enums are `StringEnum<T>` (`CheckoutPaymentIntent.Authorize`);
   money `Value` is a **string** (format `F2` invariant — D1); read the deeply nested authorization
   envelope (`OrderAuthorizeResponse.PurchaseUnits[].Payments.Authorizations[]`) and capture breakdown
   (`SellerReceivableBreakdown.PaypalFee`/`NetAmount`); pass `payPalRequestId` on every write; **prefer
   `return=representation`** on CreateOrder/AuthorizeOrder/Capture/Refund so ids/amounts come back in the
   response (else follow with the `Get*` read the sheet lists).
7. **Any SDK compile/runtime error → hand the exact error + files to the SAME warm `paypal-sdk` agent
   (follow-up message, never a second spawn), and wait.** Do not guess-fix SDK-name errors more than once.
8. If a needed capability is genuinely absent from the plugin → **STOP and report the gap** (do not invent
   a workaround). Planning found **no such gap**: all ten capabilities map to real operations (GAP-1/2/3
   in Appendix A are design decisions already handled here, not missing capabilities).

---

## 10. Cross-cutting rules

- **Idempotency in effect:**
  - Authorize/capture: application state check (already Authorized/Fulfilled → return existing) **plus** a
    persisted, reused `PayPal-Request-Id` per logical write. Persist the request key **before** the PayPal
    call so a crash-retry reuses it.
  - Refund: caller idempotency key deduped in our DB (return existing `refundId`) **and** passed as
    `PayPal-Request-Id`. Distinct keys → distinct partial refunds allowed.
  - Optimistic concurrency (`rowversion`) is a nice hardening but the **in-memory provider does not support
    it**; rely on state checks + request ids (documented limitation).
- **Money:** authorize/capture exactly `Order.Total()`; format to `F2` invariant string at the SDK
  boundary; assert held == order total to the cent (Appendix A D1).
- **Security/PII:** never persist or log PAN/CVV/expiry-as-entered; responses expose only brand/last-4/
  expiry; do not log full request bodies of `pay`/`payment-methods`. The JWT secret and PayPal secret are
  never written to the repo.
- **Error boundary:** the gateway catches `SdkException<TError>` and translates to domain results/exceptions
  (typed accessors per op — `TryGetError`/`TryGetError1`; `SearchTransactions` is the lone Case-B via
  `RawError`). **Also handle `System.Text.Json.JsonException`** — a drifted 2xx body or a non-matching
  error body throws `JsonException` that would otherwise escape or destroy the HTTP status; do **not** map
  every `JsonException` to 5xx blindly (Appendix A §4 hazards; **load `dotnet-error-handling`**). Branch
  operator-facing conditions (over-refund, expired-auth) on **HTTP status (422/409) + best-effort issue
  text**, never on a hardcoded issue string alone (D2 UNVERIFIED).

---

## 11. Persistence details

- Add `DbSet<Payment>`, `DbSet<Refund>`, `DbSet<SavedPaymentMethod>` to `CatalogContext`.
- Add `IEntityTypeConfiguration<T>` for each in `src/Infrastructure/Data/Config/` (auto-applied):
  - `Payment`: money fields `decimal(18,2)` (`Amount`, `CapturedAmount`, `PayPalFee`, `NetAmount`);
    `BuyerId` required `maxLength(256)`; string lengths for PayPal ids/status/currency/correlation ref;
    `Refunds` navigation `PropertyAccessMode.Field` (mirror `OrderConfiguration`'s field-access pattern);
    index on `OrderId` and on `CorrelationReference`.
  - `Refund`: `Amount decimal(18,2)`; `RefundId`/`Status`/`IdempotencyKey` lengths; unique index on
    (`PaymentId`,`IdempotencyKey`) for refund idempotency.
  - `SavedPaymentMethod`: `BuyerId` required `maxLength(256)`; `VaultId`/`Brand`/`LastDigits`/`Expiry`
    lengths; index on `BuyerId`. **No PAN column exists.**
- **Migrations:** the **in-memory provider ignores migrations**, and the verify run uses in-memory, so a
  migration is **not required** to run/verify. For the SQL Server path (production-grade), generating an
  `AddPaymentsAndSavedCards` migration for `CatalogContext` is **recommended** (needs EF tools + roll-forward).
  Keep it optional and clearly separate from the in-memory run.

---

## 12. Environment & run (this machine's gotchas)

- **Runtime:** run with `DOTNET_ROLL_FORWARD=Major` (only .NET 10 SDK present, ASP.NET Core 8.0 runtime
  missing), or install ASP.NET Core 8.0 runtime x64, or set `global.json` `rollForward: latestMajor`.
- **Database:** run PublicApi with **`UseOnlyInMemoryDatabase=true`** (LocalDB is absent). The in-memory
  store **loses all data on restart and ignores migrations** — so **create, pay, fulfil, and refund within
  a single run**. Set it via env (`UseOnlyInMemoryDatabase=true`) or `appsettings.Development.json`.
- **Per-host isolation:** with in-memory, Web and PublicApi hold separate stores — an order placed in the
  Web storefront is invisible to PublicApi. **Drive the whole flow through PublicApi** (that is why
  `POST /api/orders` exists).
- **Auth:** get a bearer token from `POST /api/authenticate` (storefront cookie won't work on PublicApi).
- **HTTPS dev cert:** ensure trusted (`dotnet dev-certs https --check`; `--trust` if needed).
- **Ports:** bind only to the assigned block (`APP_PORT_BLOCK_BASE … +APP_PORT_BLOCK_SIZE-1`;
  `launchSettings` already points there). Stop the previous instance before starting another.
- No other infra (no Docker/broker/PostgreSQL) — do not introduce any.
- **Env vars provided:** `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`,
  `PAYPAL_CURRENCY` (belong to a sandbox **business** account enabled for direct card + vaulting).
  Target the PayPal **sandbox**.

---

## 13. Build sequence (ordered)

1. **PayPal grounding first** (§9 steps 1–4): load `integrate-paypal`, spawn `paypal-sdk` agent, do
   read-only prep during the wait (`dotnet restore`, baseline `dotnet build`/`dotnet test`, env check),
   gate on `paypal-plan.md`, load the `dotnet-*` skills.
2. **Package**: add SDK to `Directory.Packages.props` + `Infrastructure.csproj` (confirm id/version).
3. **ApplicationCore**: enums + entities (`Payment`, `Refund`, `SavedPaymentMethod`), gateway DTOs,
   `IPaymentGateway`, service interfaces, specifications.
4. **Infrastructure**: EF configs + `CatalogContext` DbSets; `PayPalSettings`; `PayPalClientFactory` +
   `PayPalPaymentGateway` (from the sheet); DI in `Dependencies.cs`.
5. **ApplicationCore**: `OrderPlacementService`, `PaymentService`, `PaymentMethodService`,
   `ReconciliationService`.
6. **PublicApi**: env-var bridge + service registration in `Program.cs`; `appsettings.json` `PayPal`
   section (non-secret only); endpoints + request/response DTOs; error mapping.
7. **Build**; fix non-SDK errors yourself; route SDK errors to the warm `paypal-sdk` agent (§9 step 7).
8. **Run & self-verify** on sandbox (§14).
9. **Tests** (§15).

---

## 14. Self-verification & the user's verification guide

Do a real sandbox run proving: a real authorization on the test card, a real capture at fulfilment, a
real refund, and a saved card reused to pay a second order. **Test card:** Visa `4111 1111 1111 1111`,
any future expiry, any CVC, any name/billing address. Everything is drivable via PublicApi + curl (no
browser). Data is in-memory — do the whole sequence in one run.

Concise guide to hand the user (adjust host/port to the assigned block; `-k` for the dev cert):

```bash
# 0) Run PublicApi (in its own terminal), single run holds all state
DOTNET_ROLL_FORWARD=Major UseOnlyInMemoryDatabase=true \
PAYPAL_CLIENT_ID=... PAYPAL_CLIENT_SECRET=... PAYPAL_ENVIRONMENT=sandbox PAYPAL_CURRENCY=USD \
dotnet run --project src/PublicApi
API=https://localhost:37743   # or your assigned port

# 1) Tokens
SHOP=$(curl -sk $API/api/authenticate -H 'Content-Type: application/json' \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | jq -r .token)
ADMIN=$(curl -sk $API/api/authenticate -H 'Content-Type: application/json' \
  -d '{"username":"admin@microsoft.com","password":"Pass@word1"}'   | jq -r .token)

# 2) A catalog item id
ITEM=$(curl -sk "$API/api/catalog-items?pageSize=1" | jq -r '.catalogItems[0].id')

# 3) Place an order  -> orderId
OID=$(curl -sk $API/api/orders -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"items\":[{\"catalogItemId\":$ITEM,\"quantity\":1}]}" | jq -r .orderId)

# 4) Pay (authorize) with the test card
curl -sk $API/api/orders/$OID/pay -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"card":{"number":"4111111111111111","expiry":"2030-01","securityCode":"123",
       "cardholderName":"Test Buyer","billingAddress":{"addressLine1":"1 Main St","adminArea2":"Kent",
       "adminArea1":"OH","postalCode":"44240","countryCode":"US"}}}'
#    (repeat once -> same authorization, no double charge = idempotent)

# 5) my-orders shows Authorized
curl -sk $API/api/my-orders -H "Authorization: Bearer $SHOP"

# 6) Fulfil (admin) -> capture; response shows capturedAmount, paypalFee, netAmount
curl -sk -X POST $API/api/orders/$OID/fulfil -H "Authorization: Bearer $ADMIN"

# 7) Partial refund (shopper) with an idempotency key -> refundId; repeat same key -> same refundId
curl -sk $API/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"idempotencyKey":"refund-1","amount":1.00}'

# 8) Saved card reused: save -> paymentMethodId, place order2, pay with paymentMethodId, fulfil
PMID=$(curl -sk $API/api/payment-methods -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"card":{"number":"4111111111111111","expiry":"2030-01","securityCode":"123",
       "cardholderName":"Test Buyer","billingAddress":{"addressLine1":"1 Main St","adminArea2":"Kent",
       "adminArea1":"OH","postalCode":"44240","countryCode":"US"}}}' | jq -r .paymentMethodId)
OID2=$(curl -sk $API/api/orders -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"items\":[{\"catalogItemId\":$ITEM,\"quantity\":1}]}" | jq -r .orderId)
curl -sk $API/api/orders/$OID2/pay -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"paymentMethodId\":$PMID}"
curl -sk -X POST $API/api/orders/$OID2/fulfil -H "Authorization: Bearer $ADMIN"

# 9) Cancel-before-fulfil (void): order3 -> pay -> cancel (admin)
OID3=$(curl -sk $API/api/orders -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"items\":[{\"catalogItemId\":$ITEM,\"quantity\":1}]}" | jq -r .orderId)
curl -sk $API/api/orders/$OID3/pay -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"card":{"number":"4111111111111111","expiry":"2030-01","securityCode":"123","cardholderName":"T",
       "billingAddress":{"addressLine1":"1 Main St","adminArea2":"Kent","adminArea1":"OH",
       "postalCode":"44240","countryCode":"US"}}}'
curl -sk -X POST $API/api/orders/$OID3/cancel -H "Authorization: Bearer $ADMIN"

# 10) Reconciliation (admin). May be EMPTY for a just-now range (PayPal reporting lag) — expected.
curl -sk "$API/api/reconciliation?from=2020-01-01T00:00:00Z&to=2020-12-31T23:59:59Z" \
  -H "Authorization: Bearer $ADMIN"

# 11) Delete the saved card; then it must not list and not be payable
curl -sk -X DELETE $API/api/payment-methods/$PMID -H "Authorization: Bearer $SHOP"
curl -sk $API/api/payment-methods -H "Authorization: Bearer $SHOP"   # PMID gone
```

Expected: step 4 → `Authorized`; step 6 → `Fulfilled` with `capturedAmount == order total`, plus
`paypalFee`/`netAmount`; step 7 → a `refundId`, repeat returns the same one; step 8 → order2 authorized &
fulfilled via the saved card; step 9 → `Canceled` (funds released); step 10 → a report (possibly empty
over a recent range — not a failure). Ownership: a second shopper's token must get **404** on another's
order/card.

---

## 15. Testing

- **Unit tests (recommended, deterministic):** in `tests/UnitTests`, test `PaymentService`,
  `PaymentMethodService`, `ReconciliationService`, and the `Payment` aggregate against a **fake
  `IPaymentGateway`** (NSubstitute is already available). Cover: authorize-once idempotency; capture
  reads fee/net; refund idempotency key dedupe; two distinct partial refunds allowed; over-refund
  rejected; stale-auth renewal path and the "cannot renew → operator-actionable error" path; cancel
  voids; ownership 404s. (Load `dotnet-testing` for the SDK seam guidance, though unit tests fake the
  gateway, not the SDK.)
- **Integration:** `tests/PublicApiIntegrationTests` runs the API in-memory; endpoint/auth/role wiring
  can be tested with a fake gateway registered in the test host. Live sandbox calls are covered by the
  manual §14 run (keep them out of automated CI to avoid external dependency/flakiness).

---

## 16. Decisions, assumptions, and non-blocking SDK observations

**Decisions (owned here; the build session should follow them):**
- Separate `Payment` aggregate (not a field on `Order`) — additive, low-risk, keeps `Order` and Web
  checkout untouched. Compliant with the "reuse the order/order-item model" mandate.
- Refund is **shopper-scoped** (task lists only fulfil/cancel/reconciliation as operator actions; "every
  other endpoint is shopper-scoped").
- Correlate reconciliation primarily by PayPal `invoice_id` = `Payment.CorrelationReference`; `custom_id`
  (= OrderId) is a best-effort secondary.
- Amounts = catalog `Price`; currency = `PayPal:Currency`; authorize exactly `Order.Total()`.

**Assumptions (from the contract sheet, still valid):**
- A2 — Direct card / no 3DS challenge for the sandbox test card. If a challenge/`PAYER_ACTION_REQUIRED`
  appears, **STOP and report** (do not build an approval round-trip).
- A3 — Pay-with-saved-card uses `payment_source.card.vault_id` (not the billing-agreement token path).

**Non-blocking SDK observations (design-handled, NOT capability gaps):**
- GAP-1 — No Live env member; target sandbox (or set `BaseUrl` to the live host). Not needed here.
- GAP-2 — No `EXPIRED` auth status; detect staleness via `ExpirationTime` + a failed `ReauthorizePayment`
  (§6.3).
- GAP-3 — No client-side over-refund cap; enforce app-side via `TotalRefunded()` vs `CapturedAmount`
  (§6.5) in addition to PayPal's server-side rejection.
- UNVERIFIED (confirm against sandbox traffic during build): exact rejection `Issue` strings (D2) and
  whether `custom_id` echoes into `custom_field` in transaction search (D4). Branch on HTTP status +
  best-effort text; prefer `invoice_id` for correlation.

---

# Appendix A — PayPal .NET SDK CONTRACT SHEET (grounded; produced by the paypal-sdk agent)

> This is the authoritative PayPal reference for the build. It was generated by the `paypal-sdk` agent
> against the bundled SDK map (every row cites its map page). Treat these contracts as authoritative; do
> not re-derive them from memory. The build session should still run the `paypal-sdk` agent per §9 to
> confirm the package id/version, resolve the UNVERIFIED items, and fix any SDK compile error in place.

SDK: `AsadAli.Checkout.Sdk` · root namespace `PayPalServerSdk` · client `PayPalServerSdkClient` ·
map provenance tag `v1.0.1` (source commit `9653d18`). Every fact is grounded in the bundled SDK map
(page cited per row); the client/base-URL/auth facts in §A9 and the base-URL-verbatim guarantee were
confirmed from SDK source because the map does not carry the `ServerOptions` shape.

## A1. Scope & sequence

| # | Feature | Operations (controller.method) |
|---|---|---|
| 1 | Authorize with a direct card | `Orders.CreateOrder` (intent=AUTHORIZE, card payment_source) → `Orders.AuthorizeOrder` |
| 2 | Capture at fulfilment | `Payments.CaptureAuthorizedPayment` |
| 3 | Re-authorize a stale auth | `Payments.ReauthorizePayment` (detect via `Payments.GetAuthorizedPayment` + status/expiry) |
| 4 | Void an authorization | `Payments.VoidPayment` |
| 5 | Refund a capture (full/partial) | `Payments.RefundCapturedPayment` (track via `Payments.GetRefund`) |
| 6 | Idempotency | `payPalRequestId` param on CreateOrder / AuthorizeOrder / CaptureAuthorizedPayment / RefundCapturedPayment (+ Reauthorize/Void/Vault) |
| 7 | Save & reuse a card (vault) | `Vault.CreatePaymentToken`; pay with `Orders.CreateOrder` (card.vault_id); `Vault.ListCustomerPaymentTokens`; `Vault.DeletePaymentToken` |
| 8 | Reconciliation | `TransactionSearch.SearchTransactions` (manual page loop) |
| 9 | Client setup + base-URL override | `PayPalServerSdkClientOptions` (`Oauth2`, `Environment`, `Server.Default.Sandbox.BaseUrl`) |
| 10 | Error handling | `SdkException<TError>` boundary |

## A2. Namespaces (add a separate `using` per kind — C# does not import child namespaces transitively)

| Kind | Namespace | Examples |
|---|---|---|
| Client, options, `Server`, `ServerOptions`, `DefaultOptions` | `PayPalServerSdk` | `PayPalServerSdkClient`, `PayPalServerSdkClientOptions`, `ServerOptions` |
| Controllers | `PayPalServerSdk.Api` | `Orders`, `Payments`, `Vault`, `TransactionSearch` |
| Records (request/response models) | `PayPalServerSdk.Models` | `OrderRequest`, `CardRequest`, `Money`, `CapturedPayment`, `Refund`, … |
| Enums (`StringEnum<T>`) | `PayPalServerSdk.Models.Enums` | `CheckoutPaymentIntent`, `AuthorizationStatus`, `CaptureStatus`, `RefundStatus`, `CardBrand` |
| Typed error classes `{Op}Error` | `PayPalServerSdk.Errors` | `CreateOrderError`, `RefundCapturedPaymentError`, … |
| `ServerEnvironment` | `PayPalServerSdk.Servers` | `ServerEnvironment.Sandbox` |
| `SdkException<T>` | `PayPalServerSdk.Core.Exceptions` | catch type |
| `RawError`, `ApiError` | `PayPalServerSdk.Core.ErrorResponse` | Case-B error, base error |
| `RetryOptions` | `PayPalServerSdk.Core.Configuration` | `options.Retry` |
| `OAuth2ClientCredentials` | `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials` | `options.Oauth2` |
| `IOAuth2TokenStrategy<>` | `PayPalServerSdk.Core.Authentication.OAuth2` | `options.Oauth2TokenStrategy` |

> Signatures are generated code, verbatim — every parameter name is the literal C# identifier. The
> cancellation-token parameter really is named `ct`. Write each SDK type with the namespace from its own
> map row.

### Feature 1 — AUTHORIZE with a direct card

**Step 1a — `client.Orders.CreateOrder`** (`operations/Orders.md`)
- Signature: `CreateOrder(string? payPalMockResponse, string? payPalRequestId, string? payPalPartnerAttributionId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderRequest body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`
  - First 5 params are nullable with **no default** → must pass explicitly (pass `null` to skip). `body` is required (non-null).
- Returns: `Order` (`records-1-Ac-Pa.md`). Envelope: `Order.Id` is the order id; `Order.Status: OrderStatus?`; `Order.PurchaseUnits: IReadOnlyList<PurchaseUnit>?`.
- Error: `SdkException<CreateOrderError>` — Case A. `TryGetError(out Error)` [400,401,422] · `TryGetRawError(out RawError)` [fallback]. `Error` payload = `PayPalServerSdk.Models.Error`.

Request body `OrderRequest` (`records-1-Ac-Pa.md`):
| Field (wire) | Type | Req? |
|---|---|---|
| `Intent (intent)` | `CheckoutPaymentIntent` | **required** → set `CheckoutPaymentIntent.Authorize` (wire `AUTHORIZE`) |
| `PurchaseUnits (purchase_units)` | `IReadOnlyList<PurchaseUnitRequest>` | **required** |
| `PaymentSource (payment_source)` | `PaymentSource` | optional — set for direct card |
| `Payer (payer)` | `Payer` | optional |
| `ApplicationContext (application_context)` | `OrderApplicationContext` | optional |

`PurchaseUnitRequest` (`records-2-Pa-Ve.md`): `Amount (amount): AmountWithBreakdown` **required**; `ReferenceId (reference_id): string?`; `CustomId (custom_id): string?`; `InvoiceId (invoice_id): string?` (← set these two for reconciliation correlation, Feature 8); `Description`, `Items`, `Shipping`, `Payee`, …

`AmountWithBreakdown` (`records-1-Ac-Pa.md`): `CurrencyCode (currency_code): string` **req**, `Value (value): string` **req**, `Breakdown (breakdown): AmountBreakdown?`. `Money` (`records-1-Ac-Pa.md`): `CurrencyCode (currency_code): string` **req**, `Value (value): string` **req**. NOTE: `value` is a **string** — format to exact cents (D1).

Card in `PaymentSource` (`records-2-Pa-Ve.md`): `PaymentSource.Card (card): CardRequest?`.
`CardRequest` (`records-1-Ac-Pa.md`):
| Field (wire) | Type | Notes |
|---|---|---|
| `Name (name)` | `string?` | cardholder name |
| `Number (number)` | `string?` | raw PAN e.g. `4111111111111111` (sandbox test card ok) |
| `Expiry (expiry)` | `string?` | format `YYYY-MM` |
| `SecurityCode (security_code)` | `string?` | cvc |
| `BillingAddress (billing_address)` | `Address?` | see below |
| `VaultId (vault_id)` | `string?` | ← used in Feature 7 to pay with a saved card |
| `Attributes (attributes)` | `CardAttributes?` | vault-on-use / verification opts |

`Address` (`records-1-Ac-Pa.md`): `AddressLine1 (address_line_1): string?`, `AddressLine2 (address_line_2): string?`, `AdminArea2 (admin_area_2): string?` (city), `AdminArea1 (admin_area_1): string?` (state), `PostalCode (postal_code): string?`, `CountryCode (country_code): string` **req**.

**Step 1b — `client.Orders.AuthorizeOrder`** (`operations/Orders.md`)
- Signature: `AuthorizeOrder(string id, string? payPalMockResponse, string? payPalRequestId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderAuthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`
  - Params `payPalMockResponse … body` (5) nullable, no default → pass explicitly. `id` = order id from Step 1a. If the card was supplied on CreateOrder, `body` may be `null`; alternatively supply the card here via `OrderAuthorizeRequest.PaymentSource.Card` (`OrderAuthorizeRequestPaymentSource.Card: CardRequest?`).
- Returns: `OrderAuthorizeResponse` (`records-1-Ac-Pa.md`).
- Error: `SdkException<AuthorizeOrderError>` — Case A. `TryGetError(out Error)` [400,401,403,404,422,500] · `TryGetRawError(out RawError)`.

**Reading back the authorization id + status (nested — three levels down):**
`OrderAuthorizeResponse.PurchaseUnits` (`IReadOnlyList<PurchaseUnit>?`) → `PurchaseUnit.Payments` (`PaymentCollection?`) → `PaymentCollection.Authorizations` (`IReadOnlyList<AuthorizationWithAdditionalData>?`) → per element (`AuthorizationWithAdditionalData`):
- `Id (id): string?` — the authorization id (Features 2/3/4).
- `Status (status): AuthorizationStatus?` — expect `AuthorizationStatus.Created` (wire `CREATED`).
- `Amount (amount): Money?` — held amount.
- `ExpirationTime (expiration_time): string?` — honor-period expiry (Feature 3).

`CheckoutPaymentIntent` (`enums.md`): `Capture (CAPTURE)`, `Authorize (AUTHORIZE)`.
`OrderStatus` (`enums.md`): `Created, Saved, Approved, Voided, Completed, PayerActionRequired`.
`AuthorizationStatus` (`enums.md`): `Created, Captured, Denied, PartiallyCaptured, Voided, Pending`. **(No `EXPIRED` member.)**

### Feature 2 — CAPTURE at fulfilment

**`client.Payments.CaptureAuthorizedPayment`** (`operations/Payments.md`)
- Signature: `CaptureAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, CaptureRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`
  - `payPalMockResponse … body` (4) nullable, no default → pass explicitly. `body` may be `null` for full capture; for partial pass `CaptureRequest`.
- `CaptureRequest` (`records-1-Ac-Pa.md`): `Amount (amount): Money?`, `InvoiceId (invoice_id): string?`, `FinalCapture (final_capture): bool? = false`, `NoteToPayer`, `SoftDescriptor`, `PaymentInstruction`.
- Returns: `CapturedPayment` (`records-1-Ac-Pa.md`).
- Error: `SdkException<CaptureAuthorizedPaymentError>` — Case A. `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)`.

**Reading back amounts** from `CapturedPayment`:
- `Id (id): string?` — capture id (Feature 5).
- `Status (status): CaptureStatus?`.
- `Amount (amount): Money?` — captured amount charged.
- `SellerReceivableBreakdown (seller_receivable_breakdown): SellerReceivableBreakdown?` → `records-2-Pa-Ve.md`:
  - `GrossAmount (gross_amount): Money` **req** — gross captured amount.
  - `PaypalFee (paypal_fee): Money?` — **PayPal's fee**.
  - `NetAmount (net_amount): Money?` — **net proceeds to the merchant**.
  - also `PaypalFeeInReceivableCurrency`, `ReceivableAmount`, `ExchangeRate`, `PlatformFees`.

`CaptureStatus` (`enums.md`): `Completed, Declined, PartiallyRefunded, Pending, Refunded, Failed`.

### Feature 3 — RE-AUTHORIZE a stale authorization

**`client.Payments.ReauthorizePayment`** (`operations/Payments.md`)
- Signature: `ReauthorizePayment(string authorizationId, string? payPalRequestId, string? payPalAuthAssertion, ReauthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`
  - `payPalRequestId`, `payPalAuthAssertion`, `body` nullable, no default → pass explicitly.
- `ReauthorizeRequest` (`records-2-Pa-Ve.md`): **only** `Amount (amount): Money?` (only `amount` is supported).
- Returns: `PaymentAuthorization` (`records-2-Pa-Ve.md`): `Id`, `Status: AuthorizationStatus?`, `Amount`, `ExpirationTime: string?`, `StatusDetails: AuthorizationStatusDetails?`.
- Error: `SdkException<ReauthorizePaymentError>` — Case A. `TryGetError(out Error)` [400,401,403,404,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError`.

**Detect expired / cannot-reauthorize** — read current auth first with
`client.Payments.GetAuthorizedPayment(authorizationId, payPalMockResponse, payPalAuthAssertion, …)` → `PaymentAuthorization`:
- `Status: AuthorizationStatus?` — `Voided`/`Captured` cannot be reauthorized.
- `ExpirationTime: string?` — compare to now (honor period 3 days; reauthorize days 4–29; after 30 days create a new authorized payment).
- `AuthorizationStatusDetails.Reason: AuthorizationIncompleteReason?` (`enums.md`: `PendingReview`, `DeclinedByRiskFraudFilters`).

> GAP: `AuthorizationStatus` has **no `EXPIRED`**. Detect expiry from `ExpirationTime` elapsed; treat a
> failed `ReauthorizePayment` (typed `Error`, HTTP 422/400) as the authoritative "cannot reauthorize"
> signal (D2).

### Feature 4 — VOID an authorization

**`client.Payments.VoidPayment`** (`operations/Payments.md`)
- Signature: `VoidPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, string? payPalRequestId, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`
  - `payPalMockResponse`, `payPalAuthAssertion`, `payPalRequestId` nullable, no default → pass explicitly. No request body.
- Returns: `PaymentAuthorization` (status → `AuthorizationStatus.Voided`).
- Error: `SdkException<VoidPaymentError>` — Case A. `TryGetError(out Error)` [401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError`. (A fully-captured auth cannot be voided → typed error.)

### Feature 5 — REFUND a captured payment (full or partial)

**`client.Payments.RefundCapturedPayment`** (`operations/Payments.md`)
- Signature: `RefundCapturedPayment(string captureId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, RefundRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)`
  - `payPalMockResponse … body` (4) nullable, no default → pass explicitly.
  - **Full refund:** `body: null`. **Partial refund:** `RefundRequest` with `Amount`.
- `RefundRequest` (`records-2-Pa-Ve.md`): `Amount (amount): Money?`, `CustomId`, `InvoiceId`, `NoteToPayer`, `PaymentInstruction`.
- Returns: `Refund` (`records-2-Pa-Ve.md`):
  - `Id (id): string?` — refund id.
  - `Status (status): RefundStatus?`.
  - `Amount (amount): Money?` — refunded amount.
  - `SellerPayableBreakdown: SellerPayableBreakdown?` → `GrossAmount`, `PaypalFee`, `NetAmount`, and **`TotalRefundedAmount (total_refunded_amount): Money?`** (cumulative refunded — reason about remaining).
- Error: `SdkException<RefundCapturedPaymentError>` — Case A. `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError`.

`RefundStatus` (`enums.md`): `Cancelled, Failed, Pending, Completed`.

**Over-refund protection:** server-side by PayPal only — the SDK exposes **no client-side cap field**. An
over-refund is rejected as the typed `RefundCapturedPaymentError` (422/409). Cumulative-refunded state via
`SellerPayableBreakdown.TotalRefundedAmount` (or `Payments.GetRefund(refundId, …)`) and the capture's
`CaptureStatus` (`PartiallyRefunded`/`Refunded`). Enforce your own guard too (D-style, §6.5).

### Feature 6 — IDEMPOTENCY (PayPal-Request-Id)

The key is the **`payPalRequestId` (`string?`)** param on each write op (maps to header `PayPal-Request-Id`).
Nullable, no default → **pass explicitly**.

| Operation | Param name / type / position |
|---|---|
| `Orders.CreateOrder` | `payPalRequestId` `string?` (2nd) |
| `Orders.AuthorizeOrder` | `payPalRequestId` `string?` (3rd) |
| `Payments.CaptureAuthorizedPayment` | `payPalRequestId` `string?` (3rd) |
| `Payments.RefundCapturedPayment` | `payPalRequestId` `string?` (3rd) |
| `Payments.ReauthorizePayment` | `payPalRequestId` `string?` (2nd) |
| `Payments.VoidPayment` | `payPalRequestId` `string?` (4th) |
| `Vault.CreatePaymentToken` / `Vault.CreateSetupToken` | `payPalRequestId` `string?` (1st) |

### Feature 7 — SAVE & REUSE a card (vault)

**Vault — `client.Vault.CreatePaymentToken`** (`operations/Vault.md`)
- Signature: `CreatePaymentToken(string? payPalRequestId, PaymentTokenRequest body, RequestOptions? requestOptions = null, CancellationToken ct = default)`
- `PaymentTokenRequest` (`records-2-Pa-Ve.md`): `Customer (customer): Customer?`, `PaymentSource (payment_source): PaymentTokenRequestPaymentSource` **req**.
  - `Customer` (`records-1-Ac-Pa.md`): `Id (id): string?`, `MerchantCustomerId (merchant_customer_id): string?` — set/reuse to group saved cards for the list op.
  - `PaymentTokenRequestPaymentSource`: `Card (card): PaymentTokenRequestCard?`, `Token (token): VaultTokenRequest?`.
  - `PaymentTokenRequestCard`: `Name`, `Number`, `Expiry`, `SecurityCode`, `Brand (brand): CardBrand?`, `BillingAddress: Address?`.
- Returns: `PaymentTokenResponse` (`records-2-Pa-Ve.md`):
  - `Id (id): string?` — **the vault id / payment-token id** (store; reuse to pay & delete).
  - `Customer: CustomerResponse?`.
  - `PaymentSource: PaymentTokenResponsePaymentSource?` → `.Card: CardPaymentTokenEntity?` — SAFE description: `LastDigits (last_digits): string?`, `Brand: CardBrand?`, `Expiry: string?`, `Name: string?`. **No full PAN returned** — only `last_digits`.
- Error: `SdkException<CreatePaymentTokenError>` — Case A. `TryGetError1(out Error1)` [400,403,404,422,500] · `TryGetRawError`. NOTE payload type is **`Error1`**.

(A two-step alt exists — `Vault.CreateSetupToken` → `Vault.CreatePaymentToken` referencing the setup token
via `PaymentTokenRequestPaymentSource.Token: VaultTokenRequest {Id, Type=VaultTokenRequestType.SetupToken}` —
but for a raw card the one-step `CreatePaymentToken` with `.Card` is sufficient.)

**Pay later with the saved card** — reuse `Orders.CreateOrder` (Feature 1) with
`OrderRequest.PaymentSource.Card = new CardRequest { VaultId = "<PaymentTokenResponse.Id>" }` — no PAN re-entry.

**List — `client.Vault.ListCustomerPaymentTokens`** (`operations/Vault.md`)
- Signature: `ListCustomerPaymentTokens(string customerId, int? pageSize = 5, int? page = 1, bool? totalRequired = false, RequestOptions? requestOptions = null, CancellationToken ct = default)`. Call with **named arguments** (T3).
- Returns: `CustomerVaultPaymentTokensResponse` (`records-1-Ac-Pa.md`): `TotalItems`, `TotalPages`, `PaymentTokens: IReadOnlyList<PaymentTokenResponse>?`, `Customer`, `Links`.
- Error: `SdkException<ListCustomerPaymentTokensError>` — Case A. `TryGetError1(out Error1)` [400,403,500] · `TryGetRawError`.

**Delete — `client.Vault.DeletePaymentToken`** (`operations/Vault.md`)
- Signature: `DeletePaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` — `id` = payment-token/vault id.
- Returns: `void` (Task). Error: `SdkException<DeletePaymentTokenError>` — Case A. `TryGetError1(out Error1)` [400,403,500] · `TryGetRawError`.

`CardBrand` (`enums.md`) — key members: `Visa, Mastercard, Amex, Discover, Jcb, Diners, Maestro, Elo, Rupay, ChinaUnionPay, Unknown` (30 total).

> App-side note: the app is the source of truth for saved-card ownership (each `SavedPaymentMethod` stores
> `BuyerId` + `VaultId`), so `GET /api/payment-methods` lists from our DB; `Vault.ListCustomerPaymentTokens`
> is available if a PayPal-side customer grouping is used.

### Feature 8 — RECONCILIATION (transaction search with pagination)

**`client.TransactionSearch.SearchTransactions`** (`operations/TransactionSearch.md`)
- Signature: `SearchTransactions(string startDate, string endDate, string? transactionId, string? transactionType, string? transactionStatus, string? transactionAmount, string? transactionCurrency, string? paymentInstrumentType, string? storeId, string? terminalId, string? fields = "transaction_info", string? balanceAffectingRecordsOnly = "Y", int? pageSize = 100, int? page = 1, RequestOptions? requestOptions = null, CancellationToken ct = default)`
  - `startDate`/`endDate` **required**, ISO-8601 (wire `start_date`/`end_date`). Params `transactionId … terminalId` (8) nullable, no default → pass explicitly or use named args. Defaults `fields="transaction_info"`, `balanceAffectingRecordsOnly="Y"`, `pageSize=100`, `page=1`.
  - **Call with named arguments** (T3).
- Returns: `SearchResponse` (`records-2-Pa-Ve.md`):
  - `TransactionDetails: IReadOnlyList<TransactionDetails>?`
  - Pagination: `Page: int?`, `TotalItems: int?`, `TotalPages: int?`.
- Error: `SdkException<RawError>` — **Case B (the ONLY Case-B op)**. Read `ex.Error.StatusCode` / `ReadAsString()` / `ReadAsJson<T>()`; no `TryGetError` typed accessor.

**Pagination — no built-in auto-pager.** Loop manually: `page = 1`; after each call read `TotalPages`;
request `page = 2 … TotalPages`. Pass `pageSize` explicitly. (D3)

**Correlation fields** per `TransactionDetails.TransactionInfo` (`TransactionInformation`, `records-2-Pa-Ve.md`):
- `TransactionId (transaction_id): string?` — PayPal's txn id.
- `TransactionStatus (transaction_status): string?` — plain string, not an enum.
- `TransactionAmount (transaction_amount): Money?`.
- `InvoiceId (invoice_id): string?` — **correlate to eShop via `PurchaseUnitRequest.InvoiceId` set at CreateOrder.**
- `CustomField (custom_field): string?` — also correlatable; set via `PurchaseUnitRequest.CustomId`. (You SET `custom_id`, READ `custom_field` — wire asymmetry, UNVERIFIED, D4.)

### Feature 9 — CLIENT SETUP + base-URL override

```csharp
var options = new PayPalServerSdkClientOptions
{
    Environment = ServerEnvironment.Sandbox,          // PayPalServerSdk.Servers
    Oauth2 = new OAuth2ClientCredentials              // ...Core.Authentication.OAuth2.ClientCredentials
    {
        ClientId = "<from config>",
        ClientSecret = "<from config>",
        Scope = null                                  // optional
    },
    // Server = new ServerOptions { Default = new DefaultOptions { Sandbox = { BaseUrl = "<override>" } } }
};
var client = new PayPalServerSdkClient(httpClient, options);
```
`OAuth2ClientCredentials` (source-confirmed): `ClientId` (req), `ClientSecret` (req), `Scope` (opt). Set
`options.Oauth2`; leave `options.Oauth2TokenStrategy` null to use the default client-credentials flow.

**Environment (`ServerEnvironment`, `PayPalServerSdk.Servers`):** ONLY member is `Sandbox` (GAP-1 — no
Live member).

**Custom base-URL override (source-confirmed; map lacks the shape):**
`options.Server` (`ServerOptions`) → `.Default` (`DefaultOptions`) → `.Sandbox` (`DefaultOptions.SandboxOptions`)
→ **`.BaseUrl` (settable `string`, default `"https://api-m.sandbox.paypal.com"`)**. i.e.
`options.Server.Default.Sandbox.BaseUrl = "https://your-host";`.
- Prepended **verbatim** to every request path (`{BaseUrl}/v2/checkout/orders`).
- **Also governs the OAuth token request**: the default strategy builds its token URL via
  `server.Default("/v1/oauth2/token")` → same `DefaultOptions.Sandbox.BaseUrl`. So a configured base
  address applies verbatim to **all** calls **including the token request** — provided you use the default
  token strategy (do not set `options.Oauth2TokenStrategy`).
- To target live PayPal despite no Live member, set `BaseUrl = "https://api-m.paypal.com"` (GAP-1 workaround).

DI alternative (`ServiceCollectionExtensions.cs`): `services.AddPayPalServerSdkClient(o => { /* set o.Oauth2 / o.Environment / o.Server */ });`.

### Feature 10 — ERROR HANDLING

- All ops are **throw-based**; **no `…Result` no-throw variants**. On an error status the SDK throws
  `SdkException<TError>` (`PayPalServerSdk.Core.Exceptions`), exposing `.Error` of type `TError`.
- **Case A (39 of 40 ops)** — `TError` is a generated `{Op}Error : ApiError`. Read via the op's
  `TryGet…(out …)` accessors, else inherited `TryGetRawError(out RawError)`. Payloads:
  - Orders + Payments → `TryGetError(out Error)`; `Error`: `Name` **req**, `Message` **req**, `DebugId`
    **req**, `Details: IReadOnlyList<ErrorDetails>?`, `Links`. `ErrorDetails`: `Field`, `Value`,
    `Location`, `Issue` **req**, `Description`.
  - Payments ops additionally expose `TryGetNoContent(out RawError)` [500].
  - Vault ops → `TryGetError1(out Error1)`; `Error1` same shape but `Details: IReadOnlyList<ErrorDetails1>?`.
- **Case B (1 op: `TransactionSearch.SearchTransactions`)** — `TError` is `RawError`
  (`PayPalServerSdk.Core.ErrorResponse`): `StatusCode: HttpStatusCode`, `ReadAsString()`, `ReadAsJson<T>()`,
  `ReadAsBytes()`. No typed accessors.
- Read HTTP status: Case A via the matching accessor or `TryGetRawError(out var raw)` → `raw.StatusCode`;
  Case B via `ex.Error.StatusCode`.
- **Do not parse `.ToString()` when an accessor exists.** Load `dotnet-error-handling`.

## A3. Trap notes

- ⚠ Client & DI setup — HttpClient/handler lifetime & client reuse aren't visible in the ctor. **Load
  `dotnet-client-initialization`** before `new PayPalServerSdkClient(...)` / `AddPayPalServerSdkClient`.
- ⚠ Auth — where credentials go relative to construction, and secret sourcing, aren't shown by the
  `Oauth2` type. **Load `dotnet-authentication`** (first place to look on 401/403).
- ⚠ Config/resilience — T1: SDK `Retry`/`Timeout` don't bound a whole call and aren't the `HttpClient`
  timeout; whether a failed write can be silently re-sent isn't visible → this is why every write carries
  a `payPalRequestId`. **Load `dotnet-configuration-resilience`**.
- ⚠ List/search — T3: `SearchTransactions` & `ListCustomerPaymentTokens` have many optional params with no
  C# default; a positional call mis-binds. **Use named arguments.** **Load `dotnet-calling-endpoints`**.
- ⚠ Models — T4: enums are `StringEnum<T>` (`CheckoutPaymentIntent.Authorize` / `.FromValue("AUTHORIZE")`,
  not a C# enum); money `Value` is a **string**; unmodeled JSON is dropped on deserialize. **Load
  `dotnet-models`**.
- ⚠ Error boundary — T5: which typed accessor per op, `Error` vs `Error1`, the single Case-B op, and the
  `JsonException` traps. **Load `dotnet-error-handling`**.
- ⚠ Tests — T6: the `HttpClient` ctor arg is the test seam. **Load `dotnet-testing`**.

### Defensive-coding directives (some UNVERIFIED — confirm on sandbox during build)
- **D1** — `Money.Value` / `AmountWithBreakdown.Value` are **strings**. Format the total to an exact 2-dp
  invariant string (`total.ToString("F2", CultureInfo.InvariantCulture)`) so held/captured == order total
  to the cent. The SDK does no rounding.
- **D2** — Read a rejection reason best-effort from the typed error (`TryGetError`→`Details?[i].Issue`/
  `Message`, or `TryGetError1` for Vault), else `TryGetRawError` → `StatusCode` + `ReadAsString()`. The
  exact over-refund / expired-auth `Issue` strings are **UNVERIFIED** — branch on HTTP status (422/409) +
  best-effort text, never a hardcoded issue string alone.
- **D3** — `SearchTransactions` has no auto-pager: loop `page = 1..TotalPages`, passing `pageSize`
  explicitly, to cover the whole range.
- **D4** — Correlation: SET `PurchaseUnitRequest.InvoiceId` (`invoice_id`) and `CustomId` (`custom_id`) at
  CreateOrder; READ `TransactionInformation.InvoiceId` and `.CustomField` (`custom_field`). Whether live
  search echoes `custom_id`→`custom_field` for a given txn type is **UNVERIFIED** — prefer `invoice_id` as
  primary; treat `custom_field` as best-effort; don't fail reconciliation if empty.
- **D5 (added)** — Pass `prefer: "return=representation"` on CreateOrder/AuthorizeOrder/Capture/Refund so
  ids/amounts/breakdown come back in the response; otherwise follow with the listed `Get*` read.

## A4. REQUIRED READING (load BEFORE implementation)

| Skill | Governs |
|---|---|
| `dotnet-client-initialization` | Building/registering the client, HttpClient lifetime, DI (§A9) |
| `dotnet-authentication` | Setting `Oauth2` credentials, secret sourcing, 401/403 (§A9) |
| `dotnet-calling-endpoints` | First call, named-argument calling for list/search |
| `dotnet-models` | Request models, `StringEnum<T>`, string money values |
| `dotnet-error-handling` | The try/catch boundary, typed vs raw errors, `JsonException` traps |
| `dotnet-configuration-resilience` | Retries/timeouts, base-URL override, pagination |
| `dotnet-testing` | Faking the `HttpClient` seam (tests) |

Two mandatory `JsonException` hazards for the error boundary:
- A drifted/malformed **2xx** body (missing `required` member) surfaces as `System.Text.Json.JsonException`
  from deserialization, **not** `SdkException` — an SDK-exception-only catch ladder lets it escape.
- A **non-2xx** body that doesn't match its `{Op}Error` shape throws `JsonException` while the error object
  is constructed, so `JsonException` **replaces** `SdkException` and the HTTP status is lost — mapping every
  `JsonException` to 5xx then reports a deterministic rejection as an outage, and a 5xx-retrier retries
  something that can never succeed. **Load `dotnet-error-handling` before writing that boundary.**
