# PLAN — PayPal payments & saved cards for eShopOnWeb (PublicApi)

> This is the build plan for a **later session** that has the same task and the same PayPal
> `paypal-sdk` plugin, plus this file — but **not** this conversation. Everything it needs is here.
> It replaces the skill's `pay-pal-server-sdk-plan.md`; the SDK contract sheet is §7, the application
> design is §2–§6, and the build/verify sequence is §8–§9. Skills loaded are listed in §12.
>
> **Binding rules carried over from the integrate skill — the build session must honour them:**
> - Take every SDK contract fact from §7 (or re-open the one map page / source file it cites). Never
>   write an SDK signature, wire name, enum value or error accessor from memory.
> - Load every skill in §11 **before** implementing.
> - Build against the NuGet package `Darker98.PayPalServerSdk` (version-less). Do **not** copy SDK
>   source into the repo.
> - Secrets never enter the repo. Bind config by key name only.

---

## 1. SDK identity (orientation — facts come from §7)

| | |
|---|---|
| NuGet package | `Darker98.PayPalServerSdk` (install version-less: `dotnet add package Darker98.PayPalServerSdk`) |
| Root namespace | `PayPalServerSdk` · client `PayPalServerSdkClient` · options `PayPalServerSdkClientOptions` |
| Auth | OAuth 2.0 client credentials → `options.Oauth2 = new OAuth2ClientCredentials { ClientId, ClientSecret }` |
| Environment | `ServerEnvironment.Sandbox` (the **only** environment the SDK declares) |
| API spec | `2.29` · generator APIMatic 4.0.0 · target `netstandard2.0` |
| Controllers in scope | `client.Orders`, `client.Payments`, `client.Vault`, `client.TransactionSearch` |

**BaseUrl override is source-verified to cover the token request.** `AuthSchemes` builds the token URL as
`server.Default("/v1/oauth2/token")` — the identical `server.Default(...)` resolution every operation uses,
seeded from `options.Server.Default.Sandbox.BaseUrl`. So setting that one property routes **every** call,
including the credential/token request, through `PayPal:BaseUrl` verbatim. **No custom `Oauth2TokenStrategy`
is needed.** (Verified in SDK source `AuthSchemes.cs`, `PayPalServerSdkClient.cs`,
`Core/Authentication/OAuth2/ClientCredentials/OAuth2ClientCredentialsStrategy.cs`.)

---

## 2. Architecture & where code goes (eShop clean-architecture conventions)

Dependency direction: `BlazorShared ← ApplicationCore ← Infrastructure`; both `Web` and `PublicApi`
reference ApplicationCore + Infrastructure. **The PayPal SDK package is referenced by `Infrastructure`
ONLY** — ApplicationCore stays SDK-free (it references only BlazorShared). This mirrors the existing
`IEmailSender` (ApplicationCore interface) / `EmailSender` (Infrastructure impl) seam.

| Concern | Location | Convention/exemplar to imitate |
|---|---|---|
| New domain entities / aggregates | `src/ApplicationCore/Entities/<Aggregate>/` | `Entities/OrderAggregate/Order.cs` (BaseEntity + `IAggregateRoot`, private ctor + guarded public ctor, encapsulated collections) |
| New abstractions (gateway, services) | `src/ApplicationCore/Interfaces/` | `Interfaces/IEmailSender.cs`, `Interfaces/IOrderService.cs` |
| Application services (orchestration) | `src/ApplicationCore/Services/` | `Services/OrderService.cs` |
| Specifications | `src/ApplicationCore/Specifications/` | `Specifications/CustomerOrdersWithItemsSpecification.cs`, `OrderWithItemsByIdSpec.cs` |
| PayPal gateway impl, settings, DbContext changes, EF configs, migrations | `src/Infrastructure/` | `Infrastructure/Services/EmailSender.cs`; `Infrastructure/Data/Config/OrderConfiguration.cs`; `Infrastructure/Dependencies.cs` |
| API endpoints + request/response DTOs | `src/PublicApi/<Feature>Endpoints/` | `CatalogItemEndpoints/CreateCatalogItemEndpoint.cs` (POST+auth), `CatalogItemGetByIdEndpoint.cs` (GET) |
| Cross-tier shared DTOs/constants | `src/BlazorShared/` | `Authorization/Constants.cs`, `BaseUrlConfiguration.cs` |

Namespaces: ApplicationCore `Microsoft.eShopWeb.ApplicationCore.*`; Infrastructure
`Microsoft.eShopWeb.Infrastructure.*`; PublicApi `Microsoft.eShopWeb.PublicApi.*`. **No GlobalUsings / no
ImplicitUsings** — add explicit `using`s in every file (including every `PayPalServerSdk.*` child namespace
a file touches — they are not transitively imported).

---

## 3. New domain model (ApplicationCore — SDK-free)

### 3.1 `OrderPayment` (aggregate root) — `Entities/OrderPaymentAggregate/`
1:1 with an existing `Order` (by `OrderId`). Carries the PayPal-owned state so any later request can act.

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK (BaseEntity) |
| `OrderId` | int | FK to `Order`; **unique index** |
| `BuyerId` | string | denormalised from `Order.BuyerId` (ownership + idempotency scope) |
| `CurrencyCode` | string | from `PayPal:Currency` at pay time |
| `Status` | `OrderPaymentStatus` | state machine, §3.4 |
| `PayPalOrderId` | string? | id from `CreateOrder` |
| `AuthorizationId` | string? | id of the hold (`purchase_units[].payments.authorizations[].id`) |
| `AuthorizationExpiresAt` | DateTimeOffset? | from authorization `expiration_time` — staleness check |
| `AuthorizedAmount` | decimal | order total held |
| `CaptureId` | string? | id of the capture (the money taken) |
| `CapturedGrossAmount` | decimal? | `seller_receivable_breakdown.gross_amount` |
| `PayPalFee` | decimal? | `seller_receivable_breakdown.paypal_fee` |
| `NetAmount` | decimal? | `seller_receivable_breakdown.net_amount` |
| `TotalRefundedAmount` | decimal | running sum of refunds; invariant guard |
| `AuthorizeRequestId` / `CaptureRequestId` / `VoidRequestId` | string | stable `PayPal-Request-Id`s, generated at creation (`Guid.NewGuid().ToString("N")`, ≤108 chars) |
| `_refunds` | `List<OrderRefund>` | encapsulated child collection |

`OrderRefund` (entity owned by OrderPayment): `Id`, `PayPalRefundId`, `Amount`, `IdempotencyKey`
(caller-supplied), `Status` (string), `CreatedDate`. No PAN/CVV anywhere.

Amount storage: `decimal(18,2)` (match `OrderItemConfiguration`'s `decimal(18,2)`).

### 3.2 `VaultedPaymentMethod` (aggregate root) — `Entities/PaymentMethodAggregate/`
A saved card belonging to one shopper. **Never** stores PAN/CVV — only the safe descriptor PayPal returns.

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK — this is the `paymentMethodId` returned to the shopper |
| `BuyerId` | string | owner (caller identity); ownership scope |
| `PayPalVaultId` | string | vault token id from `CreatePaymentToken` (used to pay) |
| `PayPalCustomerId` | string? | PayPal-generated `customer.id`; reused for this buyer's later vaults |
| `Brand` | string? | from `CardPaymentTokenEntity.brand` |
| `LastDigits` | string? | from `CardPaymentTokenEntity.last_digits` |
| `Expiry` | string? | `YYYY-MM` from `CardPaymentTokenEntity.expiry` |
| `CardholderName` | string? | from response (safe) |
| `CreatedDate` | DateTimeOffset | |

> Name the entity `VaultedPaymentMethod`, **not** `PaymentMethod` — a `PaymentMethod` already exists under
> `Entities/BuyerAggregate/` and is unrelated.

### 3.3 `PaymentOperationClaim` (aggregate root) — `Entities/PaymentOperationClaimAggregate/`
The duplicate-prevention claim store (§9 DUPLICATE CLAIMS). **String PK** = the claim key; a second insert
of the same key is refused by the primary key — enforced even by the EF **InMemory** provider.

| Field | Type | Notes |
|---|---|---|
| `Id` | string | PK = deterministic claim key, e.g. `"{orderId}:authorize"`, `"{orderId}:capture"`, `"{orderId}:void"`, `"{orderId}:refund:{idempotencyKey}"` |
| `CreatedDate` | DateTimeOffset | for sweeping stale claims |
| `Outcome` | string? | `"completed"` once settled; stores a small result reference (e.g. refundId) so a duplicate can return the first result |

> This entity has a **string** PK, unlike the int-keyed `BaseEntity`. Give it its own `Id` (string) and
> still mark `IAggregateRoot` so the generic `EfRepository<T>` serves it. EF InMemory throws on a duplicate
> key at `SaveChanges`/`Add` (`"An item with the same key has already been added"`) — catch that to detect
> the duplicate. Confirm the exact exception type during build (it surfaces as `ArgumentException` /
> `InvalidOperationException` on InMemory, `DbUpdateException` on SQL Server) and catch both.

### 3.4 `OrderPaymentStatus` state machine
`AwaitingPayment` → *(pay / authorize)* → `Authorized` → *(fulfil / capture)* → `Captured`
`Authorized` → *(cancel / void)* → `Cancelled`
`Captured` → *(refund)* → `PartiallyRefunded` → *(more refunds)* → `Refunded`
Plus `Failed` (declined authorization / terminal error). Each transition is **guarded by the current
status** (business rule) **and** by a `PaymentOperationClaim` (concurrency). An action on a wrong-state
order returns `409 Conflict` with a clear message.

---

## 4. Config binding (`PayPal:` section) — keys only, no values

Settings class `PayPalSettings` in `src/ApplicationCore/` (or `src/Infrastructure/`), `CONFIG_NAME = "PayPal"`:

| Property | Config key | Source env var | Required | Notes |
|---|---|---|---|---|
| `ClientId` | `PayPal:ClientId` | `PAYPAL_CLIENT_ID` | `[Required]` | OAuth client id |
| `ClientSecret` | `PayPal:ClientSecret` | `PAYPAL_CLIENT_SECRET` | `[Required]` | OAuth secret |
| `Environment` | `PayPal:Environment` | `PAYPAL_ENVIRONMENT` | `[Required]` | SDK exposes only `Sandbox`; validate present, map to `ServerEnvironment.Sandbox` |
| `Currency` | `PayPal:Currency` | `PAYPAL_CURRENCY` | `[Required]` | ISO-4217; used for every `Money`/`AmountWithBreakdown` |
| `BaseUrl` | `PayPal:BaseUrl` | — | optional | when set, used **verbatim** as the base for every call incl. token |

Bind + fail-fast:
```csharp
builder.Services.AddOptions<PayPalSettings>()
    .Bind(builder.Configuration.GetSection(PayPalSettings.CONFIG_NAME))
    .ValidateDataAnnotations()
    .ValidateOnStart();            // throws at startup, not on first call
```
`ValidateOnStart()` is load-bearing (every credential **part** checked — a blank part is not a missing one;
`[Required]` + `[MinLength(1)]` catches blank). Fail-fast message names the **key** (`"PayPal:ClientId is
not configured"`), never echoes the value, no fallback/placeholder/unauthenticated client.

> `PAYPAL_*` env vars must be surfaced into the `PayPal:` config section. ASP.NET Core maps env var
> `PayPal__ClientId` → `PayPal:ClientId` by default. The task supplies **flat** names (`PAYPAL_CLIENT_ID`).
> Wire a small mapping in `Program.cs` (e.g. `AddInMemoryCollection` from the flat vars, or
> `AddEnvironmentVariables` + an explicit map) so `PayPal:ClientId` ← `PAYPAL_CLIENT_ID`, etc. **Do not** put
> values in any file; read them from the environment at startup.

---

## 5. SDK client registration (Infrastructure DI)

Register over a **named `HttpClient`** (not `AddPayPalServerSdkClient`) so timeout, pooled-connection
lifetime and an **explicit** `LoggerFactory` are all controlled (per `dotnet-client-initialization` /
`dotnet-configuration-resilience`). Register the `PayPalServerSdkClient` as a **singleton** (long-lived →
token cache reused; one token round-trip, not one per call).

```csharp
const string ClientName = "PayPal";
services.AddHttpClient(ClientName, c => c.Timeout = TimeSpan.FromSeconds(30))   // per-attempt backstop
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5) });                  // DNS freshness behind singleton
services.AddSingleton(sp => {
    var s = sp.GetRequiredService<IOptions<PayPalSettings>>().Value;
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
    var options = new PayPalServerSdkClientOptions {
        Environment = ServerEnvironment.Sandbox,
        Oauth2 = new OAuth2ClientCredentials { ClientId = s.ClientId, ClientSecret = s.ClientSecret },
        Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(10) },   // per attempt
        Logging = new LoggingOptions {
            LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),  // EXPLICIT — disarms *_LOG env var
            LogRequestBody = false },                                  // card data in bodies — keep OFF
    };
    if (!string.IsNullOrWhiteSpace(s.BaseUrl))
        options.Server.Default.Sandbox.BaseUrl = s.BaseUrl;            // verbatim; also routes token (verified)
    return new PayPalServerSdkClient(http, options);
});
```
Facts behind the choices (from §7 / skills): default `HttpMethodsToRetry` = `GET,HEAD,PUT,OPTIONS`, so the
`POST`/`DELETE`/`PATCH` writes here are **never resent by the SDK** — good; we add idempotency keys + claims
on top for caller-level double-submits and unknown outcomes. `Retry.Timeout` and `HttpClient.Timeout` are
**per attempt**; a whole-call budget is a `CancellationToken` (§6, `Bounded` helper).

---

## 6. Application services (orchestration)

- **`IPayPalPaymentGateway`** (ApplicationCore interface; impl `PayPalPaymentGateway` in Infrastructure).
  Wraps `PayPalServerSdkClient`, maps domain DTOs ↔ SDK models, and owns the **error boundary** (§7 error
  rows; `dotnet-error-handling`). Takes/returns plain domain DTOs (ids, amounts, status strings, a safe
  card descriptor) — **no `PayPalServerSdk.*` type leaks into ApplicationCore**. Methods:
  `AuthorizeAsync`, `CaptureAsync`, `ReauthorizeAsync`, `VoidAsync`, `RefundAsync`, `GetOrderAsync`,
  `VaultCardAsync`, `DeleteVaultedCardAsync`, `SearchTransactionsAsync`. Each wraps the SDK call in a
  `Bounded` deadline helper (`CancellationTokenSource.CreateLinkedTokenSource(ct)` +
  `CancelAfter(budget)`, `ct = HttpContext.RequestAborted`) so the whole call is bounded, not just an attempt.
- **`IPaymentService`** (ApplicationCore; impl `PaymentService`). Orchestrates: load Order + verify
  ownership, read/transition `OrderPayment`, take the `PaymentOperationClaim`, call the gateway, persist
  result, settle unknown outcomes. Endpoints stay thin.
- **`IOrderPlacementService`** (or extend the existing `IOrderService`). The existing
  `OrderService.CreateOrderAsync(basketId, address)` is basket-based and **not** usable in PublicApi's
  isolated in-memory store. Add a method that builds an `Order` directly from catalog-item ids+quantities:
  load each `CatalogItem` (via `IReadRepository<CatalogItem>`), snapshot into `CatalogItemOrdered`
  (catalogItemId, name, pictureUri — use `IUriComposer` as the Web flow does), build `OrderItem(itemOrdered,
  unitPrice = CatalogItem.Price, units = qty)`, then `new Order(buyerId, shipToAddress, items)` and
  `AddAsync`. Amounts come from catalog prices (never the client). Ship-to address: accept optional in the
  request; default to the existing fixed address used by `Checkout.cshtml.cs`.
- Repositories (generic `EfRepository<T>` already covers any `IAggregateRoot`): `IRepository<OrderPayment>`,
  `IReadRepository<OrderPayment>`, `IRepository<VaultedPaymentMethod>`, `IReadRepository<VaultedPaymentMethod>`,
  `IRepository<PaymentOperationClaim>`, and the existing `IRepository<Order>` / `IReadRepository<Order>` /
  `IReadRepository<CatalogItem>`.
- **PublicApi `Program.cs` additions**: register the new services + gateway + `PayPalSettings` + SDK client
  (via an Infrastructure DI extension, e.g. `services.AddPayPalIntegration(config)`), and the order-placement
  service. PublicApi does **not** currently register `IOrderService` — register whatever the new placement
  service needs. Add `AddHttpContextAccessor()` if identity is read outside the endpoint lambda (prefer
  binding `ClaimsPrincipal`/`HttpContext` directly in the minimal-API lambda).

### 6.1 Flow → PayPal call mapping (the sequences)

**`POST /api/orders/{orderId}/pay`** (authorize — put a hold, do not take):
1. Load Order; `Order.BuyerId == caller` else `404`. Load/создать `OrderPayment`; require `AwaitingPayment`
   (idempotent: if already `Authorized`, return current state).
2. Claim `"{orderId}:authorize"`. Duplicate → return existing state.
3. Build `PaymentSource`: **one-off card** → `new PaymentSource { Card = new CardRequest { Number, Expiry
   ("YYYY-MM"), SecurityCode, Name, BillingAddress } }`; **saved card** → load the named
   `VaultedPaymentMethod` (verify `BuyerId == caller`), `new PaymentSource { Card = new CardRequest { VaultId
   = pm.PayPalVaultId } }`.
4. `client.Orders.CreateOrder(new CreateOrderRequest { Body = new OrderRequest { Intent =
   CheckoutPaymentIntent.Authorize, PurchaseUnits = [ new PurchaseUnitRequest { Amount = new
   AmountWithBreakdown { CurrencyCode = cfg, Value = total("0.00") }, CustomId = orderId.ToString(),
   InvoiceId = $"ESHOP-{orderId}" } ], PaymentSource = src }, PayPalRequestId = op.AuthorizeRequestId, Prefer
   = "return=representation" })`. (`PayPal-Request-Id` is **mandatory** for single-step create-with-card.)
5. Inspect `order.Status`:
   - `OrderStatus.PayerActionRequired` (3DS/browser challenge) → **STOP**: throw
     `PaymentChallengeRequiredException` → surface `422` with a clear message. This is the documented
     "challenge requires browser approval → STOP and report" path, **not** a build gap; do not build an
     approval round-trip.
   - If `order.PurchaseUnits[0].Payments?.Authorizations` already contains an authorization (single-step
     authorized) → take its `Id`, amount, `ExpirationTime`.
   - Else → `client.Orders.AuthorizeOrder(new AuthorizeOrderRequest { Id = order.Id, PayPalRequestId =
     op.AuthorizeRequestId, Prefer = "return=representation" })` → read
     `resp.PurchaseUnits[0].Payments.Authorizations[0]` → `Id`, amount, `ExpirationTime`, `Status`.
   - *(The card is attached at CreateOrder, so AuthorizeOrder needs no body. Whether CreateOrder-with-card
     auto-creates the authorization in a single step, or requires the AuthorizeOrder call, is **UNVERIFIED**
     — only live traffic settles it. The branch above handles **both** outcomes defensively and is the
     required implementation, not a choice to revisit.)*
6. Assert authorization amount == order total to the cent (log on mismatch). Update `OrderPayment`
   (`PayPalOrderId`, `AuthorizationId`, `AuthorizationExpiresAt`, `AuthorizedAmount`, `Status=Authorized`).
   Mark claim completed. Save.

**`POST /api/orders/{orderId}/fulfil`** (admin — capture, take the money):
1. Load Order+OrderPayment; require `Authorized`. Claim `"{orderId}:capture"`.
2. **Staleness/renewal**: if `AuthorizationExpiresAt` has passed (`options.TimeProvider`/`DateTimeOffset.UtcNow`),
   `client.Payments.ReauthorizePayment(new ReauthorizePaymentRequest { AuthorizationId, Body = new
   ReauthorizeRequest { Amount = new Money { CurrencyCode, Value } }, PayPalRequestId, Prefer =
   "return=representation" })` → new `PaymentAuthorization.Id` + `ExpirationTime`; update `AuthorizationId`.
   If reauthorize **throws** (`ApiException<ReauthorizePaymentError>` — e.g. past the 29-day window) → map to
   an **operator-actionable** error (`409`/`422`, message: *"The payment hold expired and can no longer be
   renewed (beyond PayPal's 29-day reauthorization window). Ask the shopper to pay again."*).
3. `client.Payments.CaptureAuthorizedPayment(new CaptureAuthorizedPaymentRequest { AuthorizationId, Body =
   new CaptureRequest { FinalCapture = true }, PayPalRequestId = op.CaptureRequestId, Prefer =
   "return=representation" })` → `CapturedPayment`: `Id`, `Status`,
   `SellerReceivableBreakdown.{GrossAmount,PaypalFee,NetAmount}` (each a `Money` → `decimal`). Store
   `CaptureId`, `CapturedGrossAmount`, `PayPalFee`, `NetAmount`, `Status=Captured`. Save.
   - Defensive: if capture fails with an expired-authorization error and we had **not** reauthorized this
     pass, reauthorize once then retry capture. *(The exact stale-capture error shape is **UNVERIFIED**;
     the proactive `expiration_time` check in step 2 is the primary path, this is the backstop.)*

**`POST /api/orders/{orderId}/cancel`** (admin — before fulfilment, release the hold):
1. Load Order+OrderPayment; require `Authorized`. Claim `"{orderId}:void"`.
2. `client.Payments.VoidPayment(new VoidPaymentRequest { AuthorizationId, PayPalRequestId = op.VoidRequestId })`
   (no body). → `Status=Cancelled`. Save. No money moved.

**`POST /api/orders/{orderId}/refunds`** (shopper-scoped — after fulfilment):
> Per the task's role rule, only **fulfil, cancel, reconciliation** are admin; "every other endpoint is
> shopper-scoped." So refund is **shopper-scoped**: verify `Order.BuyerId == caller`.
1. Load Order+OrderPayment; require `Captured`/`PartiallyRefunded`. Read caller idempotency key (required)
   and optional partial `Amount`.
2. **Invariant** (enforced before the SDK call): `(requested ?? remaining) + TotalRefundedAmount <=
   CapturedGrossAmount`; else `422` ("refund exceeds captured amount"). Full refund (no amount) refunds the
   remaining.
3. Claim `"{orderId}:refund:{idempotencyKey}"`. Duplicate → return the **stored** `refundId` (no second
   refund). Distinct keys for two partials → both proceed.
4. `client.Payments.RefundCapturedPayment(new RefundCapturedPaymentRequest { CaptureId, Body = amount is null
   ? null : new RefundRequest { Amount = new Money { CurrencyCode, Value } }, PayPalRequestId = idempotencyKey,
   Prefer = "return=representation" })` → `Refund`: `Id`, `Status`, `Amount`. (Empty body = full refund.)
5. Append `OrderRefund`; `TotalRefundedAmount += refund.Amount`; `Status = (TotalRefunded == Captured)
   ? Refunded : PartiallyRefunded`. Save. Return `refundId` top-level.

**`GET /api/my-orders`** (shopper): orders where `Order.BuyerId == caller` (reuse
`CustomerOrdersWithItemsSpecification`) left-joined to `OrderPayment` → per order: id, total, order date,
items, payment status, authorized/captured/fee/net/refunded amounts, PayPal ids.

**`GET /api/reconciliation?from&to`** (admin): see §7 pagination + §6.2.

**`POST /api/payment-methods`** (shopper — save a card):
1. `buyerId = caller`. Find this buyer's `PayPalCustomerId` from any existing `VaultedPaymentMethod` (null on
   first save).
2. `client.Vault.CreatePaymentToken(new CreatePaymentTokenRequest { Body = new PaymentTokenRequest {
   Customer = customerId is null ? null : new Customer { Id = customerId }, PaymentSource = new
   PaymentTokenRequestPaymentSource { Card = new PaymentTokenRequestCard { Number, Expiry, Name,
   SecurityCode, BillingAddress } } }, PayPalRequestId = Guid… })` → `PaymentTokenResponse`: `Id` (vault id),
   `Customer.Id` (PayPal customer id), `PaymentSource.Card` (`CardPaymentTokenEntity`: `Brand`, `LastDigits`,
   `Expiry`, `Name`).
3. Persist `VaultedPaymentMethod` (BuyerId, PayPalVaultId=resp.Id, PayPalCustomerId=resp.Customer.Id, Brand,
   LastDigits, Expiry, CardholderName). Return `paymentMethodId` (local `Id`) top-level + safe descriptor
   (brand + last4 + expiry). **Never** return/store PAN or CVV.

**`GET /api/payment-methods`** (shopper): the caller's `VaultedPaymentMethod`s from our own store (source of
truth for what this app vaulted), filtered by `BuyerId`. Safe descriptor only.

**`DELETE /api/payment-methods/{paymentMethodId}`** (shopper):
1. Load by id; `BuyerId == caller` else `404`.
2. `client.Vault.DeletePaymentToken(new DeletePaymentTokenRequest { Id = pm.PayPalVaultId })` (returns void).
3. Delete the local row. After: not listed; pay rejects it (pay looks it up in our store).

### 6.2 Reconciliation algorithm (admin)
1. Parse `from`/`to` (ISO-8601 date-times).
2. **Chunk** `[from,to]` into windows of **≤ 31 days** (`SearchTransactions` max range is 31 days).
3. For each window, page: `page = 1..`, `PageSize = 500`, `Fields = "all"` (to get `custom_field`/`invoice_id`),
   loop while `page <= response.TotalPages`, bounded by a **page cap** (safety). Accumulate
   `response.TransactionDetails[].TransactionInfo` (`TransactionId`, `TransactionAmount`, `TransactionStatus`,
   `InvoiceId`, `CustomField`, `FeeAmount`, dates).
4. eShop side: load `OrderPayment`s whose capture/authorize activity falls in `[from,to]`.
5. Match: PayPal `invoice_id == "ESHOP-{orderId}"` (primary) or `custom_field == orderId` (secondary).
6. Report three buckets: **matched**, **PayPal-only** (PayPal has it, eShop doesn't), **eShop-only** (eShop
   has it, PayPal doesn't). Report carries a `complete`/`truncated` flag (set false if a page cap was hit)
   and the window coverage. **An empty result over a just-created range is a valid sandbox outcome (reporting
   lag), not a gap** — return the (possibly empty) report, do not error.

---

## 7. CONTRACT SHEET

> ⚠ **Signatures are generated code, verbatim.** Each operation that takes input takes **one request
> record** as its first parameter (an operation with no inputs takes none), built with an **object
> initializer** whose property names are the record's own — never flat arguments.
> ⚠ **Every SDK type is written fully-qualified by the namespace its source path implies** (take the
> namespace from the path the map gives for *that* type, never from where a neighbour sits):
> `Models/` → `PayPalServerSdk.Models`; `Models/Enums/` → `PayPalServerSdk.Models.Enums`; `Errors/` →
> `PayPalServerSdk.Errors`; `Requests/<Controller>/` → `PayPalServerSdk.Requests.<Controller>`; client &
> options → `PayPalServerSdk`; `ServerEnvironment` → `PayPalServerSdk.Servers`; `OAuth2ClientCredentials` →
> `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials`; `RetryOptions`/`LoggingOptions` →
> `PayPalServerSdk.Core.Configuration`; exceptions → `PayPalServerSdk.Core.Exceptions`;
> `RawError`/`ApiError` → `PayPalServerSdk.Core.ErrorResponse`.

Defaults that apply to **every** row unless stated: throw-only (no `…Result` siblings anywhere in this SDK);
no pagination; server group `Default` (Sandbox base URL); inputs as one request record. `PayPal-Request-Id`
(member `PayPalRequestId`) is the **real** caller idempotency key on the write ops below; the generator's
injected `Idempotency-Key: Guid.NewGuid()` header is **not** a key — ignore it.

### 7.1 Orders — `client.Orders` (source: `map/operations/Orders.md`, `Api/Orders.cs`)

| Op | Signature (req record · key members) | Body model · key fields (wire) | Returns · fields read | Error case · accessors | Source |
|---|---|---|---|---|---|
| **CreateOrder** | `CreateOrder(CreateOrderRequest req)` · `Body` (required), `PayPalRequestId` (idempotency, **mandatory** w/ card), `Prefer`="return=minimal"→set `"return=representation"` | `OrderRequest`: `Intent (intent)`:req `CheckoutPaymentIntent`, `PurchaseUnits (purchase_units)`:req `IReadOnlyList<PurchaseUnitRequest>`, `PaymentSource (payment_source)`:opt, `ApplicationContext`:opt | `Order` · `Id`, `Status`, `PurchaseUnits[].Payments.Authorizations[].Id/ExpirationTime/Status/Amount`, `PaymentSource` | **A** · `TryGetError(out Error)` [400,401,422] · `TryGetRawError` last | Orders.md; `Models/OrderRequest.cs`, `Models/Order.cs`, `Errors/CreateOrderError.cs`, `Models/Error.cs` |
| **AuthorizeOrder** | `AuthorizeOrder(AuthorizeOrderRequest req)` · `Id` (required), `Body` opt `OrderAuthorizeRequest`, `PayPalRequestId`, `Prefer` | `OrderAuthorizeRequest`: `PaymentSource (payment_source)`:opt `OrderAuthorizeRequestPaymentSource` (omit — card attached at create) | `OrderAuthorizeResponse` · `Id`, `Status`, `PurchaseUnits[].Payments.Authorizations[].Id/ExpirationTime/Status/Amount` | **A** · `TryGetError(out Error)` [400,401,403,404,422,500] · `TryGetRawError` last | Orders.md; `Models/OrderAuthorizeResponse.cs`, `Errors/AuthorizeOrderError.cs` |
| **GetOrder** | `GetOrder(GetOrderRequest req)` · `Id` (required); query `Fields`←`fields` | — | `Order` (re-read for unknown-outcome settle) | **A** · `TryGetError(out Error)` [401,404] · `TryGetRawError` last | Orders.md; `Models/Order.cs`, `Errors/GetOrderError.cs` |
| *(ConfirmOrder available, NOT used — card attached at CreateOrder)* | | | | | Orders.md |

### 7.2 Payments — `client.Payments` (source: `map/operations/Payments.md`, `Api/Payments.cs`)

| Op | Signature · key members | Body · key fields | Returns · fields read | Error case · accessors | Source |
|---|---|---|---|---|---|
| **CaptureAuthorizedPayment** | `CaptureAuthorizedPayment(CaptureAuthorizedPaymentRequest req)` · `AuthorizationId` (req), `Body` opt `CaptureRequest`, `PayPalRequestId`, `Prefer`="return=representation" | `CaptureRequest`: `Amount (amount)`:opt `Money`, `FinalCapture (final_capture)`:opt bool, `InvoiceId`:opt | `CapturedPayment` · `Id`, `Status (CaptureStatus)`, `SellerReceivableBreakdown.{GrossAmount,PaypalFee,NetAmount}` (each `Money`) | **A** · `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` last | Payments.md; `Models/CaptureRequest.cs`, `Models/CapturedPayment.cs`, `Models/SellerReceivableBreakdown.cs`, `Models/Money.cs`, `Errors/CaptureAuthorizedPaymentError.cs` |
| **ReauthorizePayment** | `ReauthorizePayment(ReauthorizePaymentRequest req)` · `AuthorizationId` (req), `Body` opt `ReauthorizeRequest`, `PayPalRequestId`, `Prefer` | `ReauthorizeRequest`: `Amount (amount)`:opt `Money` (only field supported) | `PaymentAuthorization` · `Id`, `Status (AuthorizationStatus)`, `ExpirationTime`, `Amount` | **A** · `TryGetError(out Error)` [400,401,403,404,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` last | Payments.md; `Models/ReauthorizeRequest.cs`, `Models/PaymentAuthorization.cs`, `Errors/ReauthorizePaymentError.cs` |
| **VoidPayment** | `VoidPayment(VoidPaymentRequest req)` · `AuthorizationId` (req), `PayPalRequestId`, `Prefer` — **no Body** | — (empty body) | `PaymentAuthorization` · `Status` (VOIDED) | **A** · `TryGetError(out Error)` [401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` last | Payments.md; `Models/PaymentAuthorization.cs`, `Errors/VoidPaymentError.cs` |
| **RefundCapturedPayment** | `RefundCapturedPayment(RefundCapturedPaymentRequest req)` · `CaptureId` (req), `Body` opt `RefundRequest`, `PayPalRequestId` (**caller idempotency key**), `Prefer`="return=representation" | `RefundRequest`: `Amount (amount)`:opt `Money` (omit for full refund), `CustomId`:opt, `NoteToPayer`:opt | `Refund` · `Id`, `Status (RefundStatus)`, `Amount`, `SellerPayableBreakdown` | **A** · `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` last | Payments.md; `Models/RefundRequest.cs`, `Models/Refund.cs`, `Errors/RefundCapturedPaymentError.cs` |
| **GetAuthorizedPayment** / **GetCapturedPayment** / **GetRefund** | `…(…Request req)` · `AuthorizationId`/`CaptureId`/`RefundId` (req) | — | `PaymentAuthorization` / `CapturedPayment` / `Refund` (re-read for unknown-outcome settle) | **A** · `TryGetError(out Error)` · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` last | Payments.md |

### 7.3 Vault — `client.Vault` (source: `map/operations/Vault.md`, `Api/Vault.cs`)

| Op | Signature · key members | Body · key fields | Returns · fields read | Error case · accessors | Source |
|---|---|---|---|---|---|
| **CreatePaymentToken** | `CreatePaymentToken(CreatePaymentTokenRequest req)` · `Body` (req) `PaymentTokenRequest`, `PayPalRequestId` | `PaymentTokenRequest`: `Customer (customer)`:opt `Customer{Id}`, `PaymentSource (payment_source)`:req `PaymentTokenRequestPaymentSource{ Card: PaymentTokenRequestCard{ Number(number), Expiry(expiry), Name(name), SecurityCode(security_code), BillingAddress } }` | `PaymentTokenResponse` · `Id` (vault id), `Customer.Id` (PayPal customer id), `PaymentSource.Card` → `CardPaymentTokenEntity.{Brand,LastDigits,Expiry,Name}` | **A** · `TryGetError(out Error)` [400,403,404,422,500] · `TryGetRawError` last | Vault.md; `Models/PaymentTokenRequest.cs`, `Models/PaymentTokenRequestPaymentSource.cs`, `Models/PaymentTokenRequestCard.cs`, `Models/PaymentTokenResponse.cs`, `Models/CardPaymentTokenEntity.cs`, `Errors/CreatePaymentTokenError.cs` |
| **DeletePaymentToken** | `DeletePaymentToken(DeletePaymentTokenRequest req)` · `Id` (req) | — | `void` (Task) | **A** · `TryGetError(out Error)` [400,403,500] · `TryGetRawError` last | Vault.md; `Errors/DeletePaymentTokenError.cs` |
| *(ListCustomerPaymentTokens / GetPaymentToken / CreateSetupToken / GetSetupToken available; NOT used — GET /payment-methods reads our own store; cards vaulted one-step via CreatePaymentToken)* | `ListCustomerPaymentTokens`: query `customer_id,page_size(≤5),page(≤10),total_required`; paged | | `CustomerVaultPaymentTokensResponse` · `PaymentTokens[]`, `TotalItems`, `TotalPages` | **A** | Vault.md |

### 7.4 TransactionSearch — `client.TransactionSearch` (source: `map/operations/TransactionSearch.md`, `Api/TransactionSearch.cs`)

| Op | Signature · key members | Returns · fields read | Error case | Source |
|---|---|---|---|---|
| **SearchTransactions** | `SearchTransactions(SearchTransactionsRequest req)` · `StartDate` (req, ISO-8601), `EndDate` (req, **≤31 days** after start), `Fields`="transaction_info"→set `"all"`, `PageSize`=100 (≤500), `Page`=1, `BalanceAffectingRecordsOnly`="Y" | `SearchResponse` · `TransactionDetails[].TransactionInfo.{TransactionId,TransactionAmount,FeeAmount,TransactionStatus,InvoiceId,CustomField,TransactionInitiationDate}`, `Page`, `TotalItems`, `TotalPages` | **B** · `catch (ApiException<RawError>)` → `ex.Error.StatusCode` / `ReadAsString()` | TransactionSearch.md; `Requests/TransactionSearch/SearchTransactionsRequest.cs`, `Models/SearchResponse.cs`, `Models/TransactionDetails.cs`, `Models/TransactionInformation.cs` |
| *(SearchBalances available, NOT used)* | `SearchBalances` · query `as_of_time`,`currency_code` | `BalancesResponse` | **A** · `TryGetDefaultError(out DefaultError)` [400,403,500] | TransactionSearch.md |

### 7.5 Enums needed (source: `Models/Enums/`)

| Enum | Members (C# → wire) used | Source |
|---|---|---|
| `CheckoutPaymentIntent` | `Authorize`→`AUTHORIZE` (set on CreateOrder) | `Models/Enums/CheckoutPaymentIntent.cs` |
| `OrderStatus` | `Created`,`Approved`,`Completed`,`Voided`,**`PayerActionRequired`→`PAYER_ACTION_REQUIRED`** (challenge → STOP) | `Models/Enums/OrderStatus.cs` |
| `AuthorizationStatus` | `Created`,`Captured`,`Denied`,`PartiallyCaptured`,`Voided`,`Pending` (no `EXPIRED` member — staleness is via `expiration_time`) | `Models/Enums/AuthorizationStatus.cs` |
| `CaptureStatus` | `Completed`,`Declined`,`PartiallyRefunded`,`Pending`,`Refunded`,`Failed` | `Models/Enums/CaptureStatus.cs` |
| `RefundStatus` | `Completed`,`Pending`,`Failed`,`Cancelled` | `Models/Enums/RefundStatus.cs` |
| `TokenType` | only `BillingAgreement`→`BILLING_AGREEMENT` — **not** used; pay with saved card via `CardRequest.VaultId`, not `PaymentSource.Token` | `Models/Enums/TokenType.cs` |

> Enums are `OpenStringEnum<T>` (not C# enums): use static members; read wire value with `.Value` (never
> `ToString()`/interpolation — gives debug form); branch with generated `Match(...)`; resolve a raw value
> with `TryGetKnownValue`. `CustomId`/`Value`/`Expiry` string amounts are built with `CultureInfo.InvariantCulture`,
> `"0.00"` (2 dp) to match the order total "to the cent".

### 7.6 CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
|---|---|---|
| A saved card named at pay must be one the caller saved | `CreateOrder` (pay with `CardRequest.VaultId`) ← our `VaultedPaymentMethod` store (written by `CreatePaymentToken`, scoped by `BuyerId`) | `PaymentService.PayAsync`: look up `VaultedPaymentMethod` by `paymentMethodId` **and** `BuyerId==caller` before building `PaymentSource`; 404 if absent |
| A capture's authorization id must be one this app stored from authorize | `CaptureAuthorizedPayment`/`ReauthorizePayment`/`VoidPayment` (`AuthorizationId`) ← `OrderPayment.AuthorizationId` set by `CreateOrder`/`AuthorizeOrder` | `PaymentService.FulfilAsync`/`CancelAsync`: require `OrderPayment.Status==Authorized` and a non-null stored `AuthorizationId` |
| A refund's capture id must be one this app stored from fulfil | `RefundCapturedPayment` (`CaptureId`) ← `OrderPayment.CaptureId` set by capture | `PaymentService.RefundAsync`: require `Status` ∈ {Captured,PartiallyRefunded} and non-null `CaptureId` |
| Total refunded must never exceed captured | `RefundCapturedPayment` amount ← `OrderPayment.CapturedGrossAmount` − `TotalRefundedAmount` | `PaymentService.RefundAsync`: reject (422) before the SDK call if `(requested ?? remaining)+TotalRefunded > Captured` |
| The PayPal customer id for a buyer's vault is consistent | `CreatePaymentToken` (`Customer.Id`) ← prior `VaultedPaymentMethod.PayPalCustomerId` for that `BuyerId` | `PaymentService.SaveCardAsync`: reuse the buyer's existing `PayPalCustomerId`; capture PayPal's on first save |

### 7.7 Trap notes (hazard + skill pointer — not resolved here)

- **Child-namespace imports**: referencing an enum/error/request type with only `using PayPalServerSdk.Models;`
  fails to compile — each kind sits in its own child namespace. **MUST load `dotnet-models`** /
  `dotnet-getting-started` *Namespaces* table.
- **Enum to string on the wire** (`CustomId`, `Value`, path segments): using the enum in interpolation/URL
  silently emits the debug form, not the wire value. **MUST load `dotnet-models`** (§ Enums).
- **Union/`AnyOf` payloads that match no variant throw mid-deserialize**; `PaymentSource`/response models
  may carry them. **MUST load `dotnet-models`** (§ unions).
- **`TryGetRawError` is not a catch-all** and the Payments ops add `TryGetNoContent(out RawError)` [500]:
  ordering/omitting an accessor silently drops a response. **MUST load `dotnet-error-handling`** (Case A ladder).
- **`Prefer` defaults to `return=minimal`** — the capture breakdown / authorization detail is absent unless
  you set `return=representation`. **MUST load `dotnet-calling-endpoints`** (request-record defaults).
- **`Timeout` is per-attempt, not a call budget**; a whole-call bound is a `CancellationToken`. **MUST load
  `dotnet-configuration-resilience`** (Bounding a call).
- **`LogRequestBody` logs JSON bodies unredacted; the `*_LOG` env var can force it on** — card PANs/CVVs ride
  in request bodies. **MUST load `dotnet-configuration-resilience`** (Logging; Sensitive data).
- **Paged `SearchTransactions` must be bounded and chunked (31-day max)**; "provider signals the end" is not
  a bound. **MUST load `dotnet-configuration-resilience`** (Pagination; Never leave a page loop unbounded).
- **Transport failure on a write leaves the outcome unknown** — must settle, not report failure. **MUST load
  `dotnet-configuration-resilience`** (A write whose outcome is unknown) + `dotnet-error-handling`.
- **Missing/blank credential → unauthenticated request → 401 one round-trip later, no SDK error** — fail
  fast at startup. **MUST load `dotnet-authentication`**.

### 7.8 REQUIRED READING (load every one BEFORE implementing — this sheet does NOT carry their contents)

| Skill (plugin-qualified) | Governs |
|---|---|
| `paypal-sdk:dotnet-client-initialization` | SDK client construction, named `HttpClient`, singleton + `PooledConnectionLifetime` (§5) |
| `paypal-sdk:dotnet-authentication` | `Oauth2` credentials, startup fail-fast (§4) |
| `paypal-sdk:dotnet-configuration-resilience` | retries/timeouts, base-URL override, pagination, logging/sensitive-data, write idempotency & unknown outcomes (§5, §6.2, §9) |
| `paypal-sdk:dotnet-calling-endpoints` | building request records, `Prefer`, `cancellationToken:` by name (all §6 calls) |
| `paypal-sdk:dotnet-models` | enums, unions, wire names, `Money`/amount handling, `AdditionalProperties` (all model mapping) |
| `paypal-sdk:dotnet-error-handling` | Case A/B ladders, `ResponseDeserializationException`, connection/timeout/auth-scheme arms, boundary mapping (gateway error boundary) |
| `paypal-sdk:dotnet-testing` | `HttpMessageHandler` stub seam, error/retry/connection tests, `FakeTimeProvider` (§8 tests) |

**Always-true hazard row (verbatim):** a body that does not match its declared type — a drifted/malformed
**2xx** (a missing `required` member) or a **non-2xx** body that doesn't match the operation's generated
`{Operation}Error` shape — surfaces as `ResponseDeserializationException`: an `ApiException` that keeps the
HTTP status and names the target type but is **not** `ApiException<TError>`. A catch ladder that handles only
`ApiException<TError>` lets it escape — it **must** also catch `ResponseDeserializationException` (or
`ApiException`).

---

## 8. Build sequence

1. **Prereqs (no project edits):** `dotnet restore`; baseline `dotnet build` / `dotnet test` of the untouched
   solution (so later failures are attributable). Confirm dev cert trusted (`dotnet dev-certs https --check`).
   `DOTNET_ROLL_FORWARD=Major` + `global.json` `rollForward: latestMajor` for the .NET 10 SDK / missing
   ASP.NET 8 runtime. Run everything with `UseOnlyInMemoryDatabase=true`.
2. **Package:** `dotnet add src/Infrastructure/Infrastructure.csproj package Darker98.PayPalServerSdk`
   (version-less). Infrastructure only.
3. **ApplicationCore:** entities (`OrderPayment`+`OrderRefund`, `VaultedPaymentMethod`,
   `PaymentOperationClaim`), `OrderPaymentStatus`; interfaces (`IPayPalPaymentGateway`, `IPaymentService`,
   order-placement service); DTOs the gateway exchanges; specifications (`OrderPaymentByOrderIdSpec`,
   `VaultedPaymentMethodsByBuyerSpec`, etc.). `dotnet build`.
4. **Infrastructure:** `PayPalSettings`; `PayPalPaymentGateway` (SDK mapping + error boundary — build the
   error ladders from §7 + `dotnet-error-handling`); DI extension `AddPayPalIntegration`; DbSets on
   `CatalogContext`; EF configs (`decimal(18,2)`, unique index on `OrderPayment.OrderId`, string PK on
   `PaymentOperationClaim`); add migrations (SQL path; InMemory ignores them). `dotnet build`.
5. **PublicApi:** endpoint classes (§6 list) under `OrderEndpoints/`, `PaymentEndpoints/`,
   `PaymentMethodEndpoints/`, `ReconciliationEndpoints/` using the `IEndpoint<…>` MinimalApi pattern +
   `[Authorize(...)]` (admin via `Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS` +
   `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme`; shopper via JWT scheme only); DTOs
   (`BaseRequest`/`BaseResponse` convention); register services in `Program.cs`. Top-level response ids:
   `orderId`, `paymentMethodId`, `refundId`. `dotnet build`.
6. After each change: `dotnet build`; any `CS…` on a `PayPalServerSdk.*` symbol → re-open the §7-cited map
   page / source file (never re-guess). Then open each **DUPLICATE CLAIMS** / **UNKNOWN OUTCOMES** member
   and confirm it runs on that write; fix code, not the row.
7. **Tests** (match host framework): ApplicationCore/domain → **xUnit** (`tests/UnitTests`); PublicApi
   endpoint/integration → **MSTest** (`tests/PublicApiIntegrationTests`, `WebApplicationFactory<Program>`,
   `ApiTokenHelper.GetAdminUserToken()/GetNormalUserToken()`). Gateway tests use the `HttpMessageHandler`
   stub seam (`dotnet-testing`): success mapping, Case A/B error mapping, `ResponseDeserializationException`,
   `SdkConnectionException`/`SdkTimeoutException` → unknown-outcome settle, and a 403-for-normal-user /
   success-for-admin test per admin endpoint (imitate `CreateCatalogItemEndpointTest`).

---

## 9. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
| 1 | **Credential fail-fast** | `PayPalSettings` bound from `PayPal:` with `[Required]`+`[MinLength(1)]` on `ClientId`, `ClientSecret`, `Currency`, `Environment`; `.ValidateDataAnnotations().ValidateOnStart()` → host refuses to boot on any missing/blank part (each part checked; blank ≠ missing). Message names the key, never the value, no fallback (§4). |
| 2 | **Secret sourcing & rotation** | Secrets from env (`PAYPAL_CLIENT_ID`/`PAYPAL_CLIENT_SECRET`) → `PayPal:` config. SDK options built **once at singleton registration** and captured; a rotated secret takes effect only on **process restart**. Restart-to-rotate is acceptable here (reference app, sandbox); if hot rotation were required, supply a custom `Oauth2TokenStrategy` reading current config per fetch — not needed now. |
| 3 | **Total timeout budget** | `Retry.Timeout`=10s and `HttpClient.Timeout`=30s are **per attempt**. The caller's budget is a `CancellationToken` deadline in the `Bounded` helper in `PayPalPaymentGateway` (e.g. 30s, linked to `HttpContext.RequestAborted`), applied to every SDK call. Writes are non-retryable verbs → single attempt. |
| 4 | **Write-retry ownership** | Default `HttpMethodsToRetry`=`GET,HEAD,PUT,OPTIONS`. All our writes are `POST`/`DELETE` → **never resent by the SDK**. No `PUT` used. Reads (`GetOrder`, `SearchTransactions`=GET) may retry — acceptable (idempotent). We do not widen the retry list. |
| 5 | **Idempotency & ambiguous writes** | CreateOrder/AuthorizeOrder → stable `op.AuthorizeRequestId`; Capture → `op.CaptureRequestId`; Void → `op.VoidRequestId`; Refund → the **caller-supplied** idempotency key (member `PayPalRequestId`). All generated at `OrderPayment` creation except the refund key. Each is a **real** `PayPal-Request-Id` on the request record (not the injected header). Plus a `PaymentOperationClaim` (our store) per write (§ DUPLICATE CLAIMS). |
| 6 | **Observability** | Built-in SDK logger wired to the host `ILoggerFactory` at `Information` (request/response line) / `Warning` (retry) / `Error` (terminal). `LogRequestHeaders`/`LogResponseHeaders`/`LogRequestBody` all **off**. The error boundary logs the PayPal correlation id `Error.DebugId` (`debug_id`) + `StatusCode` + operation. No card data logged. |
| 7 | **Sensitive data** | Card PAN/CVV ride in `CardRequest`/`PaymentTokenRequestCard` **request bodies**. Therefore `LogRequestBody` stays **off** AND `LoggingOptions.LoggerFactory` is assigned **explicitly** (disarms the `PAYPALSERVERSDKCLIENT_LOG` env var that `trace` would use to force body logging). PAN/CVV **never** persisted or logged; only brand/last4/expiry (from responses) are stored. Our own diagnostics never echo a request body on these paths. |
| 8 | **Environment selection** | One server group `Default`; one environment `ServerEnvironment.Sandbox` (no production/live env declared in this SDK). All traffic is sandbox by construction — no risk of hitting live. `PayPal:BaseUrl`, when set, overrides `options.Server.Default.Sandbox.BaseUrl` (verbatim, incl. the token request — source-verified §1). `Environment` config is validated present and maps to Sandbox. |
| 9 | **Duplicate prevention under concurrency** | `PaymentOperationClaim` with **string PK** = deterministic claim key; the store refuses the second claim (PK conflict, enforced even by EF InMemory). See DUPLICATE CLAIMS table. |
| 10 | **Partial results** | `SearchTransactions` reconciliation is chunked (≤31-day windows) and paged to `TotalPages` with a page-cap backstop; the report object carries a `complete`/`truncated` flag (not a log line) so the caller sees truncation. See PAGED READS. |
| 11 | **Unknown outcomes** | Each write's `catch (SdkConnectionException/SdkTimeoutException)` re-reads provider state (Get* by stored id) or re-submits with the same `PayPal-Request-Id` (idempotent) to settle, recording an `unknown` state for a sweep rather than reporting failure. See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
| Pay (CreateOrder+Authorize) | `PaymentOperationClaim` PK `"{orderId}:authorize"` (our `CatalogContext`) | PK conflict on insert (InMemory + SQL) | `catch` of `DbUpdateException`/InMemory dup-key in `PaymentService.PayAsync` → load existing `OrderPayment` state | TBD |
| Fulfil (Capture) | `PaymentOperationClaim` PK `"{orderId}:capture"` | PK conflict on insert | `catch` in `PaymentService.FulfilAsync` → return existing capture state | TBD |
| Cancel (Void) | `PaymentOperationClaim` PK `"{orderId}:void"` | PK conflict on insert | `catch` in `PaymentService.CancelAsync` | TBD |
| Refund | `PaymentOperationClaim` PK `"{orderId}:refund:{idempotencyKey}"` | PK conflict on insert | `catch` in `PaymentService.RefundAsync` → return stored `refundId` (no second refund); distinct keys → both proceed | TBD |

> Order per row: claim write → SDK call → record result. The claim is written **before** the SDK call; a
> second caller is refused before reaching PayPal. Release/mark the claim stale on provider refusal so a
> transient failure is not a permanent block.

### PAGED READS

| Read | What caps it | How the caller learns it was cut short | Where in the code |
|---|---|---|---|
| `SearchTransactions` (reconciliation) | page cap (backstop) + `SearchResponse.TotalPages`; range chunked into ≤31-day windows | report DTO field `Complete`/`Truncated` (a value the caller reads, not a log line) | TBD |

### UNKNOWN OUTCOMES

| Write | Re-read with | Reference searched by | Where in the code | Test that fails the connection |
|---|---|---|---|---|
| CreateOrder / AuthorizeOrder | `GetOrder` (if id known) or re-POST with same `AuthorizeRequestId` (idempotent) | `PayPalOrderId` / `PayPal-Request-Id` | TBD (`PaymentService.PayAsync` catch) | stub `HttpMessageHandler` throws `HttpRequestException` on CreateOrder → assert unknown/settle, not plain failure |
| CaptureAuthorizedPayment | re-submit with same `CaptureRequestId`, or `GetAuthorizedPayment` (status CAPTURED) | `AuthorizationId` / `PayPal-Request-Id` | TBD (`FulfilAsync` catch) | stub throws on capture → assert settle |
| VoidPayment | re-submit with same `VoidRequestId`, or `GetAuthorizedPayment` (status VOIDED) | `AuthorizationId` / `PayPal-Request-Id` | TBD (`CancelAsync` catch) | stub throws on void → assert settle |
| RefundCapturedPayment | re-submit with same idempotency key (dedup), or `GetRefund` (if id stored) | caller idempotency key / `refundId` | TBD (`RefundAsync` catch) | stub throws on refund → assert settle |

---

## 10. Security & ownership (enforced in every handler)

- Identity from the JWT: `User.Identity.Name` (minted as `ClaimTypes.Name` = username by
  `IdentityTokenClaimService`). Bind `ClaimsPrincipal`/`HttpContext` in the endpoint lambda.
- **Orders:** every shopper endpoint loads the `Order` and checks `Order.BuyerId == caller`; mismatch →
  `404` (do not reveal existence). Admin endpoints (fulfil/cancel/reconciliation) require the
  `Administrators` role.
- **Saved cards:** every `VaultedPaymentMethod` access checks `BuyerId == caller`; one shopper can never see,
  use, or delete another's. Delete removes the local row **and** the PayPal vault token; afterwards it is not
  listed and pay rejects it.
- Full card details are never stored in the app DB and never written to logs (§9 rows 6–7).

---

## 11. Assumptions & Blockers

**Assumptions (decided — proceed):**
- *Refund is shopper-scoped.* The task names only fulfil/cancel/reconciliation as admin and says "every
  other endpoint is shopper-scoped," so `POST /api/orders/{orderId}/refunds` verifies order ownership and is
  not admin-gated. (If the build session judges operators should refund, the admin attribute is a one-line
  change; ownership check stays for shopper callers.)
- *`POST /api/orders` builds the Order directly from catalog-item ids+quantities* (not from a basket), since
  PublicApi's in-memory store has no basket. Prices come from `CatalogItem.Price`; ship-to address optional
  (defaults to the fixed address the Web checkout uses).
- *Saved cards use one-step vaulting* via `CreatePaymentToken` with a raw card (sandbox is vault-enabled);
  the two-step `CreateSetupToken`→`CreatePaymentToken` is available if needed but not used.
- *Pay-with-saved-card uses `CardRequest.VaultId`* (not `PaymentSource.Token`, whose only `TokenType` is
  `BILLING_AGREEMENT`).

**UNVERIFIED (only live traffic settles; defensive directive is mandatory, not a gap):**
- *CreateOrder-with-card single-step vs two-step authorization.* The pay flow (§6.1 step 5) inspects the
  CreateOrder response for an existing authorization and only calls `AuthorizeOrder` if none is present —
  handling both outcomes. Implement exactly that branch.
- *Exact error shape when capturing a stale authorization.* Primary path is the proactive `expiration_time`
  check + `ReauthorizePayment`; the capture-failure→reauthorize backstop (§6.1 fulfil) covers the rest.
- *Whether a saved-card (vault) authorization needs `CardRequest.StoredCredential` attributes.* The shopper
  is present (customer-initiated), so `VaultId` alone is planned; if PayPal rejects it demanding
  stored-credential data, add `CardRequest.StoredCredential` per the error — the field exists on the model.
- *Whether `custom_field` or `invoice_id` is the reliable reconciliation key* in sandbox reporting. We set
  **both** (`custom_id`=orderId, `invoice_id`=`ESHOP-{orderId}`) and match on `invoice_id` first,
  `custom_field` second.

**Blockers:** none. The SDK covers every capability this integration requires (order create/authorize/get,
capture/reauthorize/void/refund, vault create/delete, transaction search). The runtime "challenge requires
browser approval" case (`OrderStatus.PayerActionRequired`) is a documented **STOP-and-report** path (surface
`422`), not a missing capability. Sandbox reporting lag producing an empty reconciliation over a just-created
range is an expected result, not a gap.

---

## 12. Skills loaded this session (read before the decisions above)

- `paypal-sdk:dotnet-integrate-pay-pal-server-sdk` (workflow — loaded first)
- `paypal-sdk:dotnet-getting-started` (SDK map + source grounding)
- `paypal-sdk:dotnet-client-initialization`
- `paypal-sdk:dotnet-authentication`
- `paypal-sdk:dotnet-configuration-resilience`
- `paypal-sdk:dotnet-calling-endpoints`
- `paypal-sdk:dotnet-models`
- `paypal-sdk:dotnet-error-handling`
- `paypal-sdk:dotnet-testing`

SDK map/source consulted (branch `main`, spec `2.29`, generator 4.0.0): `sdk-map.md`;
`map/operations/{Orders,Payments,Vault,TransactionSearch}.md`; `Api/{Orders,Payments}.cs`;
`Requests/{Orders,Payments,Vault,TransactionSearch}/*`; `Models/{OrderRequest,Order,OrderAuthorizeRequest,
OrderAuthorizeResponse,PurchaseUnitRequest,PurchaseUnit,AmountWithBreakdown,Money,PaymentSource,CardRequest,
Token,CaptureRequest,CapturedPayment,SellerReceivableBreakdown,PaymentAuthorization,PaymentCollection,
ReauthorizeRequest,RefundRequest,Refund,PaymentTokenRequest,PaymentTokenRequestPaymentSource,
PaymentTokenRequestCard,PaymentTokenResponse,PaymentTokenResponsePaymentSource,CardPaymentTokenEntity,
CustomerVaultPaymentTokensResponse,Customer,SearchResponse,TransactionDetails,TransactionInformation,Error}`;
`Models/Enums/{CheckoutPaymentIntent,OrderStatus,AuthorizationStatus,CaptureStatus,RefundStatus,TokenType}`;
`Core/Authentication/OAuth2/**` + `AuthSchemes.cs` + `PayPalServerSdkClient.cs` (BaseUrl/token verification).

---

## 13. Verification guide (for the build session's self-check — direct card, no browser)

Run PublicApi with `UseOnlyInMemoryDatabase=true`, `DOTNET_ROLL_FORWARD=Major`, bound to the assigned port
block; `PayPal:*` from the `PAYPAL_*` env vars. All in **one process run** (in-memory store resets on
restart). Get a JWT from `POST /api/authenticate` first (admin and normal user).

1. **Place** `POST /api/orders` (shopper token) with catalog item ids+qty → capture `orderId`.
2. **Pay** `POST /api/orders/{orderId}/pay` with card `4111 1111 1111 1111`, any future expiry, any CVC →
   expect `Authorized` (a hold; money not taken). If `422` challenge → that is the documented STOP path.
3. **Fulfil** `POST /api/orders/{orderId}/fulfil` (admin token) → expect `Captured` with gross/fee/net
   populated from `seller_receivable_breakdown`.
4. **Refund** `POST /api/orders/{orderId}/refunds` (shopper, with an idempotency key; optional partial
   amount) → capture `refundId`; repeat the same key → same `refundId` (no double refund).
5. **Cancel path** (separate order): place + pay a second order, then `POST …/cancel` (admin) → `Cancelled`,
   funds released.
6. **Save a card** `POST /api/payment-methods` (shopper) → `paymentMethodId` + brand/last4/expiry.
7. **Reuse the saved card**: place a third order, `POST …/pay` naming that `paymentMethodId` → authorizes
   without re-entering the card. Then `GET /api/payment-methods`, `DELETE /api/payment-methods/{id}`, and
   confirm it is gone and no longer usable to pay.
8. **`GET /api/my-orders`** (shopper) → each order with its payment state/amounts.
9. **`GET /api/reconciliation?from=…&to=…`** (admin) → report over a range with data (an empty result over a
   just-created range is an expected sandbox lag, not a failure).
10. **AuthZ checks**: a normal user calling fulfil/cancel/reconciliation → `403`; one shopper acting on
    another's order or saved card → `404`.
