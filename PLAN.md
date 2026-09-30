# PayPal Payments + Saved Cards for eShopOnWeb — Implementation Plan

This is a plan only. It is handed to a separate build session that has the same task
brief and the same `paypal-docs` MCP server, but not this conversation. Everything the
build session needs — architecture, exact PayPal endpoints/fields, file layout, config
wiring, edge cases, and a self-verification checklist — is below. No secret **values**
appear anywhere in this document, only the names of the environment variables / config
keys that carry them.

All PayPal endpoint paths, request/response fields, and constraints cited below were
read directly from the `paypal-docs` MCP server (Payments API docs: `guides/*`,
`api-reference/orders/*`, `api-reference/payments/*`, `api-reference/vault/*`,
`api-reference/transaction-search/*`, `reference/errors.mdx`). Nothing here is invented
or drawn from general knowledge of the PayPal API — the build session should still
re-confirm exact field names against the same MCP server while implementing, since this
plan summarizes rather than reproduces the docs verbatim.

---

## 1. How the payment actually works against PayPal

### 1.1 Money movement: Orders v2 (authorize) + Payments v2 (capture/void/refund/reauthorize)

- `POST /v2/checkout/orders` (Orders v2, `guides/orders-checkout-flow.mdx`,
  `api-reference/orders/create-order.mdx`) with `intent: "AUTHORIZE"` and a **single**
  `purchase_units[0]` (multiple purchase units are rejected for `intent=AUTHORIZE` —
  `reference/errors.mdx`: `UNSUPPORTED_INTENT`). eShop's order total maps to
  `purchase_units[0].amount.currency_code` / `.value`; do **not** send an
  `amount.breakdown` or `items[]` — with no breakdown, PayPal skips the
  item/tax/shipping cross-check validation entirely, so there's nothing to keep in
  sync with catalog prices beyond the single total.
- **Direct card payment, no browser step**: when the create-order request already
  carries `payment_source.card` (number/expiry/security_code/billing_address) or
  `payment_source.card.vault_id` (a saved card), PayPal authorizes the order
  synchronously as part of the same call — there is no buyer-approval redirect to
  wait for. This is documented as the "single-step create order" case
  (`api-reference/orders/create-order.mdx`, `authorize-payment-for-order.mdx`): the
  `PayPal-Request-Id` header is *mandatory* for these calls, and its absence produces
  `PAYPAL_REQUEST_ID_REQUIRED`. Use `Prefer: return=representation` so the response
  body contains the full authorization, not just minimal links.
- The resulting order response contains the authorization under
  `purchase_units[0].payments.authorizations[0]` (this nesting is confirmed by the
  refund recipe's equivalent path for captures: `purchase_units[0].payments.captures[0].id`,
  `recipes/refund-flow.mdx` — captures and authorizations live at the same nesting
  level under `purchase_units[0].payments`). Persist `authorizations[0].id`,
  `.status`, and `.expiration_time`.
- **Capture at fulfilment**: `POST /v2/payments/authorizations/{authorization_id}/capture`
  (`api-reference/payments/capture-authorized-payment.mdx`), body
  `{ amount, final_capture: true }`, `Prefer: return=representation`,
  `PayPal-Request-Id` for idempotency (key retained 45 days per that endpoint's docs).
  Response gives `id` (capture id), `status`
  (`COMPLETED|DECLINED|PARTIALLY_REFUNDED|PENDING|REFUNDED|FAILED`), and
  `seller_receivable_breakdown` with `gross_amount`, `paypal_fee`, `net_amount` — this
  is exactly "the captured amount, PayPal's fee, and the net proceeds" the task asks
  for; store all three.
- **Reauthorization (stale-hold handling)**: an authorization has a 3-day honor
  period, is reauthorizable from day 4 to day 29, and **cannot** be reauthorized once
  30 days have passed — at that point PayPal requires a brand-new authorized payment
  (`api-reference/payments/reauthorize-authorized-payment.mdx`). Endpoint:
  `POST /v2/payments/authorizations/{authorization_id}/reauthorize`, body
  `{ amount }` (same currency/amount as the original authorization — reauthorize
  supports no other fields). On fulfilment, first `GET
  /v2/payments/authorizations/{authorization_id}` (`show-details-for-authorized-payment.mdx`)
  to read the live `status`/`expiration_time`; if it's past `expiration_time` and
  still within the reauthorizable window, reauthorize before capturing; if PayPal
  rejects the reauthorization (business-validation `422`/`UNPROCESSABLE_ENTITY`, e.g.
  because more than 30 days elapsed or the auth was already voided/captured), do
  **not** retry — surface an operator-actionable error explaining the authorization
  can no longer be renewed and a new order/authorization is required. Do not hardcode
  a specific error `issue` string for this — treat any reauthorize failure while the
  order is still `PaymentAuthorized` as terminal and report it plainly.
- **Cancel before fulfilment**: `POST /v2/payments/authorizations/{authorization_id}/void`
  (`void-authorized-payment.mdx`) — "You cannot void an authorized payment that has
  been fully captured," which is exactly the before/after-fulfilment split the task
  wants. No amount in the request.
- **Refund after fulfilment**: `POST /v2/payments/captures/{capture_id}/refund`
  (`refund-captured-payment.mdx`). Empty body (`{}`) = full refund of
  `captured amount − previous refunds`; `{ amount: { currency_code, value } }` = partial.
  Response `status` is `CANCELLED|FAILED|PENDING|COMPLETED`, `id` is the refund id,
  and `seller_payable_breakdown.total_refunded_amount` reports the running total
  refunded against that capture — use this (or your own running sum) to enforce "never
  refundable beyond what was captured."

### 1.2 Saved cards: Vault v3

- `POST /v3/vault/setup-tokens` (`api-reference/vault/create-a-setup-token.mdx`) with
  `payment_source.card` (number/expiry/security_code/billing_address). For a **card**
  payment source (unlike PayPal wallet/3DS), no `return_url`/`cancel_url` is required —
  those `experience_context` fields are only needed for "contingency flows like PayPal
  wallet, 3DS," so a plain sandbox test card vaults with no browser step. Response
  gives a setup token `id`.
- `POST /v3/vault/payment-tokens` (`create-payment-token-for-a-given-payment-source.mdx`)
  with `payment_source.token = { id: <setup_token_id>, type: "SETUP_TOKEN" }` and a
  `customer` object. Response `payment_source.card` includes `last_digits`, `brand`,
  `expiry`, `billing_address` — enough to describe the card safely (never the PAN).
  The top-level `id` here is the durable **payment token** (a.k.a. vault id) — this is
  what you store and what you pass back to PayPal to charge the card later.
- **Design decision — `customer.id`**: Vault's `customer` object is "a customer in
  merchant's or partner's system of records" — i.e., *our* identifier, not PayPal's.
  Use the shopper's eShop identity (the `ClaimTypes.Name` value already used as
  `Order.BuyerId` throughout the app) directly as `customer.id` on both the setup-token
  and payment-token calls, and as the `customer_id` query parameter on
  `GET /v3/vault/payment-tokens`. This gives natural per-shopper scoping on PayPal's
  side with no separate mapping table needed.
- **Reuse to pay**: on `POST /v2/checkout/orders`, set
  `payment_source.card.vault_id = <stored payment token id>` instead of raw card
  fields (`create-order.mdx` documents `payment_source.card.vault_id` as "The
  PayPal-generated ID for the saved card payment source"). Everything else about the
  authorize flow is unchanged.
- `GET /v3/vault/payment-tokens?customer_id=<buyerId>` (`list-all-payment-tokens.mdx`)
  lists a shopper's saved cards; page with `page`/`page_size` if needed (defaults are
  small — `page_size` defaults to 5).
- `DELETE /v3/vault/payment-tokens/{id}` (`delete-payment-token.mdx`) removes a saved
  card from PayPal's vault — returns `204`. Call this **and** delete/deactivate the
  local `PaymentMethod` row so the card is both unlistable and unusable afterward.
- <ins>Constraint to record, not a gap</ins>: the Vault guide flags "Availability: the
  Vault API is currently available in the **US only**." The sandbox business account
  provisioned for this task is expected to be a US account and enabled for vaulting
  per the task brief; if vaulting calls fail specifically because the account isn't
  vault-enabled, that is an account-provisioning problem to report, not a reason to
  build a workaround.
- Do not set `verification_method: SCA_ALWAYS` anywhere (default is
  `SCA_WHEN_REQUIRED`) — forcing SCA risks a 3DS challenge, and the task requires
  stopping rather than building an approval round-trip if PayPal ever asks for one.
  With the sandbox Visa test card (`4111 1111 1111 1111`) and a merchant not
  configured to force 3DS, no challenge is expected.

### 1.3 Reconciliation: Transaction Search v1

- `GET /v1/reporting/transactions` (`api-reference/transaction-search/list-transactions.mdx`,
  `guides/transaction-search.mdx`). Required: `start_date`, `end_date` (RFC3339 /
  ISO-8601, seconds required). **Hard limit: max 31-day window per request.** The
  caller-supplied `from`/`to` on `GET /api/reconciliation` can span more than 31 days,
  so the implementation must **chunk** `[from, to]` into ≤31-day sub-windows and issue
  one search per chunk.
- Each search is also paginated (`page`, `page_size` — default `page_size` is 100);
  the response doesn't literally say whether it returns `total_pages`, so drive
  pagination by requesting `total_required=true` on the first page of each chunk,
  reading `total_pages` from the wrapper, then walking `page=2..total_pages` — or, more
  defensively, keep paging until a page comes back with fewer than `page_size`
  transactions. Either approach is fine; the point the task calls out explicitly is
  that the report must cover **the whole range**, not just page 1 of the first chunk.
- Matching PayPal transactions back to eShop orders: `transaction_info.invoice_id` is
  documented as reporting "the invoice ID that is sent by the merchant" — "if an
  invoice ID was sent with the capture request, the value is reported; otherwise, the
  invoice ID of the authorizing transaction is reported." So: **set `invoice_id` on
  both the initial authorize call and the capture call** to a value derived
  deterministically from the eShop order id (e.g. `"eshop-order-{orderId}"`), and use
  that same derivation to match transaction-search rows back to orders. Also set
  `custom_id` to the same value as a second, independent matching field (it's
  merchant-only, never shown to the payer) — belt and suspenders, cheap to add.
  Because the invoice id is derived deterministically from the order id and reused on
  idempotent retries (never freshly generated per attempt), there is no
  `DUPLICATE_INVOICE_ID` risk from retried pay/fulfil calls.
- Expected sandbox result: PayPal's own transaction reporting lags live activity, so a
  range covering payments made in the last few minutes may legitimately come back with
  zero PayPal-side transactions for those. Build the report correctly and verify it
  end-to-end over a range that *does* have data (e.g. transactions created a few
  minutes before the check, if the sandbox has caught up, or — more reliably — accept
  that immediate post-creation reconciliation may show those orders only on the
  eShop side, and call that out explicitly as expected in the verification guide
  rather than treating it as a bug.

### 1.4 Auth / base URL / config plumbing

- Token endpoint: `POST {baseUrl}/v1/oauth2/token`, HTTP Basic auth with
  `client_id:client_secret`, `grant_type=client_credentials`
  (`guides/authentication.mdx`). Response `access_token` + `expires_in` (seconds);
  cache it (e.g. via the `IMemoryCache` already registered in `PublicApi/Program.cs`)
  and refresh proactively a short margin before expiry, or reactively on a `401`.
- Base URL resolution — **implement exactly this precedence**, per the task:
  1. If `PayPal:BaseUrl` is configured (non-empty), use it **verbatim** for every
     PayPal call, including the token request. No path-joining tricks — just prefix
     it and call it as given.
  2. Otherwise derive from `PayPal:Environment`: `sandbox` →
     `https://api-m.sandbox.paypal.com`; `live`/`production` →
     `https://api-m.paypal.com` (`guides/authentication.mdx`,
     `guides/going-live.mdx`). Treat any other value as a startup configuration error
     (fail fast, don't silently default).
- **Binding env vars to the `PayPal:` section — this is the one place a naive
  implementation will silently do the wrong thing.** ASP.NET Core's
  `AddEnvironmentVariables()` (already called in `PublicApi/Program.cs`) maps
  `PayPal__ClientId` (double underscore) to `PayPal:ClientId`, but the task's env vars
  are `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`,
  `PAYPAL_CURRENCY` — single underscore, different casing, no automatic mapping.
  After `builder.Configuration.AddEnvironmentVariables()` (and ideally right before
  `builder.Build()`), explicitly bridge these four:
  ```csharp
  var paypalEnvBridge = new Dictionary<string, string?>
  {
      ["PayPal:ClientId"] = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"),
      ["PayPal:ClientSecret"] = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET"),
      ["PayPal:Environment"] = Environment.GetEnvironmentVariable("PAYPAL_ENVIRONMENT"),
      ["PayPal:Currency"] = Environment.GetEnvironmentVariable("PAYPAL_CURRENCY"),
  };
  builder.Configuration.AddInMemoryCollection(
      paypalEnvBridge.Where(kv => kv.Value != null).Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value)));
  ```
  (`AddInMemoryCollection` called after other sources wins, so this overrides
  whatever placeholder is in `appsettings.json`.) `PayPal:BaseUrl` has no dedicated
  single-underscore env var in the task brief — leave it bound the standard way
  (`appsettings.json` value, or the standard `PayPal__BaseUrl` double-underscore
  convention if the grader sets it that way); just make sure that whatever value ends
  up in configuration is honored per the precedence rule above.
- Bind a `PayPalOptions` class with `ClientId`, `ClientSecret`, `Environment`,
  `Currency`, `BaseUrl` from `configuration.GetSection("PayPal")`. Add a `PayPal`
  section with **empty placeholder strings** (not real values) to
  `src/PublicApi/appsettings.json` so the shape is documented in-repo:
  ```json
  "PayPal": {
    "ClientId": "",
    "ClientSecret": "",
    "Environment": "sandbox",
    "Currency": "USD",
    "BaseUrl": ""
  }
  ```

---

## 2. Domain model changes (additive)

All of this lives in the existing Clean Architecture split
(`ApplicationCore` → entities/interfaces/services, `Infrastructure` → EF configs/HTTP
clients, `PublicApi` → endpoints). Nothing here changes the *existing* Web storefront
checkout flow — `Order` gains new columns/relations that Web's flow simply never
populates (they stay in their default/`AwaitingPayment` state for Web-created orders,
which is harmless since Web never reads them).

### 2.1 `Order` (extend, `src/ApplicationCore/Entities/OrderAggregate/Order.cs`)

Add an `OrderStatus Status { get; private set; }` property (default
`AwaitingPayment`, so the existing constructor and existing Web-created rows keep
working unchanged), plus small state-transition methods (`MarkPaymentAuthorized()`,
`MarkFulfilled()`, `MarkCancelled()`, `MarkPartiallyRefunded()`, `MarkRefunded()`)
called only from the new PublicApi payment endpoints. New enum:

```csharp
namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public enum OrderStatus
{
    AwaitingPayment = 0,
    PaymentAuthorized,
    Fulfilled,
    Cancelled,
    PartiallyRefunded,
    Refunded
}
```

### 2.2 New `Payment` aggregate (`src/ApplicationCore/Entities/PaymentAggregate/`)

One `Payment` row per `Order` (1:1, created the first time `/pay` succeeds), owning a
collection of `Refund` rows.

```
Payment
  Id
  OrderId                (FK, unique)
  CurrencyCode
  Amount                 (decimal — order total at authorization time)
  IdempotencyKey         (Guid, generated once, reused as PayPal-Request-Id on retries)
  PayPalOrderId           (the /v2/checkout/orders id)
  AuthorizationId
  AuthorizationStatus     (raw PayPal string: CREATED/CAPTURED/DENIED/PARTIALLY_CAPTURED/VOIDED/PENDING)
  AuthorizationExpiresAt
  CaptureId
  CaptureStatus           (raw PayPal string: COMPLETED/DECLINED/PARTIALLY_REFUNDED/PENDING/REFUNDED/FAILED)
  CapturedAmount
  PayPalFeeAmount
  NetAmount
  CapturedAt
  Refunds                 (ICollection<Refund>, private backing field, DDD style like Order.OrderItems)
```

```
Refund
  Id
  PaymentId               (FK)
  PayPalRefundId
  Amount
  Status                  (raw PayPal string: COMPLETED/PENDING/CANCELLED/FAILED)
  IdempotencyKey           (caller-supplied string, per task requirement — unique together with PaymentId)
  CreatedAt
```

Deliberately store PayPal's own status strings verbatim rather than re-deriving a
parallel enum for every PayPal state — they're already meaningful and keeping them
raw avoids the two drifting apart. `Order.Status` is the one enum that's ours to
define, because it's the thing the rest of the app (and the task's endpoint
contracts) actually reasons about.

### 2.3 New `PaymentMethod` entity (`src/ApplicationCore/Entities/PaymentAggregate/PaymentMethod.cs`)

```
PaymentMethod
  Id
  BuyerId                 (owner — same string as Order.BuyerId)
  PayPalPaymentTokenId     (the Vault payment token / vault_id)
  CardBrand
  LastDigits
  ExpiryMonthYear
  CreatedAt
```

No raw PAN/CVV field exists anywhere in this entity or anywhere else in the app's own
storage — by construction, since only `last_digits`/`brand`/`expiry` ever come back
from Vault's payment-token response.

### 2.4 EF configuration / DbContext

- `CatalogContext` already has `DbSet<Order> Orders` / `DbSet<OrderItem> OrderItems`
  (`src/Infrastructure/Data/CatalogContext.cs`) — add `DbSet<Payment> Payments`,
  `DbSet<Refund> Refunds`, `DbSet<PaymentMethod> PaymentMethods` there, following the
  existing pattern (Order/Basket both live in this one context).
- Add `PaymentConfiguration`, `RefundConfiguration`, `PaymentMethodConfiguration` under
  `src/Infrastructure/Data/Config/`, mirroring `OrderConfiguration.cs` /
  `OrderItemConfiguration.cs` (private-collection navigation via
  `builder.Metadata.FindNavigation(...).SetPropertyAccessMode(PropertyAccessMode.Field)`
  for `Payment.Refunds`, required string length limits, decimal precision on money
  columns, a unique index on `Payment.OrderId`, and a unique index on
  `(Refund.PaymentId, Refund.IdempotencyKey)` to make the app-level refund
  idempotency check a DB constraint, not just application logic).
- Generate an EF Core migration for the SQL Server provider (`dotnet ef migrations add
  AddPayPalPayments --project src/Infrastructure --startup-project src/PublicApi`) for
  completeness/production-readiness. This environment has no LocalDB and runs with
  `UseOnlyInMemoryDatabase=true`, which **ignores migrations entirely** (the in-memory
  provider builds its schema from the model directly) — so the migration isn't
  exercised by anything in this sandbox and self-verification will run fine without
  it. Generate it anyway so a real SQL Server deployment isn't left broken; if the
  `dotnet-ef` tooling can't run in this sandbox for some environment reason, note that
  clearly rather than silently skipping it.

---

## 3. PayPal client (Infrastructure)

Keep `ApplicationCore` free of HTTP/PayPal-specific types, matching the existing
`IUriComposer`/`IAppLogger<T>` pattern (interface in Core, implementation in
Infrastructure). Suggested interface shape in
`src/ApplicationCore/Interfaces/` (one interface is fine — no need to split it into
one per PayPal product):

```csharp
public interface IPayPalClient
{
    Task<PayPalAuthorizationResult> AuthorizeOrderAsync(PayPalAuthorizeOrderRequest request, CancellationToken ct);
    Task<PayPalAuthorizationDetails> GetAuthorizationAsync(string authorizationId, CancellationToken ct);
    Task<PayPalAuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, CancellationToken ct);
    Task VoidAuthorizationAsync(string authorizationId, CancellationToken ct);
    Task<PayPalCaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string invoiceId, string requestId, CancellationToken ct);
    Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string? currency, string requestId, CancellationToken ct);
    Task<PayPalVaultToken> CreateVaultedCardAsync(PayPalCardDetails card, string customerId, CancellationToken ct);
    Task<IReadOnlyList<PayPalVaultToken>> ListVaultedCardsAsync(string customerId, CancellationToken ct);
    Task DeleteVaultedCardAsync(string vaultTokenId, CancellationToken ct);
    Task<PayPalTransactionSearchPage> SearchTransactionsAsync(DateTimeOffset start, DateTimeOffset end, int page, int pageSize, CancellationToken ct);
}
```

Implementation in `src/Infrastructure/Services/PayPal/` (a typed `HttpClient`
registered via `IHttpClientFactory`, which is available without any extra NuGet
package since `PublicApi.csproj` targets the ASP.NET Core shared framework
(`Microsoft.NET.Sdk.Web`)). Responsibilities:

- Token acquisition/caching as described in §1.4.
- Attaching `Authorization: Bearer <token>`, `PayPal-Request-Id` (only where the
  caller supplies one — it's per-operation idempotency, not global), and
  `Prefer: return=representation` on every mutating call.
- JSON (de)serialization with `System.Text.Json` (already used elsewhere in the app),
  mapping only the fields this integration actually needs (don't model the entire
  PayPal schema — e.g. no need to model `seller_protection`, `shipping`, etc.).
- Centralized error translation: on non-2xx, parse PayPal's standard error envelope
  (`name`, `message`, `debug_id`, `details[]` with `issue`/`field`/`description` —
  confirmed shape from `reference/errors.mdx` and every endpoint's `400`/`422`
  response schema) into a `PayPalApiException` carrying the HTTP status, `name`,
  `debug_id`, and the `details` list. Let `PublicApi`'s endpoints catch this and map
  it to an appropriate client-facing status (see §5.6) — log `debug_id` for
  operator follow-up, never log full request/response bodies (they can contain card
  data on the outbound side).
- **Never log or persist card number / CVV.** They exist only inside the outbound
  request DTOs for the single call that needs them (create-order /
  create-setup-token) and are discarded immediately after. Don't add any
  request/response body logging middleware to `PublicApi` as part of this work, and
  keep the card DTO's `ToString()`/serialization scoped so a future logging change
  can't accidentally dump it (e.g. don't give the card request type a default
  `record` positional `ToString()` that prints all properties — write it as a
  plain class, or override `ToString()` to redact).

---

## 4. `PublicApi` — new endpoints

Follow the existing `MinimalApi.Endpoint` `IEndpoint<TResponse, TRequest, TDependency>`
convention (see `CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`,
`CatalogItemGetByIdEndpoint.cs`): one file (or file-per-class-partial, matching the
existing `Endpoint.Request.cs` / `Endpoint.Response.cs` split already used under
`CatalogItemEndpoints/` and `AuthEndpoints/`) per endpoint, registered via
`app.MapPost/MapGet(...)`, `[Authorize(AuthenticationSchemes =
JwtBearerDefaults.AuthenticationScheme)]` for shopper-scoped endpoints, plus `Roles =
BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS` for the three operator
endpoints. `AddRoute` methods self-register — `Program.cs` already calls
`builder.Services.AddEndpoints()` / `app.MapEndpoints()`, which discovers them by
scanning the assembly, so no central route table to edit.

Get the caller's identity the same way the rest of the app does — `ClaimTypes.Name`
(this is what `ApiTokenHelper` in the test project puts in the JWT, and what
`Order.BuyerId` already stores for Web-created orders). Minimal API handlers can bind
`ClaimsPrincipal` directly as a parameter.

New folders: `src/PublicApi/OrderEndpoints/`, `src/PublicApi/PaymentMethodEndpoints/`,
`src/PublicApi/ReconciliationEndpoints/`.

### 4.1 `POST /api/orders` — shopper

- Request: list of `{ catalogItemId, quantity }` plus a shipping `Address` (reuse
  `ApplicationCore.Entities.Address`, the same value object `Order` already takes).
- Handler: resolve each `catalogItemId` via the existing `CatalogItemsSpecification`
  (see `OrderService.CreateOrderAsync` for the pattern) against
  `IRepository<CatalogItem>`, build `OrderItem`s from current catalog prices (never
  trust a client-supplied price), construct `new Order(buyerId, shipToAddress,
  items)` (status defaults to `AwaitingPayment`), persist via `IRepository<Order>`.
- Response: `orderId` (top-level, per the task's response-identifier requirement) plus
  the order total/currency for convenience.
- Deliberately **don't** reuse `IOrderService.CreateOrderAsync(basketId, address)` —
  that method is basket-shaped and Web-specific. Build the order directly against the
  repositories in the endpoint, the same way `CreateCatalogItemEndpoint` builds a
  `CatalogItem` directly rather than going through some Web-only service. This keeps
  the change additive and leaves Web's basket checkout path untouched.

### 4.2 `POST /api/orders/{orderId}/pay` — shopper

- Request: **either** `card: { number, expiryMonth, expiryYear, cvv, cardholderName,
  billingAddress }` **or** `paymentMethodId: <id>` — reject if both or neither are
  present (`400`).
- Load the order; `404` if it doesn't exist or `order.BuyerId != caller` (don't
  distinguish "not found" from "not yours" in the response — see §5.5).
- **Idempotency / state machine** (this is what "a double-click never authorizes the
  shopper twice" means in practice):
  - `order.Status == Fulfilled/Cancelled/...` (anything past `PaymentAuthorized`) →
    `409` — payment already resolved one way or another, nothing to do.
  - `order.Status == PaymentAuthorized` and a `Payment` row already exists → this is a
    repeat call; return the **existing** `Payment`'s authorization details as `200`,
    do **not** call PayPal again.
  - `order.Status == AwaitingPayment` and no `Payment` row yet → create one (with a
    freshly generated `IdempotencyKey`) and persist it **before** calling PayPal, so a
    process crash between "we decided to authorize" and "PayPal confirmed it" leaves a
    row to retry against with the same key rather than silently forgetting the
    attempt.
  - `order.Status == AwaitingPayment` and a `Payment` row already exists in a
    not-yet-authorized state (a prior attempt's PayPal call failed/timed out) → retry
    using the **same stored** `IdempotencyKey` as `PayPal-Request-Id`, so PayPal's own
    idempotency (6-hour retention on order creation, per `guides/making-requests.mdx`)
    also protects against a duplicate authorization if the original call actually
    succeeded on PayPal's side but the response was lost.
- Build `payment_source.card` from the request, or `payment_source.card.vault_id`
  from the caller's `PaymentMethod` (verify `PaymentMethod.BuyerId == caller` first —
  `404` otherwise, same non-distinguishing rule as above). `intent: AUTHORIZE`,
  `purchase_units[0].amount` = order total in `PayPal:Currency`, `invoice_id` /
  `custom_id` = the deterministic order-derived value from §1.3.
- On success: populate the `Payment` row's `PayPalOrderId`/`AuthorizationId`/
  `AuthorizationStatus`/`AuthorizationExpiresAt`; set `order.Status =
  PaymentAuthorized`.
- Response: authorization id/status/expiry, amount, currency.

### 4.3 `POST /api/orders/{orderId}/fulfil` — **operator** (`Administrators` role)

- Load order + payment. `order.Status == Fulfilled` already → return the existing
  capture as `200` (idempotent no-op). `order.Status != PaymentAuthorized` for any
  other reason → `409`.
- `GET` the authorization's current status/expiry from PayPal (don't trust a possibly
  stale local copy for this decision). If it's expired but within the
  4–29-day reauthorize window, reauthorize first (§1.1) and update the stored
  authorization id/status/expiry with whatever the reauthorize response returns before
  proceeding. If reauthorization fails or the window has passed, return a `422` (or
  `409`, your call — document whichever you pick) whose message tells the operator
  plainly: the hold can no longer be renewed; void/cancel this order and have the
  shopper place and pay for a new one.
- Capture the (possibly renewed) authorization for the full order amount,
  `final_capture: true`, `invoice_id` set again to the same deterministic value.
- On success: populate `CaptureId`/`CaptureStatus`/`CapturedAmount`/`PayPalFeeAmount`/
  `NetAmount`/`CapturedAt`; set `order.Status = Fulfilled`.
- Response: capture id/status, captured amount, PayPal fee, net proceeds.

### 4.4 `POST /api/orders/{orderId}/cancel` — **operator**

- `order.Status == Cancelled` already → `200` no-op.
- `order.Status == AwaitingPayment` (never authorized) → just mark `Cancelled`, no
  PayPal call needed (nothing was ever held).
- `order.Status == PaymentAuthorized` → void the authorization (§1.1), then mark
  `Cancelled`.
- `order.Status == Fulfilled` or beyond → `409` — money already moved, this is a
  refund situation, not a cancel.

### 4.5 `POST /api/orders/{orderId}/refunds` — **shopper-scoped**

Note the task's operator list is explicitly "fulfil, cancel and reconciliation" —
refunds are **not** in it, and the task states "every other endpoint is
shopper-scoped and acts only on the caller's own data." So this endpoint is
authorized the same way as `/pay` (caller must own the order), not restricted to
`Administrators`. Flag this reading prominently in code comments/PR description if
it looks surprising — it's a literal reading of an explicit sentence in the task, not
an oversight.

- Load order + payment; `404` if not found or not the caller's.
- `order.Status` must be `Fulfilled` or `PartiallyRefunded` — otherwise `409` (nothing
  captured yet to refund).
- Request: `{ amount?: decimal, idempotencyKey: string }` (`idempotencyKey` required —
  the task specifies it's caller-supplied). Omitted `amount` = refund the remainder.
- **Idempotency**: look up an existing `Refund` row for `(PaymentId, IdempotencyKey)`
  first (this should also be a DB unique constraint per §2.4, so a race can't slip a
  duplicate through). If found, return it as-is (`200`) without calling PayPal again —
  this is what makes "repeating a request under the same key must not refund twice"
  true even under concurrent retries. A **different** `idempotencyKey` for a
  **different** partial amount is a new, legitimate refund.
- **Over-refund guard**: before calling PayPal, check `amount (or remainder) <=
  Payment.CapturedAmount - (sum of Payment.Refunds where Status == COMPLETED)`;
  `422` if not. This is a belt-and-suspenders check — PayPal enforces the same rule
  server-side — but it gives a clean, immediate error instead of a round-trip failure.
- Call `refund-captured-payment` with `PayPal-Request-Id` derived from
  `(orderId, idempotencyKey)` so PayPal's own dedup lines up with ours.
- On success: insert the `Refund` row; recompute total refunded; set `order.Status =
  PartiallyRefunded` if less than the captured amount remains outstanding, otherwise
  `Refunded`.
- Response: `refundId` (top-level, PayPal's refund id — per the task's
  response-identifier requirement), amount, status.

### 4.6 `GET /api/my-orders` — shopper

- All orders where `BuyerId == caller`, each with its order status, total/currency,
  and (if present) payment summary: authorization status, capture status/amount/fee/
  net, and total refunded. A simple DTO combining `Order` + `Payment` (+ `Refunds`)
  fields — no need for a separate read-model/specification beyond something like
  `CustomerOrdersWithItemsSpecification` extended to also `.Include(o => o.Payment)`.

### 4.7 `GET /api/reconciliation?from={iso8601}&to={iso8601}` — **operator**

- Validate `from`/`to` parse as ISO-8601 date-times and `to > from`; `400` otherwise.
- Chunk `[from, to]` into ≤31-day windows (§1.3); for each chunk, page through
  `SearchTransactionsAsync` until exhausted; concatenate all results across chunks and
  pages.
- Also load all eShop `Payment`s whose `CapturedAt` (or `AuthorizationExpiresAt`/
  creation time — pick whichever you consider the order's canonical "when did this
  happen" timestamp, and say which in the response) falls in `[from, to]`.
- Match PayPal transactions to eShop orders via the deterministic invoice id
  (§1.3). Report three buckets: `matched` (present on both sides, with both
  amounts so a mismatch is visible), `payPalOnly` (PayPal has it, eShop's local record
  doesn't match — real red flag), `eShopOnly` (eShop captured a payment but PayPal's
  transaction-search hasn't surfaced it yet — expected for very recent activity per
  §1.3, not necessarily a red flag). Include the resolved `from`/`to` and how many
  PayPal transactions were scanned, so the caller can tell the report actually walked
  the whole range rather than silently truncating.

---

## 5. Cross-cutting concerns

### 5.1 Amount formatting

`Order.Total()` is a `decimal`. Format `amount.value` as an invariant-culture string
with the currency's standard decimal places (2 for USD, which is what this task's
sandbox uses per `PAYPAL_CURRENCY`). Don't overbuild a full ISO-4217 minor-unit table
for every currency — a documented assumption of 2 decimal places, with a comment
noting zero-decimal currencies like JPY would need different handling, is enough for
this task's scope.

### 5.2 One purchase unit, no breakdown

As noted in §1.1 — a single `purchase_units[0].amount` with no `breakdown`/`items[]`,
sized to `Order.Total()`. This sidesteps an entire class of PayPal validation errors
(`ITEM_TOTAL_MISMATCH`, `TAX_TOTAL_MISMATCH`, etc. — `reference/errors.mdx`) that only
apply when a breakdown is present, and keeps catalog-price-to-PayPal-amount mapping
trivial and exact "to the cent."

### 5.3 Authorization header vs. role checks

Shopper-scoped endpoints: `[Authorize(AuthenticationSchemes =
JwtBearerDefaults.AuthenticationScheme)]` (any authenticated caller; the *ownership*
check — `BuyerId == caller` — is application logic inside the handler, exactly like
`CreateCatalogItemEndpoint` checks business rules after the `[Authorize]` attribute
has already gated authentication/role).

Operator-scoped endpoints (fulfil, cancel, reconciliation): add `Roles =
BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS` to the same attribute,
exactly as `CreateCatalogItemEndpoint` already does for catalog mutation.

### 5.4 Error-status mapping (suggested, not prescriptive beyond what the task requires)

| Situation | Status |
|---|---|
| Malformed request (both/neither card & paymentMethodId, bad ISO date, etc.) | `400` |
| Missing/invalid JWT | `401` (handled by the existing JwtBearer middleware) |
| Authenticated but wrong role for an operator endpoint | `403` (existing `[Authorize(Roles=...)]` behavior) |
| Order/PaymentMethod not found, or found but not the caller's | `404` |
| Valid request but wrong order state for the action (pay a cancelled order, fulfil a non-authorized order, cancel a fulfilled order, refund a not-yet-captured order) | `409` |
| Business-rule violation PayPal or the app itself rejects (over-refund, card declined, authorization no longer renewable) | `422` |
| PayPal transport/5xx failure | `502` (or `503` for a clear "try again" case) |

### 5.5 Tenant isolation

Never let a shopper distinguish "this order doesn't exist" from "this order exists but
isn't yours" — return `404` for both. Same for `PaymentMethod`. This is a small
detail but it's what "one shopper must never *see* [...] another's" actually requires
at the HTTP-response level, not just at the data-access level.

### 5.6 PCI-adjacent hygiene

- Card number/CVV live only in the request DTOs for the two calls that legitimately
  need them (authorize-with-card, create-setup-token-with-card) and are never written
  to the database, never included in any response DTO, and never logged (see §3's note
  on avoiding a `ToString()` that leaks them, and on not adding request-body logging
  middleware).
- Everything the app itself stores about a card is `last_digits` + `brand` +
  `expiry` — sourced from PayPal's vault response, never derived locally from the raw
  PAN.

---

## 6. File-by-file checklist for the build session

**ApplicationCore**
- `Entities/OrderAggregate/Order.cs` — add `Status` + transition methods
- `Entities/OrderAggregate/OrderStatus.cs` — new enum
- `Entities/PaymentAggregate/Payment.cs` — new
- `Entities/PaymentAggregate/Refund.cs` — new
- `Entities/PaymentAggregate/PaymentMethod.cs` — new
- `Interfaces/IPayPalClient.cs` (+ small DTO types it uses) — new
- Optional: `Specifications/CustomerOrdersWithPaymentSpecification.cs` for §4.6

**Infrastructure**
- `Data/CatalogContext.cs` — add three `DbSet<>`s
- `Data/Config/PaymentConfiguration.cs`, `RefundConfiguration.cs`,
  `PaymentMethodConfiguration.cs` — new
- `Data/Migrations/...AddPayPalPayments...` — new (SQL Server provider; see §2.4 note
  on why the in-memory sandbox run doesn't need it)
- `Services/PayPal/PayPalOptions.cs` — new
- `Services/PayPal/PayPalClient.cs` (+ internal request/response DTOs, error
  envelope type, `PayPalApiException`) — new
- Wherever `Dependencies.ConfigureServices` (or `PublicApi/Program.cs` directly) wires
  up services — register `PayPalOptions` binding, `IHttpClientFactory` typed client
  for `IPayPalClient`

**PublicApi**
- `Program.cs` — env var bridge (§1.4), `PayPalOptions` binding, `IPayPalClient`
  DI registration
- `appsettings.json` — `PayPal` section with placeholder empty values
- `OrderEndpoints/CreateOrderEndpoint.cs` (+ Request/Response)
- `OrderEndpoints/PayOrderEndpoint.cs` (+ Request/Response)
- `OrderEndpoints/FulfilOrderEndpoint.cs` (+ Response)
- `OrderEndpoints/CancelOrderEndpoint.cs` (+ Response)
- `OrderEndpoints/RefundOrderEndpoint.cs` (+ Request/Response)
- `OrderEndpoints/MyOrdersEndpoint.cs` (+ Response)
- `PaymentMethodEndpoints/CreatePaymentMethodEndpoint.cs` (+ Request/Response)
- `PaymentMethodEndpoints/ListPaymentMethodsEndpoint.cs` (+ Response)
- `PaymentMethodEndpoints/DeletePaymentMethodEndpoint.cs` (+ Response)
- `ReconciliationEndpoints/ReconciliationEndpoint.cs` (+ Request/Response)

**Tests** (`tests/PublicApiIntegrationTests/`) — add an `OrderEndpoints/` and
`PaymentMethodEndpoints/` folder mirroring the existing `CatalogItemEndpoints/` test
structure, reusing `ApiTokenHelper.GetAdminUserToken()` /
`GetNormalUserToken()` for auth. These will need real (env-var-supplied) PayPal
sandbox credentials to run against the live sandbox — treat them as integration tests
gated the same way the rest of this task is (they need `PAYPAL_CLIENT_ID` etc. set),
not unit tests with a mocked `IPayPalClient`. A handful of `UnitTests` around the pure
state-machine logic (idempotency short-circuits, over-refund guard, status transition
rules) with a mocked `IPayPalClient` are cheap and worth adding.

---

## 7. Running in this environment

- SDK/runtime mismatch: only .NET 10 SDK is present; `global.json` pins 8.0.x. Add/
  keep `rollForward: latestMajor` in `global.json` (or run with `DOTNET_ROLL_FORWARD=Major`
  in the environment) so `dotnet build`/`run` don't fail on the missing 8.0 runtime.
- No LocalDB: run `PublicApi` with `UseOnlyInMemoryDatabase=true` (env var or
  `appsettings`). Remember: in-memory data doesn't survive a restart and each host
  (Web vs. PublicApi) gets its own isolated store — this is exactly why every flow in
  this plan is drivable through `PublicApi` alone (`POST /api/orders` included), so
  there's no dependency on Web's basket/checkout ever running.
- Set `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT=sandbox`,
  `PAYPAL_CURRENCY` (e.g. `USD`) in the environment before starting `PublicApi`.
- Trust the HTTPS dev cert (`dotnet dev-certs https --check`, `--trust` if needed) —
  `PublicApi` calls `UseHttpsRedirection()`.
- Bind only within the assigned port block (`APP_PORT_BLOCK_BASE`..`+APP_PORT_BLOCK_SIZE-1`);
  `launchSettings.json` should already point there. Stop any previously running
  instance before starting a new build.

---

## 8. Self-verification checklist (for the build session to execute and report on)

All of this is drivable purely through `PublicApi` HTTP calls (curl/Postman) — no
browser, per the task. Use PayPal's sandbox Visa test card
`4111 1111 1111 1111`, any future expiry, any CVC, any name/billing address.

1. `dotnet build` the whole solution — zero errors.
2. Start `PublicApi` with the env vars from §7.
3. `POST /api/authenticate` as `demouser@microsoft.com` (or whichever seeded shopper
   account exists — confirm via `AppIdentityDbContextSeed`) → shopper JWT. Same for
   `admin@microsoft.com` → operator JWT.
4. `GET /api/catalog-items` → pick a real `catalogItemId`.
5. **Pay flow, direct card**: `POST /api/orders` (shopper) → `orderId`. `POST
   /api/orders/{orderId}/pay` (shopper) with the test card → confirm a real
   `authorizationId` and `CREATED` status came back from PayPal (not a stub).
6. **Fulfil**: `POST /api/orders/{orderId}/fulfil` (admin) → confirm a real
   `captureId`, `COMPLETED` status, and non-zero `paypalFee`/`netAmount` came back.
7. **Cancel-before-fulfil**: place a second order, pay it, then `POST
   /api/orders/{orderId2}/cancel` (admin) **before** fulfilling — confirm the
   authorization is voided on PayPal's side (e.g. `GET` the authorization directly, or
   confirm a subsequent fulfil attempt now `409`s) and no capture exists.
8. **Refunds**: on the fulfilled order from step 6, `POST
   /api/orders/{orderId}/refunds` (shopper, as the order's owner) for a partial
   amount with `idempotencyKey: "r1"` → confirm `refundId` returned and
   `order.Status == PartiallyRefunded`. Repeat the exact same request (same
   `idempotencyKey`) → confirm it returns the **same** `refundId` and does not create
   a second PayPal refund. Refund the remainder with `idempotencyKey: "r2"` →
   confirm `order.Status == Refunded`. Attempt to refund again (any key, any amount)
   → confirm `422`, not a second real refund.
9. **Saved card**: `POST /api/payment-methods` (shopper) with the test card →
   `paymentMethodId`; confirm the response never contains the full card number.
   `GET /api/payment-methods` → confirm it's listed with only brand/last4/expiry.
   Place a **third** order and `POST .../pay` using `{ paymentMethodId }` instead of
   raw card fields → confirm it authorizes successfully (this is the "saved card
   reused to pay a second order" the task explicitly asks to verify). `DELETE
   /api/payment-methods/{id}` → confirm it disappears from `GET
   /api/payment-methods` and that attempting to pay a new order with that
   `paymentMethodId` now fails cleanly.
10. **Tenant isolation**: as one shopper, attempt `GET`/`pay`/`refund` on another
    shopper's order (or another shopper's saved card) → confirm `404` in every case,
    and confirm `GET /api/my-orders` for each shopper only shows their own orders.
11. **Reconciliation**: `GET /api/reconciliation?from=...&to=...` (admin) over a
    window that safely covers steps 5–9 → inspect the `matched`/`payPalOnly`/
    `eShopOnly` buckets. If very recent captures show up under `eShopOnly` because
    PayPal's reporting hasn't caught up yet, note that explicitly as the expected
    sandbox-lag behavior described in §1.3 rather than a failure — and additionally
    verify the endpoint's 31-day-chunking logic actually runs correctly by requesting
    a `from`/`to` spanning more than 31 days and confirming no `400`/`INVALID_REQUEST`
    from PayPal and no silently-dropped chunk.
12. Confirm nothing in application logs (console output from the `PublicApi` process
    during all of the above) contains a full card number or CVV.

If, at any point during steps 5–9, PayPal responds with a payer-action / 3DS-challenge
requirement (e.g. a `PAYER_ACTION_REQUIRED` status or a response containing a
`payer-action` HATEOAS link), **stop and report it** rather than building a
browser-approval round-trip — this is an explicit STOP condition in the task brief,
not a bug to route around.

### Deliverable at the end of the build session

Once the above passes, the build session should write a concise, copy-pasteable,
step-by-step guide (exact `curl` commands or an exported Postman collection) that lets
someone else repeat steps 3–11 themselves against a freshly started instance — this is
required by the task's "Rules of engagement" and is the build session's
responsibility to produce, not this plan's.
