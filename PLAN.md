# PLAN — PayPal payments and saved cards for eShopOnWeb

This is a build blueprint for a later session. It is concrete about contracts, file layout,
field names, and sequencing so the build session can execute with minimal re-discovery. It
does not contain secret values — only configuration **key names**.

Everything below was verified directly against the JSON files in `api-specs/paypal/` (not
memory of PayPal's public docs). Paths, schema names and enum values quoted here are exact.

---

## 1. Spec-to-capability mapping (read this first)

`api-specs/paypal/` contains five OpenAPI documents. All five are used except one:

| Spec file | Title | Used for |
|---|---|---|
| `checkout_orders_v2/checkout_orders_v2.json` | Orders v2 | Create the PayPal order and **authorize** it (the hold) |
| `payments_payment_v2/payments_payment_v2.json` | Payments v2 | **Capture** an authorization (fulfil), **reauthorize** a stale authorization, **void** an authorization (cancel), **refund** a capture |
| `vault_payment_tokens_v3/vault_payment_tokens_v3.json` | Payment Method Tokens v3 | Save a card (vault), list/delete saved cards, reuse a saved card to pay |
| `transaction_search_v1/transaction_search_v1.json` | Transaction Search v1 | Reconciliation report |
| `billing_subscriptions_v1/billing_subscriptions_v1.json` | Subscriptions v1 | **Not used.** This task is one-off order payments, not recurring billing. No endpoint in this integration needs it. Do not wire it up. |

All five specs declare the same `Oauth2` security scheme (`clientCredentials`, `tokenUrl:
"/v1/oauth2/token"`) and the same sandbox server (`https://api-m.sandbox.paypal.com`). The
token endpoint is therefore part of the authoritative contract (it's declared inside the
specs' own `securitySchemes`), not an invented endpoint — resolve it against the same base
URL as every other call (see §5).

No genuine spec gap was found for anything this task requires. Every operation below maps to
a documented path. The one thing to watch at runtime, per the task's own instruction, is order
`status == PAYER_ACTION_REQUIRED` (3-D Secure / buyer redirect) — the sandbox test card
`4111 1111 1111 1111` should not trigger it, but if it appears, **stop and report it**; do not
build a redirect/approval round-trip.

---

## 2. PayPal flow design (the part that took the most judgment)

### 2.1 Pay (authorize): `checkout_orders_v2`

`order_request.intent` is an enum of exactly `CAPTURE` | `AUTHORIZE`
(`components/schemas/checkout_payment_intent`). Use **`AUTHORIZE`** — that's what "hold now,
take later" means in this API.

Sequence for `POST /api/orders/{orderId}/pay`:

1. `POST /v2/checkout/orders` with `intent=AUTHORIZE`, one `purchase_units[0]` whose
   `amount.value`/`amount.currency_code` equals the order total in `PayPal:Currency`, and
   `payment_source.card` populated either from the raw card in the request or from
   `vault_id` (see §2.3) when a saved card is named. Set
   `purchase_units[0].invoice_id = "eshop-order-{orderId}"` and
   `purchase_units[0].custom_id = "{orderId}"` — both are echoed back later and are how
   reconciliation (§2.5) correlates a PayPal transaction to an eShop order.
   Send header `PayPal-Request-Id: order-create-{orderId}` so a retried click doesn't create
   two PayPal orders for the same eShop order.
2. Per the spec's description of `/authorize`: *"the buyer must first approve the order **or
   a valid payment_source must be provided in the request**"* — because step 1 already
   supplied `payment_source.card`, no buyer redirect/approval is needed. Call
   `POST /v2/checkout/orders/{id}/authorize` with header
   `PayPal-Request-Id: order-authorize-{orderId}`. This is the call that actually places the
   hold; its response's `purchase_units[0].payments.authorizations[0]` carries the
   authorization `id`, `status` (`authorization_status` enum:
   `CREATED|CAPTURED|DENIED|PARTIALLY_CAPTURED|VOIDED|PENDING`), `amount`, and
   `expiration_time`.
3. Before making either call, check the order's local state: if it is already past
   `AwaitingPayment` (see §3.1), don't call PayPal again — return the existing
   authorization result. Combined with the deterministic `PayPal-Request-Id` values above,
   this makes `/pay` idempotent in effect even under a concurrent double-click.
4. If the created order's `status` is `PAYER_ACTION_REQUIRED` (a `payer-action` HATEOAS link
   is present), **stop and report it** per the task rules — do not implement a redirect flow.

`card_request` (`components/schemas/card_request` in `checkout_orders_v2.json`) is the exact
shape for raw card input: `name`, `number`, `expiry` (`YYYY-MM`), `security_code`,
`billing_address`, `vault_id`. Map the request DTO's card fields onto this 1:1 — don't invent
extra fields.

### 2.2 Fulfil (capture): `payments_payment_v2`, with reauthorization fallback

`POST /api/orders/{orderId}/fulfil` (admin only):

1. Load the order's stored PayPal authorization id.
2. Call `POST /v2/payments/authorizations/{authorization_id}/capture` with
   `amount` = order total and `final_capture=true`, header
   `PayPal-Request-Id: order-capture-{orderId}`.
3. **Stale-authorization handling** (this is explicitly required by the task): PayPal
   authorizations have a 3-day honor period and expire fully at day 29
   (`payments_payment_v2` description of `/reauthorize`: *"reauthorize a payment after its
   initial three-day honor period expires... you can reauthorize an authorized payment from 4
   to 29 days after the 3-day honor period... If 30 days have transpired... you must create an
   authorized payment instead"*). If the capture call fails because the authorization is
   stale (PayPal returns a 422 `error` object — `components/schemas/error` — whose
   `details[].issue` indicates the authorization can't be captured as-is), call
   `POST /v2/payments/authorizations/{authorization_id}/reauthorize` first, replace the
   stored authorization id/expiration with the values the reauthorize response returns (it
   may or may not be a new id — always overwrite from the response, don't assume), then
   retry the capture once.
4. If `reauthorize` itself fails (i.e. the 29-day ceiling has passed, or the authorization
   was already voided/captured), that is **terminal** — do not silently fail the whole
   request with a generic 500. Return an HTTP 409 from `/fulfil` whose body surfaces PayPal's
   own `error.name`/`error.message`/`error.details` so an operator can act on it (e.g. "this
   order's hold can no longer be renewed — cancel it and have the shopper pay again"). Do
   **not** mark the order Fulfilled in this case.
5. On a successful capture, the response's `seller_receivable_breakdown`
   (`components/schemas/seller_receivable_breakdown`) has exactly the three numbers the task
   asks for: `gross_amount` (captured amount), `paypal_fee`, `net_amount`. Store all three
   plus the capture `id` and `status` (`capture_status` enum). This is what
   `GET /api/my-orders` must expose after fulfilment.

### 2.3 Saved cards (vault): `vault_payment_tokens_v3`

`POST /v3/vault/payment-tokens` (`payment_token_request` schema) creates a vault token
directly from a `payment_source.card` — no setup-token/approval round trip is needed for a
direct card (that indirection exists for redirect-based sources like PayPal Wallet, not
needed here). Request body:

```
{
  "customer": { "id": "<shopper's username/email>" },
  "payment_source": { "card": { "name", "number", "expiry", "security_code", "billing_address" } }
}
```

Use the shopper's **username** (the same string already used as `Order.BuyerId`, see §3.3) as
both PayPal's `customer.id` and, for clarity, `customer.merchant_customer_id`. The schema's
pattern for this ID (`merchant_partner_customer_id`) is
`^[0-9a-zA-Z-_.^*$@#]+$`, max length 64 — an email fits (`@` and `.` are both in the allowed
set). If a caller's username is ever >64 chars, that's a genuine input-validation edge case
for the build session to reject with a 400, not a spec gap.

Send `PayPal-Request-Id` on this call too, derived from a hash of the card's own
`security_code`-free identity is not reliable (cards aren't caller-supplied idempotency
keys) — instead scope it to the HTTP request, e.g. a GUID generated once per inbound
`POST /api/payment-methods` call and reused only if the ASP.NET model binding retries.
Duplicate-card protection is not required by the task; don't build it.

The response (`payment_token_response`) gives:
- `id` → the vault token id. This is the durable "saved card" reference — store it.
- `payment_source.card` (`card_response_entity`) → `last_digits`, `brand`
  (`components/schemas/card_brand`), `expiry`. This is exactly the "safe enough to
  recognise" description the task wants; **never** store or log `number`/`security_code` —
  the vault response never contains them.

Reuse for a later order: pass `payment_source.card.vault_id = <stored id>` on
`POST /v2/checkout/orders` (§2.1) instead of raw card fields. `card_request.vault_id` is a
documented sibling field of `number`/`expiry` on the exact same schema — no separate code
path is needed in the Orders v2 call, just a different populated field.

Deleting: `DELETE /v3/vault/payment-tokens/{id}`. If PayPal returns 404 (already gone),
treat the local delete as still-successful (idempotent) rather than surfacing an error —
the end state ("not usable to pay") already holds.

### 2.4 Cancel and refund: `payments_payment_v2`

- `POST /api/orders/{orderId}/cancel` (admin, pre-fulfilment only): if the order has an
  authorization but no capture, call
  `POST /v2/payments/authorizations/{authorization_id}/void` (releases the hold, no money
  ever moved). If the order never got as far as an authorization (shopper cancels before
  paying), there is nothing to call PayPal about — just transition local state.
- `POST /api/orders/{orderId}/refunds` (**shopper-scoped — see §2.6 for why**, post-fulfilment
  only): call `POST /v2/payments/captures/{capture_id}/refund`
  (`refund_request` schema: optional `amount`, `note_to_payer`). Per the schema's own
  description: *"For a full refund, include an empty request body. For a partial refund,
  include an `amount`... If amount is not specified, an amount equal to captured amount minus
  previous refunds is refunded."* Before calling PayPal, validate locally that
  `requestedAmount <= capturedAmount - sumOfPriorRefunds` — PayPal enforces this
  server-side too, but failing fast locally gives a cleaner error and avoids a wasted call.
- **Idempotency key handling (task-mandated, not optional):** the request carries a
  caller-supplied idempotency key. Before calling PayPal, look up whether a `Refund` row
  already exists for this order with that exact key; if so, return the stored result and make
  no new PayPal call. Otherwise call PayPal with header
  `PayPal-Request-Id: refund-{orderId}-{callerKey}` (scoping by order id prevents one
  shopper's key colliding with another's) and persist the new `Refund` row keyed by that
  same caller key. Two different keys against the same capture are two legitimate distinct
  partial refunds — do not conflate them.
- Refund response (`components/schemas/refund` — not pulled verbatim above, but structurally
  parallel to `capture`/`authorization` via `refund_status`) gives `id` and `status`
  (`refund_status` enum: `CANCELLED|FAILED|PENDING|COMPLETED`). Store both.

### 2.5 Reconciliation: `transaction_search_v1`

`GET /v1/reporting/transactions`:

- **31-day range limit is explicit in the spec**: `end_date` parameter description says *"The
  maximum supported range is 31 days."* `GET /api/reconciliation?from=&to=` must therefore
  **chunk** any wider caller-supplied range into ≤31-day windows and issue one search per
  window.
- **Pagination**: `page` (1-based) / `page_size` (max 500) / `total_pages` in
  `search_response`. Within each date window, loop `page = 1..total_pages` until exhausted.
  This is the literal meaning of the task's "covers the whole range, not just the first page
  of it."
- **Reporting lag** is explicit too: *"It takes a maximum of three hours for executed
  transactions to appear in the list transactions call."* A range covering payments just
  made in this session can legitimately come back empty — that's expected, not a bug. Build
  and verify the report logic against a wider/older range that has data (e.g. `from` a few
  hours before the run, well past when sandbox activity has posted) rather than treating an
  empty recent-range result as a failure.
- **Matching to eShop orders**: for each `transaction_detail`, `transaction_info.invoice_id`
  echoes the `invoice_id` set at order/capture time (`"eshop-order-{orderId}"`, see §2.1) —
  parse the order id back out of it. `transaction_info.paypal_reference_id` (when
  `paypal_reference_id_type == "ODR"`) equals the PayPal order id stored as
  `OrderPayment.PayPalOrderId` — use as a secondary correlation key. The report's job is to
  produce three buckets: transactions PayPal has that match a local order, PayPal
  transactions with no matching local order (surface as an anomaly), and local
  fulfilled/refunded orders with no matching PayPal transaction in range (surface as an
  anomaly — this is the "PayPal knows and eShop doesn't, or the reverse" requirement,
  verbatim).

### 2.6 Judgment call: refunds are shopper-scoped, not operator-scoped

Re-read carefully: the task names exactly three operator actions — *"Fulfil, cancel and
reconciliation are operator actions... Every other endpoint is shopper-scoped and acts only on
the caller's own data."* Refunds is not in that list. This is a deliberate reading, not an
oversight: `POST /api/orders/{orderId}/refunds` is shopper-scoped self-service (a shopper
refunding their own fulfilled order), with the same "only the caller's own order" ownership
check as `GET /api/my-orders`. Do not put `[Authorize(Roles = Administrators)]` on it.

### 2.7 Judgment call: single global currency, no FX

`PayPal:Currency` is one configured code for the whole deployment. Catalog prices
(`CatalogItem.Price`, already a plain `decimal` with no currency field) are treated as
already denominated in that currency. No conversion logic is in scope. `Money.value` must be
formatted to the correct number of minor units for the configured currency — PayPal's `money`
schema pattern accepts any decimal string, but PayPal itself rejects e.g. `"10.00"` for JPY
(0 decimals) or accepts 3 decimals for currencies like BHD/KWD/OMR. Since `PayPal:Currency` is
config-driven and not hardcoded, implement a small minor-unit lookup (0-decimal set: JPY, HUF,
TWD, KRW, etc.; 3-decimal set: BHD, KWD, OMR; default 2) rather than hardcoding `"F2"` — this
is what makes "the amount PayPal holds must equal the order total to the cent" true for
whatever currency the credentials' account actually uses.

---

## 3. Domain model changes (`src/ApplicationCore`)

### 3.1 `Order` aggregate — extend, don't replace

`src/ApplicationCore/Entities/OrderAggregate/Order.cs` currently has no state beyond
`BuyerId`/`OrderDate`/`ShipToAddress`/`OrderItems`. Add:

```csharp
public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;
public OrderPayment? Payment { get; private set; }   // null until /pay is first called

public void BeginPayment(OrderPayment payment) // called by the payment service
public void MarkFulfilled()
public void MarkCancelled()
public void ApplyRefund(bool isFullRefund)     // sets Refunded or PartiallyRefunded
```

New enum `src/ApplicationCore/Entities/OrderAggregate/OrderStatus.cs`:
`AwaitingPayment, Authorized, Fulfilled, Cancelled, PartiallyRefunded, Refunded`. Guard
transitions inside these methods (e.g. `MarkFulfilled()` throws if `Status != Authorized`) —
this is what makes "double-fulfil" and "refund before fulfil" impossible at the domain layer,
independent of the PayPal-side idempotency in §2.

New child entity `src/ApplicationCore/Entities/OrderAggregate/OrderPayment.cs` (one-to-one
with `Order`, created the first time `/pay` runs):

```csharp
public class OrderPayment : BaseEntity
{
    public int OrderId { get; private set; }
    public string PayPalOrderId { get; private set; }
    public string? PayPalAuthorizationId { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }
    public string? PayPalCaptureId { get; private set; }
    public string CurrencyCode { get; private set; }
    public decimal AuthorizedAmount { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }
    public decimal RefundedAmount { get; private set; }     // running total
    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    // behavior: RecordAuthorization(...), RecordReauthorization(...), RecordCapture(...),
    // RecordVoid(), AddRefund(Refund) — AddRefund guards RefundedAmount + amount <= CapturedAmount
}
```

New entity `src/ApplicationCore/Entities/OrderAggregate/Refund.cs`:
`Id (BaseEntity), PayPalRefundId, Amount, CurrencyCode, Status, IdempotencyKey, CreatedAt`.
`IdempotencyKey` + `OrderPaymentId` should be a unique index (EF config, §4) — this is the
actual enforcement of "repeating a request under the same key must not refund twice" at the
storage layer, on top of the app-level lookup in §2.4.

Why a child entity instead of flat fields on `Order`: the PayPal state genuinely has this
much shape (an order id, an authorization id that can change on reauthorization, a capture
id, a fee breakdown, N refunds) — flattening it onto `Order` would just mean prefixing every
field with `Payment` and losing the refund collection. This isn't speculative abstraction;
it's the state the task explicitly requires ("enough of the state PayPal owns... that a later
request can act on it").

### 3.2 `IOrderService` — add a catalog-based placement method, keep the existing one

`src/ApplicationCore/Services/OrderService.cs`'s existing `CreateOrderAsync(basketId,
address)` is used by the Web project's basket checkout — leave it untouched. Add a second
method to the same interface for the direct-from-catalog flow `POST /api/orders` needs:

```csharp
Task<Order> CreateOrderFromCatalogItemsAsync(
    string buyerId, Address shipToAddress, IReadOnlyList<(int CatalogItemId, int Quantity)> items);
```

Implementation mirrors the existing method's item-building logic (look up `CatalogItem`s,
build `CatalogItemOrdered`/`OrderItem`, `new Order(...)`) but skips the `Basket` entirely —
this is what "reuses the app's existing order/order-item model rather than a parallel one"
means: same `Order`/`OrderItem`/`CatalogItemOrdered` entities, just a different, basket-free
construction path. Validate: items non-empty, quantities > 0, every catalog item id exists
(throw a new `CatalogItemNotFoundException` otherwise — pattern-match the existing
`BasketNotFoundException` in `ApplicationCore/Exceptions`). `Order.Status` defaults to
`AwaitingPayment` per §3.1.

### 3.3 New orchestration service: `IOrderPaymentService`

New interface `src/ApplicationCore/Interfaces/IOrderPaymentService.cs` +
`src/ApplicationCore/Services/OrderPaymentService.cs`:

```csharp
Task<OrderPayment> AuthorizeAsync(int orderId, string buyerId, PaymentAuthorizationRequest request);
Task<OrderPayment> FulfilAsync(int orderId);              // no buyerId — operator action
Task CancelAsync(int orderId);                              // no buyerId — operator action
Task<Refund> RefundAsync(int orderId, string buyerId, RefundRequest request);
```

`PaymentAuthorizationRequest`/`RefundRequest` are small request models in
`ApplicationCore.Interfaces` (not PublicApi DTOs directly, so the domain layer doesn't depend
on the web layer) — `PaymentAuthorizationRequest` carries either a raw card
(`ApplicationCore.Interfaces.PayPal.CardDetails`, see §3.5) or a `PaymentMethodId`
(exactly one, validated with a guard clause that throws on both-or-neither). Every method
takes `orderId`/`buyerId` and internally: (a) loads the order via `IRepository<Order>` with a
spec that includes `Payment`/`Payment.Refunds`, (b) for shopper-scoped calls, throws a new
`OrderNotFoundException` if `order.BuyerId != buyerId` — same exception whether the order
truly doesn't exist or belongs to someone else, so PublicApi can map both to 404 without
leaking which case it was (§6.2).

### 3.4 `Buyer`/`PaymentMethod` — wire up the existing, currently-unused aggregate

`src/ApplicationCore/Entities/BuyerAggregate/Buyer.cs` and `PaymentMethod.cs` already exist
but are **dead code today** — no `DbSet<Buyer>`, no EF configuration, no usage anywhere in the
codebase (verified: they only appear in their own two files). This is exactly the shape Flow
2 needs, so extend rather than build a parallel aggregate:

- `Buyer.IdentityGuid` stores the shopper's **username**, identically to how
  `Order.BuyerId`/`Basket.BuyerId` already do (verified via `Web/Controllers/OrderController.cs`:
  `User.Identity.Name` is the value passed everywhere as the buyer key in this codebase — stay
  consistent with that, don't introduce a second identity concept).
- Add `Buyer.AddPaymentMethod(PaymentMethod)` / `Buyer.RemovePaymentMethod(int
  paymentMethodId)` (mutate the private `_paymentMethods` list, same DDD-encapsulation pattern
  `Order`/`_orderItems` already uses).
- Extend `PaymentMethod`: keep `Alias` and `Last4`; repurpose `CardId` to hold the **PayPal
  vault token id** (its existing comment — *"actual card data must be stored in a PCI
  compliant system"* — already describes exactly this design, it was just never finished).
  Add `Brand` (from `card_response_entity.brand`) and `ExpiryYearMonth` (from
  `card_response_entity.expiry`, format `YYYY-MM`). Constructor:
  `PaymentMethod(string vaultId, string brand, string last4, string expiryYearMonth, string? alias)`.
- New `src/ApplicationCore/Services/PaymentMethodService.cs` /
  `IPaymentMethodService`: `SaveAsync(buyerId, CardDetails)`, `ListAsync(buyerId)`,
  `DeleteAsync(buyerId, paymentMethodId)`. `SaveAsync` does get-or-create on `Buyer` by
  `IdentityGuid == buyerId` (create one lazily on first save — nothing in the codebase creates
  a `Buyer` today, so this endpoint is the first writer). `DeleteAsync` throws the same
  "not found for this buyer" exception pattern as §3.3 if the payment method belongs to
  someone else or doesn't exist.

### 3.5 PayPal client abstractions (ApplicationCore side — interfaces + POCOs only)

New folder `src/ApplicationCore/Interfaces/PayPal/`, one interface per spec-resource used
(mirrors §1's table so the mapping from interface to contract stays obvious):

- `IPayPalOrdersClient` — `CreateOrderAsync`, `AuthorizeOrderAsync`, `CaptureOrderAsync`,
  `GetOrderAsync` (checkout_orders_v2).
- `IPayPalPaymentsClient` — `CaptureAuthorizationAsync`, `ReauthorizeAuthorizationAsync`,
  `VoidAuthorizationAsync`, `GetAuthorizationAsync`, `RefundCaptureAsync`, `GetCaptureAsync`
  (payments_payment_v2).
- `IPayPalVaultClient` — `CreatePaymentTokenAsync`, `ListPaymentTokensAsync`,
  `DeletePaymentTokenAsync` (vault_payment_tokens_v3).
- `IPayPalTransactionSearchClient` — `SearchAsync(DateTimeOffset from, DateTimeOffset to)` →
  `IReadOnlyList<PayPalTransactionRecord>`; the 31-day chunking and page-looping from §2.5
  live **inside** this one method so callers (the reconciliation endpoint) don't have to know
  about either limit.

Plain result/request POCOs live alongside these interfaces (`PayPalOrderResult`,
`PayPalAuthorizationResult`, `PayPalCaptureResult`, `PayPalRefundResult`,
`PayPalPaymentTokenResult`, `PayPalTransactionRecord`, `CardDetails`). Field names should
track the spec's `snake_case` JSON names in PascalCase (e.g. `PayPalFeeAmount` from
`paypal_fee`) so anyone cross-referencing the spec later can follow the mapping without a
lookup table.

New exception `src/ApplicationCore/Exceptions/PayPalApiException.cs`: carries the raw
`error.name` / `error.message` / `error.debug_id` / `error.details[].issue` fields
(`components/schemas/error` — identical shape across all four specs used) plus the HTTP
status PayPal returned. This is what §2.2 step 4 and the exception middleware (§6.2) surface
to operators verbatim.

---

## 4. Infrastructure layer (`src/Infrastructure`)

### 4.1 New packages

`Microsoft.Extensions.Http` is not currently referenced anywhere in the solution (checked:
`Directory.Packages.props` has no entry for it) and is required for typed
`HttpClient`/`IHttpClientFactory` registration. Add a `PackageVersion` entry to
`Directory.Packages.props` and a `PackageReference` to `src/Infrastructure/Infrastructure.csproj`.
This is a standard Microsoft networking package, not a PayPal SDK — compliant with the
"no pre-built PayPal client" rule. `System.Net.Http.Json` is already centrally versioned but
unreferenced; add a `PackageReference` to `Infrastructure.csproj` too, for
`ReadFromJsonAsync`/`PostAsJsonAsync` convenience.

Do **not** add any NuGet package whose description mentions PayPal.

### 4.2 `PayPalOptions` and configuration binding

New `src/Infrastructure/PayPal/PayPalOptions.cs`:

```csharp
public class PayPalOptions
{
    public const string ConfigSection = "PayPal";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Environment { get; set; } = "sandbox"; // "sandbox" | "live"
    public string Currency { get; set; } = "USD";
    public string? BaseUrl { get; set; }                  // optional override, verbatim when set
}
```

**Critical wiring detail — read carefully, this is where the whole integration silently
breaks if done wrong:** the task's four env vars are `PAYPAL_CLIENT_ID`,
`PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`, `PAYPAL_CURRENCY` — single underscore, no
section prefix. ASP.NET Core's default `AddEnvironmentVariables()` only maps
`Section__Key` (double underscore) to `Section:Key`; it will **not** pick these up
automatically. In `src/PublicApi/Program.cs`, after the existing
`builder.Configuration.AddEnvironmentVariables();` line, add an explicit mapping so it wins
(later sources override earlier ones in `IConfiguration`):

```csharp
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["PayPal:ClientId"] = System.Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"),
    ["PayPal:ClientSecret"] = System.Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET"),
    ["PayPal:Environment"] = System.Environment.GetEnvironmentVariable("PAYPAL_ENVIRONMENT"),
    ["PayPal:Currency"] = System.Environment.GetEnvironmentVariable("PAYPAL_CURRENCY"),
});
builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection(PayPalOptions.ConfigSection));
```

`PayPal:BaseUrl` has no dedicated env var in the task — it's an optional override meant to be
set through normal configuration (`appsettings.*.json`, user-secrets, or the standard
double-underscore `PayPal__BaseUrl` env var). No special mapping is needed for it; just don't
default it to anything in `appsettings.json` (leave the key absent or empty so "unset" is
unambiguous).

Add an empty placeholder `"PayPal": {}` section (or omit entirely) to
`src/PublicApi/appsettings.json` — **do not write any credential values there**, not even
sandbox-looking placeholders; the binding above is what supplies real values at runtime.

### 4.3 Base URL resolution and the OAuth2 token client

New `src/Infrastructure/PayPal/PayPalBaseUrlResolver.cs` (or a method on the DI registration):
if `options.BaseUrl` is set, use it verbatim for every PayPal call including the token
request (task requirement, stated twice in the brief — don't special-case the token call).
Otherwise derive from `options.Environment`: `sandbox` →
`https://api-m.sandbox.paypal.com` (the literal value in every spec's `servers[0].url`),
`live` → `https://api-m.paypal.com` (same host pattern minus `.sandbox`, not itself declared
in these specs since they only ship the sandbox server entry — this derivation is our own
implementation choice for environment switching, not a contract claim).

New `src/Infrastructure/PayPal/PayPalAccessTokenProvider.cs` implementing a new
`ApplicationCore.Interfaces.PayPal.IPayPalAccessTokenProvider`: `POST {baseUrl}/v1/oauth2/token`
with `Authorization: Basic base64(ClientId:ClientSecret)`, body
`grant_type=client_credentials` (form-encoded, per OAuth2 client-credentials convention —
this is standard OAuth2 shape, consistent with the spec's `flows.clientCredentials`
declaration). Cache the resulting `access_token` in `IMemoryCache` (already registered in
`Program.cs`) keyed by client id, honoring the response's `expires_in`; refetch a little before
expiry (e.g. 60s skew) or on any `401` from a downstream call.

### 4.4 The four typed clients

`src/Infrastructure/PayPal/PayPalOrdersClient.cs`, `PayPalPaymentsClient.cs`,
`PayPalVaultClient.cs`, `PayPalTransactionSearchClient.cs` implement the four ApplicationCore
interfaces from §3.5. Register via `AddHttpClient<TInterface, TImpl>((sp, client) => { client.BaseAddress
= new Uri(ResolveBaseUrl(sp.GetRequiredService<IOptions<PayPalOptions>>().Value)); })` so the
base-URL-override rule in §4.3 applies uniformly. Each request handler:

1. Resolves an access token via `IPayPalAccessTokenProvider` and sets
   `Authorization: Bearer {token}`.
2. Sets `Content-Type: application/json` and any per-call header from §2 (`PayPal-Request-Id`,
   etc.).
3. On a non-2xx response, deserialize the body as `components/schemas/error` and throw
   `PayPalApiException` (§3.5) with the HTTP status attached — never throw a generic
   `HttpRequestException` here, callers need the structured PayPal error.
4. **Never log request or response bodies at a level that would capture
   `number`/`security_code`/full PAN.** Log at most: PayPal resource ids, status, and
   `debug_id` on error. This is a hard requirement from the task ("never written to logs") —
   put an explicit code comment or a redaction helper at the one place raw card JSON is
   serialized, so nobody accidentally adds a `LogInformation(requestBody)` later.

`PayPalTransactionSearchClient.SearchAsync` implements the 31-day-chunk + page-loop logic
from §2.5 internally.

### 4.5 EF configuration and migrations

Add to `CatalogContext` (`src/Infrastructure/Data/CatalogContext.cs`):
`DbSet<Buyer> Buyers`, `DbSet<PaymentMethod> PaymentMethods`, `DbSet<OrderPayment>
OrderPayments`, `DbSet<Refund> Refunds` (or rely on navigation-only discovery for the
owned/child ones — decide per whether they're modeled as owned types or independent entities;
`OrderPayment`/`Refund` should be regular entities with FKs, not owned types, since they have
their own identity and are queried directly by the reconciliation report).

New config files in `src/Infrastructure/Data/Config/`: `BuyerConfiguration.cs`,
`PaymentMethodConfiguration.cs`, `OrderPaymentConfiguration.cs`, `RefundConfiguration.cs` —
follow the exact style of the existing `OrderConfiguration.cs`/`OrderItemConfiguration.cs`
(private-field navigation access mode for the encapsulated collections, `decimal(18,2)` column
type for money fields, required/max-length constraints). Put the unique index from §3.1
(`OrderPaymentId + IdempotencyKey` on `Refund`) here:
`builder.HasIndex(r => new { r.OrderPaymentId, r.IdempotencyKey }).IsUnique();`.

Generate an EF Core migration (`dotnet ef migrations add AddPayPalPayments --project
src/Infrastructure --startup-project src/PublicApi`) even though this session's
verification run will use `UseOnlyInMemoryDatabase=true` (which ignores migrations
entirely, per the environment notes) — a real SQL Server deployment still needs it, and
"production-grade" includes that path working. Don't skip it just because it won't be
exercised in this environment.

### 4.6 DI registration

Extend `src/Infrastructure/Dependencies.cs` (or add a sibling `PayPalDependencies.cs` called
from `Program.cs` alongside the existing `Dependencies.ConfigureServices` call) with:
`services.Configure<PayPalOptions>(...)` (see §4.2, may already be done in `Program.cs`
directly — pick one place, don't duplicate), the four `AddHttpClient<...>` registrations from
§4.4, `IPayPalAccessTokenProvider`, and the new ApplicationCore services
(`IOrderPaymentService`, `IPaymentMethodService`) as scoped.

---

## 5. PublicApi endpoints (`src/PublicApi`)

Follow the `MinimalApi.Endpoint` `IEndpoint<TResponse, TRequest, TService>` convention that
`CatalogItemEndpoints/CreateCatalogItemEndpoint.cs` already uses (8 of the 9 existing endpoint
files use this pattern; `AuthenticateEndpoint.cs` is the sole `Ardalis.ApiEndpoints` outlier —
don't extend that pattern, standardize on the majority one). Every new endpoint:
`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`, plus
`Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS` for the three operator
routes. Pull the caller's username from `ClaimTypes.Name` (`httpContext.User.Identity.Name`) —
same claim the JWT already carries per `IdentityTokenClaimService` (§ investigated: the token
only ever contains `Name` + `Role` claims, no separate user-id claim — so `Name`/username is
the one stable identity string available, consistent with how `Order.BuyerId` already works
everywhere else in this codebase).

### 5.1 `OrderEndpoints/`

| File | Route | Auth | Notes |
|---|---|---|---|
| `PlaceOrderEndpoint.cs` | `POST api/orders` | shopper | body: `{ shipToAddress, items: [{catalogItemId, quantity}] }`; calls `IOrderService.CreateOrderFromCatalogItemsAsync`; response top-level `orderId` (int) |
| `PayOrderEndpoint.cs` | `POST api/orders/{orderId}/pay` | shopper | body: either `{ card: {...} }` or `{ paymentMethodId }`; calls `IOrderPaymentService.AuthorizeAsync`; 404 if order not found/not owned |
| `FulfilOrderEndpoint.cs` | `POST api/orders/{orderId}/fulfil` | **admin** | no body; calls `IOrderPaymentService.FulfilAsync`; 409 with PayPal error detail if reauthorization was needed and failed (§2.2) |
| `CancelOrderEndpoint.cs` | `POST api/orders/{orderId}/cancel` | **admin** | no body; calls `IOrderPaymentService.CancelAsync` |
| `RefundOrderEndpoint.cs` | `POST api/orders/{orderId}/refunds` | shopper (§2.6) | body: `{ idempotencyKey, amount? }`; response top-level `refundId` = PayPal's refund id (string) |
| `MyOrdersEndpoint.cs` | `GET api/my-orders` | shopper | list of orders for `User.Identity.Name` with `status`, and `payment` (nested: authorization/capture ids+status, captured/fee/net amounts, refunds) when present |

### 5.2 `PaymentMethodEndpoints/`

| File | Route | Auth | Notes |
|---|---|---|---|
| `SavePaymentMethodEndpoint.cs` | `POST api/payment-methods` | shopper | body: raw card fields; response top-level `paymentMethodId` (int) + `brand`/`last4`/`expiry` |
| `ListPaymentMethodsEndpoint.cs` | `GET api/payment-methods` | shopper | caller's saved cards only |
| `DeletePaymentMethodEndpoint.cs` | `DELETE api/payment-methods/{paymentMethodId}` | shopper | 404 if not found/not owned; after this call it must not appear in the list endpoint nor be usable in `/pay` (enforced by `IOrderPaymentService.AuthorizeAsync` re-validating ownership of the referenced `paymentMethodId` at pay time, not just at save time) |

### 5.3 `ReconciliationEndpoints/`

| File | Route | Auth | Notes |
|---|---|---|---|
| `ReconciliationReportEndpoint.cs` | `GET api/reconciliation?from={iso}&to={iso}` | **admin** | calls `IPayPalTransactionSearchClient.SearchAsync`, cross-references local `OrderPayment`s per §2.5; response: matched pairs, PayPal-only anomalies, eShop-only anomalies |

### 5.4 Response identifier convention

Every response DTO for an endpoint that creates something follows the existing project style
(`BaseResponse` + a `CorrelationId()`); add the mandated top-level id field alongside it —
`orderId`, `paymentMethodId`, `refundId` exactly as named in the task, at the top level of the
JSON body (not nested under a `data`/`result` wrapper).

### 5.5 Register in Swagger / `AddEndpoints()`

`MinimalApi.Endpoint`'s `AddEndpoints()`/`MapEndpoints()` (already called in `Program.cs`)
auto-discovers `IEndpoint<,,>` implementations by assembly scan — no explicit registration
list to maintain, same as the existing catalog endpoints. Tag each with `.WithTags(...)`
matching its folder name for a clean Swagger UI grouping, same as existing endpoints.

---

## 6. Cross-cutting concerns

### 6.1 Ownership checks

Every shopper-scoped endpoint must load the resource (order / payment method) and compare its
owner to `User.Identity.Name` *before* doing anything else, and must fail identically
(`OrderNotFoundException`/a new `PaymentMethodNotFoundException`) whether the resource is
missing or belongs to someone else. Put this check once, in the `ApplicationCore` service
layer (§3.3/§3.4), not repeated per-endpoint — that's both DRY and the only way to guarantee
Web and any future caller get the same guarantee.

### 6.2 Error mapping (`ExceptionMiddleware`)

`src/PublicApi/Middleware/ExceptionMiddleware.cs` today only special-cases
`DuplicateException` (409) with everything else falling through to a bare 500. Add cases for:

- `OrderNotFoundException` / `PaymentMethodNotFoundException` / `CatalogItemNotFoundException`
  → 404.
- `PayPalApiException` → map the *PayPal* status where it's meaningful to the caller (e.g. a
  422 validation problem on our side of the request → 400/422 to our caller), otherwise 502,
  and include `error.name`/`error.message`/`error.details` in the body verbatim (never a
  generic "internal server error" for something PayPal explained clearly) — this is what
  makes the §2.2 "must say so in terms an operator can act on" requirement real.
- A new `InvalidPaymentRequestException` (both-or-neither of card/paymentMethodId, refund
  amount exceeding remaining captured amount, etc.) → 400.

### 6.3 Idempotency summary (so it isn't scattered only in §2)

Three independent layers, deliberately redundant:
1. Domain state machine (§3.1) — a second `/fulfil` on an already-`Fulfilled` order is
   rejected before any PayPal call is made.
2. Deterministic `PayPal-Request-Id` per operation (§2.1/§2.2) — even if two requests
   somehow both pass the domain check concurrently, PayPal itself de-duplicates them.
3. Caller-supplied idempotency key + unique DB index for refunds specifically (§2.4/§3.1),
   because refunds are the one operation where the *caller* — not just the order lifecycle —
   defines what counts as "the same request."

### 6.4 Secrets

`PayPal:ClientSecret` must never be logged, never appear in an exception message forwarded to
a caller, and never be written into `appsettings.*.json`. The only place it's read is
`PayPalAccessTokenProvider` building the Basic-auth header. Confirm at the end of the build
that `git diff`/`git status` contains no credential-looking strings before anything is
committed.

---

## 7. Build sequencing

Suggested order (each step buildable/compilable before the next, minimizing rework):

1. `ApplicationCore`: `OrderStatus`, `OrderPayment`, `Refund`, extend `Order`; extend
   `Buyer`/`PaymentMethod`; new exceptions. Compiles standalone (no Infrastructure/PublicApi
   changes needed yet).
2. `ApplicationCore`: the four `IPayPalXxxClient` interfaces + POCOs + `PayPalApiException`
   (§3.5). Still no implementation — just the contract ApplicationCore depends on.
3. `ApplicationCore`: `IOrderPaymentService`/`OrderPaymentService`,
   `IPaymentMethodService`/`PaymentMethodService`, extend `IOrderService`. These reference the
   interfaces from step 2 but not their implementations — compiles fine.
4. Unit tests (`tests/UnitTests/ApplicationCore`) for the above using `NSubstitute` to fake
   the four `IPayPalXxxClient` interfaces (already a project dependency, and this is mocking
   *our own* abstraction, not a PayPal SDK — fully compliant). Cover: state-machine guards,
   refund-amount-exceeds-captured rejection, idempotency-key replay returning the cached
   refund, ownership-mismatch throwing the not-found exception. This is where most of the
   tricky logic in §2/§3 gets its fast, offline safety net.
5. `Infrastructure`: `PayPalOptions`, base URL resolver, `PayPalAccessTokenProvider`, the four
   client implementations, EF configs + migration, DI wiring (§4).
6. `PublicApi`: env-var mapping in `Program.cs` (§4.2 — do this *before* wiring endpoints so
   there's something to smoke-test against), then the endpoint files (§5), then
   `ExceptionMiddleware` additions (§6.2).
7. Integration tests (`tests/PublicApiIntegrationTests`, MSTest + `ProgramTest.NewClient` +
   `ApiTokenHelper`, same pattern as `CatalogItemEndpoints` tests) covering the full flow end
   to end against the **real sandbox** (this project deliberately has no PayPal mock to swap
   in — the task requires real sandbox verification, and there is no PayPal SDK to fake
   against anyway). Guard these specific tests to skip (not fail) when
   `PAYPAL_CLIENT_ID`/`PAYPAL_CLIENT_SECRET` aren't present in the environment, so
   `dotnet test` stays runnable without credentials elsewhere, while still being the primary
   verification path here where credentials are present.
8. Manual/scripted end-to-end pass per the task's "Rules of engagement": place an order, pay
   with `4111 1111 1111 1111`, fulfil it (real capture), refund it (full or partial), save a
   card and reuse it to pay a second order, pull a reconciliation report over an older range
   that has data. Confirm ports/HTTPS/in-memory-DB setup per the environment notes before any
   of this (`DOTNET_ROLL_FORWARD=Major`, `UseOnlyInMemoryDatabase=true`, dev cert trusted,
   only the assigned port block, one PublicApi instance running).
9. Write the concise step-by-step verification guide the task's "Rules of engagement" asks
   for, once the above is actually confirmed working — don't draft it earlier from
   assumptions.

---

## 8. What this session deliberately did not decide (left as genuine implementation
   freedom, not gaps)

- Exact JSON casing/naming of new PublicApi request/response DTOs beyond the three mandated
  top-level id fields (§5.4) — follow the existing project's DTO conventions
  (`CatalogItemDto`-style).
- Whether `OrderPayment`/`Refund` are separate tables or could be simplified — §3.1 gives the
  reasoning for separate tables; a build session that finds a cleaner shape while preserving
  every field the task requires is free to take it, this isn't a rigid schema mandate.
- Retry/backoff policy around transient PayPal HTTP failures (timeouts, 5xx) — use judgment;
  nothing in the task mandates a specific policy, just that double-clicks don't double-charge
  (§6.3), which is a different concern from transient-failure retries.
