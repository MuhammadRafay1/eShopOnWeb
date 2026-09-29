# Implementation Plan — PayPal Payments & Saved Cards for eShopOnWeb

This plan was produced by researching the live repository (architecture, conventions, existing
stubs) and PayPal's Payments API via the **paypal-docs** MCP server (Orders v2, Payments v2,
Vault v3, Transaction Search v1, and the shared auth/idempotency/error conventions). It resolves
every open design decision the task leaves to the implementer. The build session should follow
it directly; where it says "decide," that decision has already been made below — don't re-open it
unless something here turns out to be factually wrong against the live sandbox.

Everything below assumes the environment gotchas in the task brief (roll-forward SDK,
`UseOnlyInMemoryDatabase=true`, PublicApi-only verification, JWT bearer auth). Nothing here
requires Docker, a broker, or a different database engine.

---

## 1. Key design decisions (read this first)

These are the calls this plan makes on points the task deliberately left open, plus two points
where the task's own text needs a tie-breaker:

1. **Refunds are admin-only**, not shopper-initiated. The task's "Where it goes" section names
   only fulfil/cancel/reconciliation in its explicit admin-role bullet, but the Flow 1 narrative
   says "an operator then fulfils, cancels **or refunds** it" — and refund is a sensitive reversal
   of money already taken, exactly like fulfil/cancel. Treat the bullet list as non-exhaustive and
   restrict `POST /api/orders/{orderId}/refunds` to `Administrators`, same as fulfil/cancel/reconciliation.
2. **Env var → config key bridging.** The env vars are `PAYPAL_CLIENT_ID` etc. (single underscore).
   ASP.NET Core's built-in environment-variable configuration provider only maps `PayPal__ClientId`
   (double underscore) to `PayPal:ClientId` — it will **not** pick up `PAYPAL_CLIENT_ID`
   automatically. Don't rely on that provider or on a manual `dotnet user-secrets set` step (easy
   to forget, not automatic). Instead, in `PublicApi/Program.cs`, explicitly bridge the four env
   vars into the `PayPal:` section via `builder.Configuration.AddInMemoryCollection(...)` before
   `builder.Build()` (exact snippet in §4). This is the mechanism that makes "bind from `PayPal:`
   keys" and "credentials arrive as `PAYPAL_*` env vars" both literally true at once.
3. **Repurpose the existing dead `Buyer`/`PaymentMethod` stub** (`src/ApplicationCore/Entities/BuyerAggregate/`).
   It already has the right name and shape for saved cards but is unwired: no `DbSet`, no EF
   config, no migration, no public constructor, and unreferenced everywhere else. Detach
   `PaymentMethod` from `Buyer` and make it its own `IAggregateRoot` keyed directly by
   `string BuyerId` — mirroring how `Order`/`Basket` already key by the raw buyer-id string with no
   separate `Buyer` row. Delete `Buyer.cs` (fully dead once detached; leaving it around is a
   half-finished stub with no purpose). See §2.3.
4. **Single-step card authorization, with a defensive fallback.** PayPal's docs describe
   `PayPal-Request-Id` as "mandatory for all **single-step** create order calls (e.g. Create Order
   Request with payment source information like Card...)" — i.e., supplying `payment_source.card`
   (or `vault_id`) directly in `POST /v2/checkout/orders` is expected to authorize/capture inline,
   in the same call, with no buyer redirect. Implement it that way first (§3.2), but guard for the
   alternative (order comes back `APPROVED` with no authorization populated) by following up with
   `POST /v2/checkout/orders/{id}/authorize`. Both paths are implemented; which one actually fires
   will be obvious from the first real sandbox response.
5. **Stale-authorization detection uses documented fields, not guessed error codes.** The errors
   reference does not give an exact `issue` string for "authorization expired" on the capture
   endpoint. Rather than pattern-match an undocumented code, always `GET
   /v2/payments/authorizations/{id}` immediately before capturing and branch on the documented
   `status`/`expiration_time` fields (§3.4). This is more robust than string-matching and doesn't
   depend on a capability the docs don't actually specify.
6. **`refundId` in the refund response is eShop's own integer id** (consistent with `orderId` /
   `paymentMethodId` being local ids), with PayPal's own refund id included alongside as
   `payPalRefundId` for reconciliation cross-referencing.
7. **Ownership failures return 404, not 403**, for orders and payment methods a caller doesn't
   own — prevents confirming another buyer's resource exists at all.
8. **`POST /api/orders` shipping address is optional in the request** and defaults to a fixed
   placeholder address if omitted (mirroring `Web`'s own hardcoded checkout address) — the task
   only requires catalog item ids + quantities in the request body, and inventing a full shipping
   flow is out of scope.
9. **Automated tests stay hermetic**; the mandated "real sandbox authorization/capture/refund"
   verification is a separate, scripted, manual pass against the real PublicApi process and the
   real PayPal sandbox (§9), because credentials are per-run env vars, not CI fixtures, and the
   in-memory DB only survives one process lifetime anyway.

---

## 2. Domain model changes (ApplicationCore)

Keep `Order`'s existing constructor and fields untouched (`Web`'s checkout flow must keep
working unmodified) — only **add** to it.

### 2.1 `Order` — add status

New file `src/ApplicationCore/Entities/OrderAggregate/OrderStatus.cs`:
```csharp
public enum OrderStatus
{
    AwaitingPayment = 0,
    PaymentAuthorized = 1,
    Fulfilled = 2,
    Cancelled = 3,
    PartiallyRefunded = 4,
    Refunded = 5,
}
```

In `Order.cs`, add:
```csharp
public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;

public void MarkPaymentAuthorized()
{
    Guard.Against.InvalidInput(Status, nameof(Status), s => s == OrderStatus.AwaitingPayment,
        "Order must be awaiting payment to authorize.");
    Status = OrderStatus.PaymentAuthorized;
}

public void MarkFulfilled()
{
    Guard.Against.InvalidInput(Status, nameof(Status), s => s == OrderStatus.PaymentAuthorized,
        "Order must have an authorized payment to fulfil.");
    Status = OrderStatus.Fulfilled;
}

public void MarkCancelled()
{
    Guard.Against.InvalidInput(Status, nameof(Status),
        s => s is OrderStatus.AwaitingPayment or OrderStatus.PaymentAuthorized,
        "Only an unfulfilled order can be cancelled.");
    Status = OrderStatus.Cancelled;
}

public void MarkPartiallyRefunded()
{
    Guard.Against.InvalidInput(Status, nameof(Status),
        s => s is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded,
        "Only a fulfilled order can be refunded.");
    Status = OrderStatus.PartiallyRefunded;
}

public void MarkFullyRefunded()
{
    Guard.Against.InvalidInput(Status, nameof(Status),
        s => s is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded,
        "Only a fulfilled order can be refunded.");
    Status = OrderStatus.Refunded;
}
```
(`Ardalis.GuardClauses` is already a dependency of `ApplicationCore`; use whatever guard-clause
shape compiles cleanest — the point is the entity itself enforces the state machine, not the
endpoint layer, so it can never be bypassed by a future caller.)

`OrderConfiguration` (EF): add `builder.Property(o => o.Status).HasConversion<int>();`.

### 2.2 New `Payment` aggregate

New folder `src/ApplicationCore/Entities/PaymentAggregate/`.

`PaymentStatus.cs`:
```csharp
public enum PaymentStatus
{
    Authorized = 0,
    Captured = 1,
    Voided = 2,
    PartiallyRefunded = 3,
    Refunded = 4,
}
```

`Payment.cs` — `BaseEntity, IAggregateRoot`, one row per `Order` (unique `OrderId`):
```csharp
public class Payment : BaseEntity, IAggregateRoot
{
    public int OrderId { get; private set; }
    public string Currency { get; private set; }
    public decimal Amount { get; private set; }              // order total at authorize time
    public PaymentStatus Status { get; private set; }

    public string PayPalOrderId { get; private set; }
    public string PayPalAuthorizationId { get; private set; }
    public string AuthorizationStatus { get; private set; }   // raw PayPal status string
    public DateTimeOffset AuthorizationExpiresAt { get; private set; }
    public int ReauthorizationCount { get; private set; }

    public string? PayPalCaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }

    public decimal RefundedAmount { get; private set; }
    public string? LastOperatorError { get; private set; }    // actionable message, see §3.4

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    // ctor: Authorize(orderId, currency, amount, payPalOrderId, authorizationId, status, expiresAt)
    // RenewAuthorization(newAuthorizationId, status, expiresAt) => ReauthorizationCount++, clear LastOperatorError
    // RecordRenewalFailure(string message) => LastOperatorError = message
    // Capture(captureId, status, capturedAmount, fee, net) => Status = Captured, CapturedAt = now
    // Void() => Status = Voided
    // RecordRefund(Refund refund) => appends to _refunds, RefundedAmount += refund.Amount,
    //     Guard.Against amount so RefundedAmount never exceeds CapturedAmount,
    //     sets Status to PartiallyRefunded or Refunded based on RefundedAmount vs CapturedAmount
}
```

`Refund.cs` — child entity of `Payment` (not its own aggregate root):
```csharp
public class Refund : BaseEntity
{
    public int PaymentId { get; private set; }
    public string PayPalRefundId { get; private set; }
    public string IdempotencyKey { get; private set; }   // caller-supplied, see §3.5
    public decimal Amount { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
```

EF configuration (`PaymentConfiguration.cs`, `RefundConfiguration.cs` in `Infrastructure/Data/Config/`):
- `Payment`: `HasIndex(p => p.OrderId).IsUnique()`; `decimal(18,2)` on all money columns;
  `OwnsMany`/separate-table for `_refunds` via `HasMany(p => p.Refunds).WithOne()` +
  `Metadata.FindNavigation(nameof(Payment.Refunds))!.SetPropertyAccessMode(PropertyAccessMode.Field)`
  (same pattern `OrderConfiguration` already uses for `_orderItems`).
- `Refund`: `HasIndex(r => new { r.PaymentId, r.IdempotencyKey }).IsUnique()` — this is what makes
  "repeating a request under the same key must not refund twice" a database-enforced guarantee,
  not just an application-level check.

Add `DbSet<Payment> Payments` to `CatalogContext` (Refunds are reachable via `Payment.Refunds`,
no separate `DbSet` needed, same as `Order`/`OrderItem`).

### 2.3 `PaymentMethod` — detach and flesh out

Move `src/ApplicationCore/Entities/BuyerAggregate/PaymentMethod.cs` to
`src/ApplicationCore/Entities/PaymentMethodAggregate/PaymentMethod.cs`. Delete
`BuyerAggregate/Buyer.cs` and the now-empty `BuyerAggregate` folder — it has no other references
in the codebase (confirmed: not in any `DbSet`, `IEntityTypeConfiguration`, specification, or
migration).

```csharp
public class PaymentMethod : BaseEntity, IAggregateRoot
{
    public string BuyerId { get; private set; }        // same raw identity string as Order.BuyerId
    public string PayPalVaultId { get; private set; }  // PayPal Vault v3 payment-token id
    public string CardBrand { get; private set; }       // e.g. "VISA"
    public string Last4Digits { get; private set; }
    public string Expiry { get; private set; }           // "YYYY-MM", display-only
    public DateTimeOffset CreatedAt { get; private set; }

    public PaymentMethod(string buyerId, string payPalVaultId, string cardBrand,
        string last4Digits, string expiry)
    { /* guard non-empty, assign, CreatedAt = DateTimeOffset.UtcNow */ }
}
```
No `Alias`/`CardId` carryover from the old stub — those fields never had a way to be populated and
don't map to anything PayPal's Vault API returns. `PayPalVaultId` is the one field that matters:
it's what gets passed back to PayPal as `payment_source.card.vault_id` to pay with the saved card.

EF: `PaymentMethodConfiguration.cs`, `HasIndex(pm => pm.BuyerId)` (non-unique — one buyer can have
many saved cards). Add `DbSet<PaymentMethod> PaymentMethods` to `CatalogContext`.

### 2.4 New specifications (alongside the existing ones, e.g. next to `OrderWithItemsByIdSpec`)

- `OrdersByBuyerIdSpec(string buyerId)` — includes `OrderItems` and `Payment` (+ `Payment.Refunds`), for `GET /api/my-orders`.
- `OrderByIdAndBuyerIdSpec(int orderId, string buyerId)` — includes `OrderItems`/`Payment`, for shopper-scoped `pay`.
- `OrderWithPaymentByIdSpec(int orderId)` — no buyer filter, includes `Payment`, for admin fulfil/cancel/refund.
- `PaymentMethodsByBuyerIdSpec(string buyerId)`.
- `PaymentMethodByIdAndBuyerIdSpec(int id, string buyerId)` — ownership-scoped, used by pay-with-saved-card, list, and delete.

### 2.5 `IOrderService` — new creation path, no parallel model

Add to the existing `IOrderService`/`OrderService` (`ApplicationCore/Services/OrderService.cs`):
```csharp
Task<Order> CreatePendingOrderAsync(string buyerId, Address shippingAddress,
    IReadOnlyCollection<(int CatalogItemId, int Quantity)> items);
```
Implement by factoring the existing catalog-lookup + `OrderItem` construction out of
`CreateOrderAsync` into a private helper both methods share (`BuildOrderItemsAsync(catalogItemIds
+ quantities)`), reusing `CatalogItemsSpecification` exactly as `CreateOrderAsync` already does.
The new method skips the `Basket` entirely and constructs `Order` directly with
`OrderStatus.AwaitingPayment` (the default). This satisfies "reuses the app's existing
order/order-item model rather than a parallel one" literally — same `Order`/`OrderItem`/
`CatalogItemOrdered` types, same construction logic, just a different entry point that doesn't
require a pre-existing `Basket` row.

Guard: reject unknown `catalogItemId`s, non-positive quantities, and an empty item list (400/422
from the endpoint layer around this call).

---

## 3. PayPal API integration reference

Everything below was confirmed against the **paypal-docs** MCP server (Orders v2, Payments v2,
Vault v3, Transaction Search v1, and the shared guides). Treat this section as the cheat sheet;
if anything here conflicts with what the live sandbox actually returns, trust the sandbox and
adjust — but the shapes below are what the docs specify.

### 3.0 Base URL, auth, and shared conventions

- Token endpoint: `POST {baseUrl}/v1/oauth2/token`, HTTP Basic auth (`ClientId:ClientSecret`),
  body `grant_type=client_credentials`, response has `access_token` + `expires_in` (seconds).
  Cache the token and refresh ~60s before `expires_in` elapses; every other call sends
  `Authorization: Bearer <token>`.
- Base URL resolution (used for **every** call, including the token request):
  ```
  ResolveBaseUrl(options) =>
      !string.IsNullOrWhiteSpace(options.BaseUrl) ? options.BaseUrl
      : options.Environment is "Live" or "Production" (case-insensitive) ? "https://api-m.paypal.com"
      : "https://api-m.sandbox.paypal.com"
  ```
- All money values are strings, e.g. `"12.34"` — format with `amount.ToString("F2",
  CultureInfo.InvariantCulture)`.
- Idempotency: send `PayPal-Request-Id` on every POST that creates/moves money. Key retention
  varies (6h for order create/authorize, 45 days for capture/void/reauthorize/refund, 3h for
  vault setup/payment tokens) — irrelevant to correctness here since keys are derived
  deterministically per logical operation (§3.5), not reused across genuinely different
  operations.
- Send `Prefer: return=representation` on create/authorize/capture/reauthorize/void/refund calls
  so the full resource (not just id+status+links) comes back in one round trip.
- Error body shape: `{ name, message, debug_id, details: [{ issue, field, description }], links }`.
  Always log `debug_id`. Never surface `message`/`details` raw to a shopper-facing caller for
  authorize/capture failures (map to a generic "payment could not be authorized" for shoppers);
  for admin-facing endpoints (fulfil renewal failure) it's fine — and useful — to pass PayPal's
  own `message` through, since the caller is an operator who can act on it.
- Rate limiting: `429` → exponential backoff with jitter, retry only if the same
  `PayPal-Request-Id` was used (safe to retry idempotently). Not expected to matter at this
  integration's scale, but the HTTP client wrapper should not retry non-idempotent calls blindly.

### 3.1 Config → PayPal settings

`PayPalOptions` (`Infrastructure/PayPal/PayPalOptions.cs`):
```csharp
public class PayPalOptions
{
    public const string ConfigSection = "PayPal";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Environment { get; set; } = "Sandbox";
    public string Currency { get; set; } = "USD";
    public string? BaseUrl { get; set; }
}
```

### 3.2 Pay for an order — authorize (`POST /v2/checkout/orders`, intent=AUTHORIZE)

Request:
```json
{
  "intent": "AUTHORIZE",
  "purchase_units": [{
    "custom_id": "<eShop orderId>",
    "amount": { "currency_code": "<PayPal:Currency>", "value": "<order total, 2dp>" }
  }],
  "payment_source": {
    "card": {
      "name": "...", "number": "...", "expiry": "YYYY-MM", "security_code": "...",
      "billing_address": { "country_code": "...", "address_line_1": "...", "admin_area_2": "...",
                            "admin_area_1": "...", "postal_code": "..." }
    }
  }
}
```
— or, to pay with a saved card, replace the `card` object's raw fields with
`"card": { "vault_id": "<PaymentMethod.PayPalVaultId>" }`.

`custom_id` on the purchase unit is the join key for reconciliation (§8) — always set it to the
eShop `Order.Id` as a string; PayPal echoes it back as `transaction_info.custom_field` in
Transaction Search results and on the capture/refund resources too.

Headers: `PayPal-Request-Id: paypal-auth-{orderId}`, `Prefer: return=representation`.

Per §1.4, first try parsing `purchase_units[0].payments.authorizations[0]` directly out of this
response (single-step path). If it's absent and `status == "APPROVED"`, fall back to
`POST /v2/checkout/orders/{id}/authorize` (same body not needed — payment_source was already on
the order) with `PayPal-Request-Id: paypal-auth-{orderId}-confirm` and `Prefer:
return=representation`, then read the authorization from that response instead.

**Challenge/redirect guard (task-mandated STOP condition):** after creating the order, if the
response's `links[]` contains a `rel` suggesting payer action (e.g. containing `"approve"` or
`"payer-action"`), or if a setup-token/order status ever comes back as
`PAYER_ACTION_REQUIRED`, **stop implementation and report this immediately** rather than
building a redirect/approval flow — this is the exact scenario the task says to halt on. Add an
explicit check for this in `PayPalClient` (throw a distinct `PayPalPayerActionRequiredException`)
so it can't be silently swallowed by generic error handling.

Persist on `Payment`: `PayPalOrderId` = order id from the response, `PayPalAuthorizationId`,
`AuthorizationStatus` (should be `CREATED`), `AuthorizationExpiresAt` = `expiration_time`,
`Amount`/`Currency` = the order total sent. Call `Order.MarkPaymentAuthorized()`.

**App-level idempotency guard (before calling PayPal at all):** if a `Payment` row already exists
for this order with `Status >= Authorized`, short-circuit — return the existing payment state
with **409 Conflict** (not 200 — this makes "nothing new happened" explicit to the caller) rather
than calling PayPal again. This plus the deterministic `PayPal-Request-Id` gives two independent
layers against a double-click ever authorizing twice.

### 3.3 Save a card — Vault v3 setup-token → payment-token exchange

1. `POST /v3/vault/setup-tokens`:
   ```json
   { "payment_source": { "card": {
       "name": "...", "number": "...", "expiry": "YYYY-MM", "security_code": "...",
       "billing_address": { ... },
       "verification_method": "SCA_WHEN_REQUIRED"
   } } }
   ```
   No `experience_context.return_url`/`cancel_url` — those are only required for contingency
   flows (wallet, 3DS challenge); omitting them is correct for a pure server-side card flow, and
   is also exactly what makes a would-be challenge visible: if PayPal ever needs one, the response
   will surface `status: "PAYER_ACTION_REQUIRED"` (checked explicitly — same STOP condition as §3.2).
   Check the response `status`; proceed only on anything other than `PAYER_ACTION_REQUIRED`.
2. `POST /v3/vault/payment-tokens`:
   ```json
   { "payment_source": { "token": { "id": "<setup token id>", "type": "SETUP_TOKEN" } } }
   ```
   Response gives the durable `id` (the vault id to store) plus
   `payment_source.card.brand`/`last_digits`/`expiry` for display.
3. Persist `PaymentMethod(buyerId, id, brand, last_digits, expiry)`. Response to the caller:
   `{ paymentMethodId, brand, last4, expiry }` — never the card number.

`PayPal-Request-Id` on both calls (fresh GUID per call is fine — each save-card action is a
distinct, non-retried-by-key operation from the app's perspective; the header still protects
against a raw network-level retry of the same HTTP request).

### 3.4 Fulfil (capture) — with mandatory renewal path

1. Load `Order` + `Payment` (`OrderWithPaymentByIdSpec`). Require `Order.Status ==
   PaymentAuthorized` (else 409).
2. **Always** `GET /v2/payments/authorizations/{authorizationId}` first (documented fields, not
   guessed error codes — see §1.5). Branch on the response:
   - `status != "CREATED"` (e.g. `DENIED`, `VOIDED`) → hard failure, 409, with PayPal's own
     `status`/`status_details.reason` surfaced to the operator; no renewal possible.
   - `status == "CREATED"` and `DateTimeOffset.UtcNow < expiration_time` → proceed straight to
     capture (step 3).
   - `status == "CREATED"` and `DateTimeOffset.UtcNow >= expiration_time` → **renew first**:
     `POST /v2/payments/authorizations/{id}/reauthorize` with `{ "amount": { currency_code,
     value: <same order total> } }`, `PayPal-Request-Id:
     paypal-reauth-{orderId}-{Payment.ReauthorizationCount}` (computed from the count *before*
     incrementing, so a client-side retry of the same fulfil call reuses the same key). On
     success: `Payment.RenewAuthorization(newAuthId, newStatus, newExpiry)`, then proceed to
     capture using the **new** authorization id. On failure (e.g. more than the ~30-day total
     authorization window has elapsed and PayPal rejects the reauthorize): call
     `Payment.RecordRenewalFailure(paypalError.message)` and return **409 Conflict** with an
     explicit, actionable body:
     ```json
     { "code": "AUTHORIZATION_EXPIRED_UNRENEWABLE",
       "message": "<PayPal's own message>",
       "guidance": "This hold can no longer be renewed. The shopper must pay again via POST /api/orders/{orderId}/pay before this order can be fulfilled." }
     ```
     Leave `Order.Status` as `PaymentAuthorized` (don't invent a new terminal status for this —
     it's a recoverable-by-repayment condition, not a new order state) but keep
     `Payment.LastOperatorError` populated so it's visible later via `GET /api/my-orders` too,
     not just in this one response.
3. `POST /v2/payments/authorizations/{authorizationId}/capture` with `{ "final_capture": true }`
   (this integration never does partial/multi-capture — one authorization, one capture),
   `PayPal-Request-Id: paypal-capture-{orderId}`, `Prefer: return=representation`.
4. On success, persist from the response: `PayPalCaptureId = id`, `CaptureStatus = status`,
   `CapturedAmount = amount.value`, `PayPalFeeAmount =
   seller_receivable_breakdown.paypal_fee.value`, `NetAmount =
   seller_receivable_breakdown.net_amount.value`, `CapturedAt = now`. Call `Payment.Capture(...)`
   and `Order.MarkFulfilled()`. Clear `LastOperatorError` on success.

**App-level idempotency guard:** if `Order.Status` is already `Fulfilled` (or later), short-circuit
and return the existing capture state (409, same rationale as §3.2) rather than re-capturing.

### 3.5 Cancel — void before fulfilment

Admin-only. Require `Order.Status` in `{AwaitingPayment, PaymentAuthorized}` (else 409 — can't
cancel a fulfilled/refunded/already-cancelled order).
- If `AwaitingPayment`: no PayPal call needed (nothing was ever held) — just `Order.MarkCancelled()`.
- If `PaymentAuthorized`: `POST /v2/payments/authorizations/{id}/void`, `PayPal-Request-Id:
  paypal-void-{orderId}`. On success (`200`/`204`), `Payment.Void()` and `Order.MarkCancelled()`.

### 3.6 Refunds — full or partial, caller-supplied idempotency key

Admin-only (§1.1). Request body: `{ "amount": decimal? (null = full remaining), "idempotencyKey":
string (required) }`.

1. Require `Order.Status` in `{Fulfilled, PartiallyRefunded}` (else 409).
2. **Idempotency check first, before any PayPal call:** look up an existing `Refund` row for this
   `Payment.Id` + the supplied `idempotencyKey` (the unique index from §2.2 backs this). If found,
   return its stored result as-is — no new PayPal call, and this is what makes "repeating a
   request under the same key must not refund twice" hold even under concurrent duplicate
   requests (the DB unique constraint is the real guarantee; this lookup is the fast path).
3. Validate the requested amount (if given) doesn't exceed `Payment.CapturedAmount -
   Payment.RefundedAmount` — 422 if it does. This is enforced again by PayPal itself, but failing
   fast avoids an unnecessary API call and gives a clearer error.
4. `POST /v2/payments/captures/{captureId}/refund` — body `{}` for a full refund of the remaining
   amount, or `{ "amount": { currency_code, value } }` for a partial one. `PayPal-Request-Id:
   paypal-refund-{orderId}-{idempotencyKey}` (deterministic from the caller's own key, so a raw
   network retry of the identical HTTP request is also covered).
5. On success, insert a `Refund` row (`PayPalRefundId = id`, `Amount = amount.value`, `Status =
   status`, `IdempotencyKey`, `CreatedAt = now`). Call `Payment.RecordRefund(refund)`, which
   updates `RefundedAmount` and enforces it never exceeds `CapturedAmount` — this is the
   guarantee that "a partly-refunded order must never become refundable beyond what was
   captured," enforced in the domain entity, not just by trusting the request validation in step 3.
6. Call `Order.MarkPartiallyRefunded()` or `Order.MarkFullyRefunded()` depending on whether
   `RefundedAmount < CapturedAmount` after this refund.
7. Response: `{ refundId: <local Refund.Id>, payPalRefundId, orderId, amount, status,
   totalRefunded: Payment.RefundedAmount, orderStatus }`.

### 3.7 Reconciliation — Transaction Search v1

`GET /v1/reporting/transactions?start_date=...&end_date=...&fields=all&page=N&page_size=100&total_required=true`.

Two chunking dimensions, both required because the task says "covers the whole range, not just
the first page":
1. **Date-range chunking**: max supported window is 31 days per call. If `to - from > 31 days`,
   split into consecutive ≤31-day windows and issue one search per window.
2. **Pagination within each window**: start at `page=1`, read `total_pages` from the first
   response (`total_required=true`), keep incrementing `page` until `page > total_pages`.

Match by `transaction_info.custom_field` (== the eShop `Order.Id` set as `custom_id` at
authorize/capture time, §3.2) and cross-check `transaction_info.transaction_amount.value`.

Build three buckets:
- **Matched** — local `Payment`/`Refund` rows in `[from,to]` with a corresponding PayPal
  transaction found by `custom_field`.
- **OnlyInEShop** — local rows in range with no matching PayPal transaction (informational; may
  just mean PayPal hasn't reported it yet — transaction search lags live activity per the docs,
  so this isn't necessarily an error).
- **OnlyInPayPal** — PayPal transactions in range whose `custom_field` doesn't match any local
  `Order.Id` (or is empty/unparseable) — this is the more actionable direction, since it means
  PayPal recorded money movement eShop has no record of.

Response: `{ from, to, matched: [...], onlyInEShop: [...], onlyInPayPal: [...] }`.

An empty result for a freshly-created range is expected (sandbox reporting lag) — do not treat
that as a bug or a missing capability; the report is "correct" if it's structurally right and
would surface data for a range that has settled.

---

## 4. Configuration & secrets

In `PublicApi/Program.cs`, **before** `builder.Build()`:
```csharp
var payPalEnvOverrides = new Dictionary<string, string?>
{
    ["PayPal:ClientId"] = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"),
    ["PayPal:ClientSecret"] = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET"),
    ["PayPal:Environment"] = Environment.GetEnvironmentVariable("PAYPAL_ENVIRONMENT"),
    ["PayPal:Currency"] = Environment.GetEnvironmentVariable("PAYPAL_CURRENCY"),
};
builder.Configuration.AddInMemoryCollection(
    payPalEnvOverrides.Where(kv => kv.Value is not null)!);

builder.Services.Configure<PayPalOptions>(
    builder.Configuration.GetRequiredSection(PayPalOptions.ConfigSection));
```
This is the mechanism (see §1.2) that makes the `PAYPAL_*` env vars land in the `PayPal:*` config
keys reliably, without depending on a manual `dotnet user-secrets set` step. `PayPal:BaseUrl` has
no corresponding env var in this task — leave it unset by default (sandbox is derived from
`PayPal:Environment`); it only needs to be supplied via `appsettings`/user-secrets if a future
run targets a different PayPal account with a non-standard base address.

Do **not** write any credential value into `appsettings*.json`, a launch profile, a test fixture,
or a commit — only the config **key names** (`PayPal:ClientId` etc.) belong in source. Optionally
add a `<UserSecretsId>` to `PublicApi.csproj` for local developer convenience (BaseUrl overrides,
etc.), but the working mechanism for *this* task's credentials is the in-memory bridge above,
which requires no manual setup step and works the same way every time the process starts.

`Infrastructure` project registers the typed client:
```csharp
builder.Services.AddHttpClient<IPayPalClient, PayPalClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;
    client.BaseAddress = new Uri(PayPalUrlResolver.Resolve(options));
});
builder.Services.AddSingleton<IPayPalTokenProvider, PayPalTokenProvider>();
```
(`AddHttpClient<TClient, TImplementation>` service in `Infrastructure/PayPal/`; call this from
`PublicApi/Program.cs` alongside the existing `Infrastructure.Dependencies.ConfigureServices(...)`
call, or add it to that same method — either is fine, just keep it colocated with the rest of the
service registration rather than scattered.)

---

## 5. Infrastructure layer — PayPal client

New folder `src/Infrastructure/PayPal/`:
- `PayPalOptions.cs` (§3.1)
- `PayPalUrlResolver.cs` — the `Resolve(options)` static helper from §3.0.
- `PayPalTokenProvider.cs` — `IPayPalTokenProvider` (singleton), caches `(token, expiresAtUtc)`
  behind a `SemaphoreSlim` so concurrent requests don't all re-authenticate at once; refreshes
  ~60s before expiry.
- `PayPalClient.cs` — implements `IPayPalClient` (interface lives in `ApplicationCore/Interfaces/`
  so `PaymentService`/`PaymentMethodService`/`ReconciliationService` in ApplicationCore can depend
  on the abstraction, matching the existing `IRepository`/`EfRepository` split). Methods, one per
  PayPal call in §3: `CreateCardAuthorizationAsync`, `AuthorizeOrderAsync` (the §3.2 fallback),
  `GetAuthorizationAsync`, `ReauthorizeAsync`, `CaptureAuthorizedPaymentAsync`,
  `VoidAuthorizationAsync`, `RefundCaptureAsync`, `CreateSetupTokenAsync`,
  `CreatePaymentTokenAsync`, `DeletePaymentTokenAsync`, `SearchTransactionsAsync`.
- `Models/` — wire DTOs with `System.Text.Json` + `[JsonPropertyName("snake_case")]` matching the
  request/response shapes documented in §3 (e.g. `PayPalMoney`, `PayPalOrderResponse`,
  `PayPalAuthorization`, `PayPalCapture`, `PayPalRefund`, `PayPalSetupToken`,
  `PayPalPaymentToken`, `PayPalTransactionSearchResponse`, `PayPalErrorResponse`).
- `PayPalApiException.cs` — thrown on non-2xx, carries `name`/`message`/`debug_id`/`details`.
- `PayPalPayerActionRequiredException.cs` — thrown specifically for the §3.2/§3.3 challenge-guard
  condition; let this propagate up as a distinct, loud failure (500 with a clear log message)
  rather than being caught by generic error handling, since per the task this scenario means
  **stop and report**, not degrade gracefully.
- `PayPalMoneyFormatter.cs` — the single place that formats `decimal` → PayPal's 2dp string, so
  every call site is consistent.

**PCI/logging safety:** do not add verbose request/response body logging for `PayPalClient`'s
`HttpClient` (no `HttpClientLogging` body-capture handler). If any diagnostic logging is added
later, it must redact `payment_source.card.number` and `payment_source.card.security_code`
before writing anything — raw card data must never reach a log sink. Card details passed into
`IPayPalClient` methods exist only as local variables for the duration of the outbound call; they
are never persisted and never echoed back in any response or error.

---

## 6. Application layer services (ApplicationCore)

Alongside the existing `OrderService`, add:
- `IPaymentService` / `PaymentService` — orchestrates §3.2 (pay), §3.4 (fulfil), §3.5 (cancel),
  §3.6 (refund). Depends on `IRepository<Order>`, `IRepository<Payment>`, `IRepository<PaymentMethod>`
  (for the pay-with-saved-card lookup), `IPayPalClient`.
- `IPaymentMethodService` / `PaymentMethodService` — §3.3 (save card) and delete-card. Depends on
  `IRepository<PaymentMethod>`, `IPayPalClient`.
- `IReconciliationService` / `ReconciliationService` — §3.7. Depends on `IReadRepository<Payment>`,
  `IPayPalClient`.

Endpoints stay thin: parse/validate the request, resolve the caller's identity from
`ClaimTypes.Name` (the only identity claim the JWT carries — confirmed from
`IdentityTokenClaimService`; there is no separate user-id claim, so `BuyerId` is the username
string everywhere, consistent with how `Basket`/`Order` already key on it), call the service,
map the result to the response DTO, map thrown exceptions to HTTP status codes.

---

## 7. PublicApi endpoints

Use the `MinimalApi.Endpoint` (`IEndpoint<...>`) pattern for all new endpoints — it's the pattern
the existing **admin-restricted** example (`CreateCatalogItemEndpoint`) already uses, with
`[Authorize(Roles = ..., AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` applied
directly on the minimal-API route delegate. (`Ardalis.ApiEndpoints`, the other pattern in this
project, is only used by the single `AuthenticateEndpoint` — standardize on `MinimalApi.Endpoint`
for everything new, for consistency.) Constructor-inject long-lived services (the new
`IPaymentService` etc.); minimal-API-inject per-request repositories the same way
`CreateCatalogItemEndpoint` does.

New folders under `src/PublicApi/`:

| Folder | Endpoint | Route | Auth | Notes |
|---|---|---|---|---|
| `OrderEndpoints/` | `CreateOrderEndpoint` | `POST /api/orders` | any authenticated user | body: `{ items: [{catalogItemId, quantity}], shippingAddress? }` → `{ orderId, status, total, currency, items }` |
| `OrderEndpoints/` | `PayOrderEndpoint` | `POST /api/orders/{orderId}/pay` | owner only (404 if not owner) | body: `{ card?: {...} }` **xor** `{ paymentMethodId?: int }` — 422 if both/neither |
| `OrderEndpoints/` | `FulfilOrderEndpoint` | `POST /api/orders/{orderId}/fulfil` | `Administrators` | §3.4 |
| `OrderEndpoints/` | `CancelOrderEndpoint` | `POST /api/orders/{orderId}/cancel` | `Administrators` | §3.5 |
| `OrderEndpoints/` | `RefundOrderEndpoint` | `POST /api/orders/{orderId}/refunds` | `Administrators` | §1.1, §3.6; body requires `idempotencyKey` |
| `OrderEndpoints/` | `MyOrdersEndpoint` | `GET /api/my-orders` | any authenticated user | caller's orders + payment summary only |
| `ReconciliationEndpoints/` | `ReconciliationEndpoint` | `GET /api/reconciliation?from=&to=` | `Administrators` | §3.7; 400 if `to <= from` or either isn't valid ISO-8601 |
| `PaymentMethodEndpoints/` | `CreatePaymentMethodEndpoint` | `POST /api/payment-methods` | any authenticated user | body: card details → `{ paymentMethodId, brand, last4, expiry }` |
| `PaymentMethodEndpoints/` | `ListPaymentMethodsEndpoint` | `GET /api/payment-methods` | any authenticated user | caller's own only |
| `PaymentMethodEndpoints/` | `DeletePaymentMethodEndpoint` | `DELETE /api/payment-methods/{paymentMethodId}` | owner only (404 if not owner) | 204 on success |

HTTP status conventions across all of these:
- `400`/`422` — malformed input or a business-rule violation caught before touching PayPal
  (unknown catalog item, exactly-one-of card/paymentMethodId violated, over-refund amount, bad
  date range).
- `401` — no/invalid JWT (existing pipeline, unchanged).
- `403` — non-admin hitting an `Administrators`-only route (existing pipeline, unchanged).
- `404` — order/payment-method not found, or found but not owned by the caller.
- `409` — right resource, wrong state for the requested action (pay an already-authorized order,
  fulfil a non-authorized order, cancel a fulfilled order, refund a non-fulfilled order, and the
  two "already handled, here's the current state" idempotency short-circuits from §3.2/§3.4).
- `502` — PayPal itself returned an unmapped error; log `debug_id`, return a safe generic message
  to shopper-facing callers, PayPal's own `message` to admin-facing callers (fulfil renewal
  failure is the one case that's deliberately a `409` with detail, not a `502`, since it's a
  documented, expected condition rather than an unexpected upstream failure).

Swagger: annotate the same way existing endpoints do (`[SwaggerOperation]`/`.Produces<T>()`/
`.WithTags(...)`) so the new surface shows up in the existing Swagger UI without extra wiring.

---

## 8. Idempotency strategy — summary table

| Operation | App-level guard | PayPal-Request-Id |
|---|---|---|
| Authorize (`pay`) | short-circuit if `Payment` already `Authorized`+ | `paypal-auth-{orderId}` |
| Authorize fallback confirm | n/a (same call) | `paypal-auth-{orderId}-confirm` |
| Reauthorize | derived from `Payment.ReauthorizationCount` (pre-increment) | `paypal-reauth-{orderId}-{count}` |
| Capture (`fulfil`) | short-circuit if `Order.Status >= Fulfilled` | `paypal-capture-{orderId}` |
| Void (`cancel`) | short-circuit if `Payment.Status == Voided` | `paypal-void-{orderId}` |
| Refund | DB unique index on `(PaymentId, IdempotencyKey)` + pre-check lookup | `paypal-refund-{orderId}-{callerIdempotencyKey}` |
| Save card | none needed (each call is a distinct new action) | fresh GUID per call |

Every deterministic key doubles as the app-level short-circuit's backstop: even if the local DB
write after a successful PayPal call somehow fails and a client retries, PayPal itself returns
the original result for the same key rather than performing the action twice.

---

## 9. Persistence / migration

Add one EF Core migration covering: `Order.Status` column, new `Payments` table (+ `Refunds`
table), new `PaymentMethods` table, removal of the never-migrated `Buyer`/`PaymentMethod` stub
(there is no existing migration for it to remove — it was never added to a `DbSet`, so there's
nothing to drop, just don't reintroduce it). `dotnet ef migrations add
AddPaymentsAndPaymentMethods --project src/Infrastructure --startup-project src/PublicApi` only
needs the compiled model, not a live DB connection, so it works fine even though there's no
LocalDB in this environment.

Note explicitly for the build session: with `UseOnlyInMemoryDatabase=true` (required in this
environment, per the task's own gotchas), **migrations are never applied** — the in-memory
provider builds its schema from the live model directly. The migration still needs to exist for
a real SQL Server deployment to be correct, but don't expect it to do anything observable during
this session's own verification pass; `EnsureCreated`/the in-memory provider's own bootstrapping
is what actually matters here, and that already happens automatically from the updated model.

---

## 10. Testing

**Unit tests** (`tests/UnitTests`, xUnit, mirrors existing `OrderTests`/`BasketTests` style):
- `Order` state-machine guards (can't fulfil `AwaitingPayment`, can't cancel `Fulfilled`, etc.).
- `Payment.RecordRefund` over-refund guard.
- `PayPalClient` request/response shaping — use a fake `HttpMessageHandler` (standard technique
  for testing typed `HttpClient`s: subclass `DelegatingHandler`/`HttpMessageHandler`, override
  `SendAsync` to assert on the outgoing `HttpRequestMessage` and return a canned
  `HttpResponseMessage`) rather than trying to mock `HttpClient` directly with NSubstitute. This
  verifies headers (`PayPal-Request-Id`, `Authorization`, `Prefer`) and body shape without any
  network access.

**Integration tests** (`tests/PublicApiIntegrationTests`, MSTest, `WebApplicationFactory<Program>`,
mirrors `CreateCatalogItemEndpointTest.cs`): register a **fake** `IPayPalClient` via
`WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Scoped<IPayPalClient,
FakePayPalClient>())))` returning canned successful/failure responses, and `ApiTokenHelper`'s
existing `GetAdminUserToken()`/`GetNormalUserToken()` for role checks. Cover: 403 for non-admin on
fulfil/cancel/refund/reconciliation; 404 for cross-buyer order/payment-method access; the 409
idempotency short-circuits; the refund unique-index behavior (same idempotency key twice → same
result, not two refunds). Keep this suite hermetic — no real network calls to PayPal sandbox from
the automated suite, since credentials are per-run env vars, not a CI fixture.

**Manual sandbox self-verification** (§11) is the mandated "real authorization, real capture, real
refund, saved card reused" pass — do this once, by hand/script, against the actually-running
PublicApi process and the real PayPal sandbox, as a separate step from the automated test suite.

---

## 11. Self-verification sequence (for the build session to run once, live)

Everything must happen within a **single continuous run** of PublicApi (in-memory DB resets on
restart). Suggested order, all via HTTP against the running PublicApi:

1. `POST /api/authenticate` as a normal user → bearer token. Also authenticate as an admin user
   (seeded identity user in `Administrators` role — check `Infrastructure`'s identity seed data
   for an existing admin login rather than creating a new one) → admin bearer token.
2. `POST /api/orders` (normal user token) with 1-2 real catalog item ids/quantities → note `orderId`.
3. `POST /api/payment-methods` (same user) with the sandbox Visa `4111 1111 1111 1111`, any future
   expiry/CVC/name/address → note `paymentMethodId`. Confirm the response has no card number.
4. `POST /api/orders/{orderId}/pay` with `{ "paymentMethodId": <id> }` → confirm `201`/`200` with a
   PayPal authorization id and status `CREATED`.
5. `GET /api/my-orders` (same user) → confirm the order shows `PaymentAuthorized`.
6. `POST /api/orders/{orderId}/fulfil` (admin token) → confirm captured amount, PayPal fee, net
   amount all present and captured amount equals the order total.
7. `POST /api/orders/{orderId}/refunds` (admin token) with a partial amount + an `idempotencyKey`
   → confirm refund succeeds, `orderStatus` becomes `PartiallyRefunded`. Repeat the **exact same**
   request (same `idempotencyKey`) → confirm it returns the same refund, not a second one.
8. Create a **second** order (step 2 again), `pay` it using the **same saved `paymentMethodId`**
   from step 3 → confirms the saved card is genuinely reusable across orders.
9. Create a **third** order, do not pay it, `POST /api/orders/{orderId}/cancel` (admin) → confirm
   `Cancelled` with no PayPal call needed (or pay it first, then cancel, to also exercise the void
   path — do both variants if time allows).
10. `DELETE /api/payment-methods/{paymentMethodId}` (the one from step 3) → confirm `204`, then
    `GET /api/payment-methods` → confirm it's gone, and confirm a subsequent `pay` attempt
    referencing that id now 404s.
11. `GET /api/reconciliation?from=<a wide recent range>&to=<now>` (admin) → confirm the response
    is well-formed (even if `matched`/`onlyInPayPal` come back empty due to sandbox reporting lag —
    that's expected, not a failure; note it as such rather than treating it as broken).

If step 4 or step 3 ever surfaces a payer-action/redirect requirement (§3.2/§3.3's STOP
condition), halt and report it — do not attempt to work around it with a browser step.

The build session's own final deliverable to the user is a concise version of the above, phrased
as copy-pasteable `curl`/PowerShell commands with real endpoint paths and ports from its actual
run — that write-up belongs in the build session's final report, not in this plan.

---

## 12. Explicitly out of scope

- No changes to `Web`, `BlazorAdmin`, or any storefront UI — the task requires none, and the
  existing checkout flow (`OrderService.CreateOrderAsync` from a `Basket`) is left untouched.
- No webhook receiver — all status reads are synchronous PayPal GETs, sufficient for this task's
  flows and avoiding a public callback endpoint this environment can't expose anyway.
- No multi-currency support — `PayPal:Currency` is a single configured currency for the whole
  app, matching "the currency comes from configuration."
- No partial/multi-capture — one authorization per order always ends in exactly one
  `final_capture: true` capture; only refunds are partial.
- No new Docker/broker/database dependency, per the task's explicit constraint.
