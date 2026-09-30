# PayPal Payments + Saved Cards for eShopOnWeb — Implementation Plan

This plan is for a **separate build session** that has the same task brief as this one. It
does not have this conversation. Everything it needs — the confirmed PayPal contract, the
repo facts this session verified, the architecture, the data model, the exact endpoints, the
idempotency design, and a concrete verification sequence — is below. Nothing here needs to be
re-derived from scratch, but the PayPal contract details **should be spot-checked against
`https://developer.paypal.com/docs/api/...`** (or the OpenAPI specs at
`github.com/paypal/paypal-rest-api-specifications`) before being relied on for anything not
already exercised by a real sandbox call, per the task's "confirm before you commit" rule —
treat what follows as a verified starting point, not a substitute for testing against the real
sandbox during the build.

---

## 1. Confirmed PayPal contract

Sourced from `developer.paypal.com` docs and the authoritative OpenAPI specs in
`github.com/paypal/paypal-rest-api-specifications` (files `checkout_orders_v2.json`,
`payments_payment_v2.json`, `vault_payment_tokens_v3.json`, `reporting_transactions_v1.json`).
Field names and enum values below are quoted from those specs, not guessed.

### 1.1 Auth

`POST {BaseUrl}/v1/oauth2/token`, HTTP Basic auth (`ClientId:ClientSecret`, base64),
`Content-Type: application/x-www-form-urlencoded`, body `grant_type=client_credentials`.
Returns `access_token` + `expires_in` (seconds). Use as `Authorization: Bearer <token>` on
every subsequent call. Cache and refresh before expiry; don't fetch a new token per request.

### 1.2 Direct-card authorize (Flow 1 "pay") — Orders v2

- `POST /v2/checkout/orders` with `intent: "AUTHORIZE"` and `payment_source.card` populated
  directly (`number`, `expiry` as `"YYYY-MM"`, `security_code`, `name`, `billing_address`) —
  **or** `payment_source.card.vault_id` set to a previously-saved card's vault id instead of
  raw card fields.
- **Authorization happens synchronously inside this same call** when a card is supplied
  directly — there is a separate `POST /v2/checkout/orders/{id}/authorize` endpoint, but it is
  only needed for redirect/approval-based payment sources (e.g. buyer approves a PayPal wallet
  payment in a browser first). For direct card, do **not** call it separately; the create-order
  response already contains the authorization.
- Response `status`:
  - `"COMPLETED"` — the card was authorized synchronously, no challenge required. The
    authorization object is at `purchase_units[0].payments.authorizations[0]` (`id`, `status`,
    `expiration_time`, `amount`).
  - `"PAYER_ACTION_REQUIRED"` with a `links` entry `rel: "payer-action"` — PayPal is requiring
    a 3-D Secure/SCA browser challenge. **This is the task's explicit "STOP and report" case.**
    Do not build a redirect/approval round-trip. If the sandbox business account's direct-card
    processing ever returns this for the test card, stop and report it rather than working
    around it — but this is not expected for a sandbox account provisioned for direct card
    processing with the standard Visa test card.
- Raw PAN/CVV in this request requires PCI SAQ D compliance in production (noted directly in
  PayPal's schema description for `card_request`). That's expected and accepted here — the task
  explicitly calls for a direct-card integration against a sandbox account enabled for it. Note
  it in code comments/README as a real production consideration, nothing to solve now.
- Set `purchase_units[0].amount = { currency_code: <PayPal:Currency>, value: "<order total, 2dp>" }`.
  Also set `purchase_units[0].invoice_id` to the local `Order.Id` (as a string) — this is the
  join key the reconciliation report uses (§6).
- Every create-order call must carry a `PayPal-Request-Id` header (idempotency; see §4).

### 1.3 Authorizations — capture / reauthorize / void (Payments v2, `/v2/payments/authorizations/{id}/...`)

- `GET /v2/payments/authorizations/{id}` — current `status`, `expiration_time`, `amount`.
- **Capture**: `POST /v2/payments/authorizations/{id}/capture`. Request: `amount` (omit to
  capture the full authorized amount — always omit it or pass the full order total here, since
  fulfilment always captures exactly what was authorized), `final_capture: true` (we never do
  partial/multiple captures), `invoice_id`, `note_to_payer`. Response is a `capture` object:
  `id`, `status` ∈ `{COMPLETED, DECLINED, PARTIALLY_REFUNDED, PENDING, REFUNDED, FAILED}`, and
  `seller_receivable_breakdown: { gross_amount, paypal_fee, net_amount, ... }` — this is
  **exactly** the "captured amount, PayPal's fee, and net proceeds" the task asks the fulfil
  response to show.
- **Stale-authorization handling (task's "renew rather than fail outright")**: attempt the
  capture first. If it fails with HTTP 422 and an error `details[].issue` of
  `"AUTHORIZATION_EXPIRED"` (confirmed literal issue code in PayPal's spec examples), call
  reauthorize (below) once, then retry the capture against the **new** authorization id.
- **Reauthorize**: `POST /v2/payments/authorizations/{id}/reauthorize`, body `{ amount }`
  (same currency/amount as the original authorization — PayPal rejects a currency mismatch
  with issue `AUTH_CURRENCY_MISMATCH`). Confirmed rules straight from PayPal's endpoint
  description:
  - Reauthorizing is only valid starting the 4th day after the original authorization (PayPal
    rejects an early attempt with issue `REAUTHORIZATION_TOO_SOON` if attempted inside the
    initial 3-day honor period — shouldn't happen in our flow since we only reauthorize in
    response to an `AUTHORIZATION_EXPIRED` capture failure, which by definition means the honor
    period has already passed).
  - You can reauthorize **multiple times** within the authorization's total 29-day window; each
    reauthorization opens a new 3-day honor period.
  - **Past 30 days since the original authorization**, PayPal says you cannot reauthorize at
    all — a brand-new order + authorization is required instead. The spec's published examples
    do not give a specific `issue` code for this "too old" case (only `AUTH_CURRENCY_MISMATCH`
    and `REAUTHORIZATION_TOO_SOON` are documented). **Don't guess an issue code for this** —
    surface whatever `issue`/`description` PayPal actually returns, verbatim, in the operator
    error message. That satisfies "must say so in terms an operator can act on" honestly
    without inventing an unconfirmed error taxonomy.
  - If reauthorize itself fails, the fulfil call fails with a 409 telling the operator exactly
    that: the authorization cannot be renewed, quoting PayPal's `issue`/`description`, and that
    the order needs to be cancelled (voiding the stale authorization) and the shopper asked to
    pay again. Don't invent an automatic cancel-and-rebill — surfacing the actionable state is
    the task's requirement, not auto-remediation.
- **Void**: `POST /v2/payments/authorizations/{id}/void` — used for the cancel-before-fulfilment
  flow. No request body needed. Cannot void an authorization that's already fully captured
  (PayPal rejects it) — which is fine, since our cancel endpoint only runs pre-fulfilment and
  the order-state guard prevents calling it after fulfilment anyway.

### 1.4 Captures & refunds (Payments v2, `/v2/payments/captures/{id}/...` and `/v2/payments/refunds/{id}`)

- `GET /v2/payments/captures/{id}` — current status, `seller_receivable_breakdown`.
- **Refund**: `POST /v2/payments/captures/{id}/refund`. Body: `amount` (omit for a full refund
  of whatever remains uncaptured-back; include for partial), `invoice_id`, `note_to_payer`,
  `custom_id`. PayPal's own description: *"If amount is not specified, an amount equal to
  captured amount − previous refunds is refunded"* — i.e. PayPal itself won't let a second
  refund exceed what's left, but don't rely on that alone (see §4 for the local guard too).
  Response is a `refund` object: `id`, `status` ∈ `{CANCELLED, FAILED, PENDING, COMPLETED}`,
  `amount`, `seller_payable_breakdown: { gross_amount, paypal_fee, net_amount, ... }`.
- Capture `status` transitions to `PARTIALLY_REFUNDED` or `REFUNDED` after a refund — reflect
  that back onto the local `Payment`/`Order` state after every successful refund call.
- `GET /v2/payments/refunds/{id}` — for re-checking a refund's status later if needed.

### 1.5 Idempotency — `PayPal-Request-Id` header

Confirmed from PayPal's idempotency reference: this header must be sent on every call that
creates or modifies data. Its value must be **unique per request and per call type** (e.g. the
key space for "authorize" and "capture" are independent). If you replay the exact same header
value for the same call type, PayPal returns the cached result of the original call instead of
re-executing it — value is honored for up to 45 days. **If you omit it, PayPal does not
deduplicate — it processes the call again.** This header is therefore mandatory on every
mutating call in this integration; see §4 for exactly how each call derives its value.

### 1.6 Vault (saved cards) — Payment Method Tokens v3

Two-step "save without purchase" flow, confirmed against `vault_payment_tokens_v3.json`:

1. `POST /v3/vault/setup-tokens`, body `payment_source.card` (same shape as the direct-card
   fields above, plus `verification_method: "SCA_WHEN_REQUIRED"`). Response: `id` (setup token
   id), `customer.id` (PayPal-generated customer id — capture and store it; not strictly needed
   to charge the card later since `vault_id` alone suffices, but useful to keep for potential
   future multi-card linkage / support diagnostics), `status: "APPROVED"`,
   `payment_source.card` echoed back with `last_digits`, `brand`, `expiry` — this is the
   "describes it safely enough to recognise which card it is" data the task wants in the
   `POST /api/payment-methods` response. Requires a `PayPal-Request-Id` header.
2. `POST /v3/vault/payment-tokens`, body `{ payment_source: { token: { id: <setup_token_id>,
   type: "SETUP_TOKEN" } } }`. Response: `id` — this **is** the reusable `vault_id`. Also
   requires `PayPal-Request-Id`.
3. To pay with a saved card: `payment_source.card.vault_id = <that id>` on order creation
   (§1.2) instead of raw card fields.
4. `GET /v3/vault/payment-tokens/{id}` / `GET /v3/vault/payment-tokens?customer_id=...` — not
   needed for our design since we keep our own DB row per saved card (see §3); PayPal's list
   endpoint is redundant with our own `GET /api/payment-methods`.
5. `DELETE /v3/vault/payment-tokens/{id}` — removes it from PayPal's vault. Call this from
   `DELETE /api/payment-methods/{id}` before deleting the local row (§3 has the exact failure
   handling).

### 1.7 Reconciliation — Transaction Search v1

`GET /v1/reporting/transactions`. Confirmed constraints straight from the spec:

- `start_date` and `end_date` are **required** query params, RFC 3339 date-times.
- **Maximum supported range per call is 31 days** (literal spec text: *"The maximum supported
  range is 31 days"*) — a wider `from`/`to` must be chunked into ≤31-day windows and each
  window queried separately.
- Pagination: `page` (1-based) + `page_size` (max 500), response has `total_pages`; must loop
  until all pages of every chunk are fetched — the task explicitly requires covering the whole
  range, not just page 1.
- **Up to 3 hours latency** before a just-created transaction appears (spec text: *"It takes a
  maximum of three hours for executed transactions to appear in the list transactions call"*).
  This is exactly the task's documented "empty range for recent activity is expected" caveat —
  build and test the chunking/paging/matching logic correctly; don't treat an empty result for
  a just-made payment as a bug, and don't skip building the report because of it.
- Each `transaction_details[].transaction_info` entry carries: `transaction_id`,
  `paypal_reference_id` + `paypal_reference_id_type` (`"ODR"` = order id, `"TXN"` = a
  transaction/capture id — this is PayPal's own cross-reference back to the order/capture),
  `invoice_id` (echoes what we set in §1.2 — *"If an invoice ID was sent with the capture
  request, the value is reported. Otherwise, the invoice ID of the authorizing transaction is
  reported"*), `transaction_amount`, `fee_amount`, `transaction_status` ∈ `{D, P, S, V}` (denied
  / pending / success / reversed).
- **Matching strategy**: for each local `Payment` whose `PayPalAuthorizationId` /
  `PayPalCaptureId` was created inside `[from, to]`, look for a PayPal transaction whose
  `paypal_reference_id` equals one of those ids, falling back to matching `invoice_id` against
  the local `Order.Id`. Unmatched PayPal transactions → "PayPal knows about, eShop doesn't".
  Unmatched local payments → "eShop knows about, PayPal doesn't". Report both lists plus the
  matched pairs.

No PayPal capability required by the task brief turned out to be unsupported or unconfirmable —
there is no gap to report here. The one deliberate stop condition carried into the build (not a
gap, a designed runtime guard) is the `PAYER_ACTION_REQUIRED` / 3-D Secure case in §1.2.

---

## 2. Repo facts this session confirmed (so the build session doesn't have to re-discover them)

- **Endpoint style**: `src/PublicApi` uses the `MinimalApi.Endpoint` package almost everywhere
  — each endpoint is a class implementing `IEndpoint<TResult, TRequest, ...deps>` with
  `AddRoute(IEndpointRouteBuilder)` + `HandleAsync(...)`, auto-discovered by `AddEndpoints()` /
  `app.MapEndpoints()` in `Program.cs` (no per-endpoint registration needed). Request/response
  DTOs are separate partial-class files deriving from `BaseRequest` / `BaseResponse` (which
  carry a `CorrelationId`). Follow this pattern for every new endpoint — do **not** use the
  `Ardalis.ApiEndpoints`/MVC-controller style, which exists only for the one legacy
  `AuthenticateEndpoint`.
- **Admin-only template**: copy the exact attribute line from
  `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`:
  `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
  Shopper-scoped endpoints use the same `AuthenticationSchemes` without the `Roles` constraint.
- **Caller identity**: the JWT's `ClaimTypes.Name` claim carries the username, which is the
  same string already used as `Order.BuyerId` / `Basket.BuyerId` elsewhere in the app (e.g.
  `demouser@microsoft.com`). Read it the same way for every new shopper-scoped endpoint.
- **Persistence**: everything (`Order`, `OrderItem`, `Basket`, `CatalogItem`, …) lives in one
  `CatalogContext` (`src/Infrastructure/Data/CatalogContext.cs`); there's no separate ordering
  context. No `IOrderRepository` — order code uses the generic `IRepository<Order>` /
  `IReadRepository<Order>` (Ardalis.Specification + `EfRepository<T>`). Follow the same
  pattern for the new entities (§3) rather than inventing bespoke repositories.
- **`Order` today has zero payment/status fields** — `BuyerId`, `OrderDate`, `ShipToAddress`,
  `OrderItems`, and a computed `Total()`. This plan adds what's missing.
- **`Buyer`/`PaymentMethod` scaffolding already exists** in
  `src/ApplicationCore/Entities/BuyerAggregate/` but is completely unwired (no `DbSet`, no EF
  config, no repository ever touches it, not referenced from `Order`). Its `PaymentMethod` has
  an `Alias`/`CardId`/`Last4` shape with a comment that raw card data belongs in a PCI-compliant
  system — directionally right, but it's keyed through a `Buyer` aggregate that nothing else in
  the app uses (`Order.BuyerId` is a plain string, not a `Buyer` FK). **Decision: don't wire up
  `Buyer`.** Add a new, independent `PaymentMethod` entity keyed by a plain `string BuyerId`
  (same identity string convention as `Order.BuyerId`), not a `Buyer` foreign key. Wiring the
  whole unused `Buyer` aggregate just to satisfy a naming coincidence would be scope creep for
  no behavioural benefit; the existing scaffolding can be left alone or removed, build's call.
- **No FastEndpoints, no MediatR, no `Ardalis.Result` usage in `PublicApi`** — endpoints return
  `IResult` directly (`Results.Ok/NotFound/Created(...)`) and error paths throw exceptions
  caught by `src/PublicApi/Middleware/ExceptionMiddleware.cs`, which today only special-cases
  `DuplicateException` (→ 409) and otherwise returns a generic 500. This needs new exception
  types + middleware branches (§3.5).
- **`Ardalis.GuardClauses`** is the established precondition-check style in `ApplicationCore` —
  use it in the new domain/service code rather than manual `if`/`throw`.
- **DB provider**: `UseOnlyInMemoryDatabase` flag drives `CatalogContext`/`AppIdentityDbContext`
  between `UseInMemoryDatabase` and `UseSqlServer` in `src/Infrastructure/Dependencies.cs`. Per
  this machine's environment gotchas, run with the in-memory provider — but still write and
  commit a real EF Core migration for the new tables (needed for the SQL Server path / any other
  environment; the in-memory provider just won't exercise it here).
- **Target framework**: net8.0, central package management via `Directory.Packages.props`.
  `global.json` pins SDK `8.0.x` with `rollForward: latestFeature` — per this machine's
  environment gotchas, that needs loosening to `latestMajor` (only .NET 10 SDK is installed
  here) and `DOTNET_ROLL_FORWARD=Major` set when running.
- **Project reference graph**: `PublicApi → Infrastructure → ApplicationCore → BlazorShared`.
  `PublicApi` can reference `BlazorShared.Authorization.Constants.Roles` and
  `BlazorShared.Models.ErrorDetails` directly (already does, for the catalog admin endpoints).

---

## 3. Data model changes

All new types live in `src/ApplicationCore/Entities/` (a new `PaymentAggregate` folder is
reasonable) with EF configuration in `src/Infrastructure/Data/Config/`, registered as new
`DbSet<T>` on `CatalogContext`, plus one new EF Core migration
(`dotnet ef migrations add AddPayPalPayments --project src/Infrastructure --startup-project src/PublicApi`).

### 3.1 `Order` changes

- Add `OrderStatus Status` (new enum: `AwaitingPayment`, `PaymentAuthorized`, `Fulfilled`,
  `Cancelled`, `PartiallyRefunded`, `Refunded`). Starts at `AwaitingPayment` when created via
  `POST /api/orders`; `Web`'s existing checkout path (`OrderService.CreateOrderAsync`) should
  also set this to keep the entity consistent, even though the task doesn't require the Web
  storefront to change — it must not break because `Order` gained a required field.
- Add a nullable one-to-one navigation to `Payment` (below), FK `Payment.OrderId` unique.
- Store the enum as a string via `HasConversion<string>()` in the EF config — readable in the
  in-memory/SQL data for debugging, no real downside at this scale.
- `Order`'s constructor requires an `Address`. The task's `POST /api/orders` doesn't mandate an
  address input; **decision**: accept an optional `ShippingAddress` object in the request DTO
  (same shape as `Address`: street/city/state/country/zip) and default to the same placeholder
  address the `Web` checkout already hardcodes (`"123 Main St.", "Kent", "OH", "United States",
  "44240"`) when omitted. This keeps `POST /api/orders` usable with a minimal body while staying
  consistent with the existing entity contract.

### 3.2 New `ApplicationCore` service method

Extend `IOrderService`/`OrderService` with something like:

```
Task<Order> CreateOrderFromItemsAsync(string buyerId, Address shippingAddress,
    IReadOnlyList<(int catalogItemId, int quantity)> items);
```

Implementation mirrors the existing `CreateOrderAsync(basketId, address)` body (load
`CatalogItem`s via `CatalogItemsSpecification(ids)` for authoritative current `Price`, build
`CatalogItemOrdered` + `OrderItem` snapshots, construct `Order`, save via `IRepository<Order>`)
but skips the `Basket` entirely — there is no existing shortcut for this, per this session's
repo investigation, so this is new code, not a refactor of existing code. Guard against empty
item lists, unknown catalog item ids, and non-positive quantities with `Ardalis.GuardClauses`.

### 3.3 `Payment` entity (one per `Order`, created at `pay` time — not at order-creation time)

| Field | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `OrderId` | int FK, unique | |
| `Currency` | string(3) | snapshot of `PayPal:Currency` at authorize time |
| `Amount` | decimal | snapshot of `Order.Total()` at authorize time — the amount PayPal was asked to hold |
| `PayPalOrderId` | string | id from `POST /v2/checkout/orders` |
| `PayPalAuthorizationId` | string? | current/latest authorization id (overwritten on reauthorize) |
| `AuthorizationStatus` | string | mirrors PayPal's authorization `status` enum |
| `AuthorizationExpiresAt` | DateTimeOffset? | from `expiration_time` |
| `PayPalCaptureId` | string? | set once fulfilled |
| `CaptureStatus` | string? | mirrors PayPal's capture `status` enum |
| `CapturedAmount`, `PayPalFeeAmount`, `NetAmount` | decimal? | from `seller_receivable_breakdown` at capture time |
| `RefundedAmount` | decimal | running total, default 0, updated after every successful refund |
| `PaymentMethodId` | int? FK | set when paid via a saved card |
| `AuthorizeRequestId`, `CaptureRequestId` | string | the `PayPal-Request-Id` values used, stored for retry-reuse (§4) |
| `RowVersion` | byte[] (concurrency token) | guards against a concurrent fulfil/cancel race |
| `CreatedAt`, `UpdatedAt` | DateTimeOffset | |

Add a small append-only `PaymentEvent` table (`Id`, `PaymentId` FK, `EventType` —
`Authorized`/`Reauthorized`/`Captured`/`Voided`/`Refunded`, `PayPalId`, `Status`,
`OccurredAt`) written alongside every state change. Not strictly required by the task's
acceptance bullets, but genuinely load-bearing for a "production-grade" payment integration —
it's what lets an operator (or the reconciliation report) explain *why* a `Payment` is in its
current state, including intermediate reauthorizations that the "current state" fields
overwrite. Keep it minimal — this is a log, not a second source of truth.

### 3.4 `Refund` entity

| Field | Type | Notes |
|---|---|---|
| `Id` | int PK | **this is the `refundId` returned by `POST /api/orders/{orderId}/refunds`** |
| `PaymentId` | int FK | |
| `PayPalRefundId` | string | |
| `Amount` | decimal | |
| `Status` | string | mirrors `{CANCELLED, FAILED, PENDING, COMPLETED}` |
| `IdempotencyKey` | string | caller-supplied; **unique index on `(PaymentId, IdempotencyKey)`** |
| `CreatedAt` | DateTimeOffset | |

`refundId` is the local `Refund.Id`, not PayPal's refund id — kept consistent with `orderId`
and `paymentMethodId` both being local ids that a caller uses to drive further calls into this
API, not PayPal's own identifiers.

### 3.5 `PaymentMethod` entity (saved card)

| Field | Type | Notes |
|---|---|---|
| `Id` | int PK | **this is the `paymentMethodId`** |
| `BuyerId` | string | same identity string as `Order.BuyerId` (JWT `ClaimTypes.Name`) |
| `PayPalVaultId` | string | the vault `payment-tokens` id — used as `payment_source.card.vault_id` when paying |
| `PayPalCustomerId` | string | PayPal's `customer.id`, kept for reference |
| `CardBrand` | string | e.g. `"VISA"` |
| `Last4` | string | |
| `Expiry` | string | `"YYYY-MM"`, mirrors PayPal's format |
| `CreatedAt` | DateTimeOffset | |

No soft-delete flag — `DELETE /api/payment-methods/{id}` does a real delete (see §5.6 for the
exact PayPal-then-local delete ordering/failure handling). Ownership check: every read/use of a
`PaymentMethod` must verify `BuyerId` equals the caller's identity; treat a mismatch identically
to "not found" (404) so one shopper can't probe for another's saved-card ids.

### 3.6 Exceptions

Add to `ApplicationCore` (or `PublicApi`, matching where `DuplicateException` already lives):
`OrderNotFoundException`, `PaymentMethodNotFoundException`, `InvalidOrderStateException` (wrong
lifecycle stage for the requested action — e.g. paying an already-paid order, fulfilling before
payment, cancelling after fulfilment), `PayPalPaymentDeclinedException`,
`AuthorizationNotRenewableException` (carries PayPal's raw `issue`/`description` from §1.3),
`RefundAmountExceedsRemainingException`, and a catch-all `PayPalApiException` (status code,
`name`, `message`, `debug_id` from PayPal's error envelope) for anything else PayPal rejects.

---

## 4. PayPal integration layer

### 4.1 Placement

- `IPayPalGateway` — the abstraction — in `src/ApplicationCore/Interfaces/`, next to
  `IOrderService`/`IBasketService`.
- `PayPalGateway` — the HTTP implementation — in a new `src/Infrastructure/PayPal/` folder,
  registered in `Infrastructure`'s DI extension method alongside the existing repository
  registrations.
- `PayPalOptions` (`ClientId`, `ClientSecret`, `Environment`, `Currency`, `BaseUrl`) bound via
  `services.Configure<PayPalOptions>(configuration.GetSection("PayPal"))`.

### 4.2 Config binding — the env-var gotcha (read this before writing `Program.cs` changes)

The task's env vars are `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`,
`PAYPAL_CURRENCY` (single underscore). ASP.NET Core's built-in environment-variable
configuration provider only auto-binds double-underscore names (`PayPal__ClientId` →
`PayPal:ClientId`) — **these single-underscore names will not bind automatically** and
`PayPal:ClientId` etc. will silently stay empty if you just rely on the default provider. Bridge
them explicitly in `PublicApi/Program.cs`, before `builder.Build()`, e.g.:

```csharp
var overrides = new Dictionary<string, string?>();
void Map(string env, string key)
{
    var value = Environment.GetEnvironmentVariable(env);
    if (!string.IsNullOrEmpty(value)) overrides[key] = value;
}
Map("PAYPAL_CLIENT_ID", "PayPal:ClientId");
Map("PAYPAL_CLIENT_SECRET", "PayPal:ClientSecret");
Map("PAYPAL_ENVIRONMENT", "PayPal:Environment");
Map("PAYPAL_CURRENCY", "PayPal:Currency");
builder.Configuration.AddInMemoryCollection(overrides);
```

Only set keys for variables that are actually present, so this can't clobber a value already
supplied via `appsettings`/user-secrets/a differently-named env var in another environment. The
task doesn't mandate a specific env var name for the `PayPal:BaseUrl` override (it only says the
*config key* must be exactly `PayPal:BaseUrl`) — support it the same way (e.g. an optional
`PAYPAL_BASE_URL` env var, or just leave it settable via `appsettings`/user-secrets) as long as
the binding target is that exact key and, when present, it's used verbatim as the base address
for **every** PayPal call including the OAuth token request (§1.1).

**Never write an actual credential value into any file in this repo** — not in `appsettings*`,
not in test fixtures, not in this plan. Only the config *key names* are hard-coded.

### 4.3 Base URL resolution

If `PayPal:BaseUrl` is set → use it verbatim, no further logic. Otherwise map
`PayPal:Environment` case-insensitively: `"sandbox"` → `https://api-m.sandbox.paypal.com`,
`"live"`/`"production"` → `https://api-m.paypal.com`. Anything else with no `BaseUrl` override →
fail fast at startup with a clear configuration error (don't silently default to sandbox or
production).

### 4.4 HTTP plumbing

- Named `HttpClient` via `IHttpClientFactory`, base address set from §4.3.
- OAuth token cached in-memory (e.g. `IMemoryCache` or a simple locked field) keyed by nothing
  more than "the current token", refreshed a safety margin before `expires_in` elapses;
  thread-safe against concurrent requests racing to refresh.
- Every mutating call sets header `PayPal-Request-Id: <value>` per §4.5.
- Non-2xx responses: parse PayPal's error envelope (`name`, `message`, `debug_id`,
  `details[].issue`/`description`) and throw the appropriate typed exception from §3.6 — the
  capture-path code specifically inspects `details[].issue == "AUTHORIZATION_EXPIRED"` to decide
  whether to reauthorize-and-retry (§1.3); everything else about PayPal's error shape is just
  surfaced, not pattern-matched, since guessing at undocumented codes isn't something to build
  against.
- **Never log the request or response body for the two calls that carry raw PAN/CVV** —
  `POST /v2/checkout/orders` (when `payment_source.card.number` is present, i.e. the non-vault
  path) and `POST /v3/vault/setup-tokens`. Log method/path/status/elapsed/our own correlation id
  for those; other calls (capture/reauthorize/void/refund/vault payment-tokens/reporting) never
  carry a PAN and may be logged more verbosely if useful for debugging. This is a hard
  requirement from the task, not a style preference — full card details must never reach logs
  or the database.

### 4.5 Idempotency key derivation (this is how "double-click never double-charges" is actually satisfied)

Two layers, both required:

1. **Server-side state guard first, always.** Before calling PayPal for `pay`/`fulfil`/`cancel`,
   load the `Payment` row and check its current status. If it's already past the point this
   call would move it to (e.g. `pay` called again on a `Payment` whose `AuthorizationStatus` is
   already active, or `fulfil` called again once `CaptureStatus` is already `COMPLETED`), return
   the existing stored state instead of calling PayPal again. This is what actually protects
   against the common "double-click" case and keeps behaviour correct even without relying on
   PayPal's own dedup window.
2. **`PayPal-Request-Id` as defense-in-depth** against races/timeout-retries that get past the
   state guard (e.g. two requests arriving concurrently before either has committed a status
   change):
   - **Authorize (`pay`)**: derive deterministically from the order id **and** a hash of the
     normalized payment input (card last-4+expiry, or `vault_id`, plus the amount) — e.g.
     `$"authorize:{orderId}:{sha256(normalizedPayload)}"`. Deliberately *not* keyed by order id
     alone: if a shopper's first attempt is declined and they legitimately retry with a
     different card, that must be a fresh PayPal call, not a replay of the first (failed)
     result. An exact resubmission (identical body) reuses the same key and dedupes correctly.
   - **Fulfil (`capture`)**: fully deterministic — `$"capture:{paymentId}:{authorizationId}"`.
     Capture amount is always the full authorized total (no payload variance), so this is always
     safe to replay.
   - **Reauthorize** (internal step inside fulfil): `$"reauth:{authorizationId}"`.
   - **Cancel (`void`)**: `$"void:{paymentId}:{authorizationId}"`.
   - **Refund**: derive from the **caller-supplied idempotency key** directly —
     `$"refund:{paymentId}:{callerIdempotencyKey}"`. Also check the local
     `(PaymentId, IdempotencyKey)` unique index *before* calling PayPal at all: a repeat request
     under the same key returns the stored prior `Refund` row without another PayPal call at all
     (fast path); a new key for the same `Payment` is a legitimately distinct refund. Validate
     `requestedAmount <= Payment.Amount(captured) - Payment.RefundedAmount` locally before
     calling PayPal, in addition to relying on PayPal's own "captured − previous refunds" cap
     (§1.4), so a partly-refunded order can never be pushed past what was actually captured.
   - **Save card**: a fresh value per HTTP request is fine — duplicate saved cards from a
     double-click are a minor UX nuisance, not a money-movement correctness issue, so this
     endpoint isn't held to the same idempotency bar as anything that authorizes/captures/moves
     money.

---

## 5. API surface (`src/PublicApi`)

All routes JWT-authenticated (`AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme`).
Admin-only = `Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS` added to the
same attribute. Every shopper-scoped endpoint resolves the caller's identity from
`ClaimTypes.Name` and checks it against the resource's `BuyerId` — a mismatch is a 404, not a
403, to avoid confirming another shopper's order/payment-method ids exist.

**Role split — read this carefully, it's not the obvious e-commerce default.** The task states
verbatim: *"Fulfil, cancel and reconciliation are operator actions... Every other endpoint is
shopper-scoped and acts only on the caller's own data."* Refunds is not named in the operator
list. So — despite most storefronts requiring staff approval to refund — **`POST
/api/orders/{orderId}/refunds` is shopper-scoped here, restricted only to the caller's own
order**, exactly like `pay` and `my-orders`. Don't move it under the admin role; that would
contradict the explicit instruction even though it reads unusually.

| Endpoint | Role | Summary |
|---|---|---|
| `POST /api/orders` | shopper | `{ items: [{catalogItemId, quantity}], shippingAddress? }` → creates `Order` (`AwaitingPayment`). Response includes `orderId` top-level. |
| `POST /api/orders/{orderId}/pay` | shopper, own order | `{ card: {...} }` **or** `{ paymentMethodId }` (mutually exclusive, exactly one required) → authorizes via §1.2. Response: authorization id/status/expiry. |
| `POST /api/orders/{orderId}/fulfil` | admin | No body. Captures per §1.3 (with stale-auth renewal). Response: capture id/status, captured amount, PayPal fee, net amount. |
| `POST /api/orders/{orderId}/cancel` | admin | No body. Voids the authorization (§1.3) — only valid pre-fulfilment; `InvalidOrderStateException` otherwise. |
| `POST /api/orders/{orderId}/refunds` | shopper, own order | `{ idempotencyKey, amount? }` → refunds per §1.4/§4.5. Response includes `refundId` top-level, plus status/amount/remaining refundable. |
| `GET /api/my-orders` | shopper | Caller's own orders with nested payment state (status, captured/refunded amounts, currency). |
| `GET /api/reconciliation?from=&to=` | admin | Per §1.7 — chunked/paged PayPal transaction search lined up against local `Payment`s in range; matched / PayPal-only / eShop-only lists. |
| `POST /api/payment-methods` | shopper | `{ card: {...} }` → vaults via §1.6. Response includes `paymentMethodId` top-level, brand/last4/expiry — never full card details. |
| `GET /api/payment-methods` | shopper | Caller's saved cards (brand/last4/expiry/id only). |
| `DELETE /api/payment-methods/{paymentMethodId}` | shopper, own card | Removes it (§5.6). |

Every action is its own endpoint/route, per the task's explicit "each action a caller can take
stays separately invocable" — there is no combined pay+fulfil+refund call anywhere above.

### 5.1 `POST /api/orders`

Validate: item list non-empty, quantities positive, catalog item ids exist (404/400 otherwise).
Calls the new `IOrderService.CreateOrderFromItemsAsync` (§3.2). No PayPal call at this stage —
`Order.Status = AwaitingPayment`, no `Payment` row yet.

### 5.2 `POST /api/orders/{orderId}/pay`

1. Load `Order`, ownership check, require `Status == AwaitingPayment` (else
   `InvalidOrderStateException` — except see §4.5's state-guard short-circuit for the exact
   double-click case).
2. Resolve payment source: raw card fields as given, or look up the caller's own
   `PaymentMethod` by `paymentMethodId` (ownership-checked) and use its `PayPalVaultId`.
3. Compute amount = `Order.Total()`, currency = `PayPal:Currency`, formatted to 2 decimal places
   (see §7 for the currency-decimals caveat).
4. Call `IPayPalGateway.AuthorizeAsync(...)` per §1.2/§4.5. On `PAYER_ACTION_REQUIRED` → this is
   the task's stop condition; surface a clear 5xx/502-ish error indicating an unexpected SCA
   challenge was returned (this indicates something to escalate, not a normal error path to
   silently handle) — do not attempt to build a redirect flow.
5. Persist the new `Payment` row, set `Order.Status = PaymentAuthorized`.

### 5.3 `POST /api/orders/{orderId}/fulfil` (admin)

1. Load `Order` + `Payment`, require `Status == PaymentAuthorized` (idempotent short-circuit
   per §4.5 if already `Fulfilled`).
2. Call capture (§1.3); on `AUTHORIZATION_EXPIRED`, reauthorize once and retry; on
   not-renewable, throw `AuthorizationNotRenewableException` with PayPal's raw issue/description
   (→ 409, operator-actionable message, order stays `PaymentAuthorized` for a human to cancel).
3. On success: update `Payment` (`CaptureStatus`, `CapturedAmount`, `PayPalFeeAmount`,
   `NetAmount`, `PayPalCaptureId`), `Order.Status = Fulfilled`, append a `PaymentEvent`.

### 5.4 `POST /api/orders/{orderId}/cancel` (admin)

Require `Status == PaymentAuthorized` (not yet fulfilled). Void per §1.3.
`Order.Status = Cancelled`. No money ever moved — nothing to reconcile against a capture because
there isn't one.

### 5.5 `POST /api/orders/{orderId}/refunds`

Require `Status ∈ {Fulfilled, PartiallyRefunded}`. Idempotency + amount-cap logic per §4.5.
On success: `Payment.RefundedAmount += refundedAmount`; `Order.Status = Refunded` if
`RefundedAmount == CapturedAmount`, else `PartiallyRefunded`.

### 5.6 `DELETE /api/payment-methods/{paymentMethodId}`

Ownership check → 404 if not the caller's. Call PayPal `DELETE /v3/vault/payment-tokens/{id}`
first. If that PayPal call fails with 404 (already gone on PayPal's side), treat as success and
continue. If it fails with anything else (network/5xx), **still delete the local row** — the
task's hard requirement is that the card is no longer visible or usable through *this* API,
which is fully within our control regardless of PayPal's vault state; log the failure clearly
(as an operator-visible warning, e.g. via the `PaymentEvent`-style log or standard logging) so a
stale PayPal vault entry can be cleaned up out of band. Don't leave the shopper stuck with an
undeletable saved card because of a transient PayPal-side error.

---

## 6. Reconciliation report shape

`GET /api/reconciliation?from=<iso>&to=<iso>` (admin). Validate `from <= to`, both required.
Response sketch:

```
{
  "from": "...", "to": "...",
  "matched":     [{ orderId, paypalTransactionId, localStatus, paypalStatus, localAmount, paypalAmount }],
  "paypalOnly":  [{ paypalTransactionId, paypalReferenceId, amount, status }],
  "eshopOnly":   [{ orderId, paymentId, localStatus, amount }]
}
```

Implementation per §1.7: chunk `[from, to]` into ≤31-day windows, page each chunk fully
(`page`/`page_size=500`/`total_pages`), match every returned transaction against local
`Payment`s (by `paypal_reference_id` primarily, `invoice_id`→`Order.Id` as fallback). A range
with zero PayPal results (e.g. very recent activity, due to the ≤3h reporting lag) is a normal,
correctly-handled empty response — not an error, and not evidence the feature doesn't work.
Validate the chunking/paging/matching logic with a wider historical range and/or a unit test
using a fake `IPayPalGateway` response set spanning >31 days and >1 page, since a real sandbox
account freshly used in one dev session won't reliably have that much historical data to query
against live.

---

## 7. Known, deliberate simplifications (not gaps — decisions)

- **Currency decimal places**: amounts are formatted assuming a 2-decimal-place currency (USD
  and most major currencies PayPal supports). `CatalogItem.Price` has no currency/precision
  concept today, and the task only supplies one currency via config. If `PayPal:Currency` is
  ever set to a zero-decimal (e.g. `JPY`) or 3-decimal (e.g. `BHD`) currency, amount formatting
  would need adjusting per PayPal's currency table — flagged here as a known limitation, not
  built for, since the provided sandbox credentials are expected to be USD.
- **Reauthorize and "no longer renewable" are not practically exercisable against a live
  sandbox in one dev session** — reauthorize only becomes legal starting the 4th day after the
  original authorization (past the 3-day honor period), and "no longer renewable" only after
  ~30 days. **Don't try to wait for this in real time.** Prove this code path with a focused
  test against a faked `IPayPalGateway` that returns an `AUTHORIZATION_EXPIRED` 422 on the first
  capture attempt (and, separately, a reauthorize failure) rather than treating it as blocked or
  claiming it was verified against the real sandbox — it can't be, within a single session.
- **No webhooks.** Every PayPal capability this integration needs is driven synchronously by an
  operator/shopper-initiated call (authorize, capture, void, refund, vault, reporting) — nothing
  here depends on PayPal calling back into the app, so no public callback URL / webhook
  subscription is needed, which also sidesteps the lack of any tunneling/ngrok-style setup on
  this machine.
- **`confirm-payment-source`** (§1.2 note) exists in the Orders v2 API but isn't used — it's
  only relevant for redirect-based payment sources, and this integration is direct-card-only.

---

## 8. Build order

1. **Config + PayPal client skeleton first.** Wire `PayPalOptions`, the env-var bridge (§4.2),
   base URL resolution (§4.3), and get the OAuth token call working end-to-end as an early smoke
   test before building anything else on top of it — the fastest way to catch credential/config
   problems.
2. **Domain model**: `OrderStatus`, `Payment`, `Refund`, `PaymentMethod`, `PaymentEvent`, EF
   configs, the migration, extend `IOrderService`.
3. **`IPayPalGateway` full implementation**: authorize, capture (+ stale-auth retry), void,
   refund, vault save/delete, transaction search (chunked+paged). Unit-test the idempotency-key
   derivation (§4.5) and the DTO (de)serialization against PayPal's confirmed shapes (§1)
   directly, independent of a live sandbox call.
4. **PublicApi endpoints**: orders (create/pay/fulfil/cancel/refunds/my-orders), payment-methods
   (create/list/delete), reconciliation — following the `IEndpoint<>` pattern (§2).
5. **`ExceptionMiddleware`** branches for the new exception types (§3.6).
6. **Tests**: `PublicApiIntegrationTests`-style tests (existing project, existing
   `ApiTokenHelper` for admin/shopper tokens) for auth/role/ownership enforcement using the
   in-memory DB and a faked `IPayPalGateway` for fast, deterministic coverage of every endpoint
   and error path — including the ones that can't be hit live (§7). Then a smaller set of tests
   or a manual pass that really talks to the sandbox for the core money-movement path (next
   step).
7. **Self-verification against the real sandbox** — concrete sequence below.
8. **Write the user-facing step-by-step verification guide** (mirrors step 7, in terms someone
   without this plan can follow with curl/Postman + a bearer token).

### Self-verification sequence (what "actually works" means here)

Run `PublicApi` with `DOTNET_ROLL_FORWARD=Major`, `UseOnlyInMemoryDatabase=true`, and the four
`PAYPAL_*` env vars set, bound to this machine's assigned port block. Then, via curl/Postman:

1. `POST /api/authenticate` as both an admin and a non-admin seeded user (`admin@microsoft.com`
   / `demouser@microsoft.com`, password `Pass@word1`, per the existing seed data) → two bearer
   tokens.
2. `POST /api/orders` (shopper token) → `orderId`.
3. `POST /api/orders/{orderId}/pay` (shopper token) with the sandbox card
   `4111 1111 1111 1111`, any future expiry, any CVC, any name/address → expect a real
   authorization id/status back from PayPal (not `PAYER_ACTION_REQUIRED` — if it is, stop per
   §1.2/§7).
4. `POST /api/orders/{orderId}/fulfil` (admin token) → expect a real capture id, PayPal fee, and
   net amount back. `GET /api/my-orders` (shopper token) should now show it `Fulfilled`.
5. `POST /api/orders/{orderId}/refunds` (shopper token) with an idempotency key and a partial
   amount → expect success; repeat the exact same request (same key) → expect the same stored
   result, not a second PayPal refund; issue a second refund with a **different** key for the
   remaining amount → expect that one to succeed too, and a third attempt beyond the remaining
   balance to be rejected locally before ever calling PayPal.
6. Second order: `POST /api/orders` → `pay` → then, **before** fulfilling,
   `POST /api/orders/{orderId}/cancel` (admin token) → confirm no capture ever happened
   (`GET /api/my-orders` shows `Cancelled`, no captured amount).
7. `POST /api/payment-methods` (shopper token) with the same sandbox card → `paymentMethodId`.
   `GET /api/payment-methods` → confirm it's listed with brand/last4/expiry, no full card
   number anywhere in the response. Third order: `POST /api/orders` → `pay` using
   `{ "paymentMethodId": ... }` instead of raw card fields → confirm it authorizes successfully
   (this is the "saved card reused to pay a second order" proof the task asks for).
   `DELETE /api/payment-methods/{paymentMethodId}` → confirm it's gone from a subsequent
   `GET /api/payment-methods`, and that attempting to `pay` a new order with that same
   `paymentMethodId` now fails cleanly (not a 500).
8. `GET /api/reconciliation?from=<a few days back>&to=<now>` (admin token) → confirm the request
   succeeds, chunks/pages correctly, and (mind PayPal's ≤3h reporting lag — §1.7) don't treat an
   empty or partially-empty result for the transactions just created above as a failure; if
   nothing from this session's activity has landed in PayPal's reporting yet, that's expected —
   the important thing to verify is that the endpoint is correct over whatever range does return
   data (even if that's a query against an earlier date range with pre-existing sandbox
   activity, if any exists on the provided account) and handles an empty range gracefully.

Steps 2–7 constitute the real, live-sandbox proof of authorize → capture (with fee/net) →
partial refund (with idempotency) → cancel (no capture) → saved-card reuse. Step 8 proves the
reconciliation endpoint is correctly built even under the sandbox's inherent reporting lag.
Reauthorization/non-renewable behavior is proven via the faked-gateway tests from step 6 of the
build order, per §7 — not against live PayPal, for the reasons given there.
