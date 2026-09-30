# PLAN — PayPal payments & saved cards for eShopOnWeb

This is the build plan for adding PayPal card payments (authorize → capture → refund/void),
saved cards (vault), and a reconciliation report to eShopOnWeb, exposed on **`src/PublicApi`**.
It is written so a later session that has *only* this file (plus the task and the
`api-specs/` folder) can build and self-verify the feature. Read the whole file before writing
code; the "Decisions log" at the end resolves the ambiguities you will otherwise hit.

Everything is **additive**. Do not remove or rewrite the existing catalog/basket/order flow.

---

## 0. TL;DR of the shape

- **Language/stack:** .NET 8 (`net8.0`, set centrally in `Directory.Packages.props`), ASP.NET
  Core minimal APIs via the `MinimalApi.Endpoint` package, EF Core, Ardalis.Specification,
  central package management. Run on the .NET 10 SDK via roll-forward (see §14).
- **New PayPal HTTP client:** hand-written typed `HttpClient` in `Infrastructure`, built strictly
  to the OpenAPI documents in `api-specs/paypal`. **No third-party PayPal SDK / NuGet client.**
- **New domain:** a `Payment` aggregate + `PaymentRefund` child + `PaymentMethod` aggregate + a
  `PaymentStatus` enum, plus one additive `PaymentStatus` property on the existing `Order`. No
  PAN (card number) or CVV is ever persisted or logged.
- **New endpoints (all under `/api/`, JWT-authenticated, PublicApi conventions):**
  `POST /api/orders`, `POST /api/orders/{orderId}/pay`, `POST /api/orders/{orderId}/fulfil`
  (admin), `POST /api/orders/{orderId}/cancel` (admin), `POST /api/orders/{orderId}/refunds`,
  `GET /api/my-orders`, `GET /api/reconciliation` (admin), `POST /api/payment-methods`,
  `GET /api/payment-methods`, `DELETE /api/payment-methods/{paymentMethodId}`.
- **PayPal money movement:** *pay* = create PayPal order (intent `AUTHORIZE`) + authorize (a hold);
  *fulfil* = capture the authorization (money actually taken; store fee + net); *cancel* = void
  the authorization; *refund* = refund the capture (full/partial, idempotent).

---

## 1. Which PayPal spec documents we use (and which we don't)

All PayPal behavior is built to the OpenAPI JSON under `api-specs/paypal/`. The spec is the
contract; PayPal's public docs are only a secondary reference for semantics. Servers in every
document are `https://api-m.sandbox.paypal.com`.

| Document | Used? | What we use it for |
|---|---|---|
| `checkout_orders_v2/checkout_orders_v2.json` | **Yes** | Create the PayPal order and **authorize** it (the hold). |
| `payments_payment_v2/payments_payment_v2.json` | **Yes** | **Capture** an authorization, **void**, **reauthorize** (renew a stale hold), **refund** a capture, and GET capture/refund for status. |
| `vault_payment_tokens_v3/vault_payment_tokens_v3.json` | **Yes** | Save a card (`POST /v3/vault/payment-tokens` with the raw card), list, and delete. |
| `transaction_search_v1/transaction_search_v1.json` | **Yes** | Reconciliation report (`GET /v1/reporting/transactions`). |
| `billing_subscriptions_v1/billing_subscriptions_v1.json` | **No** | Subscriptions/recurring — not in scope. |

**No capability gap exists.** Every required PayPal interaction is covered by the four documents
above; do not invent endpoints/fields. (See §3 for the token endpoint, which the spec covers via
its `securityScheme`.)

### Exact operations (verified against the specs)

Checkout Orders v2 (`server + path`):
- `POST /v2/checkout/orders` — `operationId: CreateOrder`.
- `POST /v2/checkout/orders/{id}/authorize` — `operationId: AuthorizeOrder`.
- (`GET /v2/checkout/orders/{id}` exists if you need to re-read an order.)

Payments v2:
- `POST /v2/payments/authorizations/{authorization_id}/capture` — `CaptureAuthorizedPayment`.
- `POST /v2/payments/authorizations/{authorization_id}/void` — `VoidPayment`.
- `POST /v2/payments/authorizations/{authorization_id}/reauthorize` — `ReauthorizePayment`.
- `POST /v2/payments/captures/{capture_id}/refund` — `RefundCapturedPayment`.
- `GET /v2/payments/captures/{capture_id}` — `GetCapturedPayment` (re-read fee/net if needed).
- `GET /v2/payments/authorizations/{authorization_id}` — read auth status/expiry.

Vault v3:
- `POST /v3/vault/payment-tokens` — `CreatePaymentToken` (accepts a raw `payment_source.card`
  **directly**, so no setup-token round-trip is required for server-side card vaulting).
- `GET /v3/vault/payment-tokens?customer_id=...` — `ListCustomerPaymentTokens`.
- `GET /v3/vault/payment-tokens/{id}` — `GetPaymentToken`.
- `DELETE /v3/vault/payment-tokens/{id}` — `DeletePaymentToken` (204 on success).
- (`POST /v3/vault/setup-tokens` exists but is **not needed** for our direct-card flow.)

Transaction Search v1:
- `GET /v1/reporting/transactions` — `SearchTransactions`.

---

## 2. Base URL, environment & the token call

### Base URL resolution (implement exactly this precedence)
1. If **`PayPal:BaseUrl`** is set (non-empty), use it **verbatim** as the base address for
   **every** call, including the OAuth token request. Do not append or normalize beyond joining
   the path.
2. Otherwise derive from **`PayPal:Environment`** (case-insensitive):
   - `sandbox` → `https://api-m.sandbox.paypal.com` (this is the spec's `server`).
   - `live` / `production` → `https://api-m.paypal.com`.
   - Unknown value → fail fast at startup with a clear message.

All development/testing targets **sandbox**.

### OAuth2 token (declared by the spec's `securityScheme`)
Every document declares security scheme `Oauth2`, type `oauth2`, flow `clientCredentials`,
`tokenUrl: /v1/oauth2/token`. So authentication is the standard client-credentials grant — this
is **covered by the spec** and is not a gap. Implement:

- `POST {base}/v1/oauth2/token`
- Header `Authorization: Basic base64(ClientId + ":" + ClientSecret)`
- Header `Content-Type: application/x-www-form-urlencoded`
- Body `grant_type=client_credentials`
- Response JSON contains `access_token`, `token_type` (`Bearer`), `expires_in` (seconds). (These
  standard OAuth2 fields are clarified by PayPal docs; the *scheme itself* comes from the spec.)

Cache the access token in memory (`IMemoryCache` is already registered, or a small singleton
token provider) until `expires_in` minus a safety margin (e.g. 60 s), refreshing under a lock to
avoid a thundering herd. Attach `Authorization: Bearer {access_token}` to every subsequent PayPal
call.

### Headers to set on PayPal calls
- `Authorization: Bearer …` (all business calls).
- `Content-Type: application/json` (all bodies except the token form post).
- **`Prefer: return=representation`** on create-order, authorize, capture, refund — so PayPal
  returns the full body (authorization id, capture `seller_receivable_breakdown`, refund id). If
  you omit this you get a minimal body and will have to GET the resource separately.
- **`PayPal-Request-Id: {key}`** for idempotency (see §9). The spec documents this header on
  create-order (stored 6 h), authorize, capture, void, reauthorize, refund (stored 45 days), and
  vault create (stored 3 h).

---

## 3. Configuration & settings binding

Bind a settings type from the **`PayPal:`** section using **exactly** these keys (hard-code no
values):

- `PayPal:ClientId` (env `PAYPAL_CLIENT_ID`)
- `PayPal:ClientSecret` (env `PAYPAL_CLIENT_SECRET`)
- `PayPal:Environment` (env `PAYPAL_ENVIRONMENT`)
- `PayPal:Currency` (env `PAYPAL_CURRENCY`)
- `PayPal:BaseUrl` (optional override; no env var required)

Implementation notes:
- Add `PayPalSettings { ClientId, ClientSecret, Environment, Currency, BaseUrl }` in
  `ApplicationCore` (so both the client and services can see currency) and
  `builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection("PayPal"))` in
  PublicApi `Program.cs`. `Program.cs` already calls `builder.Configuration.AddEnvironmentVariables()`,
  and ASP.NET maps env var `PAYPAL_CLIENT_ID` → config key `PayPal:ClientId` only if you use the
  `__` separator (`PayPal__ClientId`). **The task supplies flat env vars** (`PAYPAL_CLIENT_ID`),
  so add an explicit mapping in `Program.cs`: build the `PayPal:*` keys from the flat env vars,
  e.g. `builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
  ["PayPal:ClientId"]=Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"), … })` **only for keys
  that are present**, added *after* the default config sources so real `PayPal:` config (or
  `PayPal__*`) still wins when provided. This guarantees the four flat env vars land on the
  mandated keys while `PayPal:BaseUrl` remains an optional override. Do **not** write any secret
  value into a committed file — `appsettings*.json` must not contain the client id/secret. (An
  empty/placeholder `"PayPal": { "Currency": "USD" }` block in `appsettings.json` for
  documentation is fine, but real values come from env only.)
- **Never** log `ClientSecret`, card numbers, or CVV (see §12).
- Validate at startup: ClientId, ClientSecret, Environment, Currency must be present (fail fast
  with a clear message if missing) — but keep this lenient enough that the app can still *start*
  for non-PayPal endpoints if you prefer; a hard fail on missing PayPal config at first PayPal
  use is acceptable too. Prefer fail-fast at startup for production-grade clarity.

---

## 4. The existing codebase — what you're building on (verified)

Paths are under `src/`.

### PublicApi endpoint conventions (`src/PublicApi`)
- Endpoints implement `MinimalApi.Endpoint`'s `IEndpoint<IResult, TRequest, TDeps...>` (or
  `IEndpoint<IResult, TDeps...>` with no request). Two methods: `AddRoute(IEndpointRouteBuilder)`
  registering via `app.MapGet/MapPost/...("api/…")` then `.Produces<TResponse>().WithTags("…")`,
  and `HandleAsync(...)` returning `Results.Ok/Created/NotFound/...`. Discovery is automatic:
  `builder.Services.AddEndpoints()` + `app.MapEndpoints()` are already wired in `Program.cs`. **A
  new class implementing `IEndpoint<>` needs no manual registration.**
- Routes are written **without a leading slash** (`"api/orders"`).
- Request DTOs extend `BaseRequest`; response DTOs extend `BaseResponse` and provide both a
  `(Guid correlationId)` ctor and a parameterless ctor. DTO files use the naming convention
  `EndpointName.RequestName.cs` / `EndpointName.ResponseName.cs`. `CorrelationId` is auto-excluded
  from Swagger by `CustomSchemaFilters`.
- **Auth:** JWT bearer configured in `Program.cs`; there is **no** `app.UseAuthentication()` —
  protected routes carry the attribute inline on the lambda:
  `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` for any logged-in
  shopper, and add `Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS` for admin.
  The admin role string is `"Administrators"`.
- **Caller identity:** the token carries the **username** in `ClaimTypes.Name` and one
  `ClaimTypes.Role` per role (issued by `IdentityTokenClaimService`, HmacSha256, 7-day expiry,
  signed with `AuthorizationConstants.JWT_SECRET_KEY`). No endpoint reads identity today; to read
  it, add `HttpContext httpContext` (or `ClaimsPrincipal user`) as a **lambda parameter** in
  `AddRoute` and read `httpContext.User.Identity!.Name`. **Use this username as the `BuyerId`/owner
  key everywhere** (orders and payment methods) — it is the same string the domain already stores
  in `Order.BuyerId`.
- Token issuance endpoint for testing: `POST api/authenticate` with body
  `{ "username": "...", "password": "..." }` → `{ token, result, ... }` (Ardalis.ApiEndpoints
  style). Seeded users (from `AppIdentityDbContextSeed`): `admin@microsoft.com` (in role
  `Administrators`) and `demouser@microsoft.com`; password `Pass@word1`
  (`AuthorizationConstants.DEFAULT_PASSWORD`).
- AutoMapper is registered by assembly scan (`MappingProfile`). You may map manually in handlers
  (both styles already coexist) — manual mapping is simplest for the new DTOs.
- **ExceptionMiddleware** (`src/PublicApi/Middleware/ExceptionMiddleware.cs`) catches all
  exceptions; today it maps `DuplicateException` → 409 and everything else → 500 with
  `{ StatusCode, Message }`. Extend it (additively) to map the new domain exceptions to the right
  status codes (see §12).

### Domain / data (`src/ApplicationCore`, `src/Infrastructure`)
- `Order : BaseEntity, IAggregateRoot` (`Entities/OrderAggregate/Order.cs`). `BaseEntity` has
  `int Id`. Ctor: `Order(string buyerId, Address shipToAddress, List<OrderItem> items)` (guards
  non-empty buyerId). Read-only `OrderItems` via private `_orderItems` field (EF field access).
  `decimal Total()` sums `UnitPrice * Units` (not persisted). `OrderDate` is `DateTimeOffset`.
  **No payment/status field exists today** — you add one (§5).
- `OrderItem : BaseEntity` (not an aggregate root): `CatalogItemOrdered ItemOrdered`,
  `decimal UnitPrice`, `int Units`. `CatalogItemOrdered` (owned value object): `CatalogItemId`,
  `ProductName`, `PictureUri`. `Address` (owned value object): `Street, City, State, Country,
  ZipCode` (required nav on Order).
- `CatalogItem : BaseEntity, IAggregateRoot` with `decimal Price` (mapped `decimal(18,2)`).
- **Repositories:** single generic `IRepository<T>` / `IReadRepository<T>` (both
  `where T : class, IAggregateRoot`) implemented by `EfRepository<T>` bound to **`CatalogContext`**.
  Ardalis.Specification supplies `AddAsync/UpdateAsync/DeleteAsync/GetByIdAsync/
  FirstOrDefaultAsync(spec)/ListAsync(spec)/CountAsync(spec)`. **Any new entity you load/save
  through a repository must implement `IAggregateRoot`.**
- **DbContext:** `CatalogContext` (`Infrastructure/Data/CatalogContext.cs`) holds Orders,
  OrderItems, Baskets, Catalog*. Entity configs are auto-discovered via
  `builder.ApplyConfigurationsFromAssembly(...)`, so a new `IEntityTypeConfiguration<T>` in
  `Infrastructure/Data/Config` is picked up automatically. **Put the new payment entities in
  `CatalogContext`** (add `DbSet`s) so they share the repository + provider switch.
- **Provider switch:** `Infrastructure/Dependencies.cs` reads config bool `UseOnlyInMemoryDatabase`;
  when true, `CatalogContext` and `AppIdentityDbContext` both use EF **InMemory**. The task runs
  with `UseOnlyInMemoryDatabase=true`. **InMemory ignores migrations and builds the model from
  code**, so new entities/props work at runtime without a migration. For production-grade
  completeness also add a SQL Server migration (see §5), but it is not exercised in-memory.
- Identity: `ApplicationUser : IdentityUser` (string GUID id) in `AppIdentityDbContext`;
  `Order.BuyerId` is a plain `string` with no FK to identity. Admin role constant:
  `BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS` (= `"Administrators"`).
- Patterns to follow: `Ardalis.GuardClauses` for guards (custom guards are extension methods on
  `IGuardClause` in namespace `Ardalis.GuardClauses`, see `Extensions/GuardExtensions.cs`); domain
  exceptions in `ApplicationCore/Exceptions` deriving from `Exception` with the full ctor set;
  services as interface in `ApplicationCore/Interfaces` + impl in `ApplicationCore/Services`
  (or `Infrastructure/Services` when they call external HTTP), registered `AddScoped` in
  `PublicApi/Program.cs` (and `Web/Configuration/ConfigureCoreServices.cs` if Web needs them —
  Web does **not** need these, so PublicApi registration is enough).
- Central packages (`Directory.Packages.props`) already include `System.Net.Http.Json` (8.0.0)
  and `System.Text.Json` (8.0.3). For `net8.0` these are in the shared framework; you may add
  `<PackageReference Include="System.Net.Http.Json" />` to `Infrastructure.csproj` if the
  extension methods aren't resolved, but no new NuGet version entry is needed. **Do not add a
  PayPal SDK package.**

---

## 5. New domain model

Add to `ApplicationCore`. Keep PAN/CVV out of every one of these types.

### 5.1 `PaymentStatus` enum (`Entities/OrderAggregate/PaymentStatus.cs`)
```
AwaitingPayment, Authorized, AuthorizationFailed, Captured, Cancelled,
PartiallyRefunded, Refunded
```
- `Captured` == "paid / fulfilled" (money taken).
- `PartiallyRefunded` / `Refunded` after refunds; `Refunded` when cumulative refunds == captured.

### 5.2 `Order` — one additive property (single source of truth for state)
Add `public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.AwaitingPayment;`
plus small transition methods (`MarkAuthorized()`, `MarkAuthorizationFailed()`, `MarkCaptured()`,
`MarkCancelled()`, `MarkRefunded(bool full)`) that guard illegal transitions
(`Guard.Against` / a new `InvalidPaymentTransitionException`). The default keeps the existing Web
checkout path unchanged (orders created there are simply `AwaitingPayment`). EF: map the enum with
`.HasConversion<string>()` in `OrderConfiguration` (additive edit) so it reads well in storage.

### 5.3 `Payment : BaseEntity, IAggregateRoot` (`Entities/PaymentAggregate/Payment.cs`)
Holds the PayPal-owned state so any later request can act on it:
- `int OrderId` (logical link to the eShop order), `string BuyerId` (owner, = username),
  `string CurrencyCode`, `decimal AuthorizedAmount`.
- Hold: `string? PayPalOrderId`, `string? AuthorizationId`, `string? AuthorizationStatus`,
  `DateTimeOffset? AuthorizationExpiresAt`.
- Capture: `string? CaptureId`, `string? CaptureStatus`, `decimal? CapturedAmount`,
  `decimal? PayPalFee`, `decimal? NetAmount`.
- Refunds: private `List<PaymentRefund> _refunds` → `IReadOnlyCollection<PaymentRefund> Refunds`.
- Idempotency bookkeeping: nothing extra needed beyond the refund keys (below) plus checking the
  hold/capture fields.
- Behavior methods (mutate + return): `SetAuthorization(orderId, authId, status, expiresAt)`,
  `RenewAuthorization(authId, status, expiresAt)`, `SetCapture(captureId, status, captured, fee,
  net)`, `AddRefund(PaymentRefund)`, and helpers `decimal TotalRefunded()` (sum of COMPLETED/PENDING
  refund amounts) and `decimal RefundableRemaining()` (`CapturedAmount - TotalRefunded()`).
- Register a spec `PaymentByOrderIdSpec(int orderId)` and `PaymentsByBuyerSpec(string buyerId)`
  (Include `.Include(p => p.Refunds)`).

Rationale for a separate aggregate (not a parallel *order* model): the task forbids a parallel
**order/order-item** model and mandates reusing `Order`/`OrderItem` — which we do. A companion
`Payment` aggregate keyed by `OrderId` is standard DDD and keeps `Order` minimal while carrying
the rich PayPal state. `Order.PaymentStatus` remains the authoritative state; `Payment` carries
the ids/amounts. Both are updated in the same service call (same `CatalogContext`, saved together).

### 5.4 `PaymentRefund : BaseEntity` (child of `Payment`)
- `string PayPalRefundId`, `decimal Amount`, `string Status`, `string IdempotencyKey`,
  `DateTimeOffset CreatedAt`. Not an aggregate root (loaded via `Payment`).

### 5.5 `PaymentMethod : BaseEntity, IAggregateRoot` (`Entities/PaymentAggregate/PaymentMethod.cs`)
Saved card — **safe descriptor only, never PAN/CVV**:
- `string BuyerId` (owner = username), `string PayPalVaultId` (the payment-token id),
  `string PayPalCustomerId` (PayPal-generated vault customer id, reused across the shopper's
  cards), `string Brand`, `string LastFourDigits`, `string ExpiryMonthYear` (e.g. `"2027-05"`),
  `string? CardholderName`, `DateTimeOffset CreatedAt`.
- Spec: `PaymentMethodsByBuyerSpec(string buyerId)`, `PaymentMethodByIdAndBuyerSpec(int id,
  string buyerId)` (so cross-user access returns nothing).

### 5.6 EF configuration & migration
- Add `IEntityTypeConfiguration<Payment>`, `<PaymentRefund>`, `<PaymentMethod>` under
  `Infrastructure/Data/Config` (auto-discovered). Map decimals as `decimal(18,2)`, strings with
  sensible `HasMaxLength`, `Payment` → `Refunds` as a collection with field access
  (`SetPropertyAccessMode(PropertyAccessMode.Field)` like `OrderConfiguration` does for
  `OrderItems`). Store enum via `HasConversion<string>()`.
- Add `DbSet<Payment>`, `DbSet<PaymentRefund>`, `DbSet<PaymentMethod>` to `CatalogContext`.
- **Migration:** InMemory needs none. For production-grade completeness, generate one SQL Server
  migration (`dotnet ef migrations add AddPayments -p src/Infrastructure -s src/PublicApi`) and
  commit it. This is optional for the in-memory self-verify and must not break the in-memory run.
  If migration generation is problematic in this environment, it is acceptable to skip it and note
  so — the runtime target is in-memory.

---

## 6. Services layer (ApplicationCore/Infrastructure)

Two layers: a thin **PayPal HTTP client** (Infrastructure, external I/O) and a **PaymentService**
(orchestration + domain/idempotency, ApplicationCore or Infrastructure — put it in ApplicationCore
`Services` with the client injected via its interface).

### 6.1 `IPayPalClient` (interface in `ApplicationCore/Interfaces`, impl in `Infrastructure/PayPal`)
Methods map 1:1 to the spec operations. Signatures (illustrative — use your own request/response
DTO records that mirror the spec subset in §8):
- `Task<PayPalOrderResult> CreateOrderAsync(CreateOrderInput input, string requestId, CancellationToken)`
- `Task<PayPalOrderResult> AuthorizeOrderAsync(string paypalOrderId, string requestId, CancellationToken)`
- `Task<PayPalCaptureResult> CaptureAuthorizationAsync(string authorizationId, decimal amount, string currency, string requestId, CancellationToken)`
- `Task VoidAuthorizationAsync(string authorizationId, string requestId, CancellationToken)`
- `Task<PayPalAuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string requestId, CancellationToken)`
- `Task<PayPalRefundResult> RefundCaptureAsync(string captureId, decimal? amount, string currency, string requestId, CancellationToken)`
- `Task<PayPalAuthorizationResult> GetAuthorizationAsync(string authorizationId, CancellationToken)`
- `Task<PayPalCaptureResult> GetCaptureAsync(string captureId, CancellationToken)`
- `Task<PayPalVaultTokenResult> CreatePaymentTokenAsync(VaultCardInput card, string? paypalCustomerId, string requestId, CancellationToken)`
- `Task<IReadOnlyList<PayPalVaultTokenResult>> ListPaymentTokensAsync(string paypalCustomerId, CancellationToken)`
- `Task DeletePaymentTokenAsync(string vaultTokenId, CancellationToken)`
- `Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken)` (handles chunking + paging internally — §11)

Implementation:
- Register as a **typed `HttpClient`**: `builder.Services.AddHttpClient<IPayPalClient, PayPalClient>()`
  and set `BaseAddress` from the resolved base URL (§2). Consider a Polly-free simple retry on
  `429`/`5xx`/token-expiry (optional; keep it minimal and production-sane).
- Acquire/cache the bearer token (§2) — a separate `IPayPalTokenProvider` (singleton) is cleanest.
- Serialize/deserialize with `System.Text.Json` (snake_case property names via
  `[JsonPropertyName("…")]` or a `JsonNamingPolicy.SnakeCaseLower` — .NET 8 has
  `JsonNamingPolicy.SnakeCaseLower`).
- On non-2xx, parse PayPal's error model (`name`, `message`, `debug_id`, `details[]`) and throw a
  `PayPalApiException(statusCode, name, message, debugId, details)`. **Do not** put card data in
  the exception or logs.

### 6.2 `IPaymentService` / `PaymentService` (ApplicationCore)
Owns the order-payment lifecycle + idempotency + ownership checks; talks to `IPayPalClient`,
`IRepository<Order>`, `IRepository<Payment>`, `IRepository<PaymentMethod>`, `IRepository<CatalogItem>`,
and reads `PayPalSettings.Currency`. Methods correspond to the endpoints (§7). This is where the
"authorize once", "capture once", "renew stale auth", "cap refunds at captured", and
"idempotent refund key" rules live (§8–§10). Register `AddScoped<IPaymentService, PaymentService>()`
and the vault/order helpers in `PublicApi/Program.cs`.

### 6.3 Order creation from catalog (new path)
`OrderService.CreateOrderAsync` requires a **basket** and returns `void`. PublicApi has no basket
(per-host in-memory) and we place orders directly from catalog ids+quantities. Add a new method
(new `IOrderService` overload or a dedicated method on `PaymentService`/a `IApiOrderService`):
`Task<Order> PlaceOrderAsync(string buyerId, IEnumerable<(int catalogItemId, int quantity)> lines,
Address shipTo)` that:
1. Loads catalog items by id (`CatalogItemsSpecification(ids)`), guards each id exists.
2. Builds `OrderItem`s with **`UnitPrice = catalogItem.Price`** (the task says amounts come from
   catalog prices), snapshotting `Name`/`PictureUri` into `CatalogItemOrdered` (compose the pic
   uri with `IUriComposer`, matching `OrderService`).
3. Constructs `new Order(buyerId, shipTo, items)`, `AddAsync`, and creates the companion
   `Payment { OrderId, BuyerId, CurrencyCode, AuthorizedAmount = order.Total(), Status via
   Order.PaymentStatus = AwaitingPayment }`.
4. Returns the `Order` (so the endpoint can return `orderId`).
Reuse `OrderWithItemsByIdSpec` for later lookups. Do **not** modify the existing basket-based
`CreateOrderAsync` used by Web.

---

## 7. HTTP endpoints (PublicApi)

All routes `api/…`, no leading slash, `.WithTags("PaymentEndpoints")` (or split tags per group),
`.Produces<TResponse>()`. All require a valid JWT. **Shopper-scoped** endpoints must load the
target by **(id AND caller username)** and return **404** if it isn't the caller's (never leak
another shopper's existence). **Admin** endpoints add
`Roles = ...Roles.ADMINISTRATORS`. Auth attribute pattern (inline on the lambda):
`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` (+ `Roles=…` for admin).

| # | Method & route | Auth | Purpose |
|---|---|---|---|
| 1 | `POST api/orders` | shopper | place order from catalog lines |
| 2 | `POST api/orders/{orderId}/pay` | shopper (own order) | **authorize** the total (hold) |
| 3 | `POST api/orders/{orderId}/fulfil` | **admin** | **capture** (take the money) |
| 4 | `POST api/orders/{orderId}/cancel` | **admin** | **void** the hold |
| 5 | `POST api/orders/{orderId}/refunds` | shopper (own order) | **refund** the capture (full/partial, idempotent) |
| 6 | `GET api/my-orders` | shopper | caller's orders + payment state |
| 7 | `GET api/reconciliation?from=&to=` | **admin** | PayPal vs eShop transaction report |
| 8 | `POST api/payment-methods` | shopper | save a card (vault) |
| 9 | `GET api/payment-methods` | shopper | caller's saved cards |
| 10 | `DELETE api/payment-methods/{paymentMethodId}` | shopper (own) | remove a saved card |

> **Authorization of the refunds endpoint (#5):** the explicit authorization mandate enumerates
> exactly three operator actions — *fulfil, cancel, reconciliation* — and states "every other
> endpoint is shopper-scoped and acts only on the caller's own data." Refunds is therefore
> **shopper-scoped (own order)**. (The Flow-1 narrative loosely says "an operator then fulfils,
> cancels or refunds"; the precise enumerated rule controls.) Keep it a one-line change to flip to
> admin if the operator interpretation is later preferred — see Decisions log D1.

### 7.1 `POST api/orders`
Request: `{ items: [{ catalogItemId:int, quantity:int }], shipToAddress?: { street, city, state,
country, zipCode } }`. If `shipToAddress` omitted, use a fixed placeholder default (address is not
the focus; Order requires a non-null `ShipToAddress`). Validate quantities ≥ 1 and ids exist.
Handler: resolve username → `PlaceOrderAsync` (§6.3). **Response (201):** top-level
**`orderId`** (the app `Order.Id`), plus `total`, `currency`, `paymentStatus:"AwaitingPayment"`,
and the line items. Use `Results.Created($"/api/orders/{id}", response)` or `Results.Ok`.

### 7.2 `POST api/orders/{orderId}/pay` (authorize)
Request (exactly one of):
- one-off card: `{ card: { number, expiry:"YYYY-MM", securityCode, name?, billingAddress? },
  saveCard?: bool }`, **or**
- saved card: `{ paymentMethodId: int }`.
Handler:
1. Load the order **for this buyer**; 404 if not theirs. Guard `order.PaymentStatus ==
   AwaitingPayment` (idempotency short-circuit below).
2. **Idempotency:** if the `Payment` already has an `AuthorizationId` with a live status
   (`CREATED`), return the existing authorization result (do not call PayPal again). This makes a
   double-click safe.
3. Determine the amount = `order.Total()`, currency = `PayPalSettings.Currency`. Format value as a
   string with the currency's decimal places (2 for USD/EUR; see §13 on formatting). The
   `purchase_units[0].amount.value` **must equal the order total to the cent**.
4. Build the PayPal **create order** body: `intent:"AUTHORIZE"`, `purchase_units:[{ invoice_id:
   "eshop-{orderId}", custom_id: "{orderId}", amount:{ currency_code, value } }]`, and
   `payment_source.card` = either the raw card fields, or `{ vault_id: paymentMethod.PayPalVaultId }`
   for a saved card. If `saveCard` is true on a one-off card, add
   `payment_source.card.attributes.vault = { store_in_vault: "ON_SUCCESS" }` and
   `attributes.customer = { id: existingPayPalCustomerId }` (if the shopper already has one) so the
   token is created during purchase; then persist a `PaymentMethod` from the returned
   `payment_source.card.attributes.vault` + `card.{last_digits,brand,expiry}`.
   Send `PayPal-Request-Id: order-{orderId}-authorize` and `Prefer: return=representation`.
5. Call `CreateOrder`, then `AuthorizeOrder(paypalOrderId)` (`PayPal-Request-Id` same family).
   Extract the authorization from
   `purchase_units[0].payments.authorizations[0]` → `{ id, status, expiration_time }`.
6. **3DS / challenge guard:** if the response indicates a required browser approval — e.g. order
   `status == "PAYER_ACTION_REQUIRED"` or a `links[].rel == "payer-action"`, or the auth is not
   `CREATED`/`CAPTURED` due to pending authentication — **do not** build an approval round-trip.
   Surface an actionable error (HTTP 4xx with a clear message) and stop. (With the sandbox test
   card `4111 1111 1111 1111` and no forced SCA this will not happen; do **not** set
   `verification.method = SCA_ALWAYS`.)
7. On success: `Payment.SetAuthorization(...)`, `Order.MarkAuthorized()`, save.
   **Response:** `orderId`, `paymentStatus:"Authorized"`, `authorizationId`, `authorizedAmount`,
   `authorizationExpiresAt`. On PayPal decline: `Order.MarkAuthorizationFailed()`, return a clear
   4xx with PayPal's `name`/`message` (no card data).

### 7.3 `POST api/orders/{orderId}/fulfil` (capture) — **admin**
1. Load order + payment (any buyer; admin). Guard `PaymentStatus == Authorized`.
2. **Idempotency:** if already `Captured` (payment has `CaptureId`), return the existing capture
   result without calling PayPal.
3. **Stale-auth renewal:** attempt `CaptureAuthorization(authorizationId, amount, currency,
   PayPal-Request-Id: order-{orderId}-capture, Prefer: return=representation)`.
   - If capture fails because the authorization has **expired**/is no longer capturable (detect via
     PayPal error, e.g. HTTP 422 with issue like `AUTHORIZATION_EXPIRED`, or a pre-check of
     `AuthorizationExpiresAt`), call `ReauthorizeAsync(authorizationId, amount, currency)` to renew,
     update `Payment.RenewAuthorization(...)` with the returned (possibly new) authorization id +
     status + expiry, then retry the capture against the renewed authorization id.
   - If **reauthorize itself fails** (the hold can no longer be renewed — e.g. beyond the honor
     period), **do not** silently fail the fulfilment: throw a domain exception whose message is
     operator-actionable, e.g. *"Authorization {id} for order {orderId} has expired and can no
     longer be renewed (PayPal: {name}/{message}). Ask the shopper to pay again to create a fresh
     authorization."* → surfaced as HTTP 409/422 with that message.
   Note: in a single in-memory run the sandbox auth stays within its honor period, so the renewal
   branch will not trigger live; still implement it. You may exercise it with the spec's
   `PayPal-Mock-Response` header (negative testing) or a unit test with a mocked `IPayPalClient`.
4. On capture success, read `seller_receivable_breakdown`:
   `gross_amount.value` (captured), `paypal_fee.value` (fee), `net_amount.value` (net proceeds).
   `Payment.SetCapture(captureId, status, captured, fee, net)`, `Order.MarkCaptured()`, save.
   **Response:** `orderId`, `paymentStatus:"Captured"`, `captureId`, `capturedAmount`, `paypalFee`,
   `netAmount`, `currency`.

### 7.4 `POST api/orders/{orderId}/cancel` (void) — **admin**
1. Load order + payment. Guard `PaymentStatus == Authorized` (cannot cancel after capture — that's
   a refund).
2. **Idempotency:** if already `Cancelled`, return success without calling PayPal.
3. `VoidAuthorization(authorizationId, PayPal-Request-Id: order-{orderId}-cancel)`. The held funds
   are released; no money moved. `Order.MarkCancelled()`, save. **Response:** `orderId`,
   `paymentStatus:"Cancelled"`.

### 7.5 `POST api/orders/{orderId}/refunds` (refund) — shopper (own order)
Request: `{ amount?: decimal, idempotencyKey: string }`. Omitted/`null` amount ⇒ full refund of
the **remaining** refundable balance.
1. Load order + payment **for this buyer**; 404 if not theirs. Guard `PaymentStatus` is `Captured`
   or `PartiallyRefunded` (must be captured to refund).
2. **Idempotency:** if a `PaymentRefund` already exists with this `IdempotencyKey`, return that
   refund's `refundId` and status **without** refunding again. (Two *different* keys ⇒ two distinct
   partial refunds are allowed.)
3. **Cap enforcement:** compute `RefundableRemaining() = CapturedAmount - TotalRefunded()`. If a
   requested `amount` exceeds `RefundableRemaining()` (or the sum would exceed captured), reject
   with HTTP 422 and a clear message. A partly-refunded order must never become refundable beyond
   what was captured.
4. Call `RefundCapture(captureId, amount?, currency, PayPal-Request-Id: idempotencyKey,
   Prefer: return=representation)`. From the response take `id` (refund id), `status`, `amount`.
   `Payment.AddRefund(new PaymentRefund{ PayPalRefundId=id, Amount, Status, IdempotencyKey })`.
   Recompute state: if `TotalRefunded() >= CapturedAmount` ⇒ `Order.MarkRefunded(full:true)`
   (`Refunded`); else `Order.MarkRefunded(full:false)` (`PartiallyRefunded`). Save.
5. **Response (201):** top-level **`refundId`** (the PayPal refund id string), plus `status`,
   `amount`, `paymentStatus`, `totalRefunded`, `refundableRemaining`.
   > Note: use the PayPal refund id as `refundId` because it is the canonical, externally
   > verifiable identifier of the refund and no later route consumes it. (See Decisions log D2.)

### 7.6 `GET api/my-orders`
Load the caller's orders (`CustomerOrdersWithItemsSpecification(username)`) and their `Payment`s
(`PaymentsByBuyerSpec(username)`), join by `OrderId`. **Response:** list of `{ orderId, orderDate,
total, currency, paymentStatus, authorizationId?, captureId?, capturedAmount?, paypalFee?,
netAmount?, refunds:[{ refundId, amount, status }] }`. Only the caller's data.

### 7.7 `GET api/reconciliation?from=&to=` — **admin**
`from`/`to` are ISO-8601 date-times (parse to `DateTimeOffset`; validate `from <= to`). See §11 for
the algorithm. **Response:** `{ from, to, matched:[…], paypalOnly:[…], eShopOnly:[…], counts,
lastRefreshedAt? }` where each row lines up a PayPal transaction with the eShop order it belongs
to (or flags the mismatch). Note in the response (or docs) that recent ranges can be empty due to
PayPal reporting lag — that is expected, not an error.

### 7.8 `POST api/payment-methods` (save a card)
Request: `{ card: { number, expiry:"YYYY-MM", securityCode, name?, billingAddress? } }`.
1. Determine the shopper's PayPal customer id: if they already have a `PaymentMethod`, reuse its
   `PayPalCustomerId`; else let PayPal generate one on this call (omit `customer.id`; optionally
   send `customer.merchant_customer_id = username`).
2. `CreatePaymentTokenAsync` → returns `{ id (vault token), customer.id, payment_source.card:
   { last_digits, brand, expiry } }`. Send `PayPal-Request-Id` for idempotency.
3. Persist `PaymentMethod { BuyerId=username, PayPalVaultId=id, PayPalCustomerId=customer.id,
   Brand, LastFourDigits=last_digits, ExpiryMonthYear=expiry, CardholderName=card.name }`. **Never
   store the number/CVV.**
4. **Response (201):** top-level **`paymentMethodId`** (app `PaymentMethod.Id`), plus a safe
   descriptor `{ brand, lastFourDigits, expiryMonthYear, cardholderName? }` — never full card data.

### 7.9 `GET api/payment-methods`
Return the caller's saved cards (`PaymentMethodsByBuyerSpec(username)`) as safe descriptors
`{ paymentMethodId, brand, lastFourDigits, expiryMonthYear, cardholderName?, createdAt }`.

### 7.10 `DELETE api/payment-methods/{paymentMethodId}`
1. Load by `(id AND username)`; 404 if not the caller's.
2. `DeletePaymentTokenAsync(PayPalVaultId)` (PayPal returns 204).
3. Delete the local `PaymentMethod`. Afterwards it must not appear in the caller's list and must
   not be usable to pay (a subsequent `pay` with that id → 404). Return 204 / 200.

---

## 8. PayPal request/response field maps (built from the specs)

Only the fields we actually send/read are listed. Property names are the spec's JSON (snake_case).

### 8.1 Create order — `POST /v2/checkout/orders` (`CreateOrder`)
Request (`application/json`):
```
intent: "AUTHORIZE"                                  // required
purchase_units: [{
  reference_id?: "default",
  invoice_id: "eshop-{orderId}",                     // reconciliation key (unique per order)
  custom_id: "{orderId}",
  amount: { currency_code: "{PayPal:Currency}", value: "{total:0.00}" }   // required
}]
payment_source: {
  card: {                                            // one-off card
    name?: "...", number: "4111111111111111",
    expiry: "YYYY-MM", security_code: "123",
    billing_address?: { address_line_1, admin_area_2, admin_area_1, postal_code, country_code },
    attributes?: {                                   // only when saving the card during purchase
      customer?: { id: "{existingPayPalCustomerId}" },
      vault: { store_in_vault: "ON_SUCCESS" }
    }
  }
  // OR, for a saved card:
  // card: { vault_id: "{PaymentMethod.PayPalVaultId}" }
}
```
Headers: `PayPal-Request-Id`, `Prefer: return=representation`.
Response (`order_authorize_response` shape): `id` (PayPal order id), `status`, `payment_source.card`
(`last_digits`, `brand`, `expiry`, and `attributes.vault.{id,status,customer.id}` when vaulting),
`purchase_units[].payments.authorizations[]` may be absent until you call authorize.

### 8.2 Authorize — `POST /v2/checkout/orders/{id}/authorize` (`AuthorizeOrder`)
Body optional (card already attached at create). Headers: `PayPal-Request-Id`,
`Prefer: return=representation`. Response: `purchase_units[0].payments.authorizations[0]` =
`{ id, status ∈ [CREATED,CAPTURED,DENIED,PARTIALLY_CAPTURED,VOIDED,PENDING], amount:{currency_code,
value}, expiration_time, links[] }`. Store `id`, `status`, `expiration_time`.

### 8.3 Capture — `POST /v2/payments/authorizations/{authorization_id}/capture`
Request: `{ amount: { currency_code, value }, final_capture: true }`. Headers: `PayPal-Request-Id`,
`Prefer: return=representation`. Response (`capture` shape): `id`, `status ∈ [COMPLETED,DECLINED,
PARTIALLY_REFUNDED,PENDING,REFUNDED,FAILED]`, `amount`, `final_capture`, and
`seller_receivable_breakdown: { gross_amount:{value}, paypal_fee:{value}, net_amount:{value} }`.
Store capture id, status, gross (captured), fee, net.

### 8.4 Void — `POST /v2/payments/authorizations/{authorization_id}/void`
No body required. Header `PayPal-Request-Id`. Success is `204 No Content` (or `200` with an
authorization body showing `status: VOIDED`). Treat both as success.

### 8.5 Reauthorize — `POST /v2/payments/authorizations/{authorization_id}/reauthorize`
Request: `{ amount: { currency_code, value } }`. Response: an authorization object
`{ id, status, amount, expiration_time }`. **Use the returned `id`** for the subsequent capture
(it may differ from the original). If this call errors, the hold can no longer be renewed → §7.3
actionable error.

### 8.6 Refund — `POST /v2/payments/captures/{capture_id}/refund`
Request: `{ amount?: { currency_code, value } }` (omit `amount` for a full refund). Headers:
`PayPal-Request-Id` (= caller idempotency key), `Prefer: return=representation`. Response (`refund`
shape): `id`, `status ∈ [CANCELLED,FAILED,PENDING,COMPLETED]`, `amount`, and
`seller_payable_breakdown.total_refunded_amount` (useful cross-check against local `TotalRefunded`).

### 8.7 Vault create — `POST /v3/vault/payment-tokens` (`CreatePaymentToken`)
Request: `{ customer?: { id?, merchant_customer_id? }, payment_source: { card: { name?, number,
expiry, security_code, billing_address? } } }` (required: `payment_source`). Header
`PayPal-Request-Id`. Response: `{ id (vault token), customer: { id }, payment_source: { card: {
last_digits, brand, expiry } } }`.

### 8.8 Vault list/delete
List: `GET /v3/vault/payment-tokens?customer_id={id}` → `{ total_items, total_pages,
payment_tokens: [{ id, payment_source }] }`. Delete: `DELETE /v3/vault/payment-tokens/{id}` → 204.

### 8.9 Transaction search — `GET /v1/reporting/transactions` (`SearchTransactions`)
Query params: `start_date` (required, RFC3339 with seconds), `end_date` (required, RFC3339 with
seconds, **max range 31 days** per the spec), `fields=transaction_info`, `page_size` (use 500),
`page` (1-based; `page=1,page_size=20` returns the first 20). Response: `{ transaction_details:
[{ transaction_info: { transaction_id, paypal_reference_id, transaction_event_code,
transaction_initiation_date, transaction_amount:{value,currency_code}, fee_amount:{value},
transaction_status, invoice_id, custom_field } }], page, total_items, total_pages,
last_refreshed_datetime }`.

---

## 9. Idempotency ("a double-click never charges twice")

Two complementary layers on every money-moving action:
1. **Application-state short-circuit** (authoritative): before calling PayPal, inspect
   `Order.PaymentStatus` / `Payment` fields and return the existing result if the action already
   happened (authorize when already `Authorized`; capture when already `Captured`; cancel when
   already `Cancelled`; refund when the `IdempotencyKey` already exists).
2. **`PayPal-Request-Id` header** on every mutating PayPal call as a network-level safety net:
   - authorize: `order-{orderId}-authorize`
   - capture: `order-{orderId}-capture`
   - cancel/void: `order-{orderId}-cancel`
   - reauthorize: `order-{orderId}-reauthorize` (or include an attempt counter if you retry)
   - refund: the **caller-supplied `idempotencyKey`** (required in the request body). Distinct keys
     ⇒ distinct partial refunds; a repeated key ⇒ the same refund (both PayPal-side via the header
     and app-side via the stored `PaymentRefund.IdempotencyKey`).
   - vault create: a per-request id (e.g. `Guid`), or derive from card fingerprint if you want
     save idempotency (optional).

Concurrency: guard against two simultaneous requests with an application-level check + a DB
save-conflict fallback. With the in-memory provider a simple re-read-and-verify inside the service
is sufficient for the self-verify; note it for production.

---

## 10. Payment lifecycle (state machine)

```
AwaitingPayment --pay/authorize-->  Authorized  --fulfil/capture-->  Captured
      |                                  |                                |
      |                                  |                          refund (partial)
      |                              cancel/void                         v
      |                                  |                        PartiallyRefunded
      v                                  v                                |
 (no money)                         Cancelled                       refund (rest)
                                    (funds released)                     v
 authorize decline --> AuthorizationFailed                          Refunded
```
- `cancel` only from `Authorized`. `refund` only from `Captured`/`PartiallyRefunded`.
- `fulfil` only from `Authorized` (with the stale-auth renewal detour in §7.3).
- Illegal transitions throw a domain exception → mapped to 409 (§12).

---

## 11. Reconciliation algorithm (must cover the whole range, not one page)

`GET /v1/reporting/transactions` has two hard limits from the spec: **max 31-day range** and
**pagination**. Implement `SearchTransactionsAsync(from, to)` to:
1. **Chunk** `[from, to]` into consecutive windows of ≤ 31 days (e.g. 31-day steps; last window
   ends at `to`).
2. For each window, request `page=1, page_size=500, fields=transaction_info`, read `total_pages`
   from the response, then loop `page = 2..total_pages`, accumulating `transaction_details`.
3. Concatenate all windows' transactions. De-duplicate by `transaction_id` if windows touch.
Then in the endpoint:
4. Load eShop `Payment`s that have a `CaptureId` (in-memory: all; optionally filter by capture
   time within `[from,to]`). Build the match key from `invoice_id` (`"eshop-{orderId}"`) / the
   capture `transaction_id` (PayPal's capture id == the transaction id for the capture event).
5. Produce three buckets:
   - **matched:** a PayPal transaction whose `invoice_id`/id maps to an eShop order (report both
     sides' amounts + fee for eyeballing drift).
   - **paypalOnly:** PayPal knows a transaction eShop has no order/capture for.
   - **eShopOnly:** eShop captured an order that does not appear in PayPal's transactions for the
     range.
6. Return counts + rows. **Recent ranges may be empty** because PayPal reporting lags live
   activity — return an empty-but-correct report; do not treat it as a gap or error. The report is
   correct over any range that actually has settled data.

---

## 12. Error handling, logging & PCI

- **PayPal errors:** parse `{ name, message, debug_id, details[] }`; throw
  `PayPalApiException`. Map in `ExceptionMiddleware` (additive edits): payment/validation errors →
  422 (or 402), illegal state transitions → 409, not-found/ownership failures → 404, PayPal
  auth/config problems → 502/500. Include PayPal's `message`/`debug_id` in the response body
  (never card data) so an operator can act — e.g. the "cannot renew authorization" message.
- **New domain exceptions** (in `ApplicationCore/Exceptions`, full ctor set):
  `OrderNotFoundException`, `PaymentMethodNotFoundException`, `InvalidPaymentTransitionException`,
  `RefundExceedsCapturedException`, `AuthorizationNotRenewableException`,
  `PaymentChallengeRequiredException` (the 3DS/PAYER_ACTION stop condition), `PayPalApiException`.
- **PCI / secrets:** the app's own database **never** stores the PAN or CVV — only brand, last 4,
  and expiry month/year. **Never log** the card number, CVV, `ClientSecret`, or full PayPal request
  bodies containing `payment_source.card`. When logging PayPal calls, log method + path + status +
  `debug_id` only; redact bodies. Configure the `System.Text.Json` serializer used for logging (if
  any) to exclude card fields, or simply never serialize request bodies to logs.
- Keep `RequireHttpsMetadata=false` as-is (dev), both hosts already `UseHttpsRedirection()`.

---

## 13. Money formatting & currency

- Currency code = `PayPal:Currency` (e.g. `USD`).
- Amount value = `Order.Total()` rendered as a string with the currency's decimal places, invariant
  culture. Default to 2 decimals (`total.ToString("0.00", CultureInfo.InvariantCulture)`), which is
  correct for USD/EUR/GBP (the sandbox test card is exercised in USD). If a zero-decimal currency
  (e.g. JPY) is ever configured, format with 0 decimals — a small `CurrencyFormatter` keyed by
  currency is a clean touch but 2-decimal default is acceptable for the self-verify.
- The authorized `amount.value` **must equal the order total to the cent** — assert this in a test
  or a guard (compare `decimal` captured/authorized vs `Order.Total()`).

---

## 14. Environment & how to run (this machine)

- **SDK roll-forward:** `global.json` pins SDK `8.0.x` but only .NET 10 SDK is installed and the
  ASP.NET Core 8.0 runtime is missing. Run the app with env `DOTNET_ROLL_FORWARD=Major` (and, if a
  build/restore needs it, allow SDK roll-forward). Do **not** commit a change that breaks the pin
  for others unless necessary; prefer the env var at run time.
- **Database:** run with `UseOnlyInMemoryDatabase=true` (already the value in
  `tests/PublicApiIntegrationTests/appsettings.test.json`, which `Program.cs` loads via
  `AddConfigurationFile("appsettings.test.json")`). InMemory loses data on restart and ignores
  migrations, so **place, pay, fulfil and refund within the same process run**.
- **Per-host isolation:** PublicApi has its own in-memory store — drive the whole flow through
  PublicApi (that's why `POST /api/orders` exists). Web's orders are invisible here.
- **Ports:** bind only to the assigned block (`APP_PORT_BLOCK_BASE … +APP_PORT_BLOCK_SIZE-1`);
  `launchSettings.json` already points PublicApi at `https://localhost:37683;http://localhost:37684`
  (adjust to the assigned block if different). Stop any previous instance before starting a new one.
- **Dev cert:** `dotnet dev-certs https --check` (trust if needed) — both hosts use HTTPS redirect.
- **Env vars for PayPal:** `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`
  (`sandbox`), `PAYPAL_CURRENCY` (e.g. `USD`) must be exported in the shell that runs PublicApi.
  Map them to the `PayPal:*` keys as in §3. **Never** print or commit their values.
- No new infra (no Docker/broker/PostgreSQL). No new NuGet beyond what's centrally listed.

---

## 15. Suggested build order

1. `PayPalSettings` (+ config mapping in `Program.cs`) and the base-URL/token provider.
2. `IPayPalClient` + `PayPalClient` typed HttpClient + spec DTO records + `PayPalApiException`.
   Unit-smoke it against sandbox (token acquisition) early.
3. Domain: `PaymentStatus`, `Order.PaymentStatus` + transitions, `Payment`, `PaymentRefund`,
   `PaymentMethod`, specs, EF configs, `CatalogContext` DbSets, (optional) migration.
4. `PlaceOrderAsync` (catalog-priced order creation) + `IPaymentService`/`PaymentService`.
5. Endpoints 1, 6, 8, 9, 10 (place order, my-orders, payment-methods CRUD).
6. Endpoints 2, 3, 4, 5 (pay/authorize, fulfil/capture, cancel/void, refund) with idempotency and
   stale-auth renewal.
7. Endpoint 7 (reconciliation) with chunking + paging.
8. `ExceptionMiddleware` mapping for the new exceptions.
9. Self-verify end-to-end (§16). Then write the user's verification guide.

---

## 16. Self-verification (build session must actually do this)

Use `curl` (or Postman) against PublicApi with the seeded users. Get tokens first:
```
# Admin token
curl -sk -X POST https://localhost:37683/api/authenticate \
  -H "Content-Type: application/json" \
  -d '{"username":"admin@microsoft.com","password":"Pass@word1"}'
# Shopper token (demouser)
curl -sk -X POST https://localhost:37683/api/authenticate \
  -H "Content-Type: application/json" \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}'
```
Then, as the shopper (Bearer = shopper token) unless noted:
1. **Place order:** `POST /api/orders` with a couple of seeded catalog item ids (e.g. 1, 2) and
   quantities → capture `orderId`.
2. **Save a card:** `POST /api/payment-methods` with card `4111 1111 1111 1111`, expiry any future
   `YYYY-MM`, any CVC, any name/billing address → capture `paymentMethodId`; confirm `GET
   /api/payment-methods` shows brand `VISA`, last 4 `1111`.
3. **Authorize (one-off card):** `POST /api/orders/{orderId}/pay` with raw card → expect
   `paymentStatus:"Authorized"` and an `authorizationId`. Repeat the same call (double-click) →
   same authorization, not a second hold.
4. **Fulfil (admin token):** `POST /api/orders/{orderId}/fulfil` → `paymentStatus:"Captured"` with
   `capturedAmount`, `paypalFee`, `netAmount` populated from PayPal.
5. **Refund (shopper):** `POST /api/orders/{orderId}/refunds` with an `idempotencyKey` and a
   partial `amount` → `refundId`, `paymentStatus:"PartiallyRefunded"`. Repeat with the **same** key
   → same `refundId` (no second refund). Try to refund more than remaining → 422.
6. **Second order paid with the saved card:** place another order, `POST /pay` with
   `{ "paymentMethodId": <id> }` → `Authorized`; fulfil it → `Captured`.
7. **Cancel path (fresh order, admin):** place + `pay` a third order, then `POST /cancel` before
   fulfil → `Cancelled` (funds released). Confirm it can't then be fulfilled.
8. **Delete card:** `DELETE /api/payment-methods/{paymentMethodId}` → 204; confirm it's gone from
   `GET /api/payment-methods` and a `pay` referencing it now 404s.
9. **my-orders:** `GET /api/my-orders` shows the orders with their payment states and refund rows.
10. **Reconciliation (admin):** `GET /api/reconciliation?from=…&to=…` returns a well-formed report;
    a range covering the just-created payments may be **empty** (reporting lag) — that's expected.
    Verify structure over a wider/older range if data exists.
11. **Ownership:** with the shopper token, attempt to read/refund an order that isn't theirs, and
    to delete another user's card → 404.

Also confirm `dotnet build` succeeds and the app starts clean on the assigned ports.

---

## 17. Files to add / edit (map)

**Add (ApplicationCore):**
- `Entities/OrderAggregate/PaymentStatus.cs`
- `Entities/PaymentAggregate/Payment.cs`, `PaymentRefund.cs`, `PaymentMethod.cs`
- `Interfaces/IPayPalClient.cs`, `IPaymentService.cs`, (`IApiOrderService.cs` or extend `IOrderService`)
- `Services/PaymentService.cs` (+ catalog-priced order placement)
- `Specifications/PaymentByOrderIdSpec.cs`, `PaymentsByBuyerSpec.cs`,
  `PaymentMethodsByBuyerSpec.cs`, `PaymentMethodByIdAndBuyerSpec.cs`
- `Exceptions/…` (the new exceptions in §12)
- `Configuration/PayPalSettings.cs`

**Add (Infrastructure):**
- `PayPal/PayPalClient.cs`, `PayPalTokenProvider.cs`, DTO records (`PayPal/Models/…`),
  `PayPalApiException.cs`
- `Data/Config/PaymentConfiguration.cs`, `PaymentRefundConfiguration.cs`,
  `PaymentMethodConfiguration.cs`
- (optional) `Data/Migrations/*_AddPayments.cs`

**Add (PublicApi):** `OrderEndpoints/` (place, pay, fulfil, cancel, refunds, my-orders) and
`PaymentMethodEndpoints/` and `ReconciliationEndpoints/` — each endpoint class + its
`.Request`/`.Response` DTO files, following the `IEndpoint<>` convention.

**Edit (additive):**
- `ApplicationCore/Entities/OrderAggregate/Order.cs` — add `PaymentStatus` + transition methods.
- `Infrastructure/Data/CatalogContext.cs` — add three `DbSet`s.
- `Infrastructure/Data/Config/OrderConfiguration.cs` — map `PaymentStatus` (`HasConversion<string>`).
- `PublicApi/Program.cs` — `Configure<PayPalSettings>`, env→`PayPal:*` mapping, `AddHttpClient`,
  `AddScoped` the new services.
- `PublicApi/Middleware/ExceptionMiddleware.cs` — map new exceptions to status codes.
- `PublicApi/appsettings.json` — optional non-secret `"PayPal": { "Currency": "USD" }` placeholder
  (no secrets).
- `*.csproj` — add `System.Net.Http.Json` reference only if needed (version is central).

---

## 18. Decisions log (resolved ambiguities — do not re-litigate)

- **D1 — Refund endpoint authorization = shopper-scoped (own order).** The explicit authorization
  mandate enumerates *fulfil, cancel, reconciliation* as the only operator actions and says every
  other endpoint is shopper-scoped. The Flow-1 narrative's "operator … refunds" is looser prose;
  the enumerated rule controls. Keep the auth attribute a one-line change so it can flip to admin
  if desired.
- **D2 — `refundId` returned = PayPal refund id (string).** `orderId` and `paymentMethodId` are the
  app's int ids (used in later routes); no later route consumes a refund id, so returning PayPal's
  canonical, externally verifiable refund id is most useful. (Returning an app int id is equally
  acceptable — pick one and be consistent.)
- **D3 — Prices from `CatalogItem.Price`.** The task says amounts come from catalog prices; the
  existing basket flow reads price from the basket item, but the PublicApi flow has no basket, so
  read `CatalogItem.Price` directly when building order items.
- **D4 — Owner key = username (`ClaimTypes.Name`).** This matches what the domain stores in
  `Order.BuyerId` and is what the JWT carries. Scope orders and saved cards by this string.
- **D5 — Save-during-purchase vs explicit vault.** The primary saved-card flow is
  `POST /api/payment-methods` → Vault v3 `CreatePaymentToken` (direct card). The pay endpoint's
  optional `saveCard` uses order-level `attributes.vault = ON_SUCCESS`; both create a
  `PaymentMethod` record. Reuse one PayPal vault `customer.id` per shopper across their cards.
- **D6 — Separate `Payment` aggregate + `Order.PaymentStatus`.** Reuses `Order`/`OrderItem` (no
  parallel order model), keeps `Order` minimal, and stores the PayPal-owned ids/amounts/refunds in
  a companion aggregate. `Order.PaymentStatus` is the authoritative state; both are saved together.
- **D7 — 3DS/challenge = stop-and-report.** If PayPal returns a browser-approval requirement
  (`PAYER_ACTION_REQUIRED` / `payer-action` link), surface an actionable error; do **not** build an
  approval round-trip. Will not trigger with the sandbox test card and no forced SCA.
- **D8 — Stale-auth renewal path is implemented but won't fire live in one run.** Capture-fails →
  reauthorize → retry; reauthorize-fails → operator-actionable error. Exercise the branch via a
  mocked `IPayPalClient` unit test and/or the `PayPal-Mock-Response` header; the live sandbox auth
  stays within its honor period during a single run.
- **D9 — No capability gap.** The four spec documents cover create/authorize/capture/void/
  reauthorize/refund, vaulting, and transaction search; the OAuth client-credentials scheme is
  declared in each document's `securityScheme`. Do not invent endpoints/fields; if a genuinely
  uncovered need appears during the build, STOP and report it rather than working around the spec.

---

## 19. Guardrails recap

- Spec (`api-specs/paypal`) is the contract for every PayPal interaction; where a doc/web source
  conflicts with the spec, the spec wins.
- No pre-built PayPal SDK/client from NuGet or anywhere. Hand-written client only.
- Secrets (client id/secret) never enter any committed file. Bind from `PayPal:*` (from the env
  vars). `PayPal:BaseUrl` overrides the base for every call including the token request when set.
- No PAN/CVV in the database or logs. Amounts equal the order total to the cent. Payment operations
  idempotent in effect. One shopper never sees/acts on another's orders or cards.
</content>
</invoke>
