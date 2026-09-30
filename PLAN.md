# PLAN — PayPal payments & saved cards for eShopOnWeb

This plan is the sole hand-off to the build session. It is written so the builder can
implement the task **without re-reading this conversation**. The builder still has the
**paypal-docs MCP server** and must use it for every PayPal detail; every PayPal fact below was
taken from that server and the source page path is cited (e.g. `/api-reference/...`) so it can be
re-opened and confirmed. **Do not web-search PayPal.**

---

## 0. What we are adding (recap)

An **additive** capability on top of the existing catalog/basket/order flow: collect money for an
order via **PayPal** as processor, using **authorize-at-checkout / capture-at-fulfilment / refund-on-return**,
plus **saved cards** (PayPal Vault) that a shopper can reuse. All surfaced as JWT-authenticated
HTTP endpoints on **`src/PublicApi`**, routed under `/api/`. No storefront UI.

Nothing in the existing catalog/basket/order/checkout code is removed or repurposed. New state and
new endpoints are added alongside it.

---

## 1. Repository facts the builder must know (already verified)

### 1.1 Solution / project layout
- Clean-architecture layers: `src/ApplicationCore` (entities, interfaces, services, specs),
  `src/Infrastructure` (EF Core `CatalogContext`, `EfRepository<T>`, identity, DI in
  `Dependencies.cs`), `src/PublicApi` (the JWT API — **our target**), `src/Web` (cookie storefront —
  do not touch functionally).
- `Directory.Packages.props` centrally manages versions; `TargetFramework` is `net8.0`.
  `Nullable` is enabled in ApplicationCore/Infrastructure/PublicApi.

### 1.2 PublicApi conventions (match these exactly)
- Two endpoint styles coexist. **Use the minimal-API `IEndpoint` style** (this is what the
  Catalog endpoints use and it is the cleanest). Pattern (see
  `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`):
  ```csharp
  public class XEndpoint : IEndpoint<IResult, XRequest, IRepository<Y>>
  {
      public void AddRoute(IEndpointRouteBuilder app)
      {
          app.MapPost("api/....",
              [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                         AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
              (XRequest request, IRepository<Y> repo, ClaimsPrincipal user) => await HandleAsync(...))
              .Produces<XResponse>()
              .WithTags("PaymentEndpoints");
      }
      public async Task<IResult> HandleAsync(...) { ... }
  }
  ```
  - Endpoints are auto-discovered by `builder.Services.AddEndpoints()` + `app.MapEndpoints()`
    (already wired in `src/PublicApi/Program.cs`). A new `IEndpoint` class is registered simply by
    existing — no manual wiring.
  - Handler lambdas can take additional DI/route/query/`ClaimsPrincipal`/`HttpContext` parameters;
    minimal-API binds them. **Get the caller identity from `ClaimsPrincipal`** (see 1.4).
  - Request DTOs derive from `BaseRequest` (gives `CorrelationId()`); response DTOs from
    `BaseResponse` (ctor takes the correlation id). Keep this for consistency.
- **Authorization**: shopper endpoints require `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`
  (any authenticated user). **Operator** endpoints (`fulfil`, `cancel`, `reconciliation`) add
  `Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS`.
- **Errors**: `src/PublicApi/Middleware/ExceptionMiddleware.cs` currently maps `DuplicateException`→409,
  everything else→500. Extend this middleware to map the new domain exceptions to the right codes
  (see 6.4), OR return typed `Results.*` from handlers. Prefer returning explicit
  `Results.BadRequest/NotFound/Conflict/Json(...,statusCode)` for expected conditions and reserve
  exceptions for truly unexpected failures.

### 1.3 Domain model (existing)
- `Order` (`src/ApplicationCore/Entities/OrderAggregate/Order.cs`) is an `IAggregateRoot`:
  ctor `Order(string buyerId, Address shipToAddress, List<OrderItem> items)`, `private set`
  properties, `Total()` = Σ `UnitPrice*Units`. **Has no payment/fulfilment state today.**
- `OrderItem`: `ItemOrdered` (`CatalogItemOrdered` value object: `CatalogItemId, ProductName,
  PictureUri`), `UnitPrice`, `Units`.
- `Address` value object (owned by Order in EF; **required, non-null** — see `OrderConfiguration.cs`):
  `Street, City, State, Country, ZipCode`.
- `Buyer` aggregate + `Buyer.PaymentMethod` **exist but are not mapped** into `CatalogContext`
  (no `DbSet`, no EF config) and are unused. `PaymentMethod` only has `Alias, CardId, Last4`.
  We will **not** repurpose these (they'd force mapping the Buyer aggregate); we add a dedicated
  aggregate instead (see 4.3). The comment on `PaymentMethod.CardId` ("actual card data must be
  stored in a PCI compliant system") is exactly our design intent — PayPal Vault is that system.
- Repositories: `IRepository<T>`/`IReadRepository<T>` where `T : IAggregateRoot`, implemented by
  `EfRepository<T>` (Ardalis.Specification). Registered open-generic in Program.cs, so **any new
  aggregate root automatically has a repository** — no new repo type needed.
- Query with `Ardalis.Specification` specs (see `CustomerOrdersWithItemsSpecification`,
  `OrderWithItemsByIdSpec`). Add new specs for our reads.

### 1.4 Identity / auth
- PublicApi validates JWTs (symmetric key `AuthorizationConstants.JWT_SECRET_KEY`), `ValidateIssuer/
  Audience = false`. Token is obtained from **`POST /api/authenticate`** (`AuthenticateEndpoint.cs`)
  with `{ "username", "password" }`. Seeded users (`AppIdentityDbContextSeed.cs`):
  - shopper: `demouser@microsoft.com` / `Pass@word1`
  - admin:  `admin@microsoft.com` / `Pass@word1` (in role `Administrators`)
- The token carries `ClaimTypes.Name` = username (email) and `ClaimTypes.Role` per role
  (`IdentityTokenClaimService.cs`). **Caller identity = `user.Identity!.Name`** (the email). The
  Web app uses `User.Identity.Name` as the order `BuyerId` (`Web/Controllers/OrderController.cs`),
  so **use the same value as `Order.BuyerId` and as the owner key for saved cards** — consistent
  and simple.

### 1.5 Runtime constraints on this machine (must design around)
- **Run in-memory**: `UseOnlyInMemoryDatabase=true` (see `Infrastructure/Dependencies.cs`). The
  in-memory provider **ignores EF migrations** and **loses all data on process restart**. So:
  - New EF migrations are **not required** to run/verify (in-memory builds the model from the
    entity configs directly). Adding migrations is optional polish for the SQL Server path; if added,
    they must not be required for the in-memory run.
  - All verification must **create, pay, fulfil, refund within one process run**.
- **Per-host isolation**: Web and PublicApi each own a separate in-memory store. An order created via
  the Web storefront is invisible to PublicApi. **This is why `POST /api/orders` exists** — the whole
  money flow must be drivable through PublicApi alone.
- **SDK/runtime**: `global.json` pins SDK `8.0.x`; only .NET 10 SDK + no ASP.NET 8 runtime is
  installed. Build/run with **`DOTNET_ROLL_FORWARD=Major`** (preferred — do not commit a `global.json`
  change), or install the ASP.NET Core 8 runtime. Do not rely on editing `global.json`.
- **Ports**: bind only to the assigned block (`APP_PORT_BLOCK_BASE … +SIZE-1`); `launchSettings.json`
  already targets the PublicApi dev ports. Stop the previous instance before starting a new one.
- **HTTPS**: both hosts call `UseHttpsRedirection()`; ensure `dotnet dev-certs https --check` passes.
- No Docker/broker/Postgres — **introduce no new infra**. Only outbound HTTPS to PayPal is added.

---

## 2. PayPal integration facts (from paypal-docs MCP — verified)

Base URLs (`/guides/authentication`): sandbox `https://api-m.sandbox.paypal.com`,
live `https://api-m.paypal.com`. **We target sandbox.**

### 2.1 Auth — OAuth2 client credentials (`/guides/authentication`, `/guides/making-requests`)
- `POST /v1/oauth2/token` with HTTP Basic auth `clientId:clientSecret`,
  body `grant_type=client_credentials`, `Content-Type: application/x-www-form-urlencoded`.
- Response has `access_token` and `expires_in` (seconds, ~1h). Send `Authorization: Bearer <token>`
  on every call. **Cache the token** until shortly before expiry; on `401`, fetch a new one and retry
  once. Never log the secret or token.

### 2.2 Common headers (`/guides/making-requests`)
- `Content-Type: application/json`, `Accept: application/json`.
- **`PayPal-Request-Id`** = idempotency key on money-moving POSTs. Same key ⇒ PayPal returns the
  original result instead of acting twice. **One key per logical operation**, reused on retry.
  Retention: order-create ~6h; capture/void/refund/reauthorize **45 days**; vault create-token 3h.
- `Prefer: return=representation` to get the full resource back (default is `return=minimal` = id/
  status/links only). **Always send `return=representation`** so we can read the nested ids.
- Dates are **RFC 3339 / ISO-8601 UTC**, e.g. `2025-06-22T11:00:00Z`.

### 2.3 Create order (`/api-reference/orders/create-order`, `POST /v2/checkout/orders`)
- Body: `intent` = `AUTHORIZE` (we authorize, not capture, at pay time); `purchase_units[]` each with
  `amount { currency_code, value }`. `value` is a **string** with the currency's decimal places
  (2 for USD). Optional but useful per unit: `custom_id`, `invoice_id`, `description`, `items[]`.
- We attach `payment_source` at authorize time (see 2.4). Providing `payment_source` (card) with the
  order makes it a **single-step** call and **requires `PayPal-Request-Id`**.
- Response: order `id`, `status`. With a card payment source + `return=representation`, the response
  is a fully-processed order carrying the authorization (see 2.4 for the field path).

### 2.4 Authorize with a card or a vaulted card (`/api-reference/orders/authorize-payment-for-order`)
Two viable shapes — **the plan uses the single-step create-with-payment-source** (fewest calls,
one idempotency key, no separate approval), with the two-step as fallback:

**Primary (single call):** `POST /v2/checkout/orders` with
`intent:"AUTHORIZE"`, `purchase_units:[{ amount, custom_id, invoice_id }]`,
`Prefer: return=representation`, `PayPal-Request-Id: <deterministic>`, and one of:
- one-off card:
  ```json
  "payment_source": { "card": {
     "name": "<holder>", "number": "4111111111111111", "expiry": "2030-01",
     "security_code": "123",
     "billing_address": { "address_line_1": "...", "admin_area_2": "...",
                          "admin_area_1": "...", "postal_code": "...", "country_code": "US" } } }
  ```
- saved card: `"payment_source": { "card": { "vault_id": "<vaultTokenId>" } }`
  (`payment_source.card.vault_id` — see the same page).

**Alternative (two calls):** create order without payment_source, then
`POST /v2/checkout/orders/{id}/authorize` with the same `payment_source` body. The authorize page
confirms: *"a valid payment_source must be provided in the request"* removes the need for buyer
approval.

- **Authorization id / status / expiry live at** `purchase_units[0].payments.authorizations[0]`:
  `.id`, `.status` (enum `CREATED, CAPTURED, DENIED, PARTIALLY_CAPTURED, VOIDED, PENDING`),
  `.expiration_time` (RFC 3339). The PayPal **order id** is the top-level `id`. Persist both.
- Also persist `payment_source.card.last_digits` / `.brand` when present (safe descriptor).

**Challenge / 3DS:** if the response is **not** a completed authorization — e.g. order `status` is
`PAYER_ACTION_REQUIRED` or a `links[]` entry has `rel` = `payer-action`/`approve`, or the card
`authentication_result` demands a challenge — **STOP and surface an actionable error**; do **not**
build a browser approval round-trip (task mandate). The sandbox test card
`4111 1111 1111 1111` (any future expiry, any CVC, any name/address) should authorize without a
challenge (`/guides/sandbox-and-testing`).

### 2.5 Capture at fulfilment (`/api-reference/payments/capture-authorized-payment`)
- `POST /v2/payments/authorizations/{authorization_id}/capture`,
  `Prefer: return=representation`, `PayPal-Request-Id: capture-{authorizationId}`.
- Body: `{ "amount": { "currency_code", "value" }, "final_capture": true }` (full capture of the
  hold; `final_capture:true` releases any remaining hold).
- Response: `id` (**capture id**), `status` (`COMPLETED, PENDING, DECLINED, …`), and
  **`seller_receivable_breakdown`** with `gross_amount`, `paypal_fee`, `net_amount` (each
  `{currency_code,value}`). **Persist capture id + captured (gross) amount + paypal_fee + net_amount +
  status.** `seller_receivable_breakdown` is absent while a capture is `PENDING`.

### 2.6 Stale authorization → reauthorize (`/api-reference/payments/reauthorize-authorized-payment`)
- An authorization has a 3-day honor period; it can be reauthorized from day 4 to day 29; after 30
  days you must create a **new** authorization.
- `POST /v2/payments/authorizations/{authorization_id}/reauthorize`, body `{ "amount": {...} }`,
  `PayPal-Request-Id`. Response is a **new** authorization object (`id`, `status`, `expiration_time`).
- **Fulfil algorithm** (see 5.3): if capture fails because the hold is stale/expired
  (HTTP 422 with an expiry-related `issue`, or `expiration_time` already passed), call reauthorize to
  get a fresh authorization id, persist it, then retry the capture. If reauthorize itself fails
  (e.g. >30 days, or an account/business decline), **do not silently fail the fulfilment** — return an
  operator-actionable message (e.g. *"authorization can no longer be renewed (reason: <issue/
  message>, debug_id: <id>); collect payment again"*). Note: sandbox cannot fast-forward days, so this
  path is exercised by logic/unit tests, not the happy-path live run — implement it defensively.

### 2.7 Cancel before fulfilment → void (`/api-reference/payments/void-authorized-payment`)
- `POST /v2/payments/authorizations/{authorization_id}/void` (optionally `Prefer` /
  `PayPal-Request-Id`). Returns `204` (minimal) or `200` with the voided authorization
  (`status: VOIDED`). *"You cannot void an authorized payment that has been fully captured."* So
  cancel is only valid **before** capture. Releases the held funds — no money moved.

### 2.8 Refund after fulfilment (`/api-reference/payments/refund-captured-payment`)
- `POST /v2/payments/captures/{capture_id}/refund`, **`PayPal-Request-Id` = caller-supplied
  idempotency key**, `Prefer: return=representation`.
- Body: **empty JSON `{}` for a full refund**; for partial, `{ "amount": { "currency_code", "value" } }`.
- Response: `id` (**refund id**), `status` (`COMPLETED, PENDING, CANCELLED, FAILED`), and
  `seller_payable_breakdown.total_refunded_amount` = cumulative refunded to date for that capture.
  Persist refund id + amount + status; use `total_refunded_amount` to keep our tally aligned.
- The doc note ("If amount is not specified, an amount equal to captured amount − previous refunds is
  refunded") plus `total_refunded_amount` is how we guarantee **cumulative refunds never exceed the
  captured amount** — but we also enforce this ourselves before calling PayPal (see 5.5).

### 2.9 Vault a card (saved cards) (`/api-reference/vault/create-payment-token-for-a-given-payment-source`)
- **Direct card vaulting, no browser** — `POST /v3/vault/payment-tokens`, `PayPal-Request-Id`,
  body:
  ```json
  { "payment_source": { "card": {
        "name": "<holder>", "number": "4111111111111111", "expiry": "2030-01",
        "security_code": "123",
        "billing_address": { "address_line_1":"...","admin_area_2":"...","admin_area_1":"...",
                             "postal_code":"...","country_code":"US" } } },
    "customer": { "id": "<existing PayPal customer id, if the shopper already has one>" } }
  ```
  - The endpoint accepts a **raw card** as `payment_source.card` (not only a setup token), so **no
    approval/browser step is needed**. (The `/guides/vault-payment-methods` guide describes the
    setup-token→approval→payment-token path and says Vault is **US-only**; we deliberately use the
    **direct card→payment-token** path, which the endpoint reference supports, to stay browserless.
    Use `country_code: "US"` billing to match Vault's US availability.)
- Response (`200`): `id` (**vault token id** — store this), `customer.id` (PayPal customer id — store
  and reuse for the shopper's future saves so all their cards group under one customer),
  `payment_source.card.last_digits`, `.brand`, `.expiry`. **Never store the PAN/CVV.**
- **Pay with a saved card:** pass `payment_source.card.vault_id = <vault token id>` at authorize (2.4).

### 2.10 List / delete vault tokens
- List (`/api-reference/vault/list-all-payment-tokens`): `GET /v3/vault/payment-tokens?customer_id=
  <id>&total_required=true` → `payment_tokens[]` with `.id`, `.payment_source.card.{last_digits,
  brand,expiry}`, plus `total_items`/`total_pages`. **We serve `GET /api/payment-methods` from our own
  DB** (source of truth for ownership); the PayPal list endpoint is documented here for reference/
  reconciliation but is not required on the read path.
- Delete (`/api-reference/vault/delete-payment-token`): `DELETE /v3/vault/payment-tokens/{id}` → `204`.

### 2.11 Reconciliation — transaction search (`/api-reference/transaction-search/list-transactions`, `/guides/transaction-search`)
- `GET /v1/reporting/transactions` with **required** `start_date` & `end_date` (RFC 3339, seconds
  required), `fields=all` (or `transaction_info`), optional `transaction_currency`, `page`, `page_size`
  (default 100). Response: `transaction_details[]` each with `transaction_info` carrying
  `transaction_id`, `transaction_event_code`, `transaction_status` (`S`=success,`P`=pending,
  `D`=denied,`V`=reversed), `transaction_amount {currency_code,value}`, `fee_amount`, `invoice_id`,
  `custom_field`, timestamps.
- **Hard limits (must design for):**
  - **Max 31 days per request** → chunk `[from,to]` into ≤31-day windows and query each.
  - **Pagination** → page through every window. Page until a page returns fewer than `page_size`
    items (robust regardless of whether `total_pages` is populated; use `total_pages` if present).
  - **Eventual availability:** *up to 3 hours* for a transaction to appear. So a range covering
    payments you just made **may legitimately return empty — that is expected, not a gap.** Build
    the report to be correct over a range that has data; don't treat the fresh-empty range as a
    failure.
- **Matching:** we set `custom_id = <eShop order id>` and a stable unique `invoice_id` on each PayPal
  order/capture (see 5.1). Transaction Search echoes these (`custom_field`, `invoice_id`). The report
  joins PayPal transactions ↔ eShop orders on those keys and lists three buckets:
  matched, **in PayPal but not eShop**, **in eShop but not PayPal**.

### 2.12 Errors (`/guides/responses-and-errors`, seen across pages)
Error body: `{ name, message, debug_id, details:[{ issue, field, description }] }`. **Log `debug_id`**
on every failure. Status map: `400` validation, `401` token (refresh+retry once), `403` permission,
`404` not found, `409`/`422` state conflict/business-rule, `429` rate-limited (exponential backoff
with jitter, retry idempotent ops only), `5xx` retry idempotently. Never log card data or secrets.

---

## 3. Configuration & credentials (exact binding)

- Secrets arrive as **env vars**: `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`,
  `PAYPAL_CURRENCY` (and optionally a base-url override). **Never write their values into any repo
  file, PLAN.md included.** Referencing the names is fine.
- **Bind from the `PayPal:` section with exactly these keys** (task mandate, hard-code no values):
  `PayPal:ClientId`, `PayPal:ClientSecret`, `PayPal:Environment`, `PayPal:Currency`, `PayPal:BaseUrl`.
- The env var **names** (`PAYPAL_CLIENT_ID`) don't match the `.NET` double-underscore convention
  (`PayPal__ClientId`), so add a tiny mapping config source in `PublicApi/Program.cs` **before**
  `Configure<PayPalSettings>`, e.g. add an in-memory collection built from
  `Environment.GetEnvironmentVariable(...)`:
  ```
  PAYPAL_CLIENT_ID     -> PayPal:ClientId
  PAYPAL_CLIENT_SECRET -> PayPal:ClientSecret
  PAYPAL_ENVIRONMENT   -> PayPal:Environment
  PAYPAL_CURRENCY      -> PayPal:Currency
  PAYPAL_BASE_URL      -> PayPal:BaseUrl   (only if such an env var is provided; else leave unset)
  ```
  Only add keys whose env var is present (don't create empty overrides). Values still originate from
  the environment; nothing is hard-coded. Bind a `PayPalSettings` POCO from the `PayPal:` section and
  fail fast at startup if `ClientId`/`ClientSecret`/`Environment`/`Currency` are missing.
- **`PayPal:BaseUrl` is an optional override.** When set, use it **verbatim** as the API base for
  **every** PayPal call **including the `/v1/oauth2/token` request**. When unset, derive from
  `PayPal:Environment`: `sandbox`→`https://api-m.sandbox.paypal.com`, `live`/`production`→
  `https://api-m.paypal.com` (default to sandbox for anything else). Currency comes from
  `PayPal:Currency` for all amounts.
- Do **not** put any of these under `appsettings.json` values; the section can be declared empty/omitted
  and populated from env at runtime.

---

## 4. Domain model changes (additive)

### 4.1 `Order` — add fulfilment/payment status (aggregate root, unchanged ctor)
Add an `OrderStatus` enum and property with private setter + domain transition methods; default is
`AwaitingPayment`. Do **not** change the existing constructor signature (Web still uses it); EF will
set a default. Suggested states:
`AwaitingPayment → Authorized → Fulfilled → (Refunded | PartiallyRefunded)` and `Authorized → Cancelled`.
Domain methods enforce legal transitions (e.g. `MarkAuthorized`, `MarkFulfilled`, `MarkCancelled`,
`MarkRefunded/MarkPartiallyRefunded`) and throw an `InvalidOrderStateException` otherwise.
`Order.Total()` already gives the authoritative amount.

### 4.2 `Payment` — new aggregate root (1:1 with Order, keyed by `OrderId`)
Rationale: pay/fulfil/refund act on payment state across separate requests; a dedicated aggregate with
its own repository (`IRepository<Payment>`, free via the open-generic registration) keeps `Order` lean
and holds "enough of the state PayPal owns … that a later request can act on it." Fields:
- `OrderId` (int, FK/logical link), `BuyerId` (copy, for ownership checks without loading Order),
- `Currency`, `AuthorizedAmount` (= order total at pay time),
- PayPal ids/status for the **hold**: `PayPalOrderId`, `AuthorizationId`, `AuthorizationStatus`,
  `AuthorizationExpiresAt`,
- the **capture**: `CaptureId`, `CaptureStatus`, `CapturedAmount`, `PayPalFee`, `NetAmount`,
- `PaymentStatus` enum mirroring the flow (`Authorized, Captured, Voided, Refunded, PartiallyRefunded,
  Failed`),
- child collection **`Refunds`** (owned entities): `{ PayPalRefundId, Amount, Status, IdempotencyKey,
  CreatedAt }`. Add domain helpers: `TotalRefunded()` = Σ refund amounts; `CanRefund(amount)` =
  `amount > 0 && TotalRefunded()+amount <= CapturedAmount`.
- Safe card descriptor used at pay time: `CardBrand`, `CardLast4` (nullable).
Domain methods: `RecordAuthorization(...)`, `RenewAuthorization(...)`, `RecordCapture(...)`,
`RecordVoid()`, `AddRefund(...)` — each updates status and guards invariants.

### 4.3 `SavedPaymentMethod` — new aggregate root (saved cards)
Fields: `OwnerId` (= caller email/`User.Identity.Name`), `VaultTokenId` (PayPal), `PayPalCustomerId`,
`Brand`, `Last4`, `ExpiryMonthYear` (e.g. `2030-01`), `CardholderName` (optional, safe), `CreatedAt`.
**Never** store PAN/CVV. `OwnerId` enforces per-shopper isolation. (We add a new aggregate rather than
reuse the unmapped `Buyer.PaymentMethod` to avoid dragging the Buyer aggregate into the context; note
the existing class as prior art in a comment.)

### 4.4 EF Core wiring (`src/Infrastructure/Data`)
- Add `DbSet<Payment> Payments`, `DbSet<SavedPaymentMethod> SavedPaymentMethods` to `CatalogContext`.
- Add `IEntityTypeConfiguration<Payment>` (map `Refunds` as an owned collection or a child entity with
  `Payment` navigation; store enums as strings via `HasConversion<string>()`; decimals with explicit
  precision) and `IEntityTypeConfiguration<SavedPaymentMethod>`. They're auto-applied by
  `builder.ApplyConfigurationsFromAssembly(...)` already in `CatalogContext.OnModelCreating`.
- Add the `OrderStatus` conversion in `OrderConfiguration` (string column, default `AwaitingPayment`).
- **Migrations**: not needed for the in-memory run (provider ignores them). Optionally add a
  `CatalogContext` migration for the SQL Server path, but ensure the in-memory run does not depend on
  it. Do not break existing migrations.

---

## 5. Endpoint specifications

All routes are under `/api/`, JWT-authenticated. Shopper endpoints act **only** on the caller's own
data (`OwnerId`/`BuyerId == User.Identity.Name`); return **404** (not 403) when a resource exists but
belongs to someone else, to avoid leaking existence. Operator endpoints require the `Administrators`
role. Every response that creates something returns the id as a **top-level** field.

A thin application service (e.g. `IPaymentService` in ApplicationCore, implemented in ApplicationCore
or Infrastructure) should own the orchestration (PayPal calls + state transitions + persistence) so
endpoints stay thin and the logic is unit-testable. The `IPayPalClient` abstraction (section 6) is the
only thing that talks to PayPal.

### 5.1 `POST /api/orders` — place order (shopper)
- Body: `{ items: [ { catalogItemId, quantity }... ], shipToAddress?: {street,city,state,country,zipCode} }`.
  `shipToAddress` is optional; if omitted use a sensible default Address (the model requires a non-null
  address). Identity from token → `BuyerId`.
- Behavior: load the referenced `CatalogItem`s (reuse `CatalogItemsSpecification`), build `OrderItem`s
  from **catalog prices** (`CatalogItem.Price`, snapshot via `CatalogItemOrdered` exactly like
  `OrderService.CreateOrderAsync`), construct `Order` (status `AwaitingPayment`), persist via
  `IRepository<Order>`. Validate items non-empty and quantities ≥ 1; unknown catalog id → 400.
- Response `201`: **top-level `orderId`**, plus order summary (items, total, currency, status).
- We do **not** call PayPal here (order is "awaiting payment").

### 5.2 `POST /api/orders/{orderId}/pay` — authorize the hold (shopper)
- Body (exactly one payment option):
  - one-off card: `{ card: { name, number, expiry ("YYYY-MM"), securityCode, billingAddress:
    {addressLine1, adminArea2, adminArea1, postalCode, countryCode} } }`, **or**
  - saved card: `{ savedPaymentMethodId: <id> }`.
- Preconditions: order exists & belongs to caller (else 404); order status `AwaitingPayment`
  (else 409). If `savedPaymentMethodId`: it must belong to caller (else 404) → resolve to `VaultTokenId`.
- PayPal: single-step create-with-payment-source (2.4) — `intent AUTHORIZE`,
  `amount.value = Order.Total()` formatted to the currency's decimals (**equal to the order total to
  the cent**), `currency_code = PayPal:Currency`, `custom_id = orderId`,
  `invoice_id = "eshop-{orderId}"` (stable & unique per order), `Prefer: return=representation`,
  **`PayPal-Request-Id = "pay-{orderId}"`** (deterministic → double-click can't authorize twice).
- Read `purchase_units[0].payments.authorizations[0].{id,status,expiration_time}` and top-level order
  `id`; persist a `Payment` (`RecordAuthorization`), copy `card.last_digits/brand` if present; set
  `Order` → `Authorized`.
- **Idempotency guard**: if a `Payment` already exists for the order with an `AuthorizationId`, return
  it (200) without re-calling PayPal.
- **Challenge guard**: if the order isn't a completed authorization (status `PAYER_ACTION_REQUIRED`, an
  `approve`/`payer-action` link, or an unmet `authentication_result`), **STOP** — return an error that
  says a browser challenge was required; do not implement approval. Log `debug_id`.
- Response `200`: payment state (auth id, status, amount held, currency, masked card).

### 5.3 `POST /api/orders/{orderId}/fulfil` — capture at fulfilment (**operator**)
- Preconditions: order exists, status `Authorized` (else 409). Load its `Payment`.
- **Stale-auth-aware capture**:
  1. If `AuthorizationExpiresAt` is in the past → reauthorize first (2.6): call reauthorize, persist
     the new authorization id/expiry (`RenewAuthorization`).
  2. Capture `POST /v2/payments/authorizations/{authId}/capture` with `amount = AuthorizedAmount`,
     `final_capture:true`, `Prefer: return=representation`, `PayPal-Request-Id = "capture-{authId}"`.
  3. If capture returns **422 with an expiry-related issue**, reauthorize once (if the reauthorize
     window allows) then retry the capture with `PayPal-Request-Id = "capture-{newAuthId}"`.
  4. If reauthorize is not possible / fails → return an **operator-actionable** error (message +
     `debug_id`, e.g. "authorization can no longer be renewed; re-collect payment"). Do **not** 500.
- On success: persist `CaptureId`, `CapturedAmount` (= `seller_receivable_breakdown.gross_amount`),
  `PayPalFee` (= `seller_receivable_breakdown.paypal_fee`), `NetAmount` (= `…net_amount`),
  `CaptureStatus`; set `Order` → `Fulfilled`, `Payment` → `Captured`.
- **Idempotency**: `PayPal-Request-Id` keyed on the auth id + our own guard (if `CaptureId` already set,
  return it) ⇒ double-click never captures twice.
- Response `200`: captured amount, PayPal fee, net proceeds, capture id, status.

### 5.4 `POST /api/orders/{orderId}/cancel` — void before fulfilment (**operator**)
- Preconditions: order `Authorized`, not captured (else 409). Void `POST /v2/payments/authorizations/
  {authId}/void` (`PayPal-Request-Id = "void-{authId}"`). Set `Order` → `Cancelled`, `Payment` →
  `Voided`. Idempotent: if already voided/cancelled, return current state. Response `200`.

### 5.5 `POST /api/orders/{orderId}/refunds` — refund after fulfilment (**shopper**; owner only)
- Body: `{ amount?: decimal, idempotencyKey: string, note?: string }`. Full refund when `amount`
  omitted; partial when present.
- Preconditions: order `Fulfilled`/`PartiallyRefunded`, `Payment.CaptureId` set (else 409); resolve
  full amount = `CapturedAmount - TotalRefunded()`. **Enforce `CanRefund(amount)`** — reject (422) if
  it would exceed the captured amount so a partly-refunded order is never refundable beyond capture.
- **Idempotency**: if a stored refund already has this `idempotencyKey`, return it unchanged (no second
  refund). Otherwise call `POST /v2/payments/captures/{captureId}/refund` with the caller's
  `idempotencyKey` as **`PayPal-Request-Id`**, empty body for full or `{amount}` for partial. Two
  **different** keys ⇒ two legitimate partial refunds.
- Persist the refund (`AddRefund`), reconcile our tally with `seller_payable_breakdown.
  total_refunded_amount`; set order → `Refunded` (fully) or `PartiallyRefunded`.
- Response `201`: **top-level `refundId`**, plus refund amount/status and remaining refundable.

### 5.6 `GET /api/my-orders` — caller's orders + payment state (shopper)
- Return the caller's orders (`BuyerId == User.Identity.Name`) with items, total, order status, and
  payment state (auth/capture/refund ids & statuses, captured/fee/net, total refunded). Reuse/extend
  `CustomerOrdersWithItemsSpecification`; join each order's `Payment`.

### 5.7 `GET /api/reconciliation?from={from}&to={to}` — (**operator**)
- `from`/`to` are ISO-8601 date-times. Query PayPal Transaction Search (2.11): **chunk into ≤31-day
  windows**, **page through every window** (page_size 100; stop when a page is short; use `total_pages`
  if present), filter by `PayPal:Currency`. Collect all transactions across the **whole** range.
- Load eShop `Payment`s/orders whose activity falls in `[from,to]`. Join on `custom_id`/`invoice_id`.
  Return three buckets: **matched**, **in PayPal only** (PayPal knows, eShop doesn't), **in eShop only**
  (eShop knows, PayPal doesn't). Include amounts/fees and the join keys.
- **Empty result for a just-created range is expected** (≤3h reporting lag) — return an empty/So-far-
  empty report, not an error. Correctness is judged over a range that has data.

### 5.8 `POST /api/payment-methods` — save a card (shopper)
- Body: `{ card: { name, number, expiry ("YYYY-MM"), securityCode, billingAddress {addressLine1,
  adminArea2, adminArea1, postalCode, countryCode="US"} } }`.
- Look up any existing `SavedPaymentMethod` for the caller to reuse its `PayPalCustomerId`. Call vault
  create-token (2.9) with the card (+ `customer.id` if known), `PayPal-Request-Id` fresh per save.
  Persist `SavedPaymentMethod { OwnerId=caller, VaultTokenId=id, PayPalCustomerId=customer.id,
  Brand, Last4=last_digits, ExpiryMonthYear=expiry, CardholderName }`.
- Response `201`: **top-level `paymentMethodId`** + safe descriptor (`brand`, `last4`, `expiry`).
  **Never** echo full card details.

### 5.9 `GET /api/payment-methods` — list caller's saved cards (shopper)
- Serve from our DB filtered by `OwnerId == caller`. Return `[{ paymentMethodId, brand, last4,
  expiry, cardholderName }]`. Never full card data.

### 5.10 `DELETE /api/payment-methods/{paymentMethodId}` — remove a saved card (shopper)
- Verify ownership (else 404). Call vault delete `DELETE /v3/vault/payment-tokens/{VaultTokenId}`
  (treat `204` and already-gone as success). Remove the row. Afterwards it no longer appears in 5.9
  and can't be used in 5.2 (the mapping is gone). Response `204`.

---

## 6. PayPal client abstraction & cross-cutting

### 6.1 `IPayPalClient` (ApplicationCore/Interfaces) — implemented in Infrastructure
Methods (return small DTOs, not raw JSON):
- `AuthorizeOrderWithCardAsync(amount, currency, customId, invoiceId, CardDetails, requestId)` and
  `AuthorizeOrderWithVaultAsync(amount, currency, customId, invoiceId, vaultId, requestId)` →
  `{ payPalOrderId, authorizationId, status, expiresAt, cardBrand, cardLast4, requiresChallenge }`.
- `CaptureAsync(authorizationId, amount, currency, requestId)` →
  `{ captureId, status, gross, paypalFee, net }`.
- `ReauthorizeAsync(authorizationId, amount, currency, requestId)` → `{ authorizationId, status, expiresAt }`.
- `VoidAsync(authorizationId, requestId)`.
- `RefundAsync(captureId, amount?, currency, idempotencyKey)` → `{ refundId, status, totalRefunded }`.
- `GetAuthorizationAsync(authorizationId)` → `{ status, expiresAt }` (used by fulfil to pre-check).
- `VaultCardAsync(CardDetails, customerId?, requestId)` → `{ vaultTokenId, customerId, brand, last4, expiry }`.
- `DeleteVaultTokenAsync(vaultTokenId)`.
- `SearchTransactionsAsync(fromUtc, toUtc, currency)` → `IReadOnlyList<PayPalTransaction>` (handles
  31-day chunking + pagination internally).

### 6.2 `PayPalClient` implementation (Infrastructure)
- Typed `HttpClient` via `builder.Services.AddHttpClient<IPayPalClient, PayPalClient>()`
  (`Microsoft.Extensions.Http` is in the ASP.NET shared framework — no new package). `System.Net.Http.Json`
  (already available centrally) for (de)serialization; or `System.Text.Json`.
- **Base URL**: resolve once from `PayPal:BaseUrl` (verbatim, incl. token endpoint) else from
  `PayPal:Environment` (2/3).
- **Token**: client-credentials (2.1), cached in `IMemoryCache` (already registered in Program.cs)
  keyed by client id, refreshed ~60s before `expires_in`; on `401`, invalidate + refetch + retry once.
- **Resilience**: on `429`/`5xx`, retry idempotent calls with exponential backoff + jitter (a few
  attempts). Since no Polly package is referenced, implement a minimal retry helper (do not add Polly
  unless the builder chooses to register it centrally). Always send `PayPal-Request-Id` on money moves
  so retries are safe.
- **Logging/security**: log method, path, status, `debug_id` on failures. **Never** log card
  number/CVV, tokens, secrets, or full request bodies containing card data. Redact.

### 6.3 Amount formatting
Format all `amount.value` from `decimal` with the currency's decimal places (2 for USD) using
`InvariantCulture` (e.g. `value.ToString("F2", CultureInfo.InvariantCulture)`), and parse PayPal
string amounts back with `InvariantCulture`. The authorized amount must equal `Order.Total()` exactly.

### 6.4 Error → HTTP mapping
Add domain exceptions (e.g. `OrderNotFoundException`, `InvalidOrderStateException`,
`PaymentChallengeRequiredException`, `RefundExceedsCaptureException`, `PayPalApiException`) and either
extend `ExceptionMiddleware` to map them (404/409/422/402-or-409/502) or return typed `Results.*` in
handlers. `PayPalApiException` should carry PayPal `name`/`issue`/`message`/`debug_id` for operator
messages (esp. the un-renewable-authorization case in 5.3).

---

## 7. DI registration (PublicApi/Program.cs, additive)
- Map `PAYPAL_*` env vars → `PayPal:*` config keys (section 3), `Configure<PayPalSettings>(section)`,
  validate on start.
- `AddHttpClient<IPayPalClient, PayPalClient>()`; register `IPaymentService`.
- New `IEndpoint` classes are auto-discovered (no manual `Map`). New aggregates get repositories from
  the existing open-generic `AddScoped(typeof(IRepository<>), typeof(EfRepository<>))`.
- Nothing else in Program.cs needs restructuring; keep the existing seeding/auth/swagger blocks.

---

## 8. Build order (suggested)
1. Config plumbing: `PayPalSettings`, env→`PayPal:` mapping, base-url/currency resolution. Verify it
   binds (log a redacted confirmation at startup).
2. `IPayPalClient` + `PayPalClient` with token caching; smoke-test auth (get a token) and one
   authorize on the sandbox test card end to end via a scratch call.
3. Domain: `OrderStatus`, `Payment` (+`Refund` child), `SavedPaymentMethod`, domain methods/invariants;
   EF configs + `DbSet`s; confirm in-memory model builds.
4. `IPaymentService` orchestration.
5. Endpoints in this order: `POST /api/orders` → `POST /pay` → `POST /fulfil` → `POST /refunds` →
   `POST /cancel` → `GET /my-orders` → payment-methods (save/list/delete) → `GET /reconciliation`.
6. Wire exception mapping; add tags/`Produces<>` for Swagger.
7. Self-verify live (section 9); write the user guide.

---

## 9. Self-verification (build session must actually run this)
Build & run PublicApi with `DOTNET_ROLL_FORWARD=Major`, `UseOnlyInMemoryDatabase=true`, on the
assigned port block, with the `PAYPAL_*` env vars set (sandbox). All in **one process run**.
1. `POST /api/authenticate` as `demouser@microsoft.com`/`Pass@word1` → shopper bearer;
   as `admin@microsoft.com`/`Pass@word1` → operator bearer.
2. **Flow 1 (one-off card):** `POST /api/orders` (some catalog items) → `orderId`. `POST /api/orders/
   {orderId}/pay` with the Visa `4111 1111 1111 1111` (future expiry, any CVC/name/US billing) →
   real **authorization** (hold = order total to the cent). `POST /api/orders/{orderId}/fulfil`
   (operator) → real **capture** showing captured amount, PayPal fee, net. `POST /api/orders/
   {orderId}/refunds` with an idempotency key (partial then, optionally, the rest) → real **refund**;
   repeat the same key → no second refund.
3. **Cancel path:** a second order, `pay`, then `POST /cancel` (operator) → authorization **voided**.
4. **Flow 2 (saved card):** `POST /api/payment-methods` (Visa test card) → `paymentMethodId`;
   `GET /api/payment-methods` shows it (brand+last4+expiry only). Place a **second order** and
   `pay` with `{ savedPaymentMethodId }` → authorize → fulfil. `DELETE /api/payment-methods/{id}`
   → gone from the list and unusable to pay.
5. **Idempotency:** double-`pay` and double-`fulfil` the same order → single authorization / single
   capture.
6. **Reconciliation:** `GET /api/reconciliation?from=…&to=…` over a wide past range returns a correct
   report; a range covering just-created payments may be empty (expected, ≤3h lag) — not a gap.
7. Confirm logs contain **no** card numbers/CVV/secrets/tokens; the app DB stores no PAN.

Then give the user a concise step-by-step curl/Postman guide mirroring the above (get token → orders
→ pay → fulfil → refund → saved-card reuse), noting the single-run in-memory caveat and the
`DOTNET_ROLL_FORWARD=Major` + `UseOnlyInMemoryDatabase=true` run flags.

## 10. Testing (optional but recommended)
- Unit-test domain invariants (state transitions, `CanRefund`/cumulative-refund cap, idempotency-key
  reuse) with a **faked `IPayPalClient`** (NSubstitute is available centrally) — no network.
- Follow the existing `tests/PublicApiIntegrationTests` pattern for endpoint tests if time allows;
  keep PayPal calls behind the faked client for deterministic runs.

---

## 11. Decisions already made (so the builder doesn't re-litigate)
- **AUTHORIZE now, capture at fulfil, refund on return, void on cancel** — matches the task's money
  model and PayPal's two-step (`/guides/payments-authorize-capture`).
- **Single-step create-with-payment-source at `/pay`** (one call, one idempotency key, no approval);
  two-step authorize is the documented fallback.
- **Direct card→vault payment token** for saved cards (browserless), using `country_code: "US"` to
  match Vault US availability; saved cards are served from our own DB (ownership source of truth).
- **New `Payment` and `SavedPaymentMethod` aggregates**; `Order` gains only a status. Repositories come
  free from the open-generic registration.
- **Caller identity = `User.Identity.Name`** (email), reused as `Order.BuyerId` and saved-card
  `OwnerId`; cross-owner access returns 404.
- **Idempotency**: deterministic `PayPal-Request-Id` per logical op (`pay-{orderId}`,
  `capture-{authId}`, `void-{authId}`) + our own state guards; refunds use the **caller-supplied** key.
- **Reconciliation**: 31-day chunking + full pagination; fresh-empty ranges are expected.

## 12. Gaps
None. The paypal-docs MCP covers every capability this integration needs: OAuth token, create/
authorize order (incl. direct card and `vault_id`), capture with `seller_receivable_breakdown`,
reauthorize, void, refund (full/partial, idempotent), vault create/list/delete of card payment tokens,
and transaction search with date-window + pagination. Open questions in this plan are design choices
(decided above), not missing documentation. If the builder finds a genuinely uncovered capability,
**STOP and report it** rather than inventing or web-searching — but none is anticipated.
