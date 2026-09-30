# PLAN — PayPal payments & saved cards for eShopOnWeb

This is the build plan for adding **PayPal** card payments (authorize → capture-at-fulfilment
→ cancel/refund) and **vaulted (saved) cards** to eShopOnWeb, exposed as JWT endpoints on
`src/PublicApi`. It is **additive**: the catalog/basket/order flow is untouched; we reuse the
existing `Order`/`OrderItem` model and attach payment state to it.

The build session gets the same task text and the same `api-specs/`. This plan front-loads the
exact contract facts extracted from those specs so the build session does not have to re-derive
them, but **the spec is still authoritative** — if any statement here disagrees with a spec file,
the spec wins.

---

## 0. TL;DR of decisions

- **Payment processor flow**: create a PayPal **Orders v2** order with `intent=AUTHORIZE` and a
  card (or vaulted-card) `payment_source`, obtain an **authorization** (hold). Capture the
  authorization via **Payments v2** at fulfilment (gives fee/net). Void to cancel, refund the
  capture to return. Save cards via **Vault v3**. Reconcile via **Transaction Search v1**.
- **Specs needed** (4 of 5): `checkout_orders_v2`, `payments_payment_v2`,
  `vault_payment_tokens_v3`, `transaction_search_v1`. **`billing_subscriptions_v1` is NOT used**
  (no subscriptions in scope).
- **Persistence**: add a `Payment` aggregate (1:1 with the existing `Order`) that holds the whole
  payment/fulfilment state machine + all PayPal ids/statuses + a `Refund` child collection; add a
  `SavedCard` aggregate for vaulted cards. Both keyed to the shopper by `BuyerId`. No parallel
  order model. **No PAN/CVV ever persisted.**
- **Client**: a hand-written typed `HttpClient` (`PayPalApiClient`) + DTOs built to the spec — **no
  third-party PayPal SDK/NuGet**. OAuth2 client-credentials token cached in memory.
- **Identity**: caller = JWT `ClaimTypes.Name` (email), same value the app uses as `Order.BuyerId`.
- **Idempotency**: deterministic `PayPal-Request-Id` per (process-instance, order, operation) plus
  local status guards; refunds keyed by a caller-supplied idempotency key.

---

## 1. Authoritative contract facts (from `api-specs/`)

All four documents declare the **same** server and auth:

- **Server (sandbox)**: `https://api-m.sandbox.paypal.com` (single `servers[0].url`, no
  templating). Production `https://api-m.paypal.com` is **not** in the specs (derive it for
  `Environment=live`; see §3). 
- **Security**: `Oauth2`, `flows.clientCredentials`, `tokenUrl` = `/v1/oauth2/token` (relative to
  the server base). No scopes declared. Each operation requires `Oauth2`. → Get a Bearer token with
  HTTP Basic (`clientId:secret`) `grant_type=client_credentials`, then send
  `Authorization: Bearer <token>` on every call.
- **Money shape everywhere**: `{ "currency_code": "<ISO-4217, 3 chars>", "value": "<string>" }`,
  `value` pattern `^((-?[0-9]+)|(-?([0-9]+)?[.][0-9]+))$`. Both fields required. Format `value`
  with invariant culture and the currency's minor units (2 dp for USD — the sandbox default).
- **Error model** (identical across specs): `error { name, message, debug_id, details[], links[] }`,
  `required: [name, message, debug_id]`; `error_details { field, value, location, issue, description }`,
  `required: [issue]`. **`issue` codes are NOT enumerated in any spec** — treat `issue` as a
  free-form string. Do **not** hard-code issue strings as control flow; drive decisions off HTTP
  status + resource `status`/`expiration_time`, and *surface* `name`/`issue`/`debug_id` in operator
  messages. (Consulting PayPal docs for the meaning of an issue string is allowed as a secondary
  reference; just don't make the contract depend on a string the spec doesn't define.)

### 1.1 Checkout Orders v2 (`checkout_orders_v2`)

- **POST `/v2/checkout/orders`** — body `order_request`, `required: [intent, purchase_units]`.
  - `intent`: enum `["CAPTURE","AUTHORIZE"]` → use **`AUTHORIZE`**.
  - `purchase_units[]` (`required:[amount]`): use `amount = { currency_code, value }` (breakdown is
    optional — omit it; send the order total only). Also set `invoice_id` and `custom_id` to our
    reconciliation reference (see §7). `description` optional.
  - `payment_source.card` (`card_request`): `name` (≤300), `number` (≤19), `expiry` `YYYY-MM`
    (pattern `^[0-9]{4}-(0[1-9]|1[0-2])$`), `security_code` (≤4), `billing_address`
    { `address_line_1`, `address_line_2`, `admin_area_2`=city, `admin_area_1`=state, `postal_code`,
    `country_code` }.
    - **Pay with a saved card**: set `payment_source.card.vault_id = <vault token id>` (schema
      `vault_id`, string 1–255). Do **not** send `number`/`security_code` in that case.
    - **Save at purchase (optional path, not our primary)**: `card.attributes.vault.store_in_vault`
      enum `["ON_SUCCESS"]`. Our primary saved-card path is the standalone Vault v3 endpoint (§1.3);
      this attribute is only a fallback if we ever want to vault during a charge.
    - `card.attributes.verification.method` enum `["SCA_ALWAYS","SCA_WHEN_REQUIRED","3D_SECURE","AVS_CVV"]`,
      default `SCA_WHEN_REQUIRED`. **Do not send `SCA_ALWAYS`** (would force a challenge). Leave
      default.
  - Headers (operation `parameters`): **`PayPal-Request-Id`** (idempotency, keys stored ~6h;
    "mandatory for single-step create calls with a card/vault_id"); **`Prefer`** default
    `return=minimal` → send **`Prefer: return=representation`** so `purchase_units[].payments.*` is
    inline. `PayPal-Auth-Assertion`/`PayPal-Partner-Attribution-Id`/`PayPal-Client-Metadata-Id`
    exist but are not needed for first-party direct-card.
  - Response `order`: `id`, `status` (`order_status` enum
    `["CREATED","SAVED","APPROVED","VOIDED","COMPLETED","PAYER_ACTION_REQUIRED"]`), `payment_source`,
    `purchase_units[].payments` (`payment_collection` = `{ authorizations, captures, refunds }`).
    - **Authorization id/status**: `order.purchase_units[0].payments.authorizations[0].id` and
      `.status` (enum `["CREATED","CAPTURED","DENIED","PARTIALLY_CAPTURED","VOIDED","PENDING"]`,
      plus `amount`, `expiration_time`, `links`).
- **POST `/v2/checkout/orders/{id}/authorize`** — body `order_authorize_request`; same headers.
  Response `order_authorize_response` with the authorization at
  `purchase_units[0].payments.authorizations[0].id`/`.status`. Use this **only if** the create call
  did not already return an authorization (defensive; see §6.2).
- **POST `/v2/checkout/orders/{id}/capture`** exists but we capture via **Payments v2** instead
  (gives `seller_receivable_breakdown` cleanly on the authorization capture).
- **GET `/v2/checkout/orders/{id}`** — full `order` (for diagnostics / reconciliation lookups).
- **SCA / challenge detection (STOP condition)**: order `status == "PAYER_ACTION_REQUIRED"`, or a
  `links[]` entry with `rel == "payer-action"`, or `payment_source.card.authentication_result`
  showing a challenge (`three_d_secure.authentication_status == "C"`). If seen → **STOP and report**
  (do not build an approval round-trip). With the sandbox direct-card account + test card
  `4111 1111 1111 1111` this should not occur.

### 1.2 Payments v2 (`payments_payment_v2`)

Paths: `GET /v2/payments/authorizations/{id}`, `POST .../authorize... /capture`,
`.../reauthorize`, `.../void`, `GET /v2/payments/captures/{id}`,
`POST /v2/payments/captures/{id}/refund`, `GET /v2/payments/refunds/{id}`.

- **GET authorization** → `authorization-2`: `status`
  (`["CREATED","CAPTURED","DENIED","PARTIALLY_CAPTURED","VOIDED","PENDING"]`), `amount`,
  **`expiration_time`** (RFC 3339; end of the honor period), `links`. Note: **there is no `EXPIRED`
  status enum** — detect staleness from `expiration_time` vs now (and/or a capture 4xx).
- **POST capture** `/v2/payments/authorizations/{id}/capture` — body `capture_request`
  (all optional): `amount` (money; omit ⇒ full authorized amount), `final_capture` (bool; send
  `true`), `invoice_id` (≤127), `note_to_payer`. Header **`PayPal-Request-Id`** (idempotency, 45-day
  retention). Response `capture-2`: `id`, `status`
  (`["COMPLETED","DECLINED","PARTIALLY_REFUNDED","PENDING","REFUNDED","FAILED"]`), and
  **`seller_receivable_breakdown`**: `gross_amount` (required), `paypal_fee`, `net_amount` (each
  money). → captured amount = `gross_amount`, fee = `paypal_fee`, net proceeds = `net_amount`.
- **POST reauthorize** `/v2/payments/authorizations/{id}/reauthorize` — body
  `reauthorize_request` (`amount` only). Spec doc: usable **once**, days **4–29** of the honor
  period; after **30 days** you must create a new authorized payment; response `authorization-2`
  with a **new** authorization id. Use for stale-auth renewal at fulfilment.
- **POST void** `/v2/payments/authorizations/{id}/void` — no body; `Prefer: return=minimal` → **204**,
  `return=representation` → 200 `authorization-2`. Effect: `VOIDED`, no further capture possible.
- **GET capture** → `capture-2` (status + `seller_receivable_breakdown`).
- **POST refund** `/v2/payments/captures/{id}/refund` — body `refund_request` (all optional):
  `amount` (omit ⇒ full), `invoice_id`, `note_to_payer`. Header **`PayPal-Request-Id`** (idempotency,
  45-day retention). Response `refund`: `id`, `status`
  (`["CANCELLED","FAILED","PENDING","COMPLETED"]`), and **`seller_payable_breakdown`** including
  **`total_refunded_amount`** (cumulative refunded from the capture to date — authoritative cap
  check) plus `gross_amount`, `paypal_fee`, `net_amount`.
- **GET refund** → `refund` (same shape) for status polling.

### 1.3 Vault Payment Tokens v3 (`vault_payment_tokens_v3`)

- **POST `/v3/vault/payment-tokens`** (create a permanent saved card) — body
  `payment_token_request`, `required:[payment_source]`. Header `PayPal-Request-Id` (keys stored 3h).
  - `payment_source.card`: `name` (1–300), `number` PAN pattern `^[0-9]{13,19}$`, `expiry` `YYYY-MM`,
    `security_code` `^[0-9]{3,4}$`, `billing_address` (`required:[country_code]`).
  - `customer` (top-level, optional): `customer.id` (PayPal-generated, ≤22) to group a shopper's
    cards; `customer.merchant_customer_id` (1–64) for our own id. We pass no `customer.id` on the
    first save (PayPal generates it), store it, and reuse it for that shopper's later saves.
  - Response `payment_token_response`: **`id`** = the vault token (`vault_id`; persist this),
    `customer.id`, and **safe display only** under `payment_source.card`: `last_digits` (2–4),
    `brand` (enum incl. VISA/MASTERCARD/AMEX/…), `expiry`, `name`, `type`
    (`["CREDIT","DEBIT","PREPAID","STORE","UNKNOWN"]`). **No `number`/PAN and no `security_code` in
    any response.** There is **no `status` field** on this response — success ⇒ `id` present.
  - Alternative two-step (setup token) is **only** for instruments needing buyer approval/3DS. For a
    raw card on the direct-card sandbox account it is unnecessary; use the direct create above.
- **POST `/v3/vault/setup-tokens`** → `setup_token_response` has `status` (`payment_token_status`
  enum `["CREATED","PAYER_ACTION_REQUIRED","APPROVED","VAULTED","TOKENIZED"]`). If we ever hit this
  path and see **`PAYER_ACTION_REQUIRED`** + an approval `links[]` → **STOP and report** (browser
  approval out of scope). Not expected for our card path.
- **GET `/v3/vault/payment-tokens/{id}`** → `payment_token_response` (safe display).
- **GET `/v3/vault/payment-tokens?customer_id=...`** (`ListCustomerPaymentTokens`): `customer_id`
  **required**, `page` (default 1), `page_size` (default 5), `total_required` (default false).
  Response `payment_tokens[]` + `total_items`/`total_pages`. We rely on our **local** `SavedCard`
  table for listing (source of truth for ownership); this endpoint is a cross-check only.
- **DELETE `/v3/vault/payment-tokens/{id}`** → **204**. Idempotent.
- **Use to pay**: pass the token `id` as `payment_source.card.vault_id` in Orders v2 (§1.1).

### 1.4 Transaction Search v1 (`transaction_search_v1`)

- **GET `/v1/reporting/transactions`** — query params: **`start_date`/`end_date` required**,
  RFC 3339 date-time (pattern enforces seconds; fractional + offset/Z allowed);
  **max range 31 days**; `fields` (default `transaction_info`; use `transaction_info` or `all`);
  `transaction_status` (`D/P/S/V`); `balance_affecting_records_only` (default `Y`, set **`N`** to
  see everything); `page` (1-based, default 1); `page_size` (default 100, **max 500**).
  Response `search_response`: `transaction_details[]`, `page`, `total_items`, `total_pages`,
  `last_refreshed_datetime`, `links`.
  - `transaction_details[].transaction_info`: `transaction_id` (≤24), `transaction_status`
    (`D`enied/`P`ending/`S`uccess/`V`reversed), `transaction_amount` (money), `fee_amount` (money),
    `transaction_initiation_date`, `transaction_updated_date`, `paypal_reference_id`,
    **`invoice_id`** (≤127), `custom_field` (≤127). → match on **`invoice_id`** (our reconciliation
    ref).
  - **Pagination**: loop `page = 1..total_pages` (per window). **Data lag: up to 3 hours** — a range
    covering just-created payments may legitimately be **empty** (expected; not a gap).
  - Error extension `RESULTSET_TOO_LARGE` carries `total_items`/`maximum_items` → narrow the range.

---

## 2. Existing codebase facts the build must respect

- **PublicApi endpoint style**: minimal-API endpoint classes implementing
  `MinimalApi.Endpoint.IEndpoint<...>` (see `CatalogItemEndpoints/*`). Each class: `AddRoute` maps the
  route (`app.MapPost("api/...", [Authorize(...)] async (req, deps, ClaimsPrincipal user) => ...)`) and
  `HandleAsync` does the work. `AddEndpoints()`/`MapEndpoints()` in `Program.cs` auto-discover them.
  Constructor injection works (e.g. `IUriComposer`, `IMapper`); additional services + `ClaimsPrincipal`
  bind as handler-lambda parameters. Routes use **no leading slash** (`"api/orders"`). Match this style
  (do **not** add Ardalis `EndpointBaseAsync` controllers for new work — keep it consistent with the
  catalog endpoints).
- **Auth**: JWT bearer already configured in `Program.cs`. Admin gate:
  `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
  Shopper gate: `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
  Caller identity = `user.Identity!.Name` (JWT `ClaimTypes.Name`, the email). This equals
  `Order.BuyerId` as used by the Web app (`Web/Controllers/OrderController.cs`).
- **Seeded users** (`AppIdentityDbContextSeed`): `admin@microsoft.com` (role `Administrators`) and
  `demouser@microsoft.com` (no role); password `Pass@word1` (`AuthorizationConstants.DEFAULT_PASSWORD`).
  `POST /api/authenticate` returns a JWT for these.
- **Order model** (`ApplicationCore/Entities/OrderAggregate`): `Order(buyerId, Address, List<OrderItem>)`,
  `Order.Total()` sums `UnitPrice*Units`. `OrderItem(CatalogItemOrdered, unitPrice, units)`.
  `CatalogItemOrdered(catalogItemId, productName, pictureUri)` (snapshot). `Address` is a required
  owned type (`OrderConfiguration`), so a new order **must** have a non-null ship-to address.
- **Persistence**: `CatalogContext` (EF Core) with `EfRepository<T> : IRepository<T>` registered
  generically for any `IAggregateRoot`. Specs use Ardalis.Specification. In-memory provider selected by
  config `UseOnlyInMemoryDatabase=true`; **it ignores migrations and resets each run** (per task).
- **Existing unused bits**: `BuyerAggregate/Buyer` + `PaymentMethod` exist but are **not** mapped in
  `CatalogContext` and are unused. Do **not** repurpose them; add fresh aggregates (below) so ownership
  and mapping are explicit.
- **Central package management**: `Directory.Packages.props`. We need **no new NuGet package** — use
  `System.Net.Http`/`System.Net.Http.Json` (already available) + `System.Text.Json` (8.0.3, referenced).
  Do not add a PayPal SDK.
- **Error surfacing**: `Middleware/ExceptionMiddleware.cs` serializes `{ StatusCode, Message }`. Extend
  it to map our new exception types (below) to proper status codes + clear messages.

---

## 3. Configuration & credentials (bind `PayPal:` exactly)

Create `PayPalOptions` (in `Infrastructure/PayPal/`), bound from the **`PayPal`** section:

```
ClientId, ClientSecret, Environment, Currency, BaseUrl   // keys: PayPal:ClientId, etc.
```

**Env-var → config mapping (required).** The credentials arrive as `PAYPAL_CLIENT_ID`,
`PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`, `PAYPAL_CURRENCY` — single underscores, which .NET's
default env provider (which expects `PayPal__ClientId`) will **not** pick up. In `Program.cs`, add an
explicit in-memory mapping *before* building, so `PayPal:` keys are populated from the env vars (only
when present, so `appsettings`/user-secrets can still override for other accounts):

```csharp
var paypalCfg = new Dictionary<string,string?>();
void MapEnv(string env, string key){ var v = Environment.GetEnvironmentVariable(env); if(!string.IsNullOrWhiteSpace(v)) paypalCfg[key]=v; }
MapEnv("PAYPAL_CLIENT_ID","PayPal:ClientId");
MapEnv("PAYPAL_CLIENT_SECRET","PayPal:ClientSecret");
MapEnv("PAYPAL_ENVIRONMENT","PayPal:Environment");
MapEnv("PAYPAL_CURRENCY","PayPal:Currency");
MapEnv("PAYPAL_BASE_URL","PayPal:BaseUrl");   // optional override
builder.Configuration.AddInMemoryCollection(paypalCfg);
builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection("PayPal"));
```

**Never** write credential *values* into any repo file (appsettings included) — only key/var *names*.
Do not add a real `PayPal` section with secrets to `appsettings*.json`. (An empty/placeholder
`"PayPal": { "Environment": "sandbox" }` with no secrets is acceptable but not required.)

**Base-URL resolution** (`PayPalOptions.ResolveBaseUrl()`), used for **every** call incl. the token
request:
1. If `BaseUrl` is set (non-empty) → use it **verbatim**.
2. Else if `Environment` case-insensitively is `live`/`production` → `https://api-m.paypal.com`.
3. Else (default/`sandbox`) → `https://api-m.sandbox.paypal.com` (the spec's server).
Token endpoint = `{base}/v1/oauth2/token`. (Production host is well-known and not in the sandbox spec;
this is a documented default, not an invented endpoint — not a gap.)

`Currency` defaults to `USD` if unset. Determine decimal places from the currency (2 for USD; keep a
tiny map for 0-dp currencies like JPY if you want, else default 2) and format money `value`
accordingly with `CultureInfo.InvariantCulture`.

**Process instance id**: generate one `Guid` at startup (e.g. a singleton holding
`InstanceId = Guid.NewGuid().ToString("N")[..8]`). Use it inside `PayPal-Request-Id` values and
`invoice_id` so they stay **unique across in-memory restarts** (order ids restart at 1 each run, and
PayPal retains request-ids 6h–45d — without the instance id a replay could collide with a prior run).

---

## 4. PayPal client layer (`src/Infrastructure/PayPal/`)

Hand-written, spec-shaped. No third-party client.

### 4.1 Token provider — `PayPalTokenProvider : IPayPalTokenProvider`
- `Task<string> GetAccessTokenAsync(ct)`: POST `{base}/v1/oauth2/token`,
  `Authorization: Basic base64(clientId:secret)`, body
  `grant_type=client_credentials` (form-urlencoded). Parse `access_token` + `expires_in`; cache in
  `IMemoryCache` (already registered) until `expires_in - 60s`. Thread-safe (lock/`SemaphoreSlim`)
  so concurrent calls don't stampede.
- On 401 from a business call, invalidate the cache and retry once.

### 4.2 Raw API client — `PayPalApiClient` (typed `HttpClient`, registered via `AddHttpClient`)
Base address = `ResolveBaseUrl()`. A `DelegatingHandler` (or inline) attaches
`Authorization: Bearer <token>` from the token provider and an `Accept: application/json`. One method
per spec operation used, each taking/returning hand-written DTOs and an optional `payPalRequestId`
and `prefer` argument:
- `CreateOrderAsync(OrderRequest, requestId, prefer="return=representation")` → `OrderResponse`
- `AuthorizeOrderAsync(orderId, requestId, prefer)` → `OrderResponse`
- `GetOrderAsync(orderId)` → `OrderResponse`
- `GetAuthorizationAsync(authId)` → `AuthorizationResponse`
- `CaptureAuthorizationAsync(authId, CaptureRequest, requestId)` → `CaptureResponse`
- `ReauthorizeAsync(authId, ReauthorizeRequest, requestId)` → `AuthorizationResponse`
- `VoidAuthorizationAsync(authId, requestId)` → 204/200
- `RefundCaptureAsync(captureId, RefundRequest, requestId)` → `RefundResponse`
- `GetRefundAsync(refundId)` → `RefundResponse`
- `CreatePaymentTokenAsync(PaymentTokenRequest, requestId)` → `PaymentTokenResponse`
- `DeletePaymentTokenAsync(tokenId)` → 204
- `GetPaymentTokenAsync(tokenId)` → `PaymentTokenResponse` (optional)
- `SearchTransactionsAsync(start, end, page, pageSize, fields, balanceOnly="N")` → `SearchResponse`

**DTOs** (`Infrastructure/PayPal/Models/`): mirror only the fields we use, with
`System.Text.Json` attributes for snake_case (`[JsonPropertyName]` or a snake-case naming policy).
Money = `{ string CurrencyCode; string Value; }`. Include: `OrderRequest`/`OrderResponse`
(+ nested `PurchaseUnit`, `PaymentSource`, `Card`, `BillingAddress`, `PaymentCollection`,
`Authorization`, `Capture`, `SellerReceivableBreakdown`), `CaptureRequest`/`CaptureResponse`,
`ReauthorizeRequest`, `RefundRequest`/`RefundResponse` (+ `SellerPayableBreakdown`),
`PaymentTokenRequest`/`PaymentTokenResponse` (+ `CardResponse`), `SearchResponse`
(+ `TransactionDetail`/`TransactionInfo`), and `PayPalError`/`PayPalErrorDetail`.

**Error handling**: on non-2xx, deserialize `error`, throw `PayPalApiException(httpStatus, name,
message, debugId, issues[])`. Carry `issue`/`name`/`debug_id` for operator messages. Never include
request bodies (card data) in exception messages or logs.

**Redaction / no-leak (mandatory)**:
- Never log request bodies for card/vault/order-create calls, never log `security_code`/`number`,
  never log the `Authorization` header or client secret/token.
- Set the PayPal `HttpClient` category log level to `Warning` (avoid `HttpClient` info logging of
  URIs with ids is fine; bodies are never logged by default — just don't add body logging).
- Model DTOs so a stray `ToString()`/serialization of a request card never reaches a logger.

### 4.3 Domain gateway — `IPayPalGateway` (interface in `ApplicationCore/Interfaces`, impl in Infrastructure)
Translate DTOs ↔ domain-friendly results so `ApplicationCore` never sees HTTP/JSON:
- `AuthorizeResult AuthorizeAsync(decimal amount, string currency, CardDetails? card, string? vaultId, string invoiceId, string requestId, ct)`
  — builds the create-order body, performs create (+authorize fallback), returns
  `{ payPalOrderId, authorizationId, authorizationStatus, expiresAt, cardBrand, cardLast4 }`; or a
  typed **challenge** signal → caller maps to STOP/report.
- `CaptureResult CaptureAsync(string authorizationId, decimal amount, string currency, string invoiceId, string requestId, ct)`
  — returns `{ captureId, status, grossAmount, paypalFee, netAmount }`.
- `AuthorizeResult ReauthorizeAsync(string authorizationId, decimal amount, string currency, string requestId, ct)`.
- `void VoidAsync(string authorizationId, string requestId, ct)`.
- `AuthorizationInfo GetAuthorizationAsync(string authorizationId, ct)` → `{ status, expiresAt }`.
- `RefundResult RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, ct)`
  — returns `{ refundId, status, totalRefunded }`.
- `SavedCardInfo SaveCardAsync(CardDetails card, string? customerId, string requestId, ct)`
  — returns `{ vaultId, customerId, brand, last4, expiry, type }`.
- `void DeleteCardAsync(string vaultId, ct)`.
- `IReadOnlyList<PayPalTransaction> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, ct)`
  — handles pagination + 31-day chunking internally; returns flattened transactions.

`CardDetails` is a transient input DTO (number/expiry/cvv/name/address) that lives only for the
duration of the call — **never** stored, never logged.

---

## 5. Domain model & persistence (`ApplicationCore` + `Infrastructure`)

### 5.1 New entities
Add under `ApplicationCore/Entities/PaymentAggregate/`:

- `enum PaymentStatus { AwaitingPayment, Authorized, Captured, PartiallyRefunded, Refunded, Cancelled, Failed }`
- `Payment : BaseEntity, IAggregateRoot`
  - `int OrderId` (1:1 with `Order`, unique), `string BuyerId`, `string CurrencyCode`,
    `decimal Amount` (order-total snapshot), `PaymentStatus Status` (init `AwaitingPayment`),
    `string InvoiceReference` (our reconciliation key, e.g. `ESHOP-{instanceId}-{orderId}`).
  - Hold: `string? PayPalOrderId`, `string? AuthorizationId`, `string? AuthorizationStatus`,
    `DateTimeOffset? AuthorizationExpiresAt`.
  - Capture: `string? CaptureId`, `string? CaptureStatus`, `decimal? CapturedAmount`,
    `decimal? PayPalFee`, `decimal? NetAmount`.
  - Display: `string? CardBrand`, `string? CardLast4`.
  - `private readonly List<Refund> _refunds`; `IReadOnlyCollection<Refund> Refunds`.
  - Domain methods enforcing the state machine + invariants:
    `MarkAuthorized(orderId, authId, status, expiresAt, brand, last4)`,
    `RenewAuthorization(newAuthId, expiresAt)`, `MarkCaptured(captureId, gross, fee, net)`,
    `MarkCancelled()`, `AddRefund(Refund)` (throws if `TotalRefunded + amount > CapturedAmount`),
    `RecomputeStatusAfterRefund()` (→ `Refunded` if total == captured else `PartiallyRefunded`),
    `MarkFailed(reason)`.
  - `decimal TotalRefunded()` = sum of completed/pending refunds.
- `Refund : BaseEntity`
  - `string PayPalRefundId`, `decimal Amount`, `string Status`, `string IdempotencyKey`,
    `DateTimeOffset CreatedAt`.

Add under `ApplicationCore/Entities/SavedCardAggregate/`:

- `SavedCard : BaseEntity, IAggregateRoot`
  - `string BuyerId`, `string PayPalVaultId`, `string? PayPalCustomerId`,
    `string Brand`, `string Last4`, `string Expiry` (`YYYY-MM`), `string? CardType`,
    `string? Label`, `DateTimeOffset CreatedAt`.
  - **No PAN, no CVV** — invariant of the type.

### 5.2 EF wiring (`Infrastructure/Data`)
- Add `DbSet<Payment>`, `DbSet<SavedCard>` to `CatalogContext` (Refund is a child collection of
  Payment).
- Add `Config/PaymentConfiguration`, `Config/RefundConfiguration`, `Config/SavedCardConfiguration`
  (property lengths, `decimal(18,2)` for money, unique index on `Payment.OrderId`, unique index on
  `(SavedCard.BuyerId, SavedCard.PayPalVaultId)`, `Payment.Refunds` as a navigation with
  `PropertyAccessMode.Field`). `ApplyConfigurationsFromAssembly` already picks these up.
- `Payment` and `SavedCard` are `IAggregateRoot` ⇒ `IRepository<Payment>`/`IRepository<SavedCard>`
  resolve through the existing generic `EfRepository<T>` registration — no new DI needed for repos.
- **Migrations**: the run uses the in-memory provider (ignores migrations), so migrations are **not
  required** to verify. Optionally add EF migrations for the SQL Server path as a nice-to-have; not on
  the critical path.

### 5.3 Specifications (`ApplicationCore/Specifications`)
- `PaymentByOrderIdSpec(int orderId)` — `Include(p => p.Refunds)`.
- `PaymentsByBuyerSpec(string buyerId)` — for `my-orders`, `Include(Refunds)`.
- `OrderWithItemsByIdSpec` already exists (reuse for order lookups).
- `SavedCardsByBuyerSpec(string buyerId)`, `SavedCardByIdForBuyerSpec(int id, string buyerId)`.

---

## 6. Application services (`ApplicationCore/Services`, interfaces in `ApplicationCore/Interfaces`)

Orchestrate repos + `IPayPalGateway`. Keep `ApplicationCore` free of HTTP.

### 6.1 `PaymentOrderService` — place order
- `Task<int> PlaceOrderAsync(string buyerId, IEnumerable<(int catalogItemId,int qty)> items, Address ship)`:
  load `CatalogItem`s by id (via `CatalogItemsSpecification`), guard all exist and qty ≥ 1, snapshot
  into `OrderItem`(`CatalogItemOrdered`, `UnitPrice = catalogItem.Price`, `Units = qty`), build `Order`
  (reusing the existing aggregate), persist. Then create a `Payment` (`AwaitingPayment`, `Amount =
  order.Total()`, `CurrencyCode = options.Currency`, `InvoiceReference = ESHOP-{instanceId}-{orderId}`)
  and persist. Return `order.Id`. (Ship-to address: taken from request; if omitted, use a fixed
  placeholder address since the model requires one — payment, not shipping, is the focus.)

### 6.2 `PaymentService` — pay / fulfil / cancel / refund
All methods first load the `Payment` (+ `Order`) and **enforce ownership** for shopper actions
(`Payment.BuyerId == caller`), else throw `NotFoundException` (don't leak).

- **Pay (authorize)** — `AuthorizePaymentAsync(orderId, caller, CardDetails? card, int? savedCardId)`:
  - Idempotency guard: if `Status ∈ {Authorized, Captured, PartiallyRefunded, Refunded}` → **no-op**,
    return current state (200). Serialize concurrent double-clicks with an in-process per-order lock
    (`SemaphoreSlim` keyed by orderId) since the in-memory provider has no row locking.
  - Resolve payment source: exactly one of `card` or `savedCardId`. If `savedCardId`, load `SavedCard`
    scoped to caller (404 if not theirs / deleted) → `vaultId`.
  - `requestId = auth-{instanceId}-{orderId}` (deterministic ⇒ PayPal dedups a retried create).
  - Call `gateway.AuthorizeAsync(amount=Payment.Amount, currency, card, vaultId, invoiceRef, requestId)`.
    Internally: create order (`intent=AUTHORIZE`, `Prefer: return=representation`); if the response
    already carries an authorization, use it; **else** call `/authorize`. If a challenge/PAYER_ACTION
    is detected → throw `PaymentChallengeException` → endpoint returns a clear "manual approval
    required — STOP" style error (do not build approval round-trip).
  - `Payment.MarkAuthorized(...)` (store PayPalOrderId, authId, status, expiresAt, brand, last4);
    persist. Amount held must equal order total to the cent (it does — we send `Payment.Amount`).
- **Fulfil (capture)** — `FulfilOrderAsync(orderId)` (**admin**):
  - Guard `Status == Authorized` (if already `Captured`/beyond → idempotent no-op returning current
    capture; if `AwaitingPayment` → 409 "not authorized yet"; if `Cancelled` → 409).
  - **Stale-auth handling**: `GetAuthorizationAsync`; if `AuthorizationExpiresAt` is past (with a small
    buffer) or status not capturable → `ReauthorizeAsync` (days 4–29) to get a new auth id
    (`RenewAuthorization`). If reauthorize fails or is out of window (≥30 days) → throw
    `AuthorizationNotRenewableException` → endpoint returns an **operator-actionable** message:
    e.g. *"The payment hold for order {id} expired and can no longer be renewed (PayPal
    reauthorization window is days 4–29; after 30 days a new payment is required). Ask the shopper to
    place and pay for a new order. [PayPal issue: {issue}, debug_id: {debugId}]"*.
  - `requestId = capture-{instanceId}-{orderId}`. `gateway.CaptureAsync(authId, amount, currency,
    invoiceRef, requestId)`. On a capture 4xx that indicates the hold went stale between the check and
    the call, do one reauthorize+retry, then surface the same actionable error if still failing.
  - `Payment.MarkCaptured(captureId, gross, fee, net)`; persist. Payment now shows captured amount,
    PayPal fee, net proceeds (from `seller_receivable_breakdown`).
- **Cancel (void)** — `CancelOrderAsync(orderId)` (**admin**):
  - Guard `Status == Authorized` (before fulfilment). If `Captured` → 409 "already captured; use
    refund". If already `Cancelled` → idempotent no-op.
  - `gateway.VoidAsync(authId, requestId=void-{instanceId}-{orderId})`; `Payment.MarkCancelled()`;
    persist. No money moved (hold released).
- **Refund** — `RefundOrderAsync(orderId, caller, decimal? amount, string idempotencyKey)`:
  - Guard `Status ∈ {Captured, PartiallyRefunded}` (must be after fulfilment). Ownership enforced
    (shopper-scoped — see §7 note; if the task's operator model is preferred for refunds it is still
    shopper-scoped per the spec text "return after fulfilment" under shopper endpoints — see §7).
  - **Idempotency**: if a `Refund` with this `IdempotencyKey` already exists on the payment → return
    the **same** `refundId` without calling PayPal again. Distinct keys ⇒ new partial refund.
  - **Cap invariant**: reject if `TotalRefunded + amount > CapturedAmount` (full refund if `amount`
    omitted = `CapturedAmount - TotalRefunded`). Never refundable beyond captured.
  - `gateway.RefundAsync(captureId, amount, currency, idempotencyKey)` (idempotencyKey ⇒
    `PayPal-Request-Id`). Cross-check `seller_payable_breakdown.total_refunded_amount` ≤ captured.
  - `Payment.AddRefund(new Refund(paypalRefundId, amount, status, idempotencyKey))` +
    `RecomputeStatusAfterRefund()`; persist. Return the PayPal `refundId`.

### 6.3 `SavedCardService` — save / list / delete
- `SaveCardAsync(buyerId, CardDetails card, label)`: look up an existing `PayPalCustomerId` for the
  buyer (from any prior `SavedCard`), pass it (or null) to `gateway.SaveCardAsync`; persist a
  `SavedCard` (buyerId, vaultId, customerId, brand, last4, expiry, type, label). Return `SavedCard.Id`.
- `ListCardsAsync(buyerId)`: local `SavedCardsByBuyerSpec`.
- `DeleteCardAsync(buyerId, id)`: load scoped to buyer (404 if not theirs); `gateway.DeleteCardAsync
  (vaultId)`; delete local row. After this it neither lists nor is resolvable by `AuthorizePaymentAsync`
  (which looks up by local id scoped to buyer) ⇒ no longer usable to pay.

### 6.4 `ReconciliationService`
- `ReconcileAsync(from, to)` (**admin**): `gateway.SearchTransactionsAsync(from, to)` (chunks >31-day
  ranges into ≤31-day windows, pages each `1..total_pages`, `page_size=500`, `balance_affecting_records_only=N`,
  `fields=transaction_info`). Load local `Payment`s whose activity falls in range (by capture/refund
  presence and `InvoiceReference`). Produce three buckets:
  1. **Matched**: PayPal txn `invoice_id` matches a local `Payment.InvoiceReference` — include both
     sides (amounts, statuses) and flag any amount/status mismatch.
  2. **PayPal-only**: txn in range with no matching local payment (or unknown `invoice_id`).
  3. **eShop-only**: local payments that should have a PayPal txn in range but none was returned.
  Return a report object. **Empty result over a recent range is expected** (≤3h reporting lag) — the
  report is "correct", not a gap.

---

## 7. PublicApi endpoints (routes, auth, shapes)

Folder `src/PublicApi/OrderPaymentEndpoints/` (orders + payment actions) and
`src/PublicApi/PaymentMethodEndpoints/` (saved cards). One class per endpoint, minimal-API style,
each with request/response record types in sibling files (mirroring `CatalogItemEndpoints`). Register
services in `Program.cs` (or a small `PayPalServiceExtensions.AddPayPalIntegration(config)` in
PublicApi that binds options, `AddHttpClient<PayPalApiClient>`, token provider, gateway, and the three
app services).

| Route | Method | Auth | Body / query | Response (top-level id in **bold**) |
|---|---|---|---|---|
| `api/orders` | POST | shopper | `{ items:[{catalogItemId,quantity}], shipToAddress? }` | `{ **orderId**, status, total, currency }` (201) |
| `api/orders/{orderId}/pay` | POST | shopper (owner) | `{ card:{number,expiry,securityCode,name,billingAddress{...}} }` **or** `{ savedCardId }` | `{ orderId, status:"Authorized", authorizationId }` |
| `api/orders/{orderId}/fulfil` | POST | **admin** | — | `{ orderId, status:"Captured", captureId, capturedAmount, paypalFee, netAmount, currency }` |
| `api/orders/{orderId}/cancel` | POST | **admin** | — | `{ orderId, status:"Cancelled" }` |
| `api/orders/{orderId}/refunds` | POST | shopper (owner) | `{ amount?, idempotencyKey }` | `{ **refundId**, status, amount, totalRefunded }` (201) |
| `api/my-orders` | GET | shopper | — | `[{ orderId, orderDate, total, currency, status, authorizationId?, captureId?, capturedAmount?, paypalFee?, netAmount?, refunds:[{refundId,amount,status}] }]` |
| `api/reconciliation?from=&to=` | GET | **admin** | ISO-8601 `from`/`to` | `{ from, to, matched:[...], payPalOnly:[...], eShopOnly:[...] }` |
| `api/payment-methods` | POST | shopper | `{ card:{number,expiry,securityCode,name,billingAddress{...}}, label? }` | `{ **paymentMethodId**, brand, last4, expiry, type }` (201) |
| `api/payment-methods` | GET | shopper | — | `[{ paymentMethodId, brand, last4, expiry, type, label }]` |
| `api/payment-methods/{paymentMethodId}` | DELETE | shopper (owner) | — | 204 |

- **Response identifiers** (mandated top-level fields): `orderId` from `POST /api/orders`,
  `paymentMethodId` from `POST /api/payment-methods`, `refundId` from `POST /api/orders/{id}/refunds`.
  `refundId` = the PayPal refund id (canonical + reconciliable); a repeat under the same
  `idempotencyKey` returns the **same** `refundId`.
- **Identity**: bind `ClaimsPrincipal user` in each handler; `buyerId = user.Identity!.Name`. For
  shopper endpoints, every read/write is scoped to `buyerId`; cross-shopper access → `Results.NotFound()`
  (no existence leak). For admin endpoints, no ownership filter (operator acts on any order).
- **Operator vs shopper split** (per task): **fulfil, cancel, reconciliation = admin-only**; everything
  else shopper-scoped. **Refund** is listed under Flow 1's shopper "return after fulfilment" and takes a
  caller idempotency key ⇒ implement as **shopper-scoped, owner-only** (the shopper returns their own
  order). Keep each action separately invocable — no do-everything route.
- **Validation**: reject empty item lists, non-positive quantities, unknown catalog ids (400);
  `pay` requires exactly one of `card`/`savedCardId` (400 otherwise). Card fields validated against the
  spec patterns (expiry `YYYY-MM`, number digits, cvv 3–4) before calling PayPal.
- **Swagger**: add `.Produces<T>()` + `.WithTags(...)` like existing endpoints so the new endpoints
  appear in the existing Swagger UI.

---

## 8. Error handling & STOP conditions

- New exceptions (in `ApplicationCore/Exceptions`): `NotFoundException` (404),
  `PaymentConflictException` (409 — wrong state, e.g. capture before authorize, cancel after capture,
  refund over cap), `PaymentChallengeException` (SCA/PAYER_ACTION — 4xx + STOP message),
  `AuthorizationNotRenewableException` (409/422 — operator-actionable), and `PayPalApiException`
  (from the client; map to `502 Bad Gateway` with `name`/`issue`/`debug_id` in the message, never card
  data). Extend `Middleware/ExceptionMiddleware` to map these to status codes + `{StatusCode,Message}`.
- **STOP-and-report (do NOT auto-handle)**:
  - Card payment returns a browser **challenge** (order `PAYER_ACTION_REQUIRED` / `rel:"payer-action"` /
    3DS `authentication_status=="C"`): surface a clear error telling the operator a browser approval
    would be required; do not implement an approval round-trip. (Task: STOP and report.)
  - Vault **setup-token** path returning `PAYER_ACTION_REQUIRED`: same.
  - A capability the specs genuinely don't cover: **none identified** for this task (see §11). If the
    build hits one, STOP and report rather than inventing endpoints/fields.

---

## 9. Idempotency summary

| Operation | Mechanism |
|---|---|
| Create order | Not money-moving; a repeat simply creates another order. (No idempotency required by task.) |
| Pay/authorize | Local status guard (no-op if already authorized+) + per-order in-process lock + deterministic `PayPal-Request-Id = auth-{instanceId}-{orderId}`. |
| Fulfil/capture | Local guard (no-op if already captured) + `PayPal-Request-Id = capture-{instanceId}-{orderId}`. |
| Cancel/void | Local guard (no-op if already cancelled) + `PayPal-Request-Id = void-{instanceId}-{orderId}`. |
| Refund | Caller `idempotencyKey` → dedup locally (same `refundId` returned) **and** sent as `PayPal-Request-Id`. Distinct keys ⇒ distinct partial refunds. Cap invariant enforced. |
| Save card | `PayPal-Request-Id` from a fresh guid (or `savecard-{instanceId}-{n}`) — best-effort. |

`instanceId` keeps request-ids/`invoice_id` unique across in-memory restarts (see §3).

---

## 10. Build order (checklist)

1. **Config**: `PayPalOptions` + env mapping + options binding + base-URL resolver + instance-id
   singleton (§3). Build.
2. **Client layer**: DTOs, `PayPalTokenProvider`, `PayPalApiClient` (+ auth handler), `PayPalApiException`,
   redaction (§4.1–4.2). Register `AddHttpClient<PayPalApiClient>`. Build.
3. **Gateway**: `IPayPalGateway` (ApplicationCore) + `PayPalGateway` (Infrastructure) mapping DTOs ↔
   domain results, incl. create+authorize fallback, challenge detection, transaction pagination/chunking
   (§4.3, §6.4). Build.
4. **Domain**: `Payment`/`Refund`/`SavedCard` + enum + EF configs + `CatalogContext` DbSets + specs
   (§5). Build.
5. **App services**: `PaymentOrderService`, `PaymentService`, `SavedCardService`, `ReconciliationService`
   + interfaces (§6). Register in DI. Build.
6. **Endpoints**: order/payment + payment-method endpoints with request/response types, auth attributes,
   response-id fields (§7). Build.
7. **Exception mapping**: extend `ExceptionMiddleware` (§8). Build.
8. **Wire-up**: ensure `Program.cs` registers everything and `AddEndpoints()`/`MapEndpoints()` picks up
   the new endpoints; add optional `AddPayPalIntegration` extension. Build the full solution
   (`eShopOnWeb.sln`).
9. **Tests** (recommended, not required to pass task): unit tests for `Payment` invariants (refund cap,
   status transitions), refund idempotency, and reconciliation matching using a fake `IPayPalGateway`;
   a `PublicApi` functional test that drives create→pay→fulfil→refund with a **mock** gateway (real
   sandbox is the manual self-verify below). Do not put live PayPal calls in the automated test run.

**Environment for building/running** (per task gotchas):
- SDK/runtime: only .NET 10 SDK + no ASP.NET Core 8 runtime present. Run with
  `DOTNET_ROLL_FORWARD=Major`. The build session **may** set `global.json` `rollForward` to
  `latestMajor` — that is a build-session change (this planning session must not). Prefer the env var
  if avoiding file changes.
- Run PublicApi with `UseOnlyInMemoryDatabase=true` (data resets each run — do the whole
  create→pay→fulfil→refund within one run).
- Bind only to the assigned port block (`APP_PORT_BLOCK_BASE … +SIZE-1`); `launchSettings` already
  points there. Stop the previous instance before starting a new one.
- Ensure dev cert trusted (`dotnet dev-certs https --check`) since `UseHttpsRedirection()` is on.

---

## 11. Self-verification guide (build session runs this; also hand to the user)

Prereqs: env vars `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT=sandbox`,
`PAYPAL_CURRENCY=USD` set in the shell; `UseOnlyInMemoryDatabase=true`; `DOTNET_ROLL_FORWARD=Major`.

1. **Start PublicApi** on the assigned ports, e.g.:
   `DOTNET_ROLL_FORWARD=Major dotnet run --project src/PublicApi -- --urls "https://localhost:<PORT>"`
   with `UseOnlyInMemoryDatabase=true`. Confirm Swagger loads at `/swagger`.
2. **Tokens**: `POST /api/authenticate {username,password}` for `demouser@microsoft.com` (shopper) and
   `admin@microsoft.com` (operator), password `Pass@word1`. Capture both JWTs.
3. **Catalog**: `GET /api/catalog-items` to pick real `catalogItemId`s + prices (seeded).
4. **Flow 1 — pay**:
   - `POST /api/orders` (shopper) with a couple of items → capture `orderId`. Expect `AwaitingPayment`.
   - `POST /api/orders/{orderId}/pay` (shopper) with the **direct card**
     `{ number:"4111111111111111", expiry:"2030-01", securityCode:"123", name:"Test Buyer",
     billingAddress:{ address_line_1:"1 Market St", admin_area_2:"San Jose", admin_area_1:"CA",
     postal_code:"95131", country_code:"US" } }` → expect `Authorized` + `authorizationId`. Confirm the
     held amount equals the order total (check the PayPal order/authorization amount).
   - Double-post `pay` → confirm it does **not** create a second authorization (idempotent).
   - `POST /api/orders/{orderId}/fulfil` (admin) → expect `Captured` + `captureId`, `capturedAmount`,
     `paypalFee`, `netAmount` populated from PayPal.
   - `POST /api/orders/{orderId}/refunds` (shopper) with `{ amount: <partial>, idempotencyKey:"k1" }`
     → `refundId`. Repeat with same `k1` → **same** `refundId` (no double refund). A second refund with
     `k2` for the remainder → succeeds; a refund exceeding remaining captured → **rejected**.
   - `GET /api/my-orders` (shopper) → order shows captured/refunded payment state.
5. **Flow 1 — cancel** (separate order): place + `pay` a new order, then `POST /cancel` (admin) **before**
   fulfil → funds released (void), status `Cancelled`; a subsequent `fulfil` is refused.
6. **Flow 2 — saved card**:
   - `POST /api/payment-methods` (shopper) with the same test card → `paymentMethodId` + safe display
     (brand/last4/expiry). `GET /api/payment-methods` lists it.
   - Place a **second** order, `POST /pay` with `{ savedCardId: <paymentMethodId> }` → authorizes from
     the vaulted card; `fulfil` → captures. Confirms reuse without re-entering the card.
   - `DELETE /api/payment-methods/{paymentMethodId}` → 204; it no longer lists and paying with it → 404.
7. **Ownership**: with the *other* shopper's token, confirm you cannot see/pay/refund the first
   shopper's order, nor see/delete their saved card (404).
8. **Reconciliation** (admin): `GET /api/reconciliation?from=...&to=...` over a **past** window that has
   data (or accept an empty recent window — reporting lags up to 3h; empty-recent is expected, not a
   bug). Confirm matched/PayPal-only/eShop-only buckets populate over a data-bearing range.

Report results plainly (what authorized/captured/refunded, the fee/net figures, and any STOP condition
hit).

---

## 12. Gap assessment

**No genuine spec gaps** for this task. Notes on judgment calls that are **not** gaps:
- **OAuth token endpoint** is declared as the security scheme `tokenUrl` (`/v1/oauth2/token`) in every
  spec — covered.
- **`issue` error codes** aren't enumerated in the specs; we deliberately don't depend on specific
  strings (drive control flow off HTTP status + resource status/`expiration_time`; surface the strings
  to operators). Consulting PayPal docs for their meaning is allowed as a secondary reference.
- **Production base URL** isn't in the sandbox specs; we target sandbox and honor `PayPal:BaseUrl`
  verbatim, deriving `api-m.paypal.com` only for `Environment=live`. Not a gap for a sandbox task.
- **Stale-authorization "EXPIRED"** isn't a status enum value; the spec models it via `expiration_time`
  + the `reauthorize` endpoint, which is exactly what we use. Covered.
- **Reconciliation reporting lag / empty recent ranges** is expected sandbox behavior (task says so),
  not a missing capability.

If the build session discovers PayPal answering the sandbox card with a **browser challenge**
(`PAYER_ACTION_REQUIRED`), that is a **STOP-and-report** condition (§8), not something to engineer
around.
