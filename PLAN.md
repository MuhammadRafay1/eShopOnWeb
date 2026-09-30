# PLAN — PayPal payments & saved cards for eShopOnWeb

This is the build plan for a later session. That session has the same task text and the
**paypal-docs** MCP server, plus this file. It does **not** have this session's conversation,
so everything needed is written here.

The work is **additive**: it adds money movement and operator flows on top of the existing
catalog/basket/order flow without replacing it. All new capability is exposed as JWT HTTP
endpoints on **`src/PublicApi`**, following that project's existing endpoint conventions.

> **PayPal source of truth.** Every PayPal detail below was taken from the **paypal-docs** MCP
> server. When you implement, keep using that MCP server for any PayPal question. Do **not**
> web-search PayPal or rely on general knowledge. If the MCP server does not cover something
> this integration needs, **STOP and report the gap** — but note that nothing in this plan is a
> gap: the MCP docs cover every call listed. Doc pages are cited by their MCP filesystem path,
> e.g. `/api-reference/orders/create-order.mdx`; OpenAPI specs live under
> `/openapi/api-reference/specs/*.json` and are the most precise field-level reference.

---

## 0. Key facts about the existing codebase (verified)

- **Solution / projects:** `eShopOnWeb.sln`. Layers: `ApplicationCore` (entities, interfaces,
  services, specs), `Infrastructure` (EF `CatalogContext`, Identity, repositories),
  `PublicApi` (JWT API — where all new endpoints go), `Web` (cookie storefront — do not touch).
- **Endpoint style in PublicApi (follow this):** the newer endpoints implement
  `MinimalApi.Endpoint.IEndpoint<TResponse, TRequest, TDeps...>` and register routes in
  `AddRoute(IEndpointRouteBuilder app)` using `app.MapPost/MapGet/...`. See
  `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs` and
  `DeleteCatalogItemEndpoint.cs`. `Program.cs` calls `builder.Services.AddEndpoints()` and
  `app.MapEndpoints()` which auto-discover every `IEndpoint`. **New endpoints are discovered
  automatically — no Program.cs registration of endpoints needed.** (The older
  `EndpointBaseAsync` style in `AuthEndpoints/AuthenticateEndpoint.cs` also works but prefer
  `IEndpoint` for consistency with the rest of the app.)
- **Auth:** JWT bearer configured in `src/PublicApi/Program.cs`. Symmetric key is
  `AuthorizationConstants.JWT_SECRET_KEY`. Admin endpoints use
  `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
  AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` (role string
  `"Administrators"`). Plain `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`
  for shopper endpoints.
- **Caller identity from the token:** the JWT carries `ClaimTypes.Name = username (email)` and
  `ClaimTypes.Role`. In an endpoint lambda, inject `System.Security.Claims.ClaimsPrincipal user`
  (or `HttpContext`) and read `user.Identity!.Name`. **This username string is the buyer id** —
  it is exactly what `Order.BuyerId` and `Buyer.IdentityGuid` hold elsewhere in the app.
- **Seed users** (`src/Infrastructure/Identity/AppIdentityDbContextSeed.cs`),
  password `Pass@word1` (`AuthorizationConstants.DEFAULT_PASSWORD`):
  - Shopper: `demouser@microsoft.com` (no roles)
  - Admin: `admin@microsoft.com` (role `Administrators`)
- **Order aggregate** (`src/ApplicationCore/Entities/OrderAggregate/Order.cs`): aggregate root
  with `BuyerId`, `OrderDate`, `ShipToAddress` (owned `Address`), private `_orderItems`, and
  `decimal Total()`. `OrderItem` holds owned `CatalogItemOrdered` snapshot + `UnitPrice` +
  `Units`. **This is the model `POST /api/orders` must reuse.**
- **Catalog prices:** `CatalogItem.Price` (decimal). `CatalogItemsSpecification(params int[] ids)`
  fetches items by id.
- **Repository:** generic `IRepository<T>`/`IReadRepository<T>` (Ardalis.Specification) over
  `CatalogContext`, registered in `Program.cs`. Query with `Specification<T>` objects.
- **Buyer aggregate exists but is NOT persisted:** `Buyer` (`IdentityGuid`, owned
  `PaymentMethod` collection) and `PaymentMethod` (`Alias`, `CardId`, `Last4`) are defined in
  `ApplicationCore/Entities/BuyerAggregate/` but **have no `DbSet` and no EF config** — they are
  currently unused. We will wire them up (see §5.2). `PaymentMethod`'s own comment says
  "actual card data must be stored in a PCI compliant system" — that PCI system is the PayPal
  Vault; `CardId` will hold the PayPal vault token id.
- **EF context:** `src/Infrastructure/Data/CatalogContext.cs` applies all
  `IEntityTypeConfiguration` from its assembly via
  `builder.ApplyConfigurationsFromAssembly(...)`. Add new configs there (they auto-apply).
- **DI for Infrastructure:** `Infrastructure/Dependencies.cs` chooses In-Memory vs SqlServer
  from `UseOnlyInMemoryDatabase`. We add a small `PayPalDependencies` registration (see §4).
- **Central package management is ON** (`Directory.Packages.props`, `net8.0`). `System.Net.Http`,
  `IHttpClientFactory` (`AddHttpClient`), and `System.Text.Json` are in the shared framework —
  **no new NuGet package is required** for the PayPal client. `System.Net.Http.Json` and
  `Ardalis.Result` are already available centrally if wanted. Avoid introducing new
  infrastructure/deps (task rule).
- **Error middleware:** `src/PublicApi/Middleware/ExceptionMiddleware.cs` turns exceptions into
  JSON `ErrorDetails`. `DuplicateException` → 409, else 500. We add mapping for a few new
  exception types (see §8.7) or return typed `IResult`s directly.

---

## 1. Environment & how to run (this machine)

These are hard constraints for build + self-verification:

- **SDK/runtime mismatch:** `global.json` pins SDK `8.0.x` but only the .NET 10 SDK is present
  and the ASP.NET Core 8.0 runtime is missing. Run with **`DOTNET_ROLL_FORWARD=Major`** (and/or
  set `rollForward: latestMajor`). Prefer setting the env var at run time so no repo file needs
  changing beyond code.
- **No LocalDB:** run every host with **`UseOnlyInMemoryDatabase=true`**. In-memory store is
  per-process, wiped on restart, ignores migrations. **Do not author EF migrations** (they
  won't run and aren't needed); model changes take effect purely from the model + configs.
- **Per-host isolation:** PublicApi has its own in-memory store, separate from Web. The whole
  payment flow must be drivable through **PublicApi alone** — that's why `POST /api/orders`
  exists. Create → pay → fulfil → refund all within one PublicApi run.
- **Ports:** bind only to the assigned block (`APP_PORT_BLOCK_BASE` …
  `+APP_PORT_BLOCK_SIZE-1`; `launchSettings` already targets there). Stop any prior instance
  before starting a new one.
- **HTTPS:** both hosts use `UseHttpsRedirection()`; ensure the dev cert is trusted
  (`dotnet dev-certs https --check`, else `dotnet dev-certs https --trust`).
- Example run (PowerShell), from repo root:
  `$env:DOTNET_ROLL_FORWARD='Major'; $env:UseOnlyInMemoryDatabase='true'; dotnet run --project src/PublicApi`
  with the PayPal env vars (see §3) also set in the same shell.

---

## 2. Scope — endpoints to deliver

All under `/api/`, on PublicApi, JWT-authenticated, identity from the token. Each action is a
**separate** route (no do-everything call). Response bodies that create something expose the new
id as a **top-level field**.

| # | Method & route | Role | Purpose | Top-level id in response |
|---|----------------|------|---------|--------------------------|
| 1 | `POST /api/orders` | shopper | Place order from catalog item ids+quantities; state = **awaiting payment** | `orderId` |
| 2 | `POST /api/orders/{orderId}/pay` | shopper (owner) | **Authorize** total (hold, no capture); card details **or** saved-card id | — |
| 3 | `POST /api/orders/{orderId}/fulfil` | **admin** | Mark fulfilled → **capture**; record captured/fee/net; renew stale auth | — |
| 4 | `POST /api/orders/{orderId}/cancel` | **admin** | Cancel before fulfilment → **void** (release hold) | — |
| 5 | `POST /api/orders/{orderId}/refunds` | shopper (owner) | Refund captured payment, full/partial; caller idempotency key | `refundId` |
| 6 | `GET /api/my-orders` | shopper | Caller's orders + payment state | — |
| 7 | `GET /api/reconciliation?from={}&to={}` | **admin** | PayPal transactions vs eShop orders over a date range | — |
| 8 | `POST /api/payment-methods` | shopper | Save a card (vault); safe description only | `paymentMethodId` |
| 9 | `GET /api/payment-methods` | shopper | Caller's saved cards (safe fields) | — |
| 10 | `DELETE /api/payment-methods/{paymentMethodId}` | shopper (owner) | Remove saved card; no longer listed or usable | — |

**Ownership rule (enforce on every shopper endpoint):** load the entity, compare its owner
(`Order.BuyerId` / `Buyer.IdentityGuid`) to `user.Identity.Name`; if different, return **404**
(not 403 — don't reveal existence of another shopper's resource). Admin endpoints may act on any
order but still resolve the order by id.

> **Exact role split (follow precisely, per task):** operator/admin = **fulfil, cancel,
> reconciliation** only. **Everything else is shopper-scoped**, including **`/refunds`** and
> **`/pay`**. Yes, refunds is shopper-scoped in this task.

---

## 3. Configuration & secrets

Bind a strongly-typed options class from the **`PayPal:`** section using **exactly** these keys.
Hard-code none of the values.

| Options property | Config key | Env var | Notes |
|---|---|---|---|
| `ClientId` | `PayPal:ClientId` | `PAYPAL_CLIENT_ID` | REST client id (sandbox business acct) |
| `ClientSecret` | `PayPal:ClientSecret` | `PAYPAL_CLIENT_SECRET` | secret — **never log, never persist** |
| `Environment` | `PayPal:Environment` | `PAYPAL_ENVIRONMENT` | e.g. `sandbox` |
| `Currency` | `PayPal:Currency` | `PAYPAL_CURRENCY` | ISO-4217, e.g. `USD` |
| `BaseUrl` | `PayPal:BaseUrl` | — | **optional override** |

- `Program.cs` already calls `builder.Configuration.AddEnvironmentVariables();`. .NET config maps
  `PAYPAL_CLIENT_ID` → `PayPal:ClientId` only if you use the double-underscore form
  (`PayPal__ClientId`). Since the task supplies single-underscore env var **names**, add an
  explicit mapping in Program.cs so the `PayPal:` section is populated from them, e.g. build an
  in-memory dictionary that reads `Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID")` etc.
  and `AddInMemoryCollection` under keys `PayPal:ClientId`…`PayPal:Currency`, **without ever
  writing the values to disk**. `PayPal:BaseUrl` comes from config/appsettings only (optional).
- **Base URL resolution (single rule, used by EVERY PayPal call including the OAuth token call):**
  - If `PayPal:BaseUrl` is set (non-empty) → use it **verbatim** as the API base address.
  - Else derive from `PayPal:Environment`: `sandbox` → `https://api-m.sandbox.paypal.com`,
    `live`/`production` → `https://api-m.paypal.com`. (Sandbox base URL confirmed in
    `/guides/authentication.mdx`.) Target **sandbox** for all dev/test.
- **Secrets rule:** never write credential values into any repo file (this PLAN included),
  logs, or responses. Only names may appear.
- Do **not** add PayPal keys to `appsettings.json`. You may add an empty/optional
  `"PayPal": { "BaseUrl": "" }` placeholder if convenient, but it's not required.

---

## 4. PayPal integration layer (Infrastructure)

Create a `PayPal` folder in `src/Infrastructure` (e.g. `Infrastructure/PayPal/`). Register via a
new `PayPalDependencies.ConfigureServices(config, services)` called from `PublicApi/Program.cs`
(next to the existing `Infrastructure.Dependencies.ConfigureServices`). Use
`services.AddHttpClient<PayPalClient>()` (typed client). Keep all PayPal HTTP concerns here;
endpoints call a thin application-facing interface (e.g. `IPayPalPaymentGateway`) so endpoints
never touch raw HTTP/JSON.

### 4.1 OAuth token (`/guides/authentication.mdx`, `/guides/making-requests.mdx`)
- `POST {base}/v1/oauth2/token`, HTTP Basic auth = `ClientId:ClientSecret`,
  `Content-Type: application/x-www-form-urlencoded`, body `grant_type=client_credentials`.
- Response: `access_token` + `expires_in` (seconds, ~8h in sandbox). **Cache** the token in
  memory (e.g. `IMemoryCache`, already registered) until near expiry (refresh ~60s early).
- Attach `Authorization: Bearer <token>` to every other call. On a `401`, refresh once and retry.

### 4.2 Common request conventions (`/guides/making-requests.mdx`)
- Headers on money-moving POSTs: `Content-Type: application/json`, `Accept: application/json`,
  `Prefer: return=representation` (so responses carry full resource incl. ids/breakdowns), and
  **`PayPal-Request-Id`** for idempotency (see §7).
- Dates/times are RFC 3339 UTC (`2025-06-22T11:00:00Z`).
- **Always capture `debug_id`** from error responses into logs (never log secrets/PAN). Error
  body shape (`name`, `message`, `debug_id`, `details[]{issue,field,description}`) is documented
  on every endpoint's 4xx and in `/reference/errors.mdx`.
- Retry policy: retry only idempotent calls / calls carrying a stable `PayPal-Request-Id`; back
  off on `429`/`5xx` with jitter. Keep it simple (a couple of retries).

### 4.3 Money formatting
- All amounts are strings with currency: `{ "currency_code": "<PayPal:Currency>", "value": "12.34" }`.
- Format `Order.Total()` to the currency's decimal places (2 for USD) using invariant culture.
  The amount authorized/held **must equal the order total to the cent** — compute once, reuse the
  same string for authorize and (full) capture.

### 4.4 3DS / browser-challenge STOP condition (task-mandated)
- Use card verification default `SCA_WHEN_REQUIRED` (i.e. do not force SCA). With the US sandbox
  business account + Visa test card, no challenge is expected.
- If any create-order / authorize / capture / vault response indicates a required buyer browser
  step — e.g. order `status = "PAYER_ACTION_REQUIRED"`, or a HATEOAS `links[]` entry with
  `rel` in {`payer-action`, `approve`}, or a 3DS contingency link — **STOP**: return a clear
  error to the caller (e.g. HTTP 502 with a message like "PayPal requires browser approval /
  3DS challenge — reported as a stop condition per task; no approval round-trip implemented")
  and log it. **Do not** build an approval/redirect round-trip.

---

## 5. Domain & persistence changes (ApplicationCore + Infrastructure)

Keep changes additive; do not alter existing catalog/basket behavior.

### 5.1 Order payment/fulfilment state
The existing `Order` currently has no status or payment. Add:

- **`OrderStatus` enum** (ApplicationCore/Entities/OrderAggregate): `AwaitingPayment`,
  `Authorized`, `Fulfilled`, `Cancelled`, `Refunded`, `PartiallyRefunded`. Add a
  `public OrderStatus Status { get; private set; }` to `Order` (default `AwaitingPayment`) with
  guarded transition methods.
- **`Payment` entity** (new), one-per-order, holding the state PayPal owns so later requests can
  act on it. Model it as a **separate aggregate** keyed by `OrderId` (simplest for EF Core
  in-memory) — `Payment : BaseEntity, IAggregateRoot` with:
  - `int OrderId`, `string Currency`
  - `string PaypalOrderId` (the `/v2/checkout/orders` id)
  - `string? AuthorizationId`, `string? AuthorizationStatus` (CREATED/…),
    `DateTimeOffset? AuthorizationExpiresAt`
  - `string? CaptureId`, `string? CaptureStatus`
  - `decimal AuthorizedAmount`, `decimal? CapturedAmount`, `decimal? PayPalFee`,
    `decimal? NetAmount`
  - `decimal RefundedTotal` (running sum)
  - owned collection `Refunds` : `{ string RefundId, decimal Amount, string Status,
    string IdempotencyKey, DateTimeOffset CreatedAt }`
  - `bool? VaultedCard` / `string? VaultTokenCreated` if a card was saved during pay (optional)
  - domain methods: `MarkAuthorized(authId, status, expiresAt)`,
    `MarkCaptured(captureId, status, captured, fee, net)`, `MarkVoided()`,
    `AddRefund(refundId, amount, status, key)` with the invariant
    **`RefundedTotal + amount <= CapturedAmount`** (throw a domain exception otherwise), and
    `HasRefundForKey(key)` for idempotent replay.
  - Rationale for separate aggregate vs. child-of-Order: either satisfies "reuse the existing
    order model" (that requirement is about `POST /api/orders` reusing `Order`/`OrderItem`, which
    it does). A separate `Payment` keeps EF mapping trivial under the in-memory provider and keeps
    `Order` the shipping/items aggregate. If you prefer, you may instead make `Payment` an owned
    reference under `Order`; both are acceptable — pick one and be consistent.
- **EF config / DbSets** in `Infrastructure/Data`:
  - `CatalogContext`: add `public DbSet<Payment> Payments { get; set; }` (and keep existing
    `Orders`). Add `OrderConfiguration` change for the new `Status` (store as int/string) and a
    `PaymentConfiguration` mapping decimals to `decimal(18,2)`, `Refunds` as an owned collection.
  - `Order.Status` needs no migration (in-memory).

### 5.2 Saved cards (reuse Buyer + PaymentMethod)
- **Wire up the existing `Buyer` aggregate:** add `public DbSet<Buyer> Buyers { get; set; }` to
  `CatalogContext` and a `BuyerConfiguration` (`IdentityGuid` key/index; owned collection
  `PaymentMethods`). `Buyer.IdentityGuid` = the shopper username (email).
- **Extend `PaymentMethod`** (additive) to carry safe descriptors + the vault token:
  - keep `Alias`, `Last4`; repurpose/keep `CardId` = **PayPal vault token id**
    (`/v3/vault/payment-tokens` `id`).
  - add `string? Brand` (e.g. `VISA`), `string? Expiry` (`YYYY-MM`), and a domain ctor/factory.
  - **Never** add PAN/CVV fields. Full card details are never stored.
- Add `string? PayPalCustomerId` to `Buyer` to hold the PayPal-generated `customer.id` returned
  on first vault, so later saves group under the same customer (optional but tidy).
- Provide a small helper to get-or-create the caller's `Buyer` by username on first save.

> **Why app-DB-owned saved cards:** ownership/authorization is enforced by the app (a
> `PaymentMethod` belongs to exactly one `Buyer`), so one shopper can never see/use/delete
> another's regardless of PayPal-side grouping. `GET /api/payment-methods` reads from the app DB
> (source of truth for the list); PayPal Vault is the PCI store for the card itself.

---

## 6. Exact PayPal calls per flow (field-level, from MCP docs)

Currency below is always `PayPal:Currency`. `{base}` per §3. All money-moving POSTs send
`Prefer: return=representation` and a `PayPal-Request-Id` (§7).

### 6.1 `POST /api/orders` — place order (no PayPal call)
- Body: array of `{ catalogItemId, quantity }` (+ optional ship-to address; if omitted, use a
  default/placeholder address — `Order` requires a `ShipToAddress`, and `OrderConfiguration`
  marks it required).
- Load catalog items via `CatalogItemsSpecification(ids)`; build `OrderItem`s exactly like
  `ApplicationCore/Services/OrderService.cs` does (snapshot `CatalogItemOrdered` with
  `catalogItem.Id, Name, ComposePicUri(PictureUri)`, `UnitPrice = catalogItem.Price`,
  `Units = quantity`). `BuyerId = user.Identity.Name`.
- Persist `Order` (status `AwaitingPayment`). **Return top-level `orderId`** (the EF-generated
  `Order.Id`), 201.
- Validate: non-empty items, quantities ≥ 1, all ids exist (else 400).

### 6.2 `POST /api/orders/{orderId}/pay` — authorize (hold) (`/api-reference/orders/create-order.mdx`, `/api-reference/orders/authorize-payment-for-order.mdx`)
Owner-scoped. Order must be `AwaitingPayment` (idempotent replay → see §7). Request body is one
of:
- **One-off card:** `{ card: { number, expiry:"YYYY-MM", securityCode, name, billingAddress:{ countryCode, addressLine1?, adminArea1?, adminArea2?, postalCode? } } }`
- **Saved card:** `{ paymentMethodId: <app PaymentMethod id> }` → resolve to the caller's
  `PaymentMethod.CardId` (PayPal vault token id); **verify ownership** first.

**Step A — Create PayPal order** `POST {base}/v2/checkout/orders`:
```jsonc
{
  "intent": "AUTHORIZE",
  "purchase_units": [{
    "reference_id": "eshop-order-<orderId>",
    "custom_id": "<orderId>",                 // used for reconciliation matching
    "invoice_id": "eshop-<orderId>",          // unique per order (see note)
    "amount": { "currency_code": "<Currency>", "value": "<order total, 2dp>" }
  }],
  "payment_source": {
    "card": {
      // one-off:
      "number": "...", "expiry": "YYYY-MM", "security_code": "...", "name": "...",
      "billing_address": { "country_code": "US", ... }
      // OR saved card instead of the raw fields:
      // "vault_id": "<PayPal vault token id>"
    }
  }
}
```
- `PayPal-Request-Id` is **mandatory** for single-step create-order-with-card (per
  create-order.mdx). Because a valid `payment_source` is supplied, **no buyer approval is
  needed** (create-order/authorize docs: "…unless a valid `payment_source` was supplied").
- `invoice_id` uniqueness: PayPal requires `invoice_id` unique per merchant account by default.
  Since the in-memory DB resets each run, `Order.Id` can repeat across runs and collide. Use a
  run-unique invoice id, e.g. `eshop-<orderId>-<short guid>`, and **persist it on `Payment`** so
  reconciliation can match it. (`custom_id` = bare `<orderId>` for human matching.)
- Card `expiry` format is **`YYYY-MM`** (RFC 3339 year-month), e.g. `2028-04`. Convert the
  caller's expiry into this form.
- Inspect response `status`. Expected `APPROVED` (card supplied). If `PAYER_ACTION_REQUIRED` or a
  payer-action/3DS link is present → **STOP** per §4.4.

**Step B — Authorize** `POST {base}/v2/checkout/orders/{paypalOrderId}/authorize` (body `{}`,
`Prefer: return=representation`, deterministic `PayPal-Request-Id`).
- Response: `purchase_units[0].payments.authorizations[0]` →
  `{ id (authorization id), status ("CREATED"), amount, expiration_time }`
  (schema `authorization_with_additional_data` / `payment_collection` in `orders-v2.json`).
- Persist on `Payment`: `PaypalOrderId`, `AuthorizationId`, `AuthorizationStatus`,
  `AuthorizationExpiresAt (expiration_time)`, `AuthorizedAmount`, `Currency`, `invoice_id`.
- Set `Order.Status = Authorized`. Return 200 (echo order + payment state; no new id required).
- **Amount check:** assert the authorization `amount.value` equals the order total to the cent.

> Alternative equivalent design: create the PayPal order with `intent: "AUTHORIZE"` in step A and
> call `/authorize` in step B (as above). Do **not** use `intent: "CAPTURE"` here — capture must
> happen at fulfilment, not at pay.

### 6.3 `POST /api/orders/{orderId}/fulfil` — capture (admin) (`/api-reference/payments/capture-authorized-payment.mdx`, `/api-reference/payments/reauthorize-authorized-payment.mdx`)
Order must be `Authorized`. Admin role.
- **Stale-authorization renewal:** an authorization has a limited honor window
  (`AuthorizationExpiresAt`; PayPal's honor period is ~3 days, reauthorizable within a 29-day
  window; after 30 days from the original you must create a fresh authorization). Before
  capturing, if the authorization is expired/stale (past `expiration_time`, or capture returns an
  expired-authorization error), call
  `POST {base}/v2/payments/authorizations/{authorizationId}/reauthorize` with body
  `{ "amount": { "currency_code": "<Currency>", "value": "<total>" } }`. On success it returns a
  **new** authorization `{ id, status }` — persist the new `AuthorizationId`/expiry and capture
  against it.
  - If reauthorize is no longer possible (e.g. >30 days, or PayPal rejects it), **do not** fail
    silently: return an **operator-actionable** error (HTTP 409) with a clear message, e.g.
    "Authorization can no longer be renewed (past reauthorization window). Ask the shopper to pay
    again / create a new order." Keep `Order.Status = Authorized` so the operator can act.
  - Note: in a single sandbox run the honor period will not elapse, so capture normally succeeds
    directly; the reauthorize path is defensive and must still be implemented + reachable.
- **Capture** `POST {base}/v2/payments/authorizations/{authorizationId}/capture`, body
  `{ "final_capture": true }` (omit `amount` to capture the full authorized amount),
  `Prefer: return=representation`, deterministic `PayPal-Request-Id`.
  - Response: `{ id (capture id), status ("COMPLETED"), amount, final_capture,
    seller_receivable_breakdown: { gross_amount, paypal_fee, net_amount } }`
    (schema `seller_receivable_breakdown` in `payments-v2.json`; note `paypal_fee`/`net_amount`
    are absent while a capture is `PENDING`).
  - Persist on `Payment`: `CaptureId`, `CaptureStatus`,
    `CapturedAmount = seller_receivable_breakdown.gross_amount`,
    `PayPalFee = seller_receivable_breakdown.paypal_fee`,
    `NetAmount = seller_receivable_breakdown.net_amount`.
  - Set `Order.Status = Fulfilled`. Return 200 with captured/fee/net.

### 6.4 `POST /api/orders/{orderId}/cancel` — void (admin) (`/api-reference/payments/void-authorized-payment.mdx`)
- Only valid **before** fulfilment (`Order.Status = Authorized`). Reject if `Fulfilled` (409 —
  "already captured; use refunds").
- `POST {base}/v2/payments/authorizations/{authorizationId}/void` (no body needed; you may send
  `Prefer: return=representation`). Releases the hold — no money moved.
- Set `Order.Status = Cancelled`, `Payment.AuthorizationStatus = VOIDED`. Idempotent: if already
  `Cancelled`, return 200 without re-calling.

### 6.5 `POST /api/orders/{orderId}/refunds` — refund (shopper owner) (`/api-reference/payments/refund-captured-payment.mdx`)
- Only after fulfilment (`Order.Status` in {`Fulfilled`, `PartiallyRefunded`}). Owner-scoped.
- Request body: `{ amount? (partial; omit for full remaining), idempotencyKey (required) }`.
- **Idempotency (caller-supplied key):** if `Payment.HasRefundForKey(key)` → return the stored
  `refundId` (no second refund). Else send the key as the **`PayPal-Request-Id`** header
  (PayPal stores refund request keys 45 days) so a network retry with the same key is also safe
  server-side. Two **different** keys = two legitimate partial refunds.
- **Over-refund guard (app-side, before calling PayPal):** reject if
  `RefundedTotal + requestedAmount > CapturedAmount` (400/409). Full refund = `CapturedAmount −
  RefundedTotal`. This guarantees a partly-refunded order never becomes refundable beyond capture.
- Call `POST {base}/v2/payments/captures/{captureId}/refund`:
  - Full: empty JSON body `{}`. Partial: `{ "amount": { "currency_code": "<Currency>",
    "value": "<amount>" }, "custom_id": "<orderId>", "invoice_id": "<same invoice id>" }`.
  - Response: `{ id (refund id), status ("COMPLETED"), amount,
    seller_payable_breakdown: { total_refunded_amount, ... } }`.
  - Persist a `Refund` on `Payment` (refundId, amount, status, key); set
    `RefundedTotal = seller_payable_breakdown.total_refunded_amount` (authoritative running
    total). Set `Order.Status = Refunded` if fully refunded else `PartiallyRefunded`.
  - **Return top-level `refundId`**, 201.

### 6.6 `GET /api/my-orders` — shopper's orders + payment state
- Query `Order`s where `BuyerId == user.Identity.Name` (use a spec like the existing
  `CustomerOrdersWithItemsSpecification`), join each with its `Payment`. Return per order:
  `orderId`, `orderDate`, `total`, `status`, and payment summary (`authorizationId?`,
  `captureId?`, `capturedAmount?`, `paypalFee?`, `netAmount?`, `refundedTotal`, refunds list).
  Never include PAN/CVV.

### 6.7 `GET /api/reconciliation?from={}&to={}` — admin (`/api-reference/transaction-search/list-transactions.mdx`, `/guides/transaction-search.mdx`)
- `from`/`to` are ISO-8601 date-times. Validate `from <= to` (400 otherwise).
- PayPal `GET {base}/v1/reporting/transactions` requires `start_date`/`end_date` (RFC 3339,
  **seconds required**) and supports a **maximum 31-day window** per request, `page`/`page_size`
  (default page_size 100), and returns `total_pages`/`total_items` for paging.
- **Cover the whole requested range (not just the first page):**
  1. Split `[from, to]` into consecutive **≤31-day** sub-windows.
  2. For each sub-window, request `page=1`, then loop `page=2..total_pages`, accumulating
     `transaction_details[]`. Use `fields=transaction_info` (enough for matching) or `fields=all`.
- **Match** each PayPal transaction to eShop orders using
  `transaction_info.custom_field` (= our `custom_id` = `<orderId>`) and/or
  `transaction_info.invoice_id` (= our persisted invoice id). Also read
  `transaction_info.transaction_id`, `transaction_amount`, `fee_amount`, `transaction_status`.
- Produce a three-way report:
  - **Matched** — PayPal txn ↔ eShop order (compare amounts/status).
  - **In PayPal, not in eShop** — transactions with no matching order (or no custom/invoice id).
  - **In eShop, not in PayPal** — captured/authorized orders in the window with no PayPal txn yet.
- **Sandbox lag is expected:** transactions can take up to ~3 hours to appear in reporting. A
  reconciliation over a range you just created may legitimately return **empty** — that is a
  correct sandbox result, **not** a gap. Build the report to be correct over a range that has
  data; do not report the empty recent range as a failure. State this in the endpoint's docs.

### 6.8 `POST /api/payment-methods` — save a card (`/api-reference/vault/create-payment-token-for-a-given-payment-source.mdx`)
- Body: `{ card: { number, expiry:"YYYY-MM", securityCode, name, billingAddress:{ countryCode, ... } }, alias? }`.
- `POST {base}/v3/vault/payment-tokens` (PayPal-Request-Id required — stored 3h):
```jsonc
{
  "payment_source": { "card": { "number":"...", "expiry":"YYYY-MM", "security_code":"...",
                                "name":"...", "billing_address": { "country_code":"US", ... } } },
  "customer": { "id": "<Buyer.PayPalCustomerId if known>" }   // omit on first save
}
```
  - The account is enabled for direct card vaulting, so a **card `payment_source` vaults without
    browser approval**. If the response instead returns an approval/3DS link or requires a setup
    token approval → **STOP** per §4.4 (do not build the approval round-trip).
  - Response: `{ id (vault token), customer: { id }, payment_source: { card: { last_digits,
    brand, expiry, name } } }`.
- Persist under the caller's `Buyer`: `PaymentMethod{ CardId = <vault token id>,
  Last4 = card.last_digits, Brand = card.brand, Expiry = card.expiry, Alias }`. Store the
  returned `customer.id` on `Buyer.PayPalCustomerId` if not set.
- **Return top-level `paymentMethodId`** (the app `PaymentMethod.Id`), 201. Response describes
  the card **safely** (brand + last4 + expiry + alias) — **never** full details.
- (Two-step alternative `POST /v3/vault/setup-tokens` → `POST /v3/vault/payment-tokens` with
  `payment_source.token{id,type:SETUP_TOKEN}` exists and is documented, but the direct-card
  single call above is sufficient here and avoids an approval step. If direct card vaulting is
  rejected by the account, that is still not a gap — but for this enabled sandbox account it is
  the supported path.)

### 6.9 `GET /api/payment-methods` — list saved cards
- Read the caller's `Buyer.PaymentMethods` from the app DB. Return `[{ paymentMethodId, brand,
  last4, expiry, alias }]`. No PAN/CVV. (App DB is the authoritative list; PayPal
  `GET /v3/vault/payment-tokens?customer_id=` is available if cross-checking is ever wanted.)

### 6.10 `DELETE /api/payment-methods/{paymentMethodId}` — remove saved card (`/api-reference/vault/delete-payment-token.mdx`)
- Owner-scoped: 404 if the id isn't the caller's.
- `DELETE {base}/v3/vault/payment-tokens/{vaultTokenId}` → **204** on success.
- Remove the `PaymentMethod` from the `Buyer` and save. Afterwards it must not appear in
  `GET /api/payment-methods` and must not be usable in `POST .../pay` (pay resolves the vault id
  from the app record, which is now gone → 404/400). Treat "already deleted at PayPal (404)" as
  success (still remove the local record).

---

## 7. Idempotency design (summary)

Payment operations must be idempotent in effect (a double-click never authorizes/captures twice).

- **`PayPal-Request-Id`** is PayPal's idempotency key (`/guides/making-requests.mdx`). Use
  **deterministic** values derived from stable ids so retries reuse the same key:
  - Create PayPal order (pay): `pay-order-<orderId>`
  - Authorize: `authorize-<orderId>`
  - Capture (fulfil): `capture-<orderId>` (or `-<authorizationId>` after a reauthorize)
  - Void (cancel): `void-<orderId>`
  - Refund: the **caller-supplied idempotency key** (verbatim)
  - Vault create: `vault-<buyerId>-<hash>` or a fresh per-request key
- **App-side state guards** (belt-and-suspenders, and required because the in-memory store is the
  source of truth for order state): before each transition, check `Order.Status` /
  `Payment` fields and, if the operation was already done, **return the existing result** instead
  of calling PayPal again (e.g. `/pay` on an already-`Authorized` order returns the existing
  authorization; `/fulfil` on `Fulfilled` returns the existing capture).
- **Refund replay:** `Payment.HasRefundForKey(key)` returns the stored `refundId`; distinct keys
  create distinct partial refunds, bounded by the over-refund guard (§6.5).

---

## 8. Endpoint implementation notes (conventions)

1. **Location:** put endpoints under `src/PublicApi/OrderEndpoints/`,
   `src/PublicApi/PaymentMethodEndpoints/`, `src/PublicApi/ReconciliationEndpoints/`. One class
   per route implementing `IEndpoint<IResult, TRequest>` (or `IEndpoint<IResult, TRequest, TDep>`).
   Mirror the folder+`.Request`/`.Response` partial-file naming used by `CatalogItemEndpoints`.
2. **Auth attributes** on the `MapPost/MapGet/...` delegate:
   - shopper: `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`
   - admin: `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
     AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`
3. **Identity:** inject `ClaimsPrincipal user` into the delegate; buyer id = `user.Identity!.Name`.
4. **Routes** exactly as in §2 (note the app convention omits the leading slash in `MapPost`
   strings, e.g. `"api/orders"`, `"api/orders/{orderId}/pay"` — routing is identical).
5. **Swagger:** add `.Produces<TResponse>()` and `.WithTags("OrderEndpoints")` etc. like existing
   endpoints so they show in the existing Swagger UI.
6. **Response ids:** `orderId`, `paymentMethodId`, `refundId` are **top-level** fields on the
   respective create responses (task requirement). Other response shapes are your choice.
7. **Error mapping:** either return typed results (`Results.NotFound()`,
   `Results.BadRequest(...)`, `Results.Conflict(...)`, `Results.Json(err, statusCode: 502)`), or
   add exception types and extend `ExceptionMiddleware`. Keep messages operator-friendly for the
   fulfil/reauthorize failure case (§6.3). Never leak card data or PayPal secrets in errors.

---

## 9. Security & PCI

- **No PAN/CVV ever stored** in the app DB and **never logged.** Card number/CVV live only in the
  request body long enough to forward to PayPal (Orders create / Vault create). Do not persist
  request bodies; scrub logging so card fields and the client secret never appear (avoid logging
  raw PayPal request/response bodies for these calls; log ids + `debug_id` only).
- Saved-card display uses only `brand + last4 + expiry + alias`.
- Ownership enforced on every shopper resource (orders, saved cards) as in §2.
- The client secret is read from config (§3) and used only for the OAuth token call; never
  written to disk, logs, or responses.

---

## 10. Build order (suggested)

1. Options + config mapping (§3); base-URL resolver; `PayPalDependencies` + typed `PayPalClient`
   with token caching (§4). Add a tiny temporary log line proving a token is obtained (then
   remove/quiet it).
2. Domain: `OrderStatus`, `Order.Status`, `Payment` (+ `Refund`), Buyer/PaymentMethod
   extensions; EF `DbSet`s + configs; register repos (generic `IRepository<>` already covers new
   aggregates).
3. Application gateway interface (`IPayPalPaymentGateway`) with methods: `AuthorizeAsync`,
   `CaptureAsync`, `ReauthorizeAsync`, `VoidAsync`, `RefundAsync`, `SaveCardAsync`,
   `DeleteCardAsync`, `SearchTransactionsAsync`. Implement over `PayPalClient`.
4. Endpoints in this order: `POST /api/orders` → `POST /pay` → `POST /fulfil` →
   `POST /refunds` → `POST /cancel` → `GET /my-orders` → payment-methods (POST/GET/DELETE) →
   `GET /reconciliation`.
5. Wire `PayPalDependencies.ConfigureServices(...)` into `PublicApi/Program.cs` and the config
   mapping. Confirm `AddEndpoints()`/`MapEndpoints()` auto-discovers the new endpoints.

---

## 11. Self-verification (build session must actually run this)

Run PublicApi with `DOTNET_ROLL_FORWARD=Major`, `UseOnlyInMemoryDatabase=true`, the PayPal env
vars set, on the assigned port block. Do **everything within one process run** (in-memory store
resets on restart). Use the sandbox test card: Visa **4111 1111 1111 1111**, any future expiry
(`YYYY-MM`), any CVC, any name, any billing address.

1. **Tokens:** `POST /api/authenticate` with `demouser@microsoft.com` / `Pass@word1` → shopper
   JWT; and `admin@microsoft.com` / `Pass@word1` → admin JWT.
2. **Order + pay (one-off card):** `POST /api/orders` (some catalog item ids/quantities) → get
   `orderId`; `POST /api/orders/{orderId}/pay` with the test card → order `Authorized`; confirm a
   real authorization id and that the held amount == order total.
3. **Fulfil (capture):** admin `POST /api/orders/{orderId}/fulfil` → order `Fulfilled`; confirm
   captured amount, `paypalFee`, `netAmount` present.
4. **Refund:** shopper `POST /api/orders/{orderId}/refunds` (full or partial) with an idempotency
   key → get `refundId`; repeat the **same** key → same `refundId`, no second refund; a partial
   then another partial (distinct keys) works and cannot exceed captured.
5. **Cancel/void:** on a **second** order, `POST /api/orders/{id2}/pay` then admin
   `POST /api/orders/{id2}/cancel` → `Cancelled`, hold released; verify capture is rejected
   afterward.
6. **Saved card + reuse:** `POST /api/payment-methods` with the test card → `paymentMethodId`;
   `GET /api/payment-methods` shows brand+last4; place a **third** order and
   `POST /api/orders/{id3}/pay` with `{ paymentMethodId }` → authorizes; fulfil it. Then
   `DELETE /api/payment-methods/{paymentMethodId}` → gone from the list and unusable.
7. **Idempotency:** double-`POST` `/pay` and `/fulfil` → no duplicate authorization/capture.
8. **Reconciliation:** `GET /api/reconciliation?from=&to=` over a wide past range → report
   renders and pages the whole range; note a just-created range may be empty (sandbox lag) — that
   is expected, not a failure.
9. **Build:** `dotnet build` clean; existing tests still pass.

Then give the reviewer a concise step-by-step curl/Postman guide mirroring the above (get bearer
token from `/api/authenticate` first — the storefront cookie does not work against PublicApi).

---

## 12. Gaps

**None.** Every capability this integration requires is covered by the paypal-docs MCP server:
- Auth token — `/guides/authentication.mdx`
- Create order + direct card + vault-on-file — `/api-reference/orders/create-order.mdx`,
  `orders-v2.json` (`payment_source.card`, `card_request`, `vault_id`)
- Authorize / capture / reauthorize / void — `/api-reference/orders/authorize-payment-for-order.mdx`,
  `/api-reference/payments/{capture-authorized-payment,reauthorize-authorized-payment,void-authorized-payment}.mdx`
- Refund (full/partial, idempotent, totals) — `/api-reference/payments/refund-captured-payment.mdx`
- Vault save/list/delete — `/api-reference/vault/{create-payment-token-for-a-given-payment-source,list-all-payment-tokens,delete-payment-token}.mdx`
- Reconciliation — `/api-reference/transaction-search/list-transactions.mdx`,
  `/guides/transaction-search.mdx`

If, during the build, PayPal returns a **browser/3DS approval requirement** for a card payment or
a card vault, that is the one **STOP-and-report** condition (§4.4) — report it; do not build an
approval round-trip. The empty recent reconciliation range is **not** a gap (§6.7).

---

## 13. Constraints recap (for the builder)

- Additive only; don't break existing catalog/basket/order flow or the Web project.
- Secrets (credential **values**) never enter the repo, logs, or responses — names only.
- Use the **paypal-docs** MCP for every PayPal detail; no web search / general knowledge.
- Each action is a separate route; no do-everything endpoint.
- No new infrastructure/deps beyond the .NET SDK/runtime (HttpClientFactory + System.Text.Json
  are already in the shared framework; central package management is on if a package is truly
  needed).
