# Plan — PayPal payments and saved cards for eShopOnWeb

This is the implementation plan for the task in `PHASE-PLAN.md`. It is written for a fresh
session with no memory of this one. It assumes familiarity with the task brief but not with
the investigation behind this document. Section 1 is the PayPal contract, verified against
primary sources before any code was designed against it. Sections 2+ are the design.

All the "your call" decisions below are made — follow them unless you find a concrete reason
not to, in which case use your own judgment and note the deviation in your final summary to
the user.

---

## 0. Repo facts this plan relies on (verified by direct inspection)

- **Central package management**: `Directory.Packages.props` at repo root
  (`ManagePackageVersionsCentrally=true`, `TargetFramework=net8.0`). Any new NuGet package
  needs a `<PackageVersion>` entry there, then a version-less `<PackageReference>` in the
  consuming `.csproj`.
- **Layering**: `ApplicationCore` (domain, zero HTTP/EF dependencies — only
  `Ardalis.GuardClauses`/`Ardalis.Result`/`Ardalis.Specification`/`System.Text.Json`) ←
  `Infrastructure` (EF Core + Identity) ← `PublicApi` / `Web`. `PublicApi` references both
  `ApplicationCore` and `Infrastructure` directly.
- **Order/Basket today**: `Order` (`src/ApplicationCore/Entities/OrderAggregate/Order.cs`) has
  `BuyerId` (string = the signed-in username, e.g. `demouser@microsoft.com`), `OrderDate`,
  `ShipToAddress` (owned value object), `OrderItems` (private list, built only via
  constructor), `Total()`. No status field exists today. `OrderService.CreateOrderAsync`
  (`src/ApplicationCore/Services/OrderService.cs`) is the only creation path today, and it
  goes basket → order; it is **not** reused as-is (see §2.1) but its snapshot pattern
  (`CatalogItemOrdered` built from live `CatalogItem` at order time) is.
- **`Buyer`/`PaymentMethod`** (`src/ApplicationCore/Entities/BuyerAggregate/`) already exist as
  dead scaffolding: `PaymentMethod.CardId` even carries the comment *"actual card data must be
  stored in a PCI compliant system, like Stripe"*. Neither has a `DbSet` in `CatalogContext`,
  neither has EF configuration, `PaymentMethod` has no public constructor. This is exactly the
  shape saved cards need — flesh these out rather than inventing new types.
- **PublicApi endpoint convention**: one file per endpoint class implementing
  `IEndpoint<IResult, TRequest, TDependency>` from the `MinimalApi.Endpoint` package, with an
  `AddRoute(IEndpointRouteBuilder)` that calls `app.MapGet/MapPost(...)` directly and a
  `HandleAsync(...)` with the logic. Request/response DTOs are separate files
  (`XEndpoint.XRequest.cs`, `XEndpoint.XResponse.cs`) in the same namespace/folder. Repositories
  and other per-request dependencies are minimal-API route-handler parameters (resolved from
  DI per call), not constructor-injected. Auth is a plain `[Authorize(...)]` attribute on the
  lambda: `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
  AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` for admin-only, or
  `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` for any signed-in
  caller. Routes are hardcoded literal strings, e.g. `"api/catalog-items"`. Reference file:
  `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`.
- **Auth reality**: the JWT issued by `POST /api/authenticate`
  (`src/Infrastructure/Identity/IdentityTokenClaimService.cs`) carries **only**
  `ClaimTypes.Name` (the username/email) and one `ClaimTypes.Role` claim per role — **no user id
  (GUID) claim**. There is exactly one role, `"Administrators"`
  (`BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS`). Do not add new claims to the
  token for this feature — keep using `User.Identity!.Name` as the caller's identity, exactly
  like `Order.BuyerId`/`Basket.BuyerId` already do. This is a deliberate scope-minimizing choice
  (see §3.4 for how saved cards get a PayPal-safe id without touching the token).
- **Error handling convention**: plain exceptions caught by
  `src/PublicApi/Middleware/ExceptionMiddleware.cs`, which currently special-cases only
  `DuplicateException` (everything else → 500). Extend it; don't introduce `Ardalis.Result` for
  this feature even though the package is referenced — it isn't used anywhere in `PublicApi`
  today and mixing patterns for one feature would be inconsistent with the rest of the project.
- **DB**: `Order` lives in `CatalogContext` (`src/Infrastructure/Data/CatalogContext.cs`),
  alongside `Basket`/`CatalogItem`. EF configuration classes live in
  `src/Infrastructure/Data/Config/*Configuration.cs`, one per entity, applied via
  `ApplyConfigurationsFromAssembly`. Value objects hang off aggregates via `OwnsOne` (see
  `OrderConfiguration.cs` for `ShipToAddress`). `UseOnlyInMemoryDatabase` is read as a flat
  top-level config key in `src/Infrastructure/Dependencies.cs` and is what this session will run
  against (per the task's environment gotchas) — but still write and commit a real EF Core
  migration; don't skip migrations just because local verification uses the in-memory provider.
- **No existing outbound-HTTP pattern**: no `IHttpClientFactory` typed clients, no Polly,
  anywhere in `src`. This feature introduces that pattern from scratch (§4).

---

## 1. The PayPal contract (verified against primary sources — do not deviate without re-checking)

Everything below was confirmed against PayPal's own OpenAPI specifications
(`github.com/paypal/paypal-rest-api-specifications`, files `checkout_orders_v2.json`,
`payments_payment_v2.json`, `vault_payment_tokens_v3.json`, `reporting_transactions_v1.json` —
these are the same specs that back `developer.paypal.com`'s reference pages) plus
`developer.paypal.com` narrative docs. Use plain `HttpClient` against these REST APIs (see
§4.1 for why, not an SDK).

### 1.1 Auth — OAuth2 client credentials

`POST {base}/v1/oauth2/token`, `Content-Type: application/x-www-form-urlencoded`, HTTP Basic
auth (`client_id:client_secret`, base64), body `grant_type=client_credentials`. Response has
`access_token` and `expires_in` (seconds). Cache and refresh before expiry; there is no
refresh-token flow, just re-request. This is the **only** endpoint that uses Basic auth and
form-encoding — everything else uses `Authorization: Bearer {access_token}` and JSON.

### 1.2 Base URL resolution (binds directly to the task's `PayPal:BaseUrl` override rule)

```
ResolveBaseUrl(options):
    if options.BaseUrl is set and non-empty: return options.BaseUrl (trimmed of trailing '/')
    if options.Environment (case-insensitive) is "live" or "production": return "https://api-m.paypal.com"
    else: return "https://api-m.sandbox.paypal.com"   // default / "sandbox"
```
Apply this **once**, to the single `HttpClient`'s `BaseAddress`, and use that same client for
the token endpoint too — the task requires the override to apply to "every PayPal call —
including the credential/token request."

### 1.3 Idempotency — `PayPal-Request-Id` header

Confirmed on order-create, authorization-capture, and capture-refund (all three declare this
header in their specs; described identically: *"A unique ID identifying the request header for
idempotency purposes"*). PayPal stores the key for 6 hours (extendable to 72h by the merchant's
account manager — not something to rely on). A repeated request with the same key and the
**same** body returns the original response again rather than repeating the side effect; the
order-create doc calls this out explicitly as mandatory for single-step card/vault_id creation.
This is the mechanism behind the task's "payment operations must be idempotent in effect" and
the refund's "caller-supplied idempotency key" requirement — see §5 for exactly which key goes
on which call.

### 1.4 Orders v2 — `POST /v2/checkout/orders` (single call authorizes a direct card payment)

Body:
```json
{
  "intent": "AUTHORIZE",
  "purchase_units": [{
    "invoice_id": "<eShop OrderId as string>",
    "amount": { "currency_code": "USD", "value": "19.99" }
  }],
  "payment_source": {
    "card": {
      "name": "...", "number": "...", "expiry": "2028-04", "security_code": "123",
      "billing_address": { "address_line_1": "...", "admin_area_2": "...", "admin_area_1": "...", "postal_code": "...", "country_code": "US" }
    }
  }
}
```
- `intent`: `"CAPTURE"` or `"AUTHORIZE"` (enum `checkout_payment_intent`). Use `AUTHORIZE` —
  this is the hold-don't-take step (`POST /api/orders/{orderId}/pay`).
- Passing raw `card.number`/`security_code` requires the merchant account to be PCI SAQ-D
  approved for direct card processing — the task states the given sandbox business account
  already has this enabled, so no hosted-fields/redirect flow is needed.
- **Paying with a saved card instead**: replace `payment_source.card` with
  `{ "vault_id": "<the PayPal vault id>", "stored_credential": { "payment_initiator": "CUSTOMER", "payment_type": "UNSCHEDULED", "usage": "SUBSEQUENT" } }`
  (schema `card_request`; `vault_id` and `stored_credential` are siblings of `number`/`expiry`
  under the same `card` object — you send one or the other, never both). `payment_initiator:
  CUSTOMER` + `usage: SUBSEQUENT` is correct here because the shopper is present and initiating
  this payment themselves, just with a card they saved earlier (not a merchant-initiated
  recurring charge).
- **`invoice_id`** on the purchase unit: set it to the eShop `Order.Id` as a plain string (e.g.
  `"42"`). PayPal's transaction-search results echo this back verbatim (confirmed in §1.6) — it
  is the join key for reconciliation. Set it consistently at order-create time; if not present
  on the capture request, PayPal reports the invoice_id of the authorizing transaction, so
  setting it once here is enough (repeating it on the capture call in §1.5 is still fine and
  slightly more explicit but not required).
- Response `id` is the **PayPal order id** (store it, but it is not what you call
  capture/void/reauthorize on). The authorization itself is nested at
  `purchase_units[0].payments.authorizations[0]` and has its **own** `id` — store *that* as
  `Payment.AuthorizationId`; that's the id every subsequent authorization-lifecycle call uses.
  It also carries `status` (see enum below) and `expiration_time` (ISO 8601 — the 29-day-from-now
  authorization deadline, useful for a proactive staleness check, see §5.3).
- **Amount-must-match-to-the-cent**: format `amount.value` as
  `total.ToString("F2", CultureInfo.InvariantCulture)` from `Order.Total()`. (USD/EUR/GBP all
  use 2 decimal places; `PAYPAL_CURRENCY` is expected to be one of the standard 2-decimal
  currencies for this integration — don't build minor-unit logic for zero-decimal currencies
  like JPY, that's out of scope.)
- **Vaulting during checkout is not used** — Flow 2 (saved cards) is a standalone action, not
  something that happens as a side effect of paying for an order, so `payment_source.card.
  attributes.vault` is never set on this call. Don't conflate this with the Vault API in §1.7.
- **`PAYER_ACTION_REQUIRED` / any response asking for a buyer-approval redirect**: per the task,
  if PayPal responds this way to a direct card payment with the sandbox test card, **stop and
  report it** — don't build a redirect/approval round-trip. In code, this shows up as
  `order.status == "PAYER_ACTION_REQUIRED"` (enum `order_status`) or an authorization
  `status_details.reason` of `PENDING_REVIEW`/`DECLINED_BY_RISK_FRAUD_FILTERS` needing further
  action. Treat detection of this state as a hard stop (throw a clearly-named exception,
  e.g. `PayPalApprovalRequiredException`, that the endpoint maps to a distinct 5xx/409 rather
  than silently retrying or swallowing it) — this is a guard for something the task says should
  not happen with the sandbox card, not a flow to build out.
- Idempotency: send `PayPal-Request-Id` on this call (mandatory per spec for single-step
  card/vault payments anyway).

Authorization status enum (`authorization_status`, exhaustive — there is **no** `EXPIRED`
member; expiry is detected only by attempting capture and getting the error in §1.5, or
proactively via `expiration_time`):
`CREATED | CAPTURED | DENIED | PARTIALLY_CAPTURED | VOIDED | PENDING`.

Order status enum (`order_status`, for completeness — mostly relevant for detecting
`PAYER_ACTION_REQUIRED` above): `CREATED | SAVED | APPROVED | VOIDED | COMPLETED |
PAYER_ACTION_REQUIRED`.

### 1.5 Payments v2 — authorization lifecycle (`POST /api/orders/{orderId}/fulfil`, `/cancel`)

All under `/v2/payments/authorizations/{authorization_id}/...`:

- **`POST .../capture`** — takes the money. Body: `{ "amount": {...}, "final_capture": true, "invoice_id": "<OrderId>" }`
  (amount required to match the authorized amount for a full capture — this integration always
  does one full capture per order, so `final_capture: true` always). Response `capture` object
  (schema `capture`) has `id` (the **capture id** — store as `Payment.CaptureId`), `status`
  (`COMPLETED | DECLINED | PARTIALLY_REFUNDED | PENDING | REFUNDED | FAILED`), and
  `seller_receivable_breakdown` with exactly the three fields the task asks for:
  `gross_amount`, `paypal_fee`, `net_amount` (all `money` objects — store their `.value` as
  decimals on `Payment`).
  - **Confirmed error** (from PayPal's own 422 example): capturing an expired authorization
    returns `422` with `details[0].issue == "AUTHORIZATION_EXPIRED"`. This is the exact signal
    to branch into the reauthorize step below.
- **`POST .../reauthorize`** — body `{ "amount": {...} }` only (amount is the only supported
  field per the spec). Confirmed rules straight from the endpoint description:
  - An authorization has a 3-day *honor period* from creation during which it can be captured
    directly; calling reauthorize inside that window fails with
    `422 REAUTHORIZATION_TOO_SOON`.
  - Between day 4 and day 29 after the original authorization, reauthorize is allowed (any
    number of times within that window per the description — "you can issue multiple
    re-authorizations after the honor period expires").
  - **Confirmed: reauthorize returns the same authorization `id`**, just with `status` reset to
    `CREATED` and a fresh 3-day honor period / new `expiration_time` (verified against PayPal's
    own example response — `id` in the reauthorize response example is identical to the id in
    the path). So there is never a second authorization id to track — just update the existing
    `Payment` row's status/expiry and bump a reauthorization counter.
  - Reauthorization currency must match the original (`422 AUTH_CURRENCY_MISMATCH` otherwise —
    won't happen here since currency is fixed by config, but worth guarding for anyway).
  - Per PayPal's own docs: *"If 30 days have transpired since the date of the original
    authorization, you must create an authorized payment instead of reauthorizing."* PayPal's
    public spec does not enumerate a specific machine-readable error code for this terminal
    case (only the two 422 examples above are documented). **Design for this by treating any
    reauthorize failure as terminal-and-operator-facing**, not by special-casing an unconfirmed
    error code: surface the PayPal error's `details[].issue`/`description` verbatim to the
    caller rather than inventing a code you haven't verified. This satisfies "one that can no
    longer be renewed must say so in terms an operator can act on" without fabricating PayPal
    behavior.
- **`POST .../void`** — cancels/voids an authorization; PayPal's own doc: *"You cannot void an
  authorized payment that has been fully captured."* No request body. This is what
  `POST /api/orders/{orderId}/cancel` calls when there's a live hold to release.
- **`GET .../{authorization_id}`** exists too, for re-fetching current status — not required
  for the core flow given the above (create-with-authorize returns everything inline, and
  capture/reauthorize/void responses carry the updated state), but useful if you want a
  reconciliation-adjacent "resync one authorization" utility. Optional.

### 1.6 Payments v2 — refunds (`POST /api/orders/{orderId}/refunds`)

`POST /v2/payments/captures/{capture_id}/refund`. Empty body `{}` = full refund of whatever
remains uncaptured-refunded; body with `amount: {value, currency_code}` = partial refund.
Response (schema references `refund_status`): `id` (the **PayPal refund id**), `status`
(`CANCELLED | FAILED | PENDING | COMPLETED`), `seller_payable_breakdown`. Multiple partial
refunds against the same capture are legitimate as long as their sum doesn't exceed the
captured gross amount — PayPal itself enforces this too (a refund exceeding what's left returns
an error), but enforce it locally first (§5.5) so the operator gets a clean 4xx instead of a
raw PayPal error.

**Idempotency for refunds is the caller-supplied key from the task, mapped 1:1 onto
`PayPal-Request-Id`** — this header is exactly PayPal's idempotency mechanism (§1.3), and using
the caller's own key as its value is the simplest, most literal way to satisfy "refunds carry a
caller-supplied idempotency key." Layer a local check in front of it too (§5.5) since PayPal's
own window is only 6 hours and this reference app's in-memory DB is the actual system of
record for the lifetime of a run.

### 1.7 Vault API v3 — saved cards (Flow 2)

This is a **different** API family (`/v3/vault/...`) from the Orders/Payments APIs above, and
is what backs `POST /api/payment-methods`, `GET /api/payment-methods`,
`DELETE /api/payment-methods/{id}` — not the Orders API's inline `attributes.vault`
mechanism (that's for vaulting *while also paying*, which Flow 2 explicitly is not: saving a
card is a standalone action here).

- **`POST /v3/vault/payment-tokens`** — creates a vaulted card directly from card details (no
  separate setup-token step needed when you already hold PCI-scoped card data server-side,
  which this integration does). Body:
  ```json
  {
    "customer": { "id": "eshop-buyer-<local Buyer.Id>" },
    "payment_source": {
      "card": { "name": "...", "number": "...", "expiry": "2028-04", "security_code": "123", "billing_address": {...} }
    }
  }
  ```
  - `customer.id` is a **caller-supplied** identifier ("the unique ID for a customer in
    merchant's or partner's system of records") — PayPal does not generate it, you own it, and
    you must supply the exact same value later to list this customer's tokens. Its pattern is
    `^[0-9a-zA-Z_-]+$`, 7–36 chars — a raw email (contains `@`/`.`) does **not** match. See §3.4
    for the exact value to use (derived from the local `Buyer.Id`, not the username/JWT claim).
  - Response: `id` (the **vault id** — this is what `Payment` later calls `vault_id`, and what
    the task calls "one of the shopper's saved cards" in Flow 1), `customer.id` echoed back,
    `payment_source.card` echoed back with `last_digits`, `brand`, `expiry`, `name`,
    `billing_address` — this is the "describes it safely enough to recognise which card"
    payload for the `POST /api/payment-methods` response; never the full PAN.
- **`GET /v3/vault/payment-tokens?customer_id=eshop-buyer-<id>`** — lists that customer's saved
  tokens (paginated: `page`, `page_size`). **Not needed at runtime** for
  `GET /api/payment-methods` — the local `PaymentMethod` rows (written at save-time,
  §3.4) are the source of truth for listing, so this call is optional/for-audit only. Documented
  here for completeness since it's part of the verified contract.
- **`DELETE /v3/vault/payment-tokens/{id}`** — removes the vaulted card from PayPal. Call this
  from `DELETE /api/payment-methods/{paymentMethodId}` (see §5.6 for ordering/failure handling).

### 1.8 Reporting v1 — transaction search (`GET /api/reconciliation`)

`GET /v1/reporting/transactions` — **two hard constraints confirmed directly from the spec's
parameter definitions**, both load-bearing for this endpoint:
1. `start_date`/`end_date` (both required, ISO 8601) — **"The maximum supported range is 31
   days."** A caller-supplied `from`/`to` spanning longer than 31 days must be served by
   issuing multiple requests over consecutive ≤31-day windows and concatenating results — do
   not truncate or reject a longer range.
2. Pagination via `page` (1-based) / `page_size` (max 500, default 100) — response
   (`search_response` schema) carries `total_items`; the task explicitly requires "the whole
   range, not just the first page of it," so loop pages within each date window until
   `page * page_size >= total_items` (or an empty `transaction_details` array comes back).

Each `transaction_info` (inside `transaction_details[]`) carries: `transaction_id`
(PayPal's own id for that money-movement event — for a capture this is the capture id),
`transaction_amount`, `transaction_status`, `transaction_initiation_date`, and — the
reconciliation join key — **`invoice_id`**, described as: *"If an invoice ID was sent with the
capture request, the value is reported. Otherwise, the invoice ID of the authorizing
transaction is reported."* Since `invoice_id` is set to the eShop `Order.Id` string at
order-create time (§1.4), every PayPal transaction for an order eShop created will carry that
order's id verbatim — match on it directly, no fuzzy matching needed.

Build the report as three buckets over the requested `[from, to]`:
- **Matched** — a PayPal transaction whose `invoice_id` parses to a known local `Order.Id` with
  a `Payment` record.
- **PayPal-only** — a PayPal transaction whose `invoice_id` doesn't resolve to a `Payment` eShop
  has (present in PayPal, missing in eShop).
- **eShop-only** — a local `Payment` whose authorization/capture timestamp falls in
  `[from, to]` but for which no matching PayPal transaction turned up in the search results
  (present in eShop, missing in PayPal — **this is expected and not a bug** for a range that
  was just created, per the task's note about PayPal's reporting lag; don't treat an empty
  bucket, or this bucket being non-empty for a *very recent* range, as an error condition in
  the code — just report it, the caller interprets it).

---

## 2. Domain model changes (`ApplicationCore`)

### 2.1 `Order` — add payment/fulfilment state

```csharp
public enum OrderStatus
{
    AwaitingPayment,
    PaymentAuthorized,
    Fulfilled,
    Cancelled,
    Refunded,
    PartiallyRefunded
}
```
Add `public OrderStatus Status { get; private set; }` to `Order`, defaulted to
`AwaitingPayment` in the constructor. Add state-transition methods with guard clauses (mirror
the existing `Guard.Against.*` style already used in `Order`'s constructor), e.g.
`MarkPaymentAuthorized()`, `MarkFulfilled()`, `MarkCancelled()`, `MarkRefunded(bool partial)` —
each should `Guard.Against` an invalid current state so illegal transitions throw from the
domain, not just get silently allowed by a careless caller. Keep `Order` itself ignorant of
PayPal — it only knows its own status enum; all PayPal-specific state lives on the new
`Payment` entity below (§2.2), referenced by `OrderId`.

**New order-creation path**: `POST /api/orders` takes catalog item ids + quantities directly
(no basket), per the task. Don't reuse `OrderService.CreateOrderAsync(basketId, ...)` as-is —
add a sibling method (same service, e.g.
`CreateOrderAsync(string buyerId, IReadOnlyCollection<(int CatalogItemId, int Quantity)> items, Address shipToAddress)`)
that skips the basket lookup but reuses the exact same snapshot pattern: load the requested
`CatalogItem`s via `CatalogItemsSpecification` (already exists), build `CatalogItemOrdered` +
`OrderItem` from each, construct `Order` (status defaults to `AwaitingPayment`), persist via
`IRepository<Order>`. The request DTO needs its own shipping address fields (street, city,
state, country, zip) — there's no basket/checkout UI supplying one for this API-only path, so
require it explicitly in the request rather than hardcoding a placeholder like
`Checkout.cshtml.cs` does.

### 2.2 New `Payment` entity (own aggregate root, FK `OrderId`, one per `Order`)

Not an EF owned type — it needs independent identity for the reconciliation report to enumerate
all payments directly, and it owns a child collection (`Refund`) that itself needs identity for
the idempotency-key lookup in §5.5.

```csharp
public enum PaymentAuthorizationStatus { Created, Captured, Denied, PartiallyCaptured, Voided, Pending }
public enum PaymentCaptureStatus { Completed, Declined, PartiallyRefunded, Pending, Refunded, Failed }

public class Payment : BaseEntity, IAggregateRoot
{
    public int OrderId { get; private set; }
    public string CurrencyCode { get; private set; }
    public decimal AuthorizedAmount { get; private set; }        // must equal Order.Total() at authorize time

    public string PayPalOrderId { get; private set; }            // the /v2/checkout/orders response id (audit trail only)
    public string AuthorizationId { get; private set; }          // what capture/void/reauthorize act on
    public PaymentAuthorizationStatus AuthorizationStatus { get; private set; }
    public DateTimeOffset AuthorizationExpiresAt { get; private set; }
    public int ReauthorizationCount { get; private set; }
    public int PaymentAttemptCount { get; private set; }         // see idempotency-key derivation, §5.2/§5.3

    public string? CaptureId { get; private set; }
    public PaymentCaptureStatus? CaptureStatus { get; private set; }
    public decimal? CapturedGrossAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }
    public decimal RefundedAmount { get; private set; }          // running total; never exceeds CapturedGrossAmount

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    // Behavior methods (all with Guard.Against state/argument checks):
    // RecordAuthorization(payPalOrderId, authorizationId, status, expiresAt, authorizedAmount, currencyCode)
    // RecordReauthorization(status, newExpiresAt)          // increments ReauthorizationCount
    // RecordCapture(captureId, status, grossAmount, feeAmount, netAmount)
    // RecordVoid()
    // AddRefund(payPalRefundId, amount, status, idempotencyKey) -> Refund   // updates RefundedAmount
    // IncrementPaymentAttempt() -> int                      // for the /pay idempotency-key derivation
}

public class Refund : BaseEntity
{
    public int PaymentId { get; private set; }
    public string PayPalRefundId { get; private set; }
    public string IdempotencyKey { get; private set; }   // caller-supplied; unique per Payment
    public decimal Amount { get; private set; }
    public string Status { get; private set; }           // mirrors PayPal's refund_status string directly
    public DateTimeOffset CreatedAt { get; private set; }
}
```
No `EXPIRED` member on `PaymentAuthorizationStatus` — deliberately mirrors PayPal's own
`authorization_status` enum exactly (§1.4), which has no such state either; staleness is a
derived fact (`AuthorizationExpiresAt < now`), not a status PayPal itself reports.

### 2.3 Flesh out `Buyer` / `PaymentMethod` (Flow 2 — saved cards)

These already exist as dead scaffolding (§0) — extend, don't replace.

```csharp
public class PaymentMethod : BaseEntity
{
    public int BuyerId { get; private set; }
    public string? Alias { get; private set; }
    public string CardId { get; private set; }      // repurposed: the PayPal vault id (was already earmarked for exactly this)
    public string Last4 { get; private set; }
    public string Brand { get; private set; }        // new — from the vault response's payment_source.card.brand
    public string ExpiryYearMonth { get; private set; } // new — "2028-04", from the vault response

    public PaymentMethod(string cardId, string last4, string brand, string expiryYearMonth, string? alias) { ... }
}
```
Add a constructor and an `AddPaymentMethod(...)`/`RemovePaymentMethod(int paymentMethodId)`
pair of behavior methods on `Buyer` (mirroring how `Basket.AddItem`/`Order` already expose
behavior methods rather than public list mutation). `IdentityGuid` on `Buyer` stays keyed by
username, exactly like `Order.BuyerId`/`Basket.BuyerId` today — look up (or lazily create, on
first `POST /api/payment-methods`) the caller's `Buyer` row by `IdentityGuid == User.Identity!.Name`.

---

## 3. Persistence (`Infrastructure`)

### 3.1 `CatalogContext` — new `DbSet`s
Add `DbSet<Buyer> Buyers`, `DbSet<PaymentMethod> PaymentMethods`, `DbSet<Payment> Payments`,
`DbSet<Refund> Refunds` to `src/Infrastructure/Data/CatalogContext.cs`.

### 3.2 New EF configuration classes (`src/Infrastructure/Data/Config/`)
`BuyerConfiguration`, `PaymentMethodConfiguration` (FK `BuyerId`), `PaymentConfiguration` (FK
`OrderId`, unique index on `OrderId` since it's 1:1), `RefundConfiguration` (FK `PaymentId`,
unique index on `(PaymentId, IdempotencyKey)` — this index is what makes the idempotent-refund
lookup in §5.5 a single indexed query rather than a race-prone check-then-act). Follow the
existing `OrderConfiguration`/`OrderItemConfiguration` style (explicit `Property(...).HasMaxLength(...)`,
`decimal(18,2)` for money columns). `Order.Status` — store via
`.HasConversion<string>()` (or an int if you prefer; either is fine, just be consistent — this
one genuinely is your call).

### 3.3 Migration
After the model/config changes, add one EF Core migration against `CatalogContext` (mirror the
existing `20211231093753_FixShipToAddress` etc. under
`src/Infrastructure/Data/Migrations/`), e.g.
`dotnet ef migrations add AddPaymentsAndSavedCards --project src/Infrastructure --startup-project src/PublicApi`.
Do this even though local verification runs on `UseOnlyInMemoryDatabase=true` (which ignores
migrations entirely) — a real migration is still required for this to be usable against actual
SQL Server, and its absence would be a real gap in the delivered feature.

### 3.4 The PayPal-safe customer id for vaulting (concrete decision, not left open)

PayPal's vault `customer.id` must match `^[0-9a-zA-Z_-]+$`, 7–36 chars (§1.7) — the signed-in
username (an email address, e.g. `demouser@microsoft.com`) does not match (contains `@`/`.`).
Rather than touching JWT claims (explicitly avoided, §0) or querying the Identity DB from
`PublicApi` for a GUID, use the local `Buyer` aggregate's own EF-generated integer id, which
already exists once a `Buyer` row is created for that shopper:

```
customer.id := $"eshop-buyer-{buyer.Id}"
```
Compute this at the point of calling `IPayPalVaultGateway.SaveCardAsync` in the
`POST /api/payment-methods` endpoint (creating the `Buyer` row first if the shopper doesn't
have one yet, looked up by `IdentityGuid == User.Identity!.Name`). This value never needs to be
looked up again at runtime (the local `PaymentMethod` rows are the source of truth for listing,
§1.7), but store it isn't required either — it's derivable from `buyer.Id` any time it's needed
(e.g. if you ever call the optional `GET /v3/vault/payment-tokens?customer_id=...` for an audit
script).

---

## 4. PayPal integration layer

### 4.1 SDK vs. plain HTTP — use plain HTTP

An official `paypal/PayPal-Dotnet-Server-SDK` exists on GitHub, but its coverage of the Vault
v3 API (§1.7) and the Reporting v1 transaction-search API (§1.8) was not confirmed during
research — only the Orders/Payments v2 surface is clearly covered by PayPal's own SDK
messaging. Since every endpoint this integration needs has already been verified directly
against PayPal's published OpenAPI specs (§1), implement against the REST APIs directly with
`HttpClient` rather than taking on a dependency whose coverage of two of the four API families
this task needs is unverified. This also keeps the whole integration's behavior traceable to
the verified contract in §1 rather than an SDK abstraction layer.

### 4.2 Where the code lives
- **Interfaces** (no HTTP/JSON types leaking out) in `ApplicationCore` — new folder
  `src/ApplicationCore/Interfaces/Payments/` (or alongside the existing `IOrderService`, your
  call on folder granularity):
  ```csharp
  public interface IPayPalPaymentGateway
  {
      Task<PayPalAuthorizationOutcome> AuthorizeAsync(PayPalAuthorizeRequest request, CancellationToken ct);
      Task<PayPalCaptureOutcome> CaptureAsync(string authorizationId, decimal amount, string currency, string orderIdForInvoice, string idempotencyKey, CancellationToken ct);
      Task<PayPalReauthorizeOutcome> ReauthorizeAsync(string authorizationId, decimal amount, string currency, CancellationToken ct);
      Task VoidAsync(string authorizationId, CancellationToken ct);
      Task<PayPalRefundOutcome> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct);
  }

  public interface IPayPalVaultGateway
  {
      Task<PayPalSavedCard> SaveCardAsync(string customerId, CardDetails card, CancellationToken ct);
      Task DeletePaymentTokenAsync(string vaultId, CancellationToken ct);
  }

  public interface IPayPalReconciliationGateway
  {
      Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
  }
  ```
  `PayPalAuthorizeRequest` needs to express "either inline card, or vault id" — a small type
  with a nullable `CardDetails? Card` and nullable `string? VaultId` (exactly one populated;
  validate at the endpoint layer before calling the gateway) is simplest; don't overbuild a
  polymorphic hierarchy for two variants.
- **Implementations** in `Infrastructure` — new folder `src/Infrastructure/Payments/PayPal/`:
  `PayPalAccessTokenProvider`, `PayPalHttpClient` (thin wrapper: builds the request, attaches
  bearer token + optional `PayPal-Request-Id`, serializes/deserializes), `PayPalOrdersGateway :
  IPayPalPaymentGateway`, `PayPalVaultGateway : IPayPalVaultGateway`,
  `PayPalReconciliationGateway : IPayPalReconciliationGateway`, plus a `PayPalOptions` class.
- **DI registration** — in `src/PublicApi/Program.cs` only (not in the shared
  `Infrastructure/Dependencies.cs`, since `Web` has no use for any of this and shouldn't gain an
  outbound PayPal dependency as a side effect of this feature).

### 4.3 Configuration — exact binding, per the task's non-negotiable keys

Bind `PayPal:ClientId`, `PayPal:ClientSecret`, `PayPal:Environment`, `PayPal:Currency`,
`PayPal:BaseUrl` into a `PayPalOptions` class via
`builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection("PayPal"))`.

**The env vars are `PAYPAL_CLIENT_ID` / `PAYPAL_CLIENT_SECRET` / `PAYPAL_ENVIRONMENT` /
`PAYPAL_CURRENCY` — plain names, not the ASP.NET Core double-underscore convention
(`PayPal__ClientId`), so they will *not* auto-bind to `PayPal:ClientId` through the default
environment-variable configuration provider.** Map them explicitly, early in `Program.cs`,
before anything reads the `PayPal` section:

```csharp
var payPalEnvOverrides = new Dictionary<string, string?>
{
    ["PayPal:ClientId"] = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"),
    ["PayPal:ClientSecret"] = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET"),
    ["PayPal:Environment"] = Environment.GetEnvironmentVariable("PAYPAL_ENVIRONMENT"),
    ["PayPal:Currency"] = Environment.GetEnvironmentVariable("PAYPAL_CURRENCY"),
}.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value);
builder.Configuration.AddInMemoryCollection(payPalEnvOverrides!);
```
`PayPal:BaseUrl` has no env var in the task's list — it's config-file/appsettings-only (or a
differently-named env var if you choose to also expose one; not required). **Never write real
`ClientId`/`ClientSecret` values into `appsettings*.json`** — those files may omit the `PayPal`
section entirely, or include only non-secret placeholders (e.g. `Currency`); the env vars above
are the only source of the real values, consistent with the task's "secrets never enter the
repository" constraint applying equally to this session and the build session.

### 4.4 HTTP client setup

- Register a single named/typed `HttpClient` for PayPal via `AddHttpClient`, with
  `BaseAddress` set from `ResolveBaseUrl` (§1.2) inside the client-configuration callback (read
  `IOptions<PayPalOptions>` there, not a captured variable, so a changed config is honored on
  each client creation).
- Add `Microsoft.Extensions.Http.Polly` (+ transitive `Polly`) as new `PackageVersion` entries
  in `Directory.Packages.props`, then a plain `PackageReference` in `PublicApi.csproj` (this is
  the project doing the `AddHttpClient` call, per §4.2). Attach a transient-fault retry policy
  (`AddTransientHttpErrorPolicy`, a handful of retries with exponential backoff) — this covers
  network blips and 5xx from PayPal; do **not** retry on 4xx (those are business-logic
  responses like `AUTHORIZATION_EXPIRED` or a declined card, not transient faults, and blindly
  retrying them is wrong).
- JSON: PayPal's wire format is `snake_case` (`purchase_units`, `payment_source`, `last_digits`,
  ...). .NET 8 (confirmed the target framework here, §0) ships
  `JsonNamingPolicy.SnakeCaseLower` — use
  `new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }`
  for all PayPal request/response DTOs instead of hand-annotating every property with
  `[JsonPropertyName]`.

---

## 5. Flow-by-flow endpoint logic

All new endpoint files go under `src/PublicApi/`, following the `MinimalApi.Endpoint`
per-class convention (§0) — suggested layout:
```
OrderEndpoints/
  CreateOrderEndpoint.cs (+ .CreateOrderRequest.cs / .CreateOrderResponse.cs)
  PayOrderEndpoint.cs (+ .PayOrderRequest.cs / .PayOrderResponse.cs)
  FulfilOrderEndpoint.cs (+ .FulfilOrderResponse.cs)
  CancelOrderEndpoint.cs (+ .CancelOrderResponse.cs)
  RefundOrderEndpoint.cs (+ .RefundOrderRequest.cs / .RefundOrderResponse.cs)
  MyOrdersListEndpoint.cs (+ .MyOrdersResponse.cs)
  OrderDto.cs / PaymentDto.cs / RefundDto.cs   // shared response shapes
PaymentMethodEndpoints/
  CreatePaymentMethodEndpoint.cs / ListPaymentMethodsEndpoint.cs / DeletePaymentMethodEndpoint.cs (+ Request/Response files)
ReconciliationEndpoints/
  ReconciliationReportEndpoint.cs (+ .ReconciliationRequest.cs / .ReconciliationResponse.cs)
```
Route table:

| Method | Route | Auth |
|---|---|---|
| POST | `api/orders` | any signed-in caller |
| POST | `api/orders/{orderId}/pay` | any signed-in caller, own order only |
| POST | `api/orders/{orderId}/fulfil` | `Administrators` |
| POST | `api/orders/{orderId}/cancel` | `Administrators` |
| POST | `api/orders/{orderId}/refunds` | any signed-in caller, own order only — **see note below, this is deliberate** |
| GET | `api/my-orders` | any signed-in caller, own orders only |
| GET | `api/reconciliation?from&to` | `Administrators` |
| POST | `api/payment-methods` | any signed-in caller |
| GET | `api/payment-methods` | any signed-in caller, own methods only |
| DELETE | `api/payment-methods/{paymentMethodId}` | any signed-in caller, own methods only |

**Note on refund's auth scope**: the task names exactly three operator actions — "Fulfil,
cancel and reconciliation are operator actions... Every other endpoint is shopper-scoped."
Refunds are not in that named list, so per the task's own explicit words, `POST
/api/orders/{orderId}/refunds` is shopper-scoped (the caller must own the order), not
admin-gated — even though a self-service instant refund is unusual for a real storefront.
Implement it exactly as specified; this is a literal requirement, not a design gap to
"correct."

Ownership checks throughout: `Order.BuyerId == User.Identity!.Name` for order-scoped
endpoints; `Buyer.IdentityGuid == User.Identity!.Name` (join through `PaymentMethod.BuyerId`)
for payment-method-scoped ones. Not found or not owned → `404` (don't leak existence of another
shopper's order/card). Extract the caller via a `ClaimsPrincipal user` minimal-API parameter
(ASP.NET Core binds this automatically) — no new pattern needed beyond what `User.Identity!.Name`
already means elsewhere in this codebase (§0).

### 5.1 `POST /api/orders`
Request: `{ catalogItems: [{ catalogItemId, quantity }], shippingAddress: { street, city, state, country, zipCode } }`.
Validate item ids exist (reuse `CatalogItemsSpecification`) and quantities are positive. Call
the new `OrderService` method (§2.1). Response: `{ orderId: <int>, ... }` (`orderId` is the
required top-level identifier field per the task).

### 5.2 `POST /api/orders/{orderId}/pay` — authorize
Request: exactly one of `{ card: { number, expiry, securityCode, name, billingAddress } }` or
`{ paymentMethodId: <int> }`. Reject (400) if both or neither are present.

1. Load `Order`; must be owned by caller; must be `Status == AwaitingPayment` — if already
   `PaymentAuthorized` or later, **don't call PayPal again**, just return the existing
   `Payment` state (idempotency-in-effect for the double-click case, independent of PayPal's own
   6-hour window — see §0's note that this is an in-memory-DB reference app so "within this run"
   is the only durability horizon that matters anyway).
2. If `paymentMethodId` given: load the caller's `PaymentMethod` by id + ownership; use its
   `CardId` (the vault id) as `vault_id` per §1.4's vault-id branch. If `card` given: use it
   inline.
3. Idempotency key: `$"authorize:{orderId}:{payment.IncrementPaymentAttempt()}"` — the attempt
   counter (persisted on `Payment`, or on `Order` if you haven't created the `Payment` row yet
   for the very first attempt) means a genuine retry of the *same* attempt (e.g. a doubled HTTP
   request from a flaky client before any response came back) reuses the same key and dedupes
   at PayPal, while a *second, distinct* attempt after a decline (different card, or same card
   retried deliberately) gets a fresh key and a real new authorization attempt.
4. Call `IPayPalPaymentGateway.AuthorizeAsync`. On success: create/update the `Payment` row
   (§2.2's `RecordAuthorization`), `order.MarkPaymentAuthorized()`. On a card decline or other
   non-approval outcome: persist a `Payment` row anyway with `AuthorizationStatus = Denied` (or
   don't transition `Order` out of `AwaitingPayment` if you prefer not to persist failed
   attempts at all — either is defensible; persisting is more audit-friendly and is the
   recommended choice) and return a `4xx` with PayPal's decline reason; `Order` stays
   `AwaitingPayment` so the shopper can retry.
5. On a `PAYER_ACTION_REQUIRED`/approval-needed response: this is the task's explicit stop
   condition (§1.4) — don't handle it as a recoverable error path, let it surface distinctly
   (its own exception type) so it's obviously not "just another decline" if it's ever hit
   during real sandbox testing.

### 5.3 `POST /api/orders/{orderId}/fulfil` — capture, with reauthorize-on-stale

1. Load `Order` (any buyer — admin-only route); must be `Status == PaymentAuthorized`. Already
   `Fulfilled`? Return the existing capture state (idempotent no-op), don't re-capture.
2. Idempotency key: `$"capture:{orderId}:{payment.ReauthorizationCount}"` — deliberately
   includes the reauthorization count, so a genuine repeat of a call at the *same* auth state
   dedupes at PayPal, but the retry-after-reauthorize in step 4 below gets a **new** key (since
   the first capture attempt's key, tied to the pre-reauthorize count, would otherwise just
   replay PayPal's cached *failure* response instead of attempting the fresh capture).
3. Call `IPayPalPaymentGateway.CaptureAsync(authorizationId, order.Total(), currency, orderId.ToString(), key)`.
4. On success: `payment.RecordCapture(...)`, `order.MarkFulfilled()`, return the capture
   details including `grossAmount`/`paypalFee`/`netAmount` (exactly what the task asks the
   response to show).
5. On failure with PayPal issue `AUTHORIZATION_EXPIRED` (§1.5): call
   `IPayPalPaymentGateway.ReauthorizeAsync(authorizationId, order.Total(), currency)`.
   - Success: `payment.RecordReauthorization(...)`, retry the capture **once** with the new
     (reauthorization-count-incremented) idempotency key from step 2. If that retry also fails,
     surface it as a normal capture failure (don't loop indefinitely).
   - Failure: surface a distinct, operator-actionable error — include PayPal's own
     `details[].issue`/`description` text in the response (§1.5's guidance: don't invent a
     specific terminal error code, just forward what PayPal actually said) and leave `Order` in
     `PaymentAuthorized` (not `Fulfilled`, not `Cancelled` — an operator needs to decide the next
     step, e.g. cancel-and-ask-shopper-to-repay, which is a manual follow-up outside this
     endpoint's scope).
6. On any other capture failure (declined, etc.): surface it; `Order` stays
   `PaymentAuthorized`.

### 5.4 `POST /api/orders/{orderId}/cancel` — before fulfilment
1. Load `Order` (admin-only). Must be `Status` in `{AwaitingPayment, PaymentAuthorized}` —
   `Fulfilled`/`Cancelled`/`Refunded`/`PartiallyRefunded` → `409`.
2. If `PaymentAuthorized` and `AuthorizationStatus` is still live (`Created`/`Pending` — i.e.
   not already `Voided`/`Captured`): call `IPayPalPaymentGateway.VoidAsync(authorizationId)`,
   then `payment.RecordVoid()`. If `AwaitingPayment` (no `Payment` row / nothing was ever held),
   skip the PayPal call entirely — there's nothing to release.
3. `order.MarkCancelled()`.

### 5.5 `POST /api/orders/{orderId}/refunds` — after fulfilment, partial-safe, idempotent
Request: `{ amount: decimal?, idempotencyKey: string }` (`idempotencyKey` required, caller-supplied,
per the task — this is the one place the caller supplies the key rather than the server
deriving one, unlike §5.2/§5.3).

1. Load `Order` (**shopper-scoped, own order only** — §5's note above); must be `Status` in
   `{Fulfilled, PartiallyRefunded}` — anything else → `409`.
2. **Local idempotency check first**: look up `Refund` by `(PaymentId, IdempotencyKey)` (the
   unique index from §3.2). If found, return that refund's stored result — **do not** call
   PayPal again, regardless of PayPal's own 6-hour window, since this reference app's system of
   record is the local DB for the lifetime of the run.
3. Resolve the amount to refund: `request.Amount ?? (payment.CapturedGrossAmount - payment.RefundedAmount)`
   (full remaining balance if omitted). Validate
   `payment.RefundedAmount + amount <= payment.CapturedGrossAmount` locally (`400`/`409` if
   not) — this is the concrete mechanism behind "a partly-refunded order must never become
   refundable beyond what was captured," enforced before ever calling PayPal.
4. Call `IPayPalPaymentGateway.RefundAsync(captureId, amount, currency, idempotencyKey)` — the
   `PayPal-Request-Id` header carries the caller's own `idempotencyKey` value verbatim (§1.6).
5. On success: `payment.AddRefund(payPalRefundId, amount, status, idempotencyKey)` (updates
   `RefundedAmount`); if `RefundedAmount == CapturedGrossAmount` →
   `order.MarkRefunded(partial: false)`, else `order.MarkRefunded(partial: true)`
   (`PartiallyRefunded`). Response top-level `refundId` = the local `Refund.Id` (int) — kept
   consistent with `orderId`/`paymentMethodId` both being *this app's own* database identifiers
   rather than PayPal's; include PayPal's own refund id as a secondary field
   (e.g. `payPalRefundId`) in the response body for full traceability.

### 5.6 `POST /api/payment-methods`, `GET /api/payment-methods`, `DELETE /api/payment-methods/{id}`
- **POST**: find-or-create the caller's `Buyer` row (by `IdentityGuid`); compute
  `customer.id = $"eshop-buyer-{buyer.Id}"` (§3.4); call
  `IPayPalVaultGateway.SaveCardAsync(customerId, card)`; on success, `buyer.AddPaymentMethod(vaultId, last4, brand, expiry, alias)`,
  persist, respond with `{ paymentMethodId: <int>, brand, last4, expiry }` (never the PAN —
  it was never stored locally in the first place, only ever forwarded to PayPal over HTTPS and
  never logged: don't log request/response bodies for this endpoint, or if you do log PayPal
  client activity generally, redact the `card` object first).
- **GET**: return the caller's `Buyer.PaymentMethods` (local DB only — no PayPal call needed,
  §1.7) mapped to the same safe shape (id/brand/last4/expiry/alias), scoped to
  `IdentityGuid == User.Identity!.Name`.
- **DELETE**: load the caller's `PaymentMethod` by id + ownership (404 if missing/not owned).
  Delete the local row **first** (this alone already satisfies both required post-conditions —
  "no longer appears," because it's gone from the list query, and "no longer usable to pay,"
  because `POST /api/orders/{orderId}/pay`'s `paymentMethodId` lookup in §5.2 step 2 will no
  longer find it). Then best-effort call `IPayPalVaultGateway.DeletePaymentTokenAsync(vaultId)`
  for hygiene on PayPal's side — log a failure there but don't fail the API call over it; the
  correctness guarantee the task asks for is already satisfied by the local delete, and making
  the shopper-facing delete depend on PayPal's own availability would be a worse trade.

### 5.7 `GET /api/my-orders`
Project the caller's `Order`s (own `BuyerId` only) joined with their `Payment` (if any) into a
response DTO showing `orderId`, `status`, and payment summary (`authorizationStatus`,
`captureStatus`, amounts, refunds list) — this is the "payment state" the task asks for.
Straightforward read/projection endpoint; no PayPal calls needed, everything is already
persisted locally by the flows above.

### 5.8 `GET /api/reconciliation?from={iso}&to={iso}`
Admin-only. Parse `from`/`to` as `DateTimeOffset` (400 if unparseable or `from > to`). Call
`IPayPalReconciliationGateway.SearchTransactionsAsync(from, to)` (internally chunks into ≤31-day
windows and paginates fully within each, per §1.8 — this belongs inside the gateway
implementation, not the endpoint). Load local `Payment`s whose authorization/capture activity
falls in `[from, to]`. Build and return the three-bucket report from §1.8 (matched / PayPal-only
/ eShop-only), keyed on `invoice_id` == `Order.Id.ToString()`.

---

## 6. Error handling
Add new exceptions alongside the existing `src/ApplicationCore/Exceptions/*` (same minimal
style as `BasketNotFoundException`/`DuplicateException`), e.g.: `OrderNotFoundException`,
`InvalidOrderStateException`, `PaymentDeclinedException`, `PayPalApprovalRequiredException`
(the hard-stop case from §1.4/§5.2 step 5), `PaymentRenewalFailedException` (the
can't-reauthorize case from §5.3 step 5, carrying PayPal's own message), `RefundLimitExceededException`,
`PaymentMethodNotFoundException`. Extend `src/PublicApi/Middleware/ExceptionMiddleware.cs`'s
`HandleExceptionAsync` with a branch per new exception type mapping to the right status code
(404/409/402/422/502 as appropriate) instead of letting them all fall through to 500, following
the exact pattern already there for `DuplicateException`.

---

## 7. Suggested build order
1. Domain model (§2) + EF config/migration (§3) — compiles and passes existing tests standalone,
   no PayPal involved yet.
2. PayPal gateway interfaces + HTTP implementations + config wiring (§4) — unit-testable in
   isolation (mock `HttpClient` via `HttpMessageHandler`, or a thin fake) without needing live
   sandbox credentials yet.
3. `POST /api/orders` + `GET /api/my-orders` (§5.1, §5.7) — no PayPal dependency, verifies the
   new order-creation path end-to-end first.
4. `POST /api/orders/{orderId}/pay` (§5.2) against the real sandbox with the test Visa card —
   first real PayPal round trip.
5. `POST /api/payment-methods` / `GET` / `DELETE` (§5.6) — needed before finishing `/pay`'s
   saved-card branch can be exercised end-to-end.
6. `POST /api/orders/{orderId}/fulfil` (§5.3) including the reauthorize branch (this branch is
   hard to trigger naturally within a single short verification session, since it needs the
   3-day honor period to elapse — see §9 on testing it without waiting 3 real days).
7. `POST /api/orders/{orderId}/cancel` (§5.4) and `POST /api/orders/{orderId}/refunds` (§5.5).
8. `GET /api/reconciliation` (§5.8) last, once there's real captured activity in the sandbox
   account to query against.

---

## 8. Testing strategy
- **Domain-level unit tests** (`tests/UnitTests`, xUnit, existing pattern e.g.
  `ApplicationCore/Entities/OrderTests/OrderTotal.cs`): `Order` state-transition guards,
  `Payment`/`Refund` amount-validation behavior methods, the idempotency-key derivation logic —
  none of this needs PayPal or a database.
- **Gateway unit tests**: fake `HttpMessageHandler` returning canned JSON matching the exact
  shapes verified in §1 (including the `AUTHORIZATION_EXPIRED` 422 shape) to test the
  gateway implementations' parsing and branching (e.g. the reauthorize-on-expired retry logic in
  §5.3) without live network calls.
- **Endpoint integration tests** (`tests/PublicApiIntegrationTests`, MSTest +
  `WebApplicationFactory<Program>`, existing pattern e.g. `CreateCatalogItemEndpointTest.cs`,
  using `ApiTokenHelper` for admin vs. normal-user tokens): cover auth/ownership scoping (a
  normal user can't fulfil/cancel/reconcile → 403; a user can't see/act on another user's
  order/card → 404) without needing real PayPal calls, by substituting fake
  `IPayPalPaymentGateway`/`IPayPalVaultGateway`/`IPayPalReconciliationGateway` implementations
  in the test host's DI container (`WebApplicationFactory.WithWebHostBuilder(...).ConfigureServices(...)`).
- **Live sandbox verification** (manual or a small opt-in test category, guarded so it doesn't
  run/fail in environments without the real `PAYPAL_*` env vars set): the actual end-to-end
  proof the task's "Rules of engagement" asks the build session to produce — place an order,
  authorize with the test Visa card, fulfil (real capture), refund (real refund), and save a
  card + pay a second order with it. This is what the build session's final user-facing
  verification guide should walk through directly against the running `PublicApi` host, since
  that's simpler and more convincing than a hidden automated test for a one-off demo like this.
- **Testing the reauthorize branch without waiting 3 real days**: don't build a
  time-travel/clock-injection mechanism just for this — it's disproportionate to the task. Cover
  it with the gateway-level fake-`HttpMessageHandler` unit test (returns the
  `AUTHORIZATION_EXPIRED` shape, asserts the code calls reauthorize-then-retries-capture) as the
  primary proof; note in the final verification guide that the live sandbox path for this
  specific branch cannot be exercised within a short manual session because of PayPal's own
  3-day honor period, and that this is expected, not a gap.

---

## 9. Non-negotiables carried over from the task (do not relitigate these during the build)
- Full card details (PAN) are never persisted in eShop's own database and never logged —
  they only ever transit to PayPal over HTTPS as part of the authorize/vault request bodies.
- `PayPal:BaseUrl`, when set, overrides the base address for **every** PayPal call, the token
  endpoint included (§1.2).
- Secret values (`PAYPAL_CLIENT_ID`/`SECRET`) never get written into any file in the repo —
  only referenced by name (§4.3).
- If a direct card payment ever comes back asking for a shopper-facing approval/challenge
  (`PAYER_ACTION_REQUIRED` or equivalent), stop and report it — don't build a redirect flow
  (§1.4, §5.2).
- An empty reconciliation result for a very recent date range is an expected sandbox artifact
  (reporting lag), not a bug to chase (§1.8).
