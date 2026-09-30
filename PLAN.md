# PayPal payments + saved cards for eShopOnWeb — implementation plan

This is a plan for a **separate build session**. It does not assume any memory of this
session. Every file path, type name, and PayPal endpoint below was verified against the
actual repository and the actual spec files in `api-specs/paypal/` during planning.

No code in this repo was changed to produce this plan.

---

## 1. Which PayPal specs this task needs, and why

`api-specs/paypal/` contains five documents. All servers in all five point at
`https://api-m.sandbox.paypal.com` (sandbox) — that host is the *default*; `PayPal:BaseUrl`,
when configured, overrides it for every call, token request included.

| Spec | Used for | Key operations |
|---|---|---|
| `checkout_orders_v2` | Creating a PayPal order and **authorizing** the hold | `POST /v2/checkout/orders` (intent=`AUTHORIZE`) |
| `payments_payment_v2` | Everything after authorization: capture, reauthorize, void, refund | `POST /v2/payments/authorizations/{id}/capture`, `.../reauthorize`, `.../void`; `GET /v2/payments/authorizations/{id}`; `POST /v2/payments/captures/{id}/refund`; `GET /v2/payments/captures/{id}`, `GET /v2/payments/refunds/{id}` |
| `vault_payment_tokens_v3` | Saved cards | `POST /v3/vault/payment-tokens`, `GET /v3/vault/payment-tokens`, `GET/DELETE /v3/vault/payment-tokens/{id}` |
| `transaction_search_v1` | Reconciliation report | `GET /v1/reporting/transactions` |
| `billing_subscriptions_v1` | **Not used.** This task has no recurring billing; nothing here calls it. Confirmed by reading its full path list — only plans/subscriptions endpoints, no relevance to one-off card authorize/capture/refund/vault. |

The OAuth2 token endpoint (`POST /v1/oauth2/token`, HTTP Basic auth with client id/secret,
`grant_type=client_credentials`) is **not** a `paths` entry in any of the five documents — it
only appears as the `tokenUrl` of the shared `Oauth2` `securityScheme`
(`components.securitySchemes.Oauth2.flows.clientCredentials.tokenUrl` = `/v1/oauth2/token`),
identical across all five specs. That security-scheme object *is* the contract for
authentication per the task's own framing ("auth scheme ... come from the spec"), so treat it
as such — do not look this up elsewhere. This is not a gap.

### The one flow decision the spec settles for you

`checkout_orders_v2`'s `/v2/checkout/orders/{id}/authorize` and `.../capture` sub-resource
endpoints exist for the *buyer-redirect* flow (buyer approves via `rel:approve` link, then you
authorize/capture). For a **direct card payment** (or a saved card via `vault_id`), you supply
`payment_source` straight into the **create-order** call
(`POST /v2/checkout/orders`, `intent: "AUTHORIZE"`), and PayPal processes it synchronously —
no buyer redirect, no separate `/authorize` call needed. The created order's
`purchase_units[0].payments.authorizations[0]` is already populated in the create-order
response. This is confirmed by `card_request.vault_id`
("The PayPal-generated ID for the saved card payment source") existing directly on the
`payment_source.card` object used in `order_request` — saved-card reuse is just
`payment_source.card.vault_id = <paymentMethodId>` on the same create-order call, no
special-casing needed between "one-off card" and "saved card" beyond which field of
`payment_source.card` is populated.

`checkout_payment_intent`'s spec description states outright: **AUTHORIZE intent is not
supported when you have more than one `purchase_unit`**. So every PayPal order this
integration creates uses exactly one `purchase_unit`, whose `amount.value` is the eShop
order's total to the cent, `amount.currency_code` from `PayPal:Currency`, and
`custom_id` set to the eShop order id (`custom_id` is documented as: "Used to reconcile API
caller-initiated transactions with PayPal transactions. Appears in transaction and settlement
reports" — exactly what `GET /api/reconciliation` needs).

So the mapping from task flows to PayPal calls is:

- **Pay (authorize)** → `POST /v2/checkout/orders` (intent=AUTHORIZE, single purchase_unit,
  `payment_source.card` = either full card fields or `{vault_id}`). Capture the
  `authorization.id`, `.status`, `.amount`, `.expiration_time` from the response.
- **Fulfil (capture)** → `GET /v2/payments/authorizations/{id}` to check current status and
  `expiration_time`; if capturable, `POST .../capture`; if expired, `POST .../reauthorize`
  first, then capture. Persist `capture.id`, `.status`, and
  `seller_receivable_breakdown.gross_amount` / `.paypal_fee` / `.net_amount` — these three
  fields are exactly "the captured amount, PayPal's fee, and the net proceeds" the task asks
  for.
- **Cancel (before fulfilment)** → `POST /v2/payments/authorizations/{id}/void`.
- **Refund (after fulfilment)** → `POST /v2/payments/captures/{id}/refund`, full (empty body)
  or partial (`amount`), with the caller's idempotency key sent as the `PayPal-Request-Id`
  header.
- **Save a card** → `POST /v3/vault/payment-tokens` with `payment_source.card` (name, number,
  expiry, security_code, billing_address) and `customer.merchant_customer_id` = the buyer's
  eShop identity string. Response gives the vault token `id` (this becomes eShop's
  `paymentMethodId`) and a PayPal-generated `customer.id` — store that customer id (see §3)
  so later listing/lookup doesn't have to guess it.
- **List/delete saved cards** → `GET /v3/vault/payment-tokens?customer_id=...`,
  `DELETE /v3/vault/payment-tokens/{id}`.
- **Reconciliation** → `GET /v1/reporting/transactions`, see §6 for the pagination design
  this spec forces on you.

### Idempotency headers the spec already gives you

Every mutating PayPal call in scope (`create order`, `authorize`, `capture`, `void`,
`reauthorize`, `refund`, `vault create`) accepts an optional `PayPal-Request-Id` header,
documented as "The server stores keys for 45 days" — PayPal's own idempotency key. Use it for
every one of these calls:

- For pay/fulfil/cancel (no caller-supplied key in the task's contract), derive a
  **deterministic** value per logical action, e.g. `eshop-order-{orderId}-authorize`,
  `eshop-order-{orderId}-capture`, `eshop-order-{orderId}-void`. A retried click reuses the
  same derived key, so PayPal itself de-dupes it. Layer this with a same-effect check on our
  own side too (see §4) so a retry after our own DB write succeeded, but before the HTTP
  response reached the caller, is still a no-op both locally and at PayPal.
- For refunds, the task requires a **caller-supplied** idempotency key — pass it straight
  through as `PayPal-Request-Id` verbatim. Store it alongside the refund row so a repeat
  request under the same key can be recognized and answered from our own state without
  calling PayPal again (belt-and-braces alongside PayPal's own 45-day dedupe window).

### The one thing to stop and check, not build around

If a create-order (or capture) response comes back with order/payment status
`PAYER_ACTION_REQUIRED` (PayPal asking for a 3-D Secure/buyer approval step), that is the
"challenge requiring browser approval" the task says to stop and report — do not build an
approval round-trip. The sandbox test card (`4111 1111 1111 1111`) is not expected to trigger
this for a direct, non-3DS-forced integration, but detect it (check `order.status` /
`authorization.status` after the create-order call) and fail loudly with a clear message
instead of silently treating it as authorized.

---

## 2. Domain model changes (`src/ApplicationCore`)

### 2.1 `Order` gets a status

`Order` (`Entities/OrderAggregate/Order.cs`) currently has no state machine at all — it's
written once by `OrderService.CreateOrderAsync` and never touched again. Add:

- `OrderStatus` enum (new file `Entities/OrderAggregate/OrderStatus.cs`):
  `AwaitingPayment`, `PaymentAuthorized`, `Fulfilled`, `Cancelled`. (Refund state lives on the
  `Payment`, not the `Order` — an order stays `Fulfilled` whether or not it was later
  refunded; the `Payment.CaptureStatus`/`RefundedAmount` carry that detail. This keeps the two
  concerns — "did the shopper get their stuff" vs "what happened to the money" — from being
  tangled into one enum.)
- `Order.Status { get; private set; }`, defaulting to `AwaitingPayment` in the constructor.
- Behavior methods with guard clauses for legal transitions only, e.g.
  `MarkPaymentAuthorized()`, `MarkFulfilled()`, `MarkCancelled()` — each throws if called from
  the wrong state (e.g. `MarkFulfilled()` from anything but `PaymentAuthorized` throws), so the
  entity itself — not the endpoint — is the source of truth for what's legal. This mirrors the
  existing DDD style already used for `OrderItems`.
- A `New EF Core migration for CatalogContext adding an OrderStatus column (int) with a
  default — this is a real migration file (in-memory dev runs won't apply it, but production
  SQL Server runs need it; do not skip it just because local dev uses
  `UseOnlyInMemoryDatabase=true`).

### 2.2 New `Payment` entity — one per `Order`

New folder `Entities/OrderAggregate/` gets a `Payment.cs` (child of the `Order` aggregate,
same aggregate root, not its own `IAggregateRoot` — it's always loaded/saved through `Order`,
same pattern as `OrderItem`). Fields:

- `OrderId` (FK)
- `Currency` (from `PayPal:Currency` at authorization time — freeze it on the payment, don't
  re-read config later)
- `PayPalOrderId` — the v2 checkout order id from the create-order call
- `AuthorizationId`, `AuthorizationStatus` (string mirroring PayPal's enum:
  `CREATED`/`CAPTURED`/`DENIED`/`PARTIALLY_CAPTURED`/`VOIDED`/`PENDING`), `AuthorizedAmount`,
  `AuthorizationExpiresAt` (from `expiration_time`)
- `CaptureId`, `CaptureStatus`, `CapturedAmount`, `PayPalFee`, `NetAmount`, `CapturedAt`
- `RefundedAmount` (running total, updated as `Refund` children are added)
- `PaymentMethodId` (nullable FK to the saved card used, if any — null for one-off card pay)
- A private `List<Refund>` + `IReadOnlyCollection<Refund> Refunds` — same
  encapsulated-collection pattern as `Order.OrderItems`.

New `Refund.cs` (child of `Payment`): `PayPalRefundId`, `Amount`, `Status`, `IdempotencyKey`
(the caller-supplied key — unique-indexed alongside `PaymentId` so EF/the DB itself rejects a
second row for a repeat key rather than relying purely on application logic), `CreatedAt`.

Behavior lives on `Payment`, not the endpoints: `Authorize(...)`, `Capture(...)`,
`RecordReauthorization(...)`, `Void()`, `AddRefund(...)` — each does its own state guard
(e.g. `AddRefund` throws `Guard`-style if `refundedAmount + newAmount > CapturedAmount`, so
"never refundable beyond what was captured" is enforced in one place regardless of which
endpoint calls it, and is exercised directly by a unit test rather than only indirectly via an
HTTP round-trip).

Add `DbSet<Payment>` (and EF will pick up `Refund` as an owned/child collection, no separate
`DbSet` needed) to `CatalogContext`, and a `PaymentConfiguration` /
`RefundConfiguration` under `Infrastructure/Data/Config/` mirroring `OrderConfiguration`'s
style (private-field navigation + `PropertyAccessMode.Field`, `HasMaxLength` on the PayPal id
strings, `decimal` precision on the money columns — eShopOnWeb's SQL Server default decimal
mapping needs an explicit `HasColumnType("decimal(18,2)")\-equivalent or it silently truncates;
check `CatalogItem.Price`'s mapping for the existing convention and match it). Add the
corresponding EF Core migration.

### 2.3 Saved cards — replace `PaymentMethod`, don't resurrect `Buyer`

`Entities/BuyerAggregate/Buyer.cs` and `PaymentMethod.cs` exist today but are **completely
disconnected from the rest of the app**: no `DbSet<Buyer>` in `CatalogContext`, no
`IEntityTypeConfiguration`, nothing references either type outside their own files. Every
other place identity flows through the system (`Order.BuyerId`, `Basket.BuyerId`,
`CustomerOrdersSpecification`), it's a **plain string** — the identity username/email from the
JWT's `ClaimTypes.Name` — not a foreign key to a `Buyer` row.

Recommendation: don't wire up `Buyer` as a parallel identity concept — that would create two
disagreeing notions of "who the shopper is" (string `BuyerId` vs `Buyer.Id` foreign key) for no
benefit here. Instead:

- Delete/replace the existing stub `PaymentMethod.cs` (it has no public constructor and no
  vault/PayPal fields — it cannot be used as-is; confirm nothing external depends on it before
  removing, and the earlier grep confirmed nothing does).
- Add a new `PaymentMethod` as its own aggregate root in
  `Entities/OrderAggregate/` or a new `Entities/PaymentMethodAggregate/` folder (either is
  fine; keep it out of `BuyerAggregate` to avoid resurrecting the confusion) keyed directly by
  the same `BuyerId` string convention as `Order`/`Basket`:
  - `BuyerId` (string, same identity string as everywhere else)
  - `PayPalVaultId` (the vault payment-token id — this *is* eShop's `paymentMethodId`, no
    separate local id needed; using PayPal's own id as the primary key avoids a pointless
    extra mapping table)
  - `PayPalCustomerId` (the PayPal-generated `customer.id` from the vault response — needed to
    call `GET /v3/vault/payment-tokens?customer_id=...` later without guessing; store it on
    the *first* saved card for a buyer and reuse it for that buyer's subsequent saves so all of
    a buyer's cards land under one PayPal customer)
  - `Brand`, `Last4` (from `last_digits`), `ExpiryMonth`/`ExpiryYear` (parsed from `expiry`,
    format `YYYY-MM`) — enough to build a safe display alias like "Visa ending in 1111, exp
    12/2028" without ever touching the PAN.
  - No card number, no CVV, anywhere on this entity or its DTOs — full card details only ever
    exist in the request payload sent straight to PayPal and are never persisted or logged.
- Add `DbSet<PaymentMethod>` + `PaymentMethodConfiguration` to `CatalogContext`, with a
  migration. Add a `PaymentMethodsByBuyerSpecification` (Ardalis.Specification, same style as
  `CustomerOrdersSpecification`) filtering `p => p.BuyerId == buyerId` for both the list
  endpoint and for authorizing that a `paymentMethodId` named in a pay request actually
  belongs to the caller before it's ever sent to PayPal.

---

## 3. PayPal client layer (new project or folder — your call on exact placement, suggestion below)

Build a small hand-written HTTP client against the specs — no generated SDK is required by the
task, and given only ~10 endpoints are in scope across 4 specs, hand-writing is almost
certainly less overhead than wiring up a codegen pipeline (NSwag/OpenAPI Generator) for a
one-time build. If the build session prefers codegen, that's equally acceptable per the task —
just make sure the generated client is checked in or built from `api-specs/` at build time, not
downloaded as a package.

Suggested placement: `src/Infrastructure/PayPal/` (it's an outbound integration, same
architectural layer as `Infrastructure/Services/EmailSender.cs`), with an interface in
`ApplicationCore/Interfaces` (`IPayPalClient` or split into `IPayPalOrdersClient` /
`IPayPalPaymentsClient` / `IPayPalVaultClient` / `IPayPalReportingClient` — splitting by spec
document is a reasonable seam) so `ApplicationCore` stays dependency-free of any HTTP/PayPal
specifics, consistent with the existing Clean Architecture layering in this repo (`IRepository`,
`IEmailSender`, etc. all live as interfaces in `ApplicationCore` with implementations in
`Infrastructure`).

### 3.1 Configuration

Bind exactly these keys from a `PayPal` section, per the task's mandate:

```
PayPal:ClientId
PayPal:ClientSecret
PayPal:Environment
PayPal:Currency
PayPal:BaseUrl        (optional — when set, use verbatim as the base for every PayPal call,
                        token request included; when absent, derive sandbox vs live host from
                        PayPal:Environment using the sandbox host already given in every
                        api-specs/paypal/*.json `servers[0].url`,
                        https://api-m.sandbox.paypal.com, and the standard live equivalent
                        https://api-m.paypal.com for a non-sandbox Environment value)
```

**Concrete gotcha, spelled out precisely because it's easy to get subtly wrong:** the four
credential env vars are `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`,
`PAYPAL_CURRENCY` — flat names, single underscore. ASP.NET Core's environment-variable
configuration provider (`builder.Configuration.AddEnvironmentVariables()`, already called in
`Program.cs`) only nests sections on **double** underscore (`Section__Key`). A flat
`PAYPAL_CLIENT_ID` will **not** bind to `PayPal:ClientId` automatically — it lands as its own
top-level key. Don't discover this at runtime via a null client id; bridge it explicitly in
`Program.cs`, right after the existing `builder.Configuration.AddEnvironmentVariables()` call:

```csharp
var payPalEnvKeys = new (string EnvVar, string ConfigKey)[]
{
    ("PAYPAL_CLIENT_ID", "PayPal:ClientId"),
    ("PAYPAL_CLIENT_SECRET", "PayPal:ClientSecret"),
    ("PAYPAL_ENVIRONMENT", "PayPal:Environment"),
    ("PAYPAL_CURRENCY", "PayPal:Currency"),
};
var payPalOverrides = payPalEnvKeys
    .Select(kv => (kv.ConfigKey, Value: builder.Configuration[kv.EnvVar]))
    .Where(kv => kv.Value is not null)
    .Select(kv => new KeyValuePair<string, string?>(kv.ConfigKey, kv.Value));
builder.Configuration.AddInMemoryCollection(payPalOverrides);
```

(`PayPal:BaseUrl` is deliberately *not* in this bridge table — it has no matching env var per
the task; it's meant to be set directly under the `PayPal:BaseUrl` key by whatever
configuration source a given deployment uses, e.g. `appsettings.json` or an actual
`PayPal__BaseUrl` env var, which *does* nest correctly on its own.)

Bind into a `PayPalOptions` (in `ApplicationCore` or `Infrastructure`, either is fine) via
`builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection("PayPal"))`, added
next to the existing `catalogSettings`/`baseUrlConfig` wiring in `PublicApi/Program.cs`.

Do not put any credential value in `appsettings*.json` or anywhere else in the repo — only the
four config *key names* above should appear in code/config, never a value. (This matches the
task's own constraint and this session's secret-handling rule.)

### 3.2 Token handling

`POST {BaseUrl}/v1/oauth2/token`, `Content-Type: application/x-www-form-urlencoded`, body
`grant_type=client_credentials`, HTTP Basic auth header built from `ClientId`:`ClientSecret`.
Cache the returned `access_token` in memory (`IMemoryCache`, already registered in
`Program.cs` via `builder.Services.AddMemoryCache()`) keyed by nothing more than "the current
token", refreshing a little before `expires_in` elapses (PayPal sandbox tokens are typically
~9 hours; don't hardcode that number, use the response's own `expires_in`). A single shared
cached token is fine — this is a single-merchant integration (one client id/secret pair for
the whole app), not multi-tenant.

### 3.3 Error mapping

The specs' `default`/4xx responses use PayPal's standard `error` schema
(`name`, `message`, `debug_id`, `details[]`) — check `components/schemas` in
`checkout_orders_v2` or `payments_payment_v2` for the exact shape (both define it, effectively
identically) and surface `name` + `details[].issue` back through the API's existing
`ExceptionMiddleware` pattern (`PublicApi/Middleware/ExceptionMiddleware.cs`) as a new
exception type (e.g. `PayPalApiException`), rather than letting a raw `HttpRequestException`
turn into an opaque 500. This matters most for the two "must say so in terms an operator can
act on" requirements (stale-and-unrenewable authorization; over-refund) — map the specific
PayPal error names you hit in practice (e.g. an `AUTHORIZATION_EXPIRED` /
`ORDER_ALREADY_CAPTURED`-style name from the reauthorize/capture path) into a distinct field on
the fulfilment response rather than a bare exception message.

---

## 4. Idempotency and state design (applies across §5's endpoints)

The task's "idempotent in effect" requirement is satisfied by combining two layers, not by
inventing an idempotency framework:

1. **State-check-then-act on our own row, inside a transaction.** Every mutating endpoint
   loads the `Order`/`Payment` first and checks whether the requested transition already
   happened: `pay` on an order already `PaymentAuthorized` (or later) returns the existing
   authorization state instead of re-authorizing; `fulfil` on an already-`Fulfilled` order
   returns the existing capture instead of re-capturing; `cancel` on an already-`Cancelled` (or
   already-`Fulfilled`) order is rejected or no-ops appropriately rather than voiding twice.
   This is the primary defense — it means a double-click never even reaches PayPal a second
   time for the same logical action.
2. **Deterministic `PayPal-Request-Id` as a second line of defense**, per §1, for the narrow
   window between "our HTTP call to PayPal succeeded" and "our DB commit succeeded" (a crash
   or timeout in between). If our own state-check missed it (because our row wasn't updated
   yet) and we call PayPal again with the same derived request id, PayPal's own 45-day
   dedupe returns the original result instead of double-authorizing/capturing/voiding.

For refunds specifically, the caller-supplied idempotency key is the primary key of a `Refund`
row (unique per `(PaymentId, IdempotencyKey)`) — a repeat request with the same key finds the
existing `Refund` row and returns it without calling PayPal again; a *different* key for a
*different* partial amount against the same capture is a distinct legitimate refund, exactly as
the task specifies. Validate `sum(existing refunds) + requestedAmount <= CapturedAmount`
locally before ever calling PayPal, so an over-refund is rejected with a clear 4xx rather than
relying on PayPal's own rejection.

---

## 5. HTTP surface (`src/PublicApi`)

Follow the existing `IEndpoint<TResult, TRequest, TDep>` + `MapPost/MapGet(...).WithTags(...)`
convention seen throughout `CatalogItemEndpoints/*`. New folders, one per resource, mirroring
the existing `CatalogItemEndpoints`/`AuthEndpoints` layout:

- `OrderEndpoints/` — `CreateOrderEndpoint` (`POST api/orders`), `PayOrderEndpoint`
  (`POST api/orders/{orderId}/pay`), `FulfilOrderEndpoint` (`POST api/orders/{orderId}/fulfil`,
  admin-only), `CancelOrderEndpoint` (`POST api/orders/{orderId}/cancel`, admin-only per the
  task's split of "operator actions" — re-read: cancel is listed under operator actions
  ("Fulfil, cancel and reconciliation are operator actions"), `RefundOrderEndpoint`
  (`POST api/orders/{orderId}/refunds`), `MyOrdersEndpoint` (`GET api/my-orders`).
- `PaymentMethodEndpoints/` — `CreatePaymentMethodEndpoint` (`POST api/payment-methods`),
  `ListPaymentMethodsEndpoint` (`GET api/payment-methods`), `DeletePaymentMethodEndpoint`
  (`DELETE api/payment-methods/{paymentMethodId}`).
- `ReconciliationEndpoints/` — `ReconciliationReportEndpoint`
  (`GET api/reconciliation?from=...&to=...`, admin-only).

All routes use `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`;
the three operator routes (`fulfil`, `cancel`, `reconciliation`) additionally require
`Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS`, exactly like
`CreateCatalogItemEndpoint` already does. Every shopper-scoped endpoint resolves the caller's
identity from `HttpContext.User` (`ClaimTypes.Name`, same claim `IdentityTokenClaimService`
puts in the JWT) inside the endpoint's lambda (bind `ClaimsPrincipal`/`HttpContext` as an extra
minimal-API parameter, same as any other DI-resolved parameter) and uses it as `BuyerId` — never
trust a buyer id supplied in the request body. For `pay`, `fulfil`, `cancel`, `refunds`, load
the `Order` by `orderId` and additionally check `order.BuyerId == callerIdentity` (shopper
routes) or that the caller has the admin role (operator routes); return `404` (not `403`, to
avoid confirming the order id exists) when a shopper requests an order that isn't theirs — this
is what "one shopper must never see or act on another's [order]" means in HTTP terms. Same
`404`-not-`403` treatment for `DELETE /api/payment-methods/{id}` on another shopper's card.

### 5.1 `POST /api/orders`

Request: list of `{ catalogItemId, quantity }` + shipping `Address` fields (reuse
`OrderService`'s existing item/price-lookup logic — extend `OrderService` with a new
`CreateOrderFromItemsAsync(buyerId, address, items)` overload that, like
`CreateOrderAsync`, looks up `CatalogItem` prices from the DB rather than trusting client-sent
prices, builds `CatalogItemOrdered`/`OrderItem` exactly as today, then constructs the `Order` in
`OrderStatus.AwaitingPayment`). Response: `{ orderId, ... }` (top-level `orderId` per the task's
response-identifier requirement), plus whatever else is useful (total, currency, status).

### 5.2 `POST /api/orders/{orderId}/pay`

Request: either `{ card: { number, expiry, cvv, name, billingAddress } }` for one-off, or
`{ paymentMethodId }` naming a saved card. Validate exactly one of the two is present. If
`paymentMethodId` is given, load it via `PaymentMethodsByBuyerSpecification` scoped to the
caller — a `paymentMethodId` that exists but belongs to someone else must 404 exactly like a
foreign order id would (this is the concrete mechanism behind "never use... another's [card]").
Compute the order total from `Order.Total()` (already exists) rounded/formatted to the PayPal
`money.value` string format for `PayPal:Currency`'s decimal precision. Call the PayPal
create-order flow from §1/§3, persist the resulting `Payment` via `Payment.Authorize(...)`,
call `order.MarkPaymentAuthorized()`. If the create-order response indicates
`PAYER_ACTION_REQUIRED`, stop and surface it as a distinct error (see §1) rather than treating
it as success.

### 5.3 `POST /api/orders/{orderId}/fulfil` (admin)

Load `Order` + `Payment`. If `Payment.AuthorizationStatus` isn't in a capturable state, or
`AuthorizationExpiresAt` has passed, call PayPal reauthorize first; if reauthorize itself fails
in a way indicating it can no longer be renewed (see §1/§3.3 error mapping), return a specific
4xx/409 body like `{ error: "authorization_unrenewable", message: "..." }` — something an
operator-facing client can branch on, not a generic failure — and leave the order in its
current state (don't mark it fulfilled). Otherwise capture, persist
`CaptureId`/`CaptureStatus`/`CapturedAmount`/`PayPalFee`/`NetAmount` from
`seller_receivable_breakdown`, call `order.MarkFulfilled()`.

### 5.4 `POST /api/orders/{orderId}/cancel` (admin)

Only legal from `AwaitingPayment` or `PaymentAuthorized` (guarded on `Order` itself, §2.1). If
`PaymentAuthorized`, void the PayPal authorization first, then `order.MarkCancelled()`. If still
`AwaitingPayment` (never paid), just `MarkCancelled()` — nothing to void at PayPal.

### 5.5 `POST /api/orders/{orderId}/refunds`

Request: `{ amount?: decimal, idempotencyKey: string }` (amount omitted = full refund of the
remaining capturable balance). Only legal once `Fulfilled`. Enforce the
"never refundable beyond what was captured" invariant per §4 before calling PayPal. Response:
top-level `refundId`.

### 5.6 `GET /api/my-orders`

`CustomerOrdersWithItemsSpecification`-style query scoped to the caller's `BuyerId`, projected
to include each order's `Payment` summary (status, captured/refunded amounts) — add a
`PaymentMethod`-analogous specification (e.g. extend `CustomerOrdersWithItemsSpecification` or
add a sibling that also `.Include`s `Payment`) rather than N+1-loading payment per order.

### 5.7 `POST /api/payment-methods`, `GET /api/payment-methods`,
### `DELETE /api/payment-methods/{paymentMethodId}`

Straightforward CRUD against the vault client (§1/§3) + the `PaymentMethod` entity (§2.3).
`POST` response: top-level `paymentMethodId` plus the safe display fields (`brand`, `last4`,
`expiryMonth`/`expiryYear`) — never the card number. `DELETE` calls PayPal's vault delete *and*
removes the local row in the same operation, scoped to the caller's own `BuyerId` (404 if the
id doesn't belong to them) — "must no longer be usable to pay" falls out naturally once the
local row (and thus the ability to reference it via `paymentMethodId` in a future `/pay` call)
is gone; PayPal's own vault delete additionally invalidates the `vault_id` itself, so even a
captured/cached reference to it stops working at PayPal's end too.

---

## 6. `GET /api/reconciliation` — pagination design

`GET /v1/reporting/transactions` has two constraints that make "just call it once" wrong for
any non-trivial range, both taken directly from the spec, not assumed:

1. `end_date` minus `start_date` must be **≤ 31 days** ("The maximum supported range is 31
   days" — the spec's literal wording on the `end_date` parameter).
2. `page_size` maxes out at **5**, `page` maxes out at **10** — so a single 31-day window call
   can retrieve at most **50** transactions total, no matter how it's paged.

So the endpoint's implementation:

1. Split the caller's `[from, to]` into consecutive ≤31-day windows.
2. For each window, call with `page_size=5`, `total_required=true` on `page=1` to learn
   `total_pages`. Then fetch `page=2..min(total_pages, 10)`.
3. If `total_pages > 10` for a window (more than 50 transactions in that 31-day slice — plausible
   for a busy merchant, less so in a sandbox test run, but the code must not silently drop data),
   bisect that window into two halves and recurse — halving a date range that's too dense is a
   correct, bounded way to stay under the API's page cap without ever giving up granularity.
4. Concatenate all `transaction_details` across all windows/pages into one report.
5. For each PayPal transaction, look up a matching eShop `Payment` by
   `transaction_info.paypal_reference_id` (where `paypal_reference_id_type` is `TXN` or `ODR`)
   against our stored `AuthorizationId`/`CaptureId`/`PayPalOrderId`. Emit rows for: eShop
   payments with no matching PayPal transaction in range, PayPal transactions with no matching
   eShop payment, and matched pairs — "so a payment PayPal knows about and eShop doesn't — or
   the reverse — is visible" is a direct three-way diff, not a novel design.

The task's note that "a reconciliation range covering payments you have just created may
legitimately come back empty" (PayPal's reporting lag, up to ~3 hours per this spec's own
endpoint description) is a real, expected sandbox behavior — when self-verifying this endpoint
later, test it against a date range from a previous, already-settled sandbox run (or accept an
empty-but-well-formed result for a just-now range as correct), not as a bug to chase.

---

## 7. What's genuinely new vs. what's reused

Reused as-is: `IRepository<T>`/`EfRepository<T>`, `Ardalis.Specification` patterns,
`CatalogContext`, `OrderService`'s existing item-price-lookup logic (extended, not replaced),
JWT auth/`IdentityTokenClaimService`, `ExceptionMiddleware`, `IEndpoint<>` convention,
`BaseRequest`/`BaseResponse`/`CorrelationId()` pattern, `AutoMapper`/`MappingProfile` if useful
for DTO projection (existing endpoints use manual DTO construction more often than AutoMapper in
practice — follow whichever the specific endpoint's neighbors do).

Net-new: `OrderStatus` enum + `Order` transition methods; `Payment`/`Refund` entities +
configs + migration; replacement `PaymentMethod` entity + config + migration;
`IPayPalClient`-family interfaces + `Infrastructure/PayPal/*` implementation (token caching,
the ~9 PayPal calls in scope, error mapping); `PayPalOptions` + the env-var-bridge in
`Program.cs`; the three new endpoint folders under `PublicApi`; reconciliation's
window-splitting logic.

---

## 8. Testing approach for the build session's self-verification

- **Unit tests** (`tests/UnitTests/ApplicationCore`, matching existing layout): `Order`/
  `Payment` state-transition guards (illegal transitions throw; legal ones update state and,
  for `Payment.AddRefund`, enforce the over-refund guard without needing PayPal at all).
- **Integration tests** (`tests/PublicApiIntegrationTests`, matching `CatalogItemEndpoints`'s
  existing `WebApplicationFactory`-based style): endpoint auth/ownership checks (404 on
  cross-buyer access, 403/`Administrators`-only enforcement on operator routes) — these don't
  need real PayPal calls, only real HTTP + real (in-memory) DB.
- **End-to-end runbook against real sandbox PayPal** (this is the part the task's "Rules of
  engagement" asks the build session to actually execute, not merely design): place an order
  via `POST /api/orders`, pay it with the sandbox Visa test card via `POST .../pay`, fulfil it
  as an admin token via `POST .../fulfil` and confirm `paypalFee`/`netAmount` came back
  populated, refund it via `POST .../refunds`, and separately save a card via
  `POST /api/payment-methods` and reuse its `paymentMethodId` to pay a *second* order created
  in the same run (remember: in-memory DB means everything must happen in one continuous run
  of the PublicApi host, per the environment notes already given). Also exercise cancel-before-
  fulfil on a third order (assert no capture ever occurs), and hit `/api/reconciliation` for a
  date range containing that run's activity — expect it to legitimately come back sparse/empty
  per §6's note, and separately confirm the report's *shape* and pagination logic against
  whatever date range has actual sandbox history.
