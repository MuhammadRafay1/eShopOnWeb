# PLAN — PayPal payments + saved cards for eShopOnWeb (PublicApi)

This is the build plan for a later session that gets the same task + the PayPal `paypal-sdk`
plugin, but **not** this conversation. It additively adds card payment (authorize → capture →
refund/void), saved cards (vault), and a reconciliation report to **`src/PublicApi`**, using the
**PayPal Server SDK (.NET)** (`Darker98.PayPalServerSdk`, root namespace `PayPalServerSdk`, API
spec `2.29`) for every PayPal interaction.

It embeds the **CONTRACT SHEET** the integrate skill requires (the skill routes
`pay-pal-server-sdk-plan.md` content into this file). Every SDK fact below was taken from the SDK
map + SDK source this session; the **Source** column of each contract row cites the file. Load the
skills in **REQUIRED READING** before writing any code.

> **Secrets:** this file names only configuration **keys** and **env-var names** — never values.
> Credentials come from env vars `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`,
> `PAYPAL_CURRENCY` → bound to config keys `PayPal:ClientId`, `PayPal:ClientSecret`,
> `PayPal:Environment`, `PayPal:Currency`, plus optional `PayPal:BaseUrl`.

---

## 0. What the build session must honor from this plan

- The PayPal **call surface** (signatures, request records, body models, response envelopes, error
  cases, enums) in the CONTRACT SHEET is authoritative — do not re-derive it from memory. If a name
  fails to compile, re-read the one source file its row cites and fix in place (Step 4 of the
  integrate skill); never guess.
- Rows labelled `YOUR CALL — not in the map` are application-design decisions already made here;
  keep them unless the task forces otherwise.
- The flows must be drivable through the PublicApi HTTP surface alone, each action separately
  invocable. No storefront UI.

---

## 1. Architecture & where code goes

Clean-architecture layering already in the repo (survey confirmed):
`ApplicationCore` ← `Infrastructure` ← `PublicApi`. The PayPal SDK package is a dependency of
**Infrastructure only**; `ApplicationCore` stays SDK-free behind an interface; `PublicApi` depends
on both and exposes HTTP endpoints.

| Layer | Additions |
| --- | --- |
| `ApplicationCore` | New aggregates `OrderPayment`, `PaymentRefund`, `SavedPaymentMethod`, `ShopperPayPalProfile`; `PaymentStatus` enum; `IPayPalPaymentGateway` interface + its domain result/command records (no SDK types); application services `IOrderPaymentService`, `IPaymentMethodService`, `IReconciliationService`; domain exceptions; money-formatting helper. |
| `Infrastructure` | `PayPalPaymentGateway : IPayPalPaymentGateway` (the sole SDK caller); `PayPalSettings` options POCO; DI wiring in `Dependencies.cs`; EF `IEntityTypeConfiguration`s + new `DbSet`s on `CatalogContext`; error translation to a `PayPalException`. |
| `PublicApi` | Endpoint classes (auto-discovered `IEndpoint<...>`); request/response DTOs; `[Authorize]` roles; `ExceptionMiddleware` branches. |

**Why a satellite `OrderPayment` aggregate rather than mutating `Order`.** `POST /api/orders`
reuses the existing `Order`/`OrderItem` model unchanged (additive, as mandated). Payment/fulfilment
state lives in a 1:1 satellite aggregate keyed by the order — this both keeps the change additive
and gives us a **primary-key** claim row for duplicate-prevention (see §9, which the EF in-memory
provider actually enforces; a unique index would not). `YOUR CALL — not in the map.`

**Repo conventions to imitate (pattern · exemplar):**
- Endpoint = one class implementing `MinimalApi.Endpoint.IEndpoint<IResult, TRequest, …deps>` with
  `AddRoute(IEndpointRouteBuilder)` + `HandleAsync(...)`; auto-discovered by
  `builder.Services.AddEndpoints()` / `app.MapEndpoints()` in `src/PublicApi/Program.cs`. Exemplars:
  `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs` (POST, returns
  `Results.Created`), `CatalogItemGetByIdEndpoint.cs` (GET), `DeleteCatalogItemEndpoint.cs` (DELETE).
- Route literals `"api/..."` (no leading slash, kebab-case) passed to `MapPost`/`MapGet`;
  `.Produces<T>()`, `.WithTags("...")`.
- DTOs: `Endpoint.Request.cs` / `Endpoint.Response.cs`, deriving `BaseRequest` / `BaseResponse`
  (`src/PublicApi/BaseRequest.cs`, `BaseResponse.cs`), plain `{ get; set; }` auto-props.
- Admin-only: `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`
  (role string `"Administrators"`, `src/BlazorShared/Authorization/Constants.cs`). Shopper-scoped:
  plain `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
- Caller identity = the username/email in `ClaimTypes.Name` (JWT issued by
  `src/Infrastructure/Identity/IdentityTokenClaimService.cs`; the same string is `Order.BuyerId`).
  Read it in a handler by injecting `ClaimsPrincipal`/`HttpContext` and `user.FindFirstValue(ClaimTypes.Name)`.
- Entity = aggregate root `: BaseEntity, IAggregateRoot` (int `Id`) OR, for PK-claim aggregates, a
  class with an explicit non-identity key (see §9). Repos: open-generic `IRepository<T>` /
  `IReadRepository<T>` already registered in `Program.cs`; specifications derive
  `Ardalis.Specification.Specification<T>` (exemplar `src/ApplicationCore/Specifications/OrderWithItemsByIdSpec.cs`).
- EF config: `IEntityTypeConfiguration<T>` in `src/Infrastructure/Data/Config/`, applied by
  `CatalogContext.OnModelCreating` → `ApplyConfigurationsFromAssembly`; money columns
  `HasColumnType("decimal(18,2)")`, value objects via `OwnsOne` (exemplar `OrderConfiguration.cs`).
  Add new `DbSet`s to `src/Infrastructure/Data/CatalogContext.cs`.
- Options: POCO + `const CONFIG_NAME` bound via `builder.Services.Configure<T>(section)` (exemplar
  `src/BlazorShared/BaseUrlConfiguration.cs`).
- Error mapping: domain exceptions in `src/ApplicationCore/Exceptions/`; branch in
  `src/PublicApi/Middleware/ExceptionMiddleware.cs` (today maps `DuplicateException`→409, else 500).
- Tests: endpoint tests = **MSTest** + `WebApplicationFactory<Program>` in
  `tests/PublicApiIntegrationTests/` (exemplar `CatalogItemEndpoints/CreateCatalogItemEndpointTest.cs`;
  tokens via `ApiTokenHelper.GetAdminUserToken()`/`GetNormalUserToken()`; InMemory DB forced by
  `appsettings.test.json`). Domain/unit = **xUnit** + NSubstitute in `tests/UnitTests/` (no
  FluentAssertions anywhere — classic `Assert`).
- Central package management is ON: add `PackageVersion` to `Directory.Packages.props` and a
  **versionless** `PackageReference` in `Infrastructure.csproj`.

---

## 2. Scope & sequence (build order)

1. **Package & config.** Add `Darker98.PayPalServerSdk` (versionless; floats to latest — do not pin
   from memory) to `Directory.Packages.props` + `src/Infrastructure/Infrastructure.csproj`. Add a
   `"PayPal"` section to `src/PublicApi/appsettings.json` with keys present-but-blank (values arrive
   from env vars). Create `PayPalSettings` options POCO.
2. **Fail-fast credentials + DI.** In `src/Infrastructure/Dependencies.cs` bind `PayPal` →
   `PayPalSettings` with `.ValidateOnStart()` checking `ClientId`, `ClientSecret`, `Environment`,
   `Currency` all non-blank; register `PayPalServerSdkClient` over a **named** `HttpClient`
   (`PayPalServerSdkClient` operations used: all five groups via `client.Orders/.Payments/.Vault/.TransactionSearch`).
3. **Domain.** Add aggregates, `PaymentStatus` enum, `IPayPalPaymentGateway` + result/command
   records, application-service interfaces, and domain exceptions in `ApplicationCore`.
4. **Infrastructure gateway + persistence.** Implement `PayPalPaymentGateway` (the only SDK caller),
   using operations `CreateOrder`, `AuthorizeOrder`, `CaptureAuthorizedPayment`, `ReauthorizePayment`,
   `VoidPayment`, `RefundCapturedPayment`, `GetOrder`, `GetAuthorizedPayment`, `GetCapturedPayment`,
   `GetRefund`, `CreatePaymentToken`, `ListCustomerPaymentTokens`, `DeletePaymentToken`,
   `SearchTransactions`. Add EF configs + `DbSet`s. Add error translation.
5. **Application services.** `OrderPaymentService` (place/pay/fulfil/cancel/refund/list),
   `ReconciliationService`, `PaymentMethodService` (save/list/delete) orchestrating gateway + repos
   + domain state machine + money formatting + idempotency claims (§9).
6. **PublicApi endpoints + DTOs** (§4), role attributes, `ExceptionMiddleware` branches.
7. **Tests** (§12).
8. **Self-verify on sandbox** + write the user verification guide (§13).

A capability the map lacks is a Blocker in §15 — none found; every flow maps to operations below.

---

## 3. Domain model additions (`ApplicationCore`)

`PaymentStatus` enum (`src/ApplicationCore/Entities/OrderAggregate/PaymentStatus.cs`):
`AwaitingPayment, Authorizing, Authorized, AuthorizationFailed, Fulfilling, Captured,
PartiallyRefunded, Refunded, Cancelled`.

- **`OrderPayment`** (aggregate root; **key = `OrderId` (int)**, not `BaseEntity.Id` — see §9):
  `OrderId`, `Status (PaymentStatus)`, `string CurrencyCode`, `decimal Amount` (snapshot of order
  total authorized), `string? PayPalOrderId`, `string? AuthorizationId`,
  `string? AuthorizationStatus`, `string? AuthorizationExpiryUtc`, `string? CaptureId`,
  `string? CaptureStatus`, `decimal? CapturedGross`, `decimal? PayPalFee`, `decimal? NetAmount`,
  `decimal RefundedTotal` (default 0), `string? PaymentSourceDescriptor` (brand+last4 for display),
  `string InvoiceId` (unique per order payment; sent to PayPal), timestamps. Domain methods enforce
  the transitions in §10 via `Ardalis.GuardClauses`.
- **`PaymentRefund`** (aggregate root; **key = `string IdempotencyKey`** — caller-supplied): `OrderId`,
  `CaptureId`, `decimal Amount`, `string? PayPalRefundId`, `string Status`, `DateTimeOffset CreatedAt`.
- **`SavedPaymentMethod`** (aggregate root; **key = `string Id`** = PayPal vault payment-token id):
  `string BuyerId`, `string Brand`, `string LastDigits`, `string? Expiry`, `string? CardholderName`,
  `DateTimeOffset CreatedAt`. **No PAN/CVV ever stored.**
- **`ShopperPayPalProfile`** (aggregate root; **key = `string BuyerId`**): `string PayPalCustomerId`.
  Maps an eShop shopper to the PayPal-generated vault customer id (see §11 Saved cards).

`IPayPalPaymentGateway` (ApplicationCore interface, SDK-type-free — the only seam Infrastructure
implements). Methods return small ApplicationCore records (e.g. `AuthorizeResult`, `CaptureResult`,
`RefundResult`, `VaultCardResult`, `ReconciliationRow`) carrying the PayPal ids/statuses/amounts the
services persist. Card data is passed in via a `CardDetails` command record (number/expiry/cvv/
name/address) OR a `vaultId` — the gateway never returns or logs PAN/CVV.

Domain exceptions (`src/ApplicationCore/Exceptions/`): `PaymentException` (base),
`PaymentAuthorizationException`, `AuthorizationExpiredException`, `RefundAmountExceededException`,
`PayPalGatewayException` (carries provider status + `debug_id` + name/message, caller-safe). Map in
`ExceptionMiddleware` (§4).

---

## 4. HTTP endpoints (`src/PublicApi`)

All routed under `api/`, JWT-authenticated, acting only on the caller's own data unless marked
**ADMIN**. Response bodies derive `BaseResponse`; **created-resource identifiers are top-level
fields** as mandated.

| Method & route | Role | Does | Response id |
| --- | --- | --- | --- |
| `POST /api/orders` | shopper | Build `Order` from `{items:[{catalogItemId, quantity}], shipToAddress?}`; `BuyerId` from token; snapshot catalog prices; create `OrderPayment(AwaitingPayment)` | `orderId` (int) |
| `POST /api/orders/{orderId}/pay` | shopper (own) | Authorize order total: `CreateOrder(AUTHORIZE, card|vaultId)` + `AuthorizeOrder` (hold, no capture) | — |
| `POST /api/orders/{orderId}/fulfil` | **ADMIN** | Capture the authorization; renew first if stale (§10); record captured/fee/net | — |
| `POST /api/orders/{orderId}/cancel` | **ADMIN** | Void the authorization before capture (funds released) | — |
| `POST /api/orders/{orderId}/refunds` | **ADMIN** | Refund captured payment, full or partial; caller-supplied idempotency key | `refundId` |
| `GET /api/my-orders` | shopper | Caller's orders + payment state | — |
| `GET /api/reconciliation?from={iso}&to={iso}` | **ADMIN** | PayPal transactions for range, lined up against eShop orders | — |
| `POST /api/payment-methods` | shopper | Vault a card; return safe descriptor | `paymentMethodId` |
| `GET /api/payment-methods` | shopper | Caller's saved cards | — |
| `DELETE /api/payment-methods/{paymentMethodId}` | shopper (own) | Remove saved card (unusable after) | — |

**Role rationale (task):** fulfil, cancel, reconciliation are operator actions → `Administrators`.
Everything else is shopper-scoped: the handler loads by id, then asserts `entity.BuyerId == caller`
(401/403/404 as appropriate) before acting — **ownership is enforced in every shopper handler**.
`refunds` is admin (an operator return action) and so uses the order id, not buyer scoping.

`pay` request carries **either** inline card `{number, expiry (YYYY-MM), securityCode, name,
billingAddress}` **or** `{savedPaymentMethodId}` (one of the caller's `SavedPaymentMethod`s) — never
both. (Cross-op invariant, §6.)

`ExceptionMiddleware` branches (map to `BlazorShared.Models.ErrorDetails`): `PaymentException`
family → 409/422 as appropriate; `PayPalGatewayException` → its carried status for caller-fault 4xx,
else 502; `AuthorizationExpiredException` (non-renewable) → 409 with an operator-actionable message;
`RefundAmountExceededException` → 422. Never surface raw SDK/exception text (§ error-handling trap).

---

## 5. Configuration, client construction, fail-fast (`Infrastructure`)

- `PayPalSettings` POCO, `const CONFIG_NAME = "PayPal"`, props `ClientId, ClientSecret, Environment,
  Currency, BaseUrl` (all `string?`; `BaseUrl` optional). Bind + `[Required]` on the four mandatory
  + `.ValidateOnStart()` so the host **refuses to boot** when any of the four is missing/blank
  (every part checked — a blank `ClientSecret` is not a missing `ClientId`). Message names the
  config key, never the value, no fallback.
- Register `PayPalServerSdkClient` as a **singleton** over a **named** `HttpClient` (per
  `dotnet-client-initialization`): set `Timeout` explicitly (~10s per attempt) and
  `PooledConnectionLifetime` on the primary handler (singleton + stale-DNS). Build
  `PayPalServerSdkClientOptions` **once at registration**:
  - `Environment = ServerEnvironment.Sandbox` (the SDK declares only `Sandbox`).
  - `Oauth2 = new OAuth2ClientCredentials { ClientId = settings.ClientId, ClientSecret = settings.ClientSecret }`.
  - If `settings.BaseUrl` is non-blank: `options.Server.Default.Sandbox.BaseUrl = settings.BaseUrl`
    — **confirmed** to govern the token request too (token URL is `server.Default("/v1/oauth2/token")`,
    same resolution as every API call; source `AuthSchemes.cs`). Use the value verbatim.
  - `Retry`, `Logging`, `TimeProvider` per PRODUCTION READINESS (§8).
- `PayPal:Environment`/`PayPal:Currency` are read from `PayPalSettings`, never hard-coded. Currency
  flows into every `Money`/`AmountWithBreakdown` the gateway builds.
- **Rotation note:** options are captured once at registration into the singleton; a rotated secret
  takes effect only on process restart (acceptable here; stated in §8 row 2).

---

## 6. CONTRACT SHEET — PayPal Server SDK (.NET), spec 2.29

> ⚠ **Signatures below are generated code, verbatim.** Each operation that takes input takes **one
> request record** as its first parameter (an operation with no inputs takes none), built with an
> object initializer whose property names are the record's own — **never flat arguments**. Call as
> `client.{Group}.{Operation}(new {Op}Request { … }, cancellationToken: ct)`; `requestOptions` sits
> between the record and the token — pass the token **by name** or a bare `ct` binds to
> `RequestOptions?` (`CS1503`).
> ⚠ **Every SDK type is written fully-qualified with the namespace its source path implies**, taken
> from the path the map gives for **that** type (records → `PayPalServerSdk.Models`; enums →
> `PayPalServerSdk.Models.Enums`; request records → `PayPalServerSdk.Requests.{Controller}`; typed
> errors → `PayPalServerSdk.Errors`) — never from where a neighbouring type sits.

Common facts (sdk-map.md): throw-only (no `…Result` no-throw variants anywhere); no pagination
wrappers (all return a single response — `SearchTransactions` paging is driven by hand, §9); server
group `Default`, base `https://api-m.sandbox.paypal.com`, override `options.Server.Default.Sandbox.BaseUrl`;
auth `options.Oauth2` on every operation. Every non-GET operation also carries a generator-injected
`Idempotency-Key: Guid.NewGuid()` header — **not** a real key (§8 row 5); the real key is the
`PayPalRequestId` **record member** where present.

### 6a. Operations used

| Op (`client.X`) | Signature (first param = request record) · required members | Body model + fields used (wire) | Response envelope → fields read | Error case · accessors | Source |
| --- | --- | --- | --- | --- | --- |
| `Orders.CreateOrder` | `CreateOrder(CreateOrderRequest req)` · req req: `Body`. req members used: `Body`, `PayPalRequestId` (real idempotency key, ≤108, keys 6h — "mandatory for single-step create with card/vault_id"), `Prefer="return=representation"` | `OrderRequest`: `Intent (intent, req)`=`CheckoutPaymentIntent.Authorize`; `PurchaseUnits (purchase_units, req, 1..10)`; `PaymentSource (payment_source)` | `Order` → `Id`, `Status (OrderStatus)`, `PurchaseUnits[]` | A · `TryGetError(out Error)` [400,401,422] · `TryGetRawError` | map/operations/Orders.md; `Requests/Orders/CreateOrderRequest.cs`; `Models/OrderRequest.cs`; `Models/Order.cs`; `Api/Orders.cs` |
| `Orders.AuthorizeOrder` | `AuthorizeOrder(AuthorizeOrderRequest req)` · req req: `Id`. members used: `Id`, `PayPalRequestId` (real key), `Prefer="return=representation"`; `Body` left null (payment source supplied at create) | `OrderAuthorizeRequest` (optional): `PaymentSource (payment_source)` — alt path only | `OrderAuthorizeResponse` → `Id`, `Status`, `PurchaseUnits[].Payments.Authorizations[]` | A · `TryGetError(out Error)` [400,401,403,404,422,500] · `TryGetRawError` | Orders.md; `Requests/Orders/AuthorizeOrderRequest.cs`; `Models/OrderAuthorizeResponse.cs`; `Api/Orders.cs` |
| `Payments.CaptureAuthorizedPayment` | `CaptureAuthorizedPayment(CaptureAuthorizedPaymentRequest req)` · req req: `AuthorizationId`. members used: `AuthorizationId`, `PayPalRequestId` (real key, keys 45d), `Prefer="return=representation"`, `Body` | `CaptureRequest`: `Amount (amount, Money?)` — omit for full capture; `FinalCapture (final_capture)`=`true` | `CapturedPayment` → `Id`, `Status (CaptureStatus)`, `Amount`, `SellerReceivableBreakdown` | A · `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` | Payments.md; `Requests/Payments/CaptureAuthorizedPaymentRequest.cs`; `Models/CaptureRequest.cs`; `Models/CapturedPayment.cs`; `Api/Payments.cs` |
| `Payments.ReauthorizePayment` | `ReauthorizePayment(ReauthorizePaymentRequest req)` · req req: `AuthorizationId`. members used: `AuthorizationId`, `PayPalRequestId`, `Prefer`, `Body` | `ReauthorizeRequest`: `Amount (amount, Money?)` only | `PaymentAuthorization` → `Id`, `Status`, `ExpirationTime` | A · `TryGetError` [400,401,403,404,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` | Payments.md; `Requests/Payments/ReauthorizePaymentRequest.cs`; `Models/ReauthorizeRequest.cs`; `Models/PaymentAuthorization.cs`; `Api/Payments.cs` |
| `Payments.VoidPayment` | `VoidPayment(VoidPaymentRequest req)` · req req: `AuthorizationId`. members used: `AuthorizationId`, `PayPalRequestId`, `Prefer`. **No body** (EmptyBody) | — | `PaymentAuthorization` → `Status (AuthorizationStatus)` (expect `Voided`) | A · `TryGetError` [401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` | Payments.md; `Requests/Payments/VoidPaymentRequest.cs`; `Models/PaymentAuthorization.cs`; `Api/Payments.cs` |
| `Payments.RefundCapturedPayment` | `RefundCapturedPayment(RefundCapturedPaymentRequest req)` · req req: `CaptureId`. members used: `CaptureId`, `PayPalRequestId` (**caller refund idempotency key**, keys 45d), `Prefer="return=representation"`, `Body` | `RefundRequest`: `Amount (amount, Money?)` — omit for full, set for partial; `InvoiceId`, `CustomId`, `NoteToPayer` | `Refund` → `Id`, `Status (RefundStatus)`, `Amount`, `SellerPayableBreakdown` | A · `TryGetError` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` | Payments.md; `Requests/Payments/RefundCapturedPaymentRequest.cs`; `Models/RefundRequest.cs`; `Models/Refund.cs`; `Api/Payments.cs` |
| `Orders.GetOrder` | `GetOrder(GetOrderRequest req)` · req req: `Id`. query `fields`←`Fields` | — | `Order` → `Status`, `PurchaseUnits[].Payments` | A · `TryGetError` [401,404] · `TryGetRawError` | Orders.md; `Requests/Orders/GetOrderRequest.cs`; `Models/Order.cs` |
| `Payments.GetAuthorizedPayment` | `GetAuthorizedPayment(GetAuthorizedPaymentRequest req)` · req req: `AuthorizationId` | — | `PaymentAuthorization` → `Status`, `ExpirationTime` | A · `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError` | Payments.md; `Requests/Payments/GetAuthorizedPaymentRequest.cs` |
| `Payments.GetCapturedPayment` | `GetCapturedPayment(GetCapturedPaymentRequest req)` · req req: `CaptureId` | — | `CapturedPayment` → `Status`, `SellerReceivableBreakdown` | A · `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError` | Payments.md; `Requests/Payments/GetCapturedPaymentRequest.cs` |
| `Payments.GetRefund` | `GetRefund(GetRefundRequest req)` · req req: `RefundId` | — | `Refund` → `Status`, `Amount` | A · `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError` | Payments.md; `Requests/Payments/GetRefundRequest.cs` |
| `Vault.CreatePaymentToken` | `CreatePaymentToken(CreatePaymentTokenRequest req)` · req req: `Body` | `PaymentTokenRequest`: `PaymentSource (payment_source, req)` = `PaymentTokenRequestPaymentSource { Card = PaymentTokenRequestCard{…} }`; `Customer (customer)` | `PaymentTokenResponse` → `Id`, `Customer.Id`, `PaymentSource.Card (CardPaymentTokenEntity)` | A · `TryGetError` [400,403,404,422,500] · `TryGetRawError` | Vault.md; `Requests/Vault/CreatePaymentTokenRequest.cs`; `Models/PaymentTokenRequest.cs`; `Models/PaymentTokenResponse.cs` |
| `Vault.ListCustomerPaymentTokens` | `ListCustomerPaymentTokens(ListCustomerPaymentTokensRequest req)` · req req: `CustomerId`. query: `customer_id`←`CustomerId`, `page_size`←`PageSize`, `page`←`Page`, `total_required`←`TotalRequired` | — | `CustomerVaultPaymentTokensResponse` → `PaymentTokens[]`, `TotalItems`, `TotalPages` | A · `TryGetError` [400,403,500] · `TryGetRawError` | Vault.md; `Requests/Vault/ListCustomerPaymentTokensRequest.cs`; `Models/CustomerVaultPaymentTokensResponse.cs` |
| `Vault.DeletePaymentToken` | `DeletePaymentToken(DeletePaymentTokenRequest req)` · req req: `Id` | — | `void` (Task) | A · `TryGetError` [400,403,500] · `TryGetRawError` | Vault.md; `Requests/Vault/DeletePaymentTokenRequest.cs` |
| `TransactionSearch.SearchTransactions` | `SearchTransactions(SearchTransactionsRequest req)` · req req: `StartDate`, `EndDate` (ISO-8601, range ≤31 days). members: `Fields="transaction_info"` (default), `BalanceAffectingRecordsOnly` default `"Y"` (set `"N"` to include all), `PageSize` (1..500, default 100), `Page` (default 1) | — | `SearchResponse` → `TransactionDetails[]`, `Page`, `TotalItems`, `TotalPages` | **B** · `ApiException<RawError>` (no typed accessors) | TransactionSearch.md; `Requests/TransactionSearch/SearchTransactionsRequest.cs`; `Models/SearchResponse.cs` |

### 6b. Key nested model shapes (read fields/wire names here)

- `OrderRequest` (`Models/OrderRequest.cs`): `Intent`*, `PurchaseUnits`* (`IReadOnlyList<PurchaseUnitRequest>`), `PaymentSource`, `ApplicationContext`.
- `PurchaseUnitRequest` (`Models/PurchaseUnitRequest.cs`): `Amount`* (`AmountWithBreakdown`); `CustomId (custom_id)` = eShop `orderId`; `InvoiceId (invoice_id)` = unique per-order-payment invoice; `Description`. (`ReferenceId` optional; default single PU → `"default"`.)
- `AmountWithBreakdown` (`Models/AmountWithBreakdown.cs`): `CurrencyCode (currency_code)`*=`PayPal:Currency`, `Value (value)`* = formatted order total string (§11).
- `PaymentSource` (`Models/PaymentSource.cs`): `Card (card, CardRequest?)` — the only funding path used.
- `CardRequest` (`Models/CardRequest.cs`): one-off → `Number (number)`, `Expiry (expiry, YYYY-MM)`, `SecurityCode (security_code)`, `Name (name)`, `BillingAddress (billing_address, Address)`; saved card → `VaultId (vault_id)` only. **PAN/CVV are request-only, never stored/logged.**
- `Order` (`Models/Order.cs`): `Id`, `Status (OrderStatus)`, `PurchaseUnits (IReadOnlyList<PurchaseUnit>)`.
- `OrderAuthorizeResponse` (`Models/OrderAuthorizeResponse.cs`): `Id`, `Status (OrderStatus?)`, `PurchaseUnits[] (PurchaseUnit)`.
- **Authorization id path:** `OrderAuthorizeResponse.PurchaseUnits[0].Payments.Authorizations[0]` → `AuthorizationWithAdditionalData { Id, Status (AuthorizationStatus), Amount (Money), ExpirationTime }` (`Models/PurchaseUnit.cs` → `Models/PaymentCollection.cs` → `Models/AuthorizationWithAdditionalData.cs`). Read defensively: every level is nullable (§ trap).
- `CapturedPayment` (`Models/CapturedPayment.cs`): `Id`, `Status (CaptureStatus?)`, `Amount (Money?)`, `SellerReceivableBreakdown`.
- `SellerReceivableBreakdown` (`Models/SellerReceivableBreakdown.cs`): `GrossAmount (gross_amount, Money)`* , `PaypalFee (paypal_fee, Money?)`, `NetAmount (net_amount, Money?)` → captured/fee/net the task requires.
- `Money` (`Models/Money.cs`): `CurrencyCode (currency_code)`*, `Value (value)`* — both **strings**.
- `Refund` (`Models/Refund.cs`): `Id`, `Status (RefundStatus?)`, `Amount (Money?)`, `SellerPayableBreakdown`.
- `PaymentAuthorization` (`Models/PaymentAuthorization.cs`): `Id`, `Status (AuthorizationStatus?)`, `ExpirationTime`.
- Vault save: `PaymentTokenRequestPaymentSource { Card (PaymentTokenRequestCard?) , Token }` (`Models/PaymentTokenRequestPaymentSource.cs`); `PaymentTokenRequestCard` (`Models/PaymentTokenRequestCard.cs`): `Number, Expiry, SecurityCode, Name, Brand, BillingAddress` — direct card vaulting. `Customer` (`Models/Customer.cs`): `Id (id, PayPal-generated, ≤22, [0-9a-zA-Z_-])`, `MerchantCustomerId (merchant_customer_id)` = eShop buyerId.
- Vault safe descriptor: `PaymentTokenResponse.PaymentSource.Card` = `CardPaymentTokenEntity` (`Models/CardPaymentTokenEntity.cs`): `LastDigits (last_digits)`, `Brand (CardBrand?)`, `Expiry`, `Name`. **This is the only card data persisted/returned.**
- Reconciliation: `SearchResponse.TransactionDetails[]` → `TransactionDetails.TransactionInfo` = `TransactionInformation` (`Models/TransactionInformation.cs`): `TransactionId (transaction_id)`, `TransactionStatus (transaction_status, 1-char D/P/S/V)`, `TransactionAmount (transaction_amount, Money)`, `FeeAmount (fee_amount)`, `InvoiceId (invoice_id)`, `CustomField (custom_field)`. Line up `InvoiceId`/`CustomField` ↔ eShop `OrderPayment.InvoiceId`/`OrderId`.
- Error payload `Error` (`Models/Error.cs`): `Name (name)`*, `Message (message)`*, `DebugId (debug_id)`* (PayPal correlation id — log it), `Details[] (ErrorDetails)`.

### 6c. Enums (member → wire; `Models/Enums/*.cs`; open string enums — use static members, read `.Value`, branch with `Match`)

| Enum | Members used (wire) | Source |
| --- | --- | --- |
| `CheckoutPaymentIntent` | `Authorize`→`AUTHORIZE` (also `Capture`→`CAPTURE`) | `CheckoutPaymentIntent.cs` |
| `OrderStatus` | `Created`/`Saved`/`Approved`/`Voided`/`Completed`/`PayerActionRequired` (`PAYER_ACTION_REQUIRED` → challenge; §15 STOP) | `OrderStatus.cs` |
| `AuthorizationStatus` | `Created`,`Captured`,`Denied`,`PartiallyCaptured`,`Voided`,`Pending` | `AuthorizationStatus.cs` |
| `CaptureStatus` | `Completed`,`Declined`,`PartiallyRefunded`,`Pending`,`Refunded`,`Failed` | `CaptureStatus.cs` |
| `RefundStatus` | `Cancelled`,`Failed`,`Pending`,`Completed` | `RefundStatus.cs` |
| `CardBrand` | brand display (read `.Value`) | `Models/Enums/CardBrand.cs` |

### 6d. Client construction / auth / server (sdk-map.md "Getting a client", "Servers & auth")

- Construct: `new PayPalServerSdkClient(HttpClient, PayPalServerSdkClientOptions)` (only ctor) or DI
  `services.AddPayPalServerSdkClient(options => …)`. Options props: `Environment, Retry, Logging,
  TimeProvider, Server, StreamReadTimeout, Hooks, Oauth2, Oauth2TokenStrategy`.
- `ServerEnvironment` ∈ `PayPalServerSdk.Servers`; only `Sandbox` is declared. `OAuth2ClientCredentials`
  ∈ `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials` (`ClientId`, `ClientSecret`, `Scope?`).
- Token endpoint = `server.Default("/v1/oauth2/token")` → **respects `BaseUrl` override** (`AuthSchemes.cs`).

### 6e. CROSS-OPERATION INVARIANTS (derived from the task — the map never states these)

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| `savedPaymentMethodId` given to `pay` must be one of the caller's own saved cards | `pay` (→ `Orders.CreateOrder` with `card.vault_id`) ← `SavedPaymentMethod` rows written by `Vault.CreatePaymentToken` / listed via `Vault.ListCustomerPaymentTokens` | `OrderPaymentService.Pay`: load `SavedPaymentMethod` by id **and** assert `BuyerId == caller` before building `PaymentSource`; reject if absent/not owned (the vault_id passed to PayPal is only ever one we stored for this buyer) |
| `AuthorizationId` captured/voided/reauthorized must be one PayPal returned for THIS order | `fulfil`/`cancel`/`reauthorize` ← `Orders.AuthorizeOrder` | `OrderPaymentService`: read `OrderPayment.AuthorizationId` persisted at `pay` time; never accept an id from the caller |
| `CaptureId` refunded must be one PayPal returned at fulfilment | `refunds` ← `Payments.CaptureAuthorizedPayment` | `OrderPaymentService.Refund`: read `OrderPayment.CaptureId`; never from caller |
| Refund currency = captured currency; cumulative refunds ≤ captured | `refunds` ← capture | `OrderPaymentService.Refund`: `RefundedTotal + amount ≤ CapturedGross` else `RefundAmountExceededException` (before SDK call) |
| `pay` carries card XOR savedPaymentMethodId | `pay` | request validation in handler/service |
| PayPal `customer_id` listed/saved-against is the one PayPal generated for this buyer | `payment-methods` list ← `Vault.CreatePaymentToken` first-save | `PaymentMethodService`: read `ShopperPayPalProfile.PayPalCustomerId`; if none, buyer has no saved cards (return empty; no PayPal call) |

---

## 7. Trap notes (named hazard + consequence + skill to load — resolved only by loading it)

- **T1 — client lifetime & token-cache cost.** Registering the SDK client per-request pays a fresh
  OAuth token round-trip on every first call, and a singleton over `IHttpClientFactory` caches DNS
  indefinitely. Getting the lifetime/handler wrong is a correctness/perf defect, not style. **MUST
  load `dotnet-client-initialization`.**
- **T2 — credential application is silent.** An unset/blank credential yields a no-op scheme, so a
  missing secret surfaces as a provider `401` one round-trip away from the cause instead of at
  startup. **MUST load `dotnet-authentication`.**
- **T3 — request-record shape & the injected `Idempotency-Key`.** Every input rides one request
  record; the real idempotency key is the `PayPalRequestId` **record member**, while the on-wire
  `Idempotency-Key` header is generator-injected (`Guid.NewGuid()`, fresh per call) and deduplicates
  nothing — mistaking it for the key is a duplicate-charge defect. **MUST load `dotnet-calling-endpoints`.**
- **T4 — open enums, nullable response chains, money-as-string.** `OrderStatus`/`CaptureStatus`/… are
  `OpenStringEnum` (no C# enum, read `.Value`/`Match`, `ToString()` gives a debug form); the
  authorization id sits behind a chain of nullable collections (`PurchaseUnits?→Payments?→Authorizations?`)
  that can each be absent; `Money.Value` is a string needing culture-correct parse/format. **MUST
  load `dotnet-models`.**
- **T5 — error taxonomy & deserialization leak.** Operations are Case A (`ApiException<{Operation}Error>`
  with per-status `TryGet…` incl. `TryGetNoContent(out RawError)` for 500) except `SearchTransactions`
  (Case B `ApiException<RawError>`); a drifted body throws `ResponseDeserializationException` (an
  `ApiException`, **not** `ApiException<TError>`) that a typed-only catch lets escape and a generic
  handler leaks as SDK type + URL. **MUST load `dotnet-error-handling`.**
- **T6 — retry/timeout semantics & unredacted body logging.** `Timeout` is per-attempt not a call
  budget; `POST`/`PATCH`/`DELETE` are never resent by default (good — keeps writes single) but a
  bounded whole-call needs a `CancellationToken`; `LogRequestBody` logs JSON bodies **verbatim** and
  the `PAYPALSERVERSDKCLIENT_LOG` env var can switch body logging on from outside the code unless
  `LoggerFactory` is set explicitly — card data path. **MUST load `dotnet-configuration-resilience`.**
- **T7 — test seam.** The `HttpClient` ctor arg is the only seam; stub `HttpMessageHandler`, read the
  request body **inside** the handler (disposed per attempt afterwards), and match the repo's MSTest/
  xUnit + classic-`Assert` style. **MUST load `dotnet-testing`.**

---

## 8. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | **Credential fail-fast** | `PayPalSettings` bound from `PayPal` in `Dependencies.cs`; `[Required]` on `ClientId`,`ClientSecret`,`Environment`,`Currency` + `.ValidateOnStart()` → host refuses to boot if any is missing/blank (each of the 4 checked; blank ≠ missing). Error names the key, never the value; no fallback to unauthenticated. `BaseUrl` optional. |
| 2 | **Secret sourcing & rotation** | Secrets from env vars → `IConfiguration` `PayPal:*`; options built once at registration and captured in the singleton `PayPalServerSdkClient`. Rotation requires a process restart — acceptable for this app; stated explicitly. |
| 3 | **Total timeout budget** | `options.Retry.Timeout` ≈10s per attempt; whole-call bound via a linked `CancellationTokenSource` (`HttpContext.RequestAborted` + `CancelAfter`) applied in the gateway's one `Bounded(...)` helper so every op inherits it. `pay`/`fulfil` make 1–2 PayPal calls → budget ≈ calls×per-attempt < the request deadline. |
| 4 | **Write-retry ownership** | Default `HttpMethodsToRetry` = `GET,HEAD,PUT,OPTIONS`; all our writes are `POST`/`DELETE` → never resent by the SDK. We do **not** widen the list (no `PUT`s in scope). |
| 5 | **Idempotency & ambiguous writes** | Real key = `PayPalRequestId` member: `CreateOrder`=`"ord-{orderId}"`, `AuthorizeOrder`=`"auth-{orderId}"`, `CaptureAuthorizedPayment`=`"cap-{orderId}"`, `VoidPayment`=`"void-{orderId}"`, `RefundCapturedPayment`=**caller idempotency key** (verbatim). These are deterministic/caller-supplied, stable across caller retries (unlike the injected header). Local PK claims (§9) stop the second request before it reaches PayPal. |
| 6 | **Observability** | Log at Info: op name, eShop orderId, PayPal order/auth/capture/refund ids, status, `Error.DebugId` on failure. **Never** log request bodies/card fields. `Error.Name/Message/DebugId` carried into `PayPalGatewayException`. Request-line logging via the built-in logger at Info during first-run wire verification, then dialled back. |
| 7 | **Sensitive data** | Card PAN/CVV/expiry are in JSON bodies of `CreateOrder`,`AuthorizeOrder`,`CreatePaymentToken` (request-model files confirm). Therefore: `LogRequestBody` stays **off**; `options.Logging.LoggerFactory` set **explicitly** (to the host factory) so `PAYPALSERVERSDKCLIENT_LOG` can't turn body logging on from outside; our own code/logs never echo card fields; only `last_digits`/`brand`/`expiry` persisted. |
| 8 | **Environment selection** | One server group `Default`; SDK declares only `ServerEnvironment.Sandbox`. All traffic → Sandbox. `PayPal:BaseUrl`, when set, overrides `options.Server.Default.Sandbox.BaseUrl` (governs API **and** token calls). No `Production` member exists in this SDK — test traffic cannot accidentally hit live via environment selection; a different live account is reached only by explicit `BaseUrl`. |
| 9 | **Duplicate prevention under concurrency** | PK-claim rows (§9 DUPLICATE CLAIMS). |
| 10 | **Partial results** | `SearchTransactions` paging — §9 PAGED READS; reconciliation DTO carries `pagesFetched`,`totalPages`,`truncated`. |
| 11 | **Unknown outcomes** | §9 UNKNOWN OUTCOMES — each write settled by re-read / idempotent re-POST before reporting failure. |

### DUPLICATE CLAIMS (claim → SDK call → record; claim store = `CatalogContext`, this app's EF store; refusal = **primary-key** insert conflict, which the in-memory provider enforces — a unique index would not)

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `pay` (authorize) | `OrderPayment` row, **PK = `OrderId`**, inserted with `Status=Authorizing` before any PayPal call | duplicate-PK insert refused by `CatalogContext` at `SaveChanges` | catch the store's duplicate-key exception → load existing `OrderPayment`; return its state (idempotent) or 409 if still `Authorizing` | TBD |
| `fulfil` (capture) | same `OrderPayment` row; atomic status transition `Authorized`→`Fulfilling` (guarded, persisted before capture) | a concurrent transition sees status already `Fulfilling`/`Captured` and is refused by the guard+save | catch concurrency/guard failure → return current captured state | TBD |
| `refunds` | `PaymentRefund` row, **PK = caller `IdempotencyKey`**, inserted before the PayPal refund call | duplicate-PK insert refused at `SaveChanges` | catch duplicate-key → load existing `PaymentRefund`; return its `refundId` (no second refund) | TBD |
| `cancel` (void) | same `OrderPayment` row; transition `Authorized`→`Cancelled` guarded+persisted before void | concurrent transition sees non-`Authorized` and is refused | catch guard failure → return current state | TBD |
| `payment-methods` save | `SavedPaymentMethod` row keyed by returned vault id (written after PayPal returns the id); first-save of a buyer also inserts `ShopperPayPalProfile` **PK = `BuyerId`** | `ShopperPayPalProfile` duplicate-PK insert refused for a racing second first-save | catch duplicate-key → reload profile, reuse its `PayPalCustomerId` | TBD |

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `TransactionSearch.SearchTransactions` (reconciliation; hand-driven `Page` 1..`TotalPages`, `PageSize`=500, `BalanceAffectingRecordsOnly="N"`) | `MaxPages` config constant (backstop) + `TotalPages` from the response | reconciliation response DTO fields `pagesFetched`, `totalPages`, `truncated` (bool) — a truncated report is marked, not silently partial | TBD |

> The whole range must be covered (not just page 1): loop `Page` from 1 while `Page <= TotalPages`
> and `Page <= MaxPages`, aggregating `TransactionDetails`. An empty result for a just-created range
> is a legitimate sandbox outcome (reporting lag), **not** a gap — the report returns empty, not an
> error (§15).

### UNKNOWN OUTCOMES (connection fails after the provider may have acted — settle by re-read, never report failure blindly)

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `CreateOrder` | re-POST `CreateOrder` with the **same** `PayPalRequestId` (idempotent → returns same order), or `GetOrder` if the id was already persisted | `PayPalRequestId="ord-{orderId}"` / persisted `PayPalOrderId` | TBD | TBD |
| `AuthorizeOrder` | `GetOrder(PayPalOrderId)` → inspect `PurchaseUnits[].Payments.Authorizations[]` | persisted `PayPalOrderId` | TBD | TBD |
| `CaptureAuthorizedPayment` | `GetAuthorizedPayment(AuthorizationId)` (status `Captured`/`PartiallyCaptured`) then `GetOrder`/`GetCapturedPayment` for the capture id; or idempotent re-POST with same `PayPalRequestId` | persisted `AuthorizationId` | TBD | TBD |
| `VoidPayment` | `GetAuthorizedPayment(AuthorizationId)` (status `Voided`) | persisted `AuthorizationId` | TBD | TBD |
| `RefundCapturedPayment` | idempotent re-POST with same caller key, or `GetRefund(refundId)` if persisted | caller `IdempotencyKey` → `PayPalRefundId` | TBD | TBD |
| `ReauthorizePayment` | `GetAuthorizedPayment(AuthorizationId)` (new `ExpirationTime`/status) | persisted `AuthorizationId` | TBD | TBD |
| `CreatePaymentToken` | `ListCustomerPaymentTokens(PayPalCustomerId)` → any token id not already in our `SavedPaymentMethod` rows for this buyer is the just-created one | `PayPalCustomerId` | TBD | TBD |
| `DeletePaymentToken` | `GetPaymentToken(id)` → `404` means deleted | vault token id | TBD | TBD |

> Replace each `TBD` once the code compiles with the specific `catch`/member, per the integrate
> skill. The `catch` for these is `SdkConnectionException`/`SdkTimeoutException` at the gateway
> boundary; the re-read helper must actually be called by the failing write's handler.

---

## 9. (folded into §8 tables above)

---

## 10. Order-payment state machine (service layer)

```
AwaitingPayment --pay--> Authorizing --(authorize ok)--> Authorized
                                     --(decline)-------> AuthorizationFailed (re-pay allowed)
Authorized --cancel/void--> Cancelled            (funds released; no money moved)
Authorized --fulfil--> Fulfilling --(capture ok)--> Captured
                                   --(auth stale)--> reauthorize --(ok)--> capture --> Captured
                                                                  --(no renew)--> AuthorizationExpiredException (operator-actionable; stays Authorized)
Captured --refund(partial)--> PartiallyRefunded --refund...--> Refunded (when RefundedTotal == CapturedGross)
Captured --refund(full)--> Refunded
```

- **`pay`** (shopper, own): claim `OrderPayment`; build `OrderRequest{Intent=Authorize, PurchaseUnits=[{Amount=order total, CustomId=orderId, InvoiceId}], PaymentSource.Card=…}`; `CreateOrder(Prefer=representation, PayPalRequestId="ord-{orderId}")`; then `AuthorizeOrder(PayPalRequestId="auth-{orderId}")`; persist `PayPalOrderId`, `AuthorizationId`, `AuthorizationStatus`, `AuthorizationExpiryUtc`, amount held. If PayPal returns `OrderStatus.PayerActionRequired` or an `Authorizations[]` requiring buyer approval → **STOP**: surface a clear "challenge not supported" error (do not build a browser round-trip) — §15.
- **`fulfil`** (ADMIN): transition→`Fulfilling`; `CaptureAuthorizedPayment(FinalCapture=true, Prefer=representation, PayPalRequestId="cap-{orderId}")`; on capture read `CapturedPayment.SellerReceivableBreakdown` → persist `CapturedGross`, `PayPalFee`, `NetAmount`, `CaptureId`, `CaptureStatus`; →`Captured`. **Stale auth:** if capture fails with an expiry-indicating 422/400, call `ReauthorizePayment` → persist the new authorization id/expiry → retry capture once. If reauthorize fails (card not eligible, or >29 days) → `AuthorizationExpiredException` with an operator message naming the order and that re-authorization is no longer possible (card auth reauth is a PayPal-account feature — `UNVERIFIED` whether sandbox card auth can be reauthorized; code must treat failure as "cannot renew" and say so, never silently fail the fulfilment).
- **`cancel`** (ADMIN): only from `Authorized`; `VoidPayment(PayPalRequestId="void-{orderId}")`; →`Cancelled`.
- **`refunds`** (ADMIN): only from `Captured`/`PartiallyRefunded`; enforce `RefundedTotal+amount ≤ CapturedGross` (§6e) **before** the call; `RefundCapturedPayment(Body=amount? (omit for full), PayPalRequestId=callerKey, Prefer=representation)`; persist `PaymentRefund` + bump `RefundedTotal`; status →`PartiallyRefunded` or `Refunded`.

---

## 11. Money formatting & saved-card/customer linkage

- **Money:** `Money.Value`/`AmountWithBreakdown.Value` are strings. Format the decimal order total with
  `CultureInfo.InvariantCulture` to the currency's decimal count: 2 for most, 0 for zero-decimal
  currencies (JPY, HUF, TWD, …), 3 for 3-decimal (BHD, KWD, …). Provide a small
  `currency → decimals` helper (default 2). The authorize amount = `Order.Total()` formatted so the
  hold **equals the order total to the cent**; full capture omits `amount` (captures the full
  authorization); partial refund formats the requested amount. Parse PayPal amounts back with
  `decimal.Parse(value, CultureInfo.InvariantCulture)`. `YOUR CALL — not in the map.`
- **Saved-card / customer linkage** (`YOUR CALL — not in the map`): `Customer.id` is PayPal-generated.
  First save for a buyer → `CreatePaymentToken` with `Customer{MerchantCustomerId=buyerId}` (no `Id`);
  read `PaymentTokenResponse.Customer.Id` → store in `ShopperPayPalProfile(BuyerId→PayPalCustomerId)`.
  Later saves → `Customer{Id=stored, MerchantCustomerId=buyerId}`. `GET /api/payment-methods` reads
  our `SavedPaymentMethod` rows for the buyer (source of truth for ownership); we may cross-check
  `ListCustomerPaymentTokens(PayPalCustomerId)` but never show another buyer's tokens. If a buyer has
  no `ShopperPayPalProfile`, they have no saved cards → return empty without calling PayPal.
- **Pay with saved card:** `PaymentSource.Card{VaultId = SavedPaymentMethod.Id}` — only after
  asserting the card belongs to the caller (§6e). **Delete:** `DeletePaymentToken(id)` then remove the
  `SavedPaymentMethod` row so it can neither be listed nor used to pay; deleting one not owned by the
  caller → 404.
- **Direct card vaulting assumption** (`UNVERIFIED`): the business account is "enabled for vaulting
  cards", so `CreatePaymentToken` with `payment_source.card` (raw card) is expected to vault without a
  browser. If PayPal rejects direct-card vaulting, fall back to the two-step `CreateSetupToken(card)`
  → `CreatePaymentToken(token=setupTokenId)` (both in the Vault group). The build should try the
  one-step path first and only add the setup-token step if the sandbox rejects it — not a gap; decide
  at build against live sandbox behaviour.

---

## 12. Testing plan

- **Unit (xUnit, `tests/UnitTests/`):** state-machine transitions on `OrderPayment`; money
  formatting (2/0/3-decimal, round-trip); refund-cap invariant; ownership checks. Reuse
  `tests/UnitTests/Builders/OrderBuilder.cs`.
- **Gateway (xUnit + stub `HttpMessageHandler`):** per `dotnet-testing` — construct
  `PayPalServerSdkClient(new HttpClient(stub), options)`; assert: authorize maps nested
  `Authorizations[0].Id`; capture maps gross/fee/net; Case A error → `ApiException<CaptureAuthorizedPaymentError>`
  with `TryGetError`/`TryGetNoContent`; `SearchTransactions` Case B → `ApiException<RawError>`;
  `ResponseDeserializationException` on drifted body; `SdkConnectionException` triggers the re-read
  (UNKNOWN OUTCOMES tests); `POST` not resent on 503. Read request body **inside** the stub.
- **Endpoint (MSTest, `tests/PublicApiIntegrationTests/`):** `WebApplicationFactory<Program>`, tokens
  via `ApiTokenHelper`; assert `Forbidden` for a shopper hitting `fulfil`/`cancel`/`reconciliation`
  and success for admin; ownership (shopper A cannot see/pay/delete shopper B's order/card);
  top-level `orderId`/`paymentMethodId`/`refundId` present. These run against InMemory DB with the
  PayPal gateway **faked** (register a test `IPayPalPaymentGateway`) so no network is needed in CI.

---

## 13. Self-verification (build session) + user guide

**Build session self-verify (real sandbox, no browser):**
1. `dotnet build` the solution (see env gotchas below). Baseline build of the untouched solution
   first, so new failures are attributable.
2. Run PublicApi with `DOTNET_ROLL_FORWARD=Major`, `UseOnlyInMemoryDatabase=true`, and the four
   `PAYPAL_*` env vars set; bind only to the assigned `APP_PORT_BLOCK_BASE` block; stop any prior
   instance first.
3. Drive the full flow through PublicApi with the sandbox Visa `4111 1111 1111 1111`, future expiry,
   any CVC/name/address — all within one process run (in-memory store is per-process):
   authenticate → `POST /api/orders` → `POST /pay` (real authorization) → `POST /fulfil` (real
   capture; assert captured/fee/net populated) → `POST /refunds` (real partial refund) → second
   order → `POST /api/payment-methods` (vault) → `POST /pay` on the 2nd order with the saved card →
   `GET /api/my-orders`, `GET /api/payment-methods`, `DELETE`, `GET /api/reconciliation`.
4. A reconciliation range covering just-created payments may come back empty (reporting lag) — that
   is expected, not a failure.

**User verification guide (to include in the build session's final message):** step-by-step curl
sequence — (a) get a shopper bearer token from the authenticate endpoint; (b) create an order; (c)
pay with the test card; (d) get an admin token; (e) fulfil; (f) partial refund; (g) save a card as
the shopper; (h) create+pay a second order with `savedPaymentMethodId`; (i) list orders, list/delete
cards; (j) admin reconciliation over a past range — with the exact ports, roles, and expected
top-level id fields at each step.

**Environment gotchas (this machine):** `global.json` pins SDK 8.0.x but only .NET 10 is installed
and the ASP.NET Core 8 runtime is missing → run with `DOTNET_ROLL_FORWARD=Major` (or set
`rollForward: latestMajor`), or install the ASP.NET Core 8 runtime. No LocalDB → `UseOnlyInMemoryDatabase=true`
(data is per-process and ignores migrations — pay/fulfil/refund within the same run; Web and PublicApi
have separate stores, so drive everything through PublicApi). Ensure the HTTPS dev cert is trusted
(`dotnet dev-certs https --check`). Bind only to the assigned port block.

---

## 14. Assumptions

- `POST /api/orders` builds the `Order` directly from catalog item ids + quantities (no Basket),
  snapshotting `CatalogItem.Price` into `OrderItem.UnitPrice`; `BuyerId` = token `ClaimTypes.Name`;
  ship-to address optional (default placeholder if omitted — payment, not shipping, is the focus).
- Operator actions (`fulfil`/`cancel`/`reconciliation`) are restricted to the `Administrators` role;
  `refunds` is likewise an operator action and admin-gated.
- One `OrderPayment` per order (1:1); re-paying is allowed only from `AwaitingPayment`/`AuthorizationFailed`.
- EF in-memory enforces **primary-key** uniqueness at `SaveChanges` (the basis of the claim rows); the
  exact duplicate-key exception type (ArgumentException on InMemory vs DbUpdateException on SqlServer)
  is an EF detail the build confirms and the catch handles both. (Not a PayPal SDK fact.)

## 15. Blockers / STOP conditions

- **No planning Blockers.** Every required capability maps to an operation in §6 (CreateOrder,
  AuthorizeOrder, Capture, Reauthorize, Void, Refund, Vault create/list/delete, SearchTransactions,
  plus the Get* re-reads). The token endpoint honouring `BaseUrl` was verified in source.
- **Runtime STOP conditions (surface to the operator/user, do not work around):** (1) PayPal answers
  a card payment with a challenge requiring browser approval — `OrderStatus.PayerActionRequired` or a
  `payer-action` HATEOAS link / authorization needing approval → STOP and report; do **not** build an
  approval round-trip. (2) A capability the SDK does not expose is discovered mid-build → STOP and
  report the gap; do not invent a workaround.

---

## 16. Skills loaded this session (before deciding how the app uses the SDK)

- `paypal-sdk:dotnet-integrate-pay-pal-server-sdk` (workflow; loaded first)
- `paypal-sdk:dotnet-getting-started` (map layer)
- `paypal-sdk:dotnet-client-initialization`
- `paypal-sdk:dotnet-authentication`
- `paypal-sdk:dotnet-calling-endpoints`
- `paypal-sdk:dotnet-models`
- `paypal-sdk:dotnet-error-handling`
- `paypal-sdk:dotnet-configuration-resilience`
- `paypal-sdk:dotnet-testing`

## REQUIRED READING — load every skill below **before implementation starts** (this sheet deliberately does not carry their contents)

| Skill (plugin-qualified) | Governs the step |
| --- | --- |
| `paypal-sdk:dotnet-client-initialization` | Step 2 — constructing/registering `PayPalServerSdkClient` over a named `HttpClient` (lifetime, handler, DI) |
| `paypal-sdk:dotnet-authentication` | Step 2 — `Oauth2`/`OAuth2ClientCredentials`, startup credential fail-fast |
| `paypal-sdk:dotnet-calling-endpoints` | Step 4 — building each request record; real `PayPalRequestId` vs injected `Idempotency-Key` |
| `paypal-sdk:dotnet-models` | Step 4 — open enums, nullable response chains, `Money` string, unions/`AdditionalProperties` |
| `paypal-sdk:dotnet-error-handling` | Step 4/6 — the error boundary (always needed). Case A `ApiException<{Operation}Error>` incl. `TryGetNoContent(out RawError)`; Case B `ApiException<RawError>` for `SearchTransactions`. **Always-applicable hazard:** a drifted/malformed **2xx** (missing `required` member) or a **non-2xx** body not matching the operation's generated `{Operation}Error` shape surfaces as `ResponseDeserializationException` — an `ApiException` carrying the HTTP status and target type but **not** an `ApiException<TError>`; a catch ladder handling only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`). |
| `paypal-sdk:dotnet-configuration-resilience` | Step 2/4 — retries/timeouts/`CancellationToken` budget, pagination for `SearchTransactions`, logging (card-data redaction, `PAYPALSERVERSDKCLIENT_LOG`) |
| `paypal-sdk:dotnet-testing` | Step 7 — `HttpClient`/stub-handler seam, error & unknown-outcome paths |

(All `dotnet-*` names recur across every APIMatic .NET plugin — load the copies shipped by the
**`paypal-sdk`** plugin; confirm the loaded copy describes this runtime: `RequestOptions` carrying
both a log level and `Hooks`, and a `Core/Hooks/SdkHook.cs` in the SDK source.)
```
