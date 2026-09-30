# PLAN — PayPal payments & saved cards for eShopOnWeb

This is the implementation plan for adding **PayPal** card payments (authorize → capture at
fulfilment → cancel/refund) and **saved cards** to the eShopOnWeb PublicApi. It is additive:
the existing catalog/basket/order flow is untouched. A later build session executes this plan;
it has the same task text and the same PayPal tooling but **not** this conversation, so
everything needed is written here.

Repo root: the solution `eShopOnWeb.sln`. Target framework **net8.0**, versions pinned centrally
in `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`, so new `PackageReference`
entries carry **no** `Version` attribute — add the version to `Directory.Packages.props`).

---

## 0. Environment preconditions (this machine)

Do these before building/running (they are environment realities, not code):

- **SDK/runtime roll-forward.** `global.json` pins SDK `8.0.x` but only .NET 10 SDK is present and
  the ASP.NET Core 8.0 runtime is missing. Run with `DOTNET_ROLL_FORWARD=Major`. (Do **not** edit
  `global.json` unless necessary; env var is the least invasive.)
- **In-memory DB only.** Run with `UseOnlyInMemoryDatabase=true` (read in
  `src/Infrastructure/Dependencies.cs`). Consequences to design around:
  - Migrations are ignored by the in-memory provider, and **all data is lost on restart**. So
    place/pay/fulfil/refund must all happen **within one process run**. Still author a real EF
    migration for correctness on SQL Server, but do not rely on it at runtime here.
  - Web and PublicApi each hold their **own isolated** in-memory store. That is exactly why
    `POST /api/orders` exists — the whole flow must be drivable through PublicApi alone.
- **Ports.** Bind only to the assigned block (`APP_PORT_BLOCK_BASE … +APP_PORT_BLOCK_SIZE-1`;
  `launchSettings.json` already targets it). Stop any prior instance before starting a new one.
- **HTTPS dev cert.** Both hosts use `UseHttpsRedirection()`; ensure `dotnet dev-certs https --check`
  passes.
- **Auth model.** PublicApi is JWT. Get a bearer token from `POST /api/authenticate` first
  (seeded admin: `admin@microsoft.com` / password constant `AuthorizationConstants.DEFAULT_PASSWORD`;
  seeded normal user `demouser@microsoft.com`). For local tests you can also mint tokens with the
  same secret (see §12).
- Do not introduce Docker/broker/Postgres/any new infra.

---

## 1. Existing code the plan builds on (exact locations)

**Endpoint style (use this one for all new endpoints):** `IEndpoint<TResponse,TRequest,TDeps...>`
from the `MinimalApi.Endpoint` package — the pattern used by every catalog endpoint, e.g.
`src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`. Route declared in
`AddRoute(IEndpointRouteBuilder app)` via `app.MapPost/MapGet/...`; dependencies are injected as
**route-lambda parameters** (from DI), not the constructor; `[Authorize(...)]` goes on the lambda;
finish with `.Produces<TResponse>().WithTags("...")`. Registered by `builder.Services.AddEndpoints();`
+ `app.MapEndpoints();` already in `src/PublicApi/Program.cs`. (The Ardalis `EndpointBaseAsync`
style is used only by `AuthenticateEndpoint`; do **not** use it for new work.)

- Request/response DTOs: `Request : BaseRequest`, `Response : BaseResponse` (`src/PublicApi/BaseRequest.cs`,
  `BaseResponse.cs`, `BaseMessage.cs`). Responses are constructed with the request correlation id:
  `new XxxResponse(request.CorrelationId())`. One request + one response class per endpoint, in the
  endpoint's folder.
- Swagger: `AddSwaggerGen` with `EnableAnnotations()` and a `Bearer` security scheme is already set
  up in `Program.cs`; new endpoints appear automatically. Use `[SwaggerOperation(...)]` only if you
  need a summary (optional).

**Auth / identity:**
- JWT config in `src/PublicApi/Program.cs` (symmetric key = `AuthorizationConstants.JWT_SECRET_KEY`
  from `src/ApplicationCore/Constants/AuthorizationConstants.cs`; `ValidateIssuer/Audience=false`).
- Admin gate: `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
  AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`. The role string is
  **`"Administrators"`** (`src/BlazorShared/Authorization/Constants.cs`).
- Shopper identity: token carries `ClaimTypes.Name` = username (see
  `src/Infrastructure/Identity/IdentityTokenClaimService.cs`). **No existing PublicApi endpoint reads
  the caller identity yet** — obtain it by adding `ClaimsPrincipal user` (or `HttpContext`) to the
  route lambda and reading `user.Identity!.Name`. Use that string as the **BuyerId / owner key** for
  orders and saved cards (mirrors the Web storefront, which uses `User.Identity.Name`). Reject with
  `Results.Unauthorized()` if null/empty.

**Order aggregate** — `src/ApplicationCore/Entities/OrderAggregate/`:
`Order : BaseEntity, IAggregateRoot` (`BuyerId`, `OrderDate`, `ShipToAddress`, private `_orderItems`,
`Total()`), `OrderItem`, value objects `Address` (Street/City/State/Country/ZipCode) and
`CatalogItemOrdered` (CatalogItemId/ProductName/PictureUri). **Order has no payment or status fields
today and there are no order enums anywhere** — we add them (§4). Order create flow today:
`src/ApplicationCore/Services/OrderService.cs` (`CreateOrderAsync(int basketId, Address)`) is
basket-centric; we add a catalog-ids overload (§6). Read spec `OrderWithItemsByIdSpec.cs`,
customer-scoped `CustomerOrdersWithItemsSpecification.cs`.

**Catalog / prices:** `CatalogItem.Price` (`src/ApplicationCore/Entities/CatalogItem.cs`). Fetch by
ids with `CatalogItemsSpecification(ids)` via `IRepository<CatalogItem>`.

**Persistence:** generic `IRepository<T>` / `IReadRepository<T>` (Ardalis.Specification) implemented by
`src/Infrastructure/Data/EfRepository.cs` over `CatalogContext`
(`src/Infrastructure/Data/CatalogContext.cs`). `IRepository<T>`/`IReadRepository<T>` are constrained
to `IAggregateRoot`, so any new **directly repo-accessed** entity must implement `IAggregateRoot`.
`CatalogContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly(...)`, so **any new
`IEntityTypeConfiguration<T>` placed in the Infrastructure assembly is auto-discovered**. Existing
configs live in `src/Infrastructure/Data/Config/`. Migrations in
`src/Infrastructure/Data/Migrations/`.

> **Note — `Buyer`/`PaymentMethod` are code-only stubs.** `src/ApplicationCore/Entities/BuyerAggregate/`
> has `Buyer` and a `PaymentMethod` stub (`Alias`, `CardId` — commented "actual card data must be
> stored in a PCI compliant system", `Last4`) but they are **not persisted** (no `DbSet`, no config,
> not in the model snapshot). We introduce a persisted saved-card entity (§5).

**DI gap:** `src/PublicApi/Program.cs` does **not** register `IOrderService`/`IBasketService` (only
the Web project does, in `src/Web/Configuration/ConfigureCoreServices.cs`). The build must register
the services it needs in `Program.cs` (§10). Config uses `IOptions<T>` via
`builder.Services.Configure<T>(section)` (e.g. `CatalogSettings`, `BaseUrlConfiguration` bound from
`"baseUrls"`). `appsettings.json` lives at `src/PublicApi/appsettings.json`.

**Tests:** `tests/PublicApiIntegrationTests` (**MSTest**, `WebApplicationFactory<Program>`,
`appsettings.test.json` forces `UseOnlyInMemoryDatabase:true`, tokens minted by `ApiTokenHelper.cs`).
`tests/FunctionalTests/PublicApi` (**xUnit**, custom `TestApiApplication` swapping both DbContexts to
in-memory). Copy these patterns for new endpoint tests.

---

## 2. PayPal integration contract (researched & confirmed)

All shapes below were confirmed against official PayPal developer docs / OpenAPI schemas. The build
session must still **re-confirm before coding** (per the task), but this is the verified contract to
build to. Sources are listed in §14. **Money values are strings with 2 decimals** (e.g. `"100.00"`),
`currency_code` from config.

**Base URL & environment.** If `PayPal:BaseUrl` is set, use it **verbatim for every call including
the OAuth token request**. Otherwise derive from `PayPal:Environment`:
`sandbox` → `https://api-m.sandbox.paypal.com`, `live`/`production` → `https://api-m.paypal.com`.
Target **sandbox** for all dev/test here.

**2.1 OAuth token** — `POST {base}/v1/oauth2/token`
- Headers: `Authorization: Basic base64(ClientId:ClientSecret)`, `Content-Type: application/x-www-form-urlencoded`.
- Body: `grant_type=client_credentials`.
- Response: `{ "access_token": "...", "token_type": "Bearer", "expires_in": 32400, ... }`.
- Cache the token in memory; refresh a bit before `expires_in`. All non-token calls send
  `Authorization: Bearer {access_token}`, `Content-Type: application/json`.

**2.2 Create order + authorize inline (card)** — `POST {base}/v2/checkout/orders`
- Headers: `PayPal-Request-Id: {idempotency-key}`, `Prefer: return=representation` (so the response
  includes the authorization object).
- **Confirmed:** when a full `payment_source.card` is supplied with `intent:"AUTHORIZE"`, PayPal
  processes the authorization **inline in this one call** — no separate `/authorize` call is needed.
  The response contains `purchase_units[0].payments.authorizations[0]`.
- Body (one-off card):
```json
{
  "intent": "AUTHORIZE",
  "purchase_units": [{
    "custom_id": "{eShopOrderId}",
    "invoice_id": "{unique-reference}",
    "amount": { "currency_code": "{PayPal:Currency}", "value": "{orderTotal:F2}" }
  }],
  "payment_source": {
    "card": {
      "number": "4111111111111111",
      "expiry": "2027-02",
      "name": "Firstname Lastname",
      "billing_address": {
        "address_line_1": "2211 N First Street", "admin_area_2": "San Jose",
        "admin_area_1": "CA", "postal_code": "95131", "country_code": "US"
      },
      "attributes": { "verification": { "method": "SCA_WHEN_REQUIRED" } }
    }
  }
}
```
- Body (saved card): replace the `card` object with `{ "vault_id": "{savedVaultId}" }` (plus the same
  `attributes.verification` if desired).
- Response (relevant parts):
```json
{
  "id": "5O1...",              // PayPal order id
  "status": "COMPLETED",       // for AUTHORIZE, funds are held
  "purchase_units": [{ "payments": { "authorizations": [{
      "id": "0VF...",          // AUTHORIZATION id  -> store this
      "status": "CREATED",     // authorization status
      "amount": { "currency_code": "USD", "value": "100.00" },
      "expiration_time": "2027-..."
  }]}}],
  "payment_source": { "card": { "last_digits": "1111", "brand": "VISA" } }
}
```

**STOP condition (browser challenge).** If the response indicates the buyer must approve in a browser
— order `status == "PAYER_ACTION_REQUIRED"`, or a `links[]` entry with `rel == "payer-action"`, or
`payment_source.card.authentication_result.three_d_secure` shows
`authentication_status`/`enrollment_status` requiring a challenge (e.g. `authentication_status` in
`U`/`C` with `liability_shift: UNKNOWN`) — **do not build an approval round-trip**. Return a clear
error to the caller and, per the task, STOP and report it. With the sandbox test card
`4111 1111 1111 1111` on a direct-card-enabled account this should not occur, but handle it.

**2.3 Capture authorization (at fulfilment)** — `POST {base}/v2/payments/authorizations/{authId}/capture`
- Headers: `PayPal-Request-Id: {idempotency-key}`, `Prefer: return=representation`.
- Body: `{ "amount": { "currency_code": "...", "value": "{total:F2}" }, "final_capture": true,
  "invoice_id": "{ref}" }`.
- Response: `id` (capture id → store), `status` (`COMPLETED`|`PENDING`), and
  `seller_receivable_breakdown` = `{ gross_amount, paypal_fee, net_amount }` (each `{currency_code,value}`).
  Persist captured amount = `gross_amount`, fee = `paypal_fee`, net = `net_amount`. If the breakdown
  is momentarily absent, `GET {base}/v2/payments/captures/{captureId}` returns the same fields.

**2.4 Reauthorize (stale auth before fulfilment)** — `POST {base}/v2/payments/authorizations/{authId}/reauthorize`
- Body: `{ "amount": { "currency_code": "...", "value": "{total:F2}" } }`.
- Response: new authorization `id` (+ `status`, `expiration_time`). Use the **new** id for the capture.
- Rules: an authorization has a 3-day honor period; it can be reauthorized from day 4 to day 29;
  after 30 days you must create a fresh authorization. See §7 for the fulfilment renewal logic.

**2.5 Void authorization (cancel before fulfilment)** — `POST {base}/v2/payments/authorizations/{authId}/void`
- Success is `204 No Content` (or `200` with `Prefer: return=representation`). A fully captured
  authorization **cannot** be voided.

**2.6 Refund capture (return after fulfilment)** — `POST {base}/v2/payments/captures/{captureId}/refund`
- Headers: `PayPal-Request-Id: {caller idempotency key}`.
- Body (partial): `{ "amount": { "currency_code": "...", "value": "{refund:F2}" },
  "invoice_id": "{ref}", "custom_id": "{eShopOrderId}", "note_to_payer": "..." }`. Omit `amount` for a
  full refund.
- Response: `id` (refund id → store & return as `refundId`), `status` (`COMPLETED`|`PENDING`),
  `seller_payable_breakdown` (incl. `total_refunded_amount`).

**2.7 Saved card without purchase (Vault v3, two steps).**
- `POST {base}/v3/vault/setup-tokens` with `PayPal-Request-Id`. Body:
  `{ "payment_source": { "card": { "number", "expiry":"YYYY-MM", "name", "billing_address":{...},
  "verification_method": "SCA_WHEN_REQUIRED" } } }`. Response: `{ "id":"setupTokenId",
  "customer":{"id":"customer_..."}, "status":"APPROVED", "payment_source":{"card":{"last_digits","brand","expiry"}} }`.
  If `status != "APPROVED"` or a `links[]` `rel:"confirm"`/payer-action requires a browser →
  treat as the browser-challenge STOP condition (§2.2); do not build a redirect.
- `POST {base}/v3/vault/payment-tokens` with body
  `{ "payment_source": { "token": { "id":"setupTokenId", "type":"SETUP_TOKEN" } } }`. Response:
  `{ "id":"paymentTokenId", "customer":{"id":"customer_..."},
  "payment_source":{"card":{"last_digits","brand","expiry"}} }`. **`id` is the vault id** used later as
  `payment_source.card.vault_id`.
- Store only: our PK, owner (BuyerId), `paymentTokenId` (vault id), `customer.id`, `brand`,
  `last_digits`, `expiry`. **Never store the card number/CVV.**

**2.8 Reconciliation (Transaction Search v1)** — `GET {base}/v1/reporting/transactions`
- Query params: `start_date`, `end_date` (**ISO-8601, seconds required**, e.g.
  `2026-09-01T00:00:00Z`), `fields=all` (or `transaction_info`), `page`, `page_size` (max **500**,
  default 100), plus optional `transaction_status`.
- **Constraints:** max **31-day** range per call; data available up to 3 years back; **up to ~3-hour
  reporting lag** before a transaction is searchable.
- Response: `{ transaction_details: [ { transaction_info: { transaction_id, paypal_reference_id,
  paypal_reference_id_type, transaction_status, transaction_amount:{currency_code,value}, fee_amount,
  transaction_initiation_date, transaction_updated_date, custom_field, invoice_id } } ],
  page, total_items, total_pages, links:[{rel:"next",href,method}] }`.
- **Cover the whole [from,to] range:** split it into ≤31-day windows, and within each window page
  from `page=1` through `total_pages` (or follow `rel:"next"`). Do not stop at the first page.
- **Expected empty recent range is NOT a gap** — because of the reporting lag, a range covering
  just-created payments may legitimately return nothing. Build the report to be correct over a range
  that has data; do not report an empty recent range as a missing capability.

---

## 3. Configuration binding (`PayPal:` section)

Add a `PayPalSettings` options class and bind it from the **`PayPal`** section using exactly these
keys: `ClientId`, `ClientSecret`, `Environment`, `Currency`, `BaseUrl`. **Hard-code none of the
values.**

The credentials arrive as env vars `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`,
`PAYPAL_CURRENCY` (and optional `PAYPAL_BASE_URL`). .NET's default env provider maps `PayPal__ClientId`
(double underscore), **not** `PAYPAL_CLIENT_ID`, so add an explicit mapping in `Program.cs` **before**
`Configure<PayPalSettings>`:

```csharp
builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
    ["PayPal:ClientId"]     = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"),
    ["PayPal:ClientSecret"] = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET"),
    ["PayPal:Environment"]  = Environment.GetEnvironmentVariable("PAYPAL_ENVIRONMENT"),
    ["PayPal:Currency"]     = Environment.GetEnvironmentVariable("PAYPAL_CURRENCY"),
    ["PayPal:BaseUrl"]      = Environment.GetEnvironmentVariable("PAYPAL_BASE_URL"), // optional override
});
builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection("PayPal"));
```
(`AddInMemoryCollection` entries with `null` values are ignored, so an unset `PAYPAL_BASE_URL` leaves
`BaseUrl` empty and the client derives it from `Environment` per §2. Values set directly in a
`PayPal` section of `appsettings.json` also work — but never commit real secret values.)

Add a placeholder `"PayPal"` section (empty strings only, **no secrets**) to
`src/PublicApi/appsettings.json` for documentation, and to `appsettings.test.json` if tests need it.

`PayPalSettings` shape:
```csharp
public class PayPalSettings {
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Environment { get; set; } = "sandbox";
    public string Currency { get; set; } = "USD";
    public string? BaseUrl { get; set; }
    public string ResolvedBaseUrl() => !string.IsNullOrWhiteSpace(BaseUrl) ? BaseUrl!
        : Environment.Equals("live", StringComparison.OrdinalIgnoreCase) ||
          Environment.Equals("production", StringComparison.OrdinalIgnoreCase)
          ? "https://api-m.paypal.com" : "https://api-m.sandbox.paypal.com";
}
```

---

## 4. Domain model changes — payment/fulfilment state on the order

Add an order status enum and a `Payment` entity (a child of the **Order aggregate**, not a separate
aggregate root — Order stays the consistency boundary). Keep card data out entirely.

`src/ApplicationCore/Entities/OrderAggregate/OrderStatus.cs`:
```csharp
public enum OrderStatus { AwaitingPayment, Authorized, Fulfilled, Cancelled, PartiallyRefunded, Refunded }
```

`src/ApplicationCore/Entities/OrderAggregate/Payment.cs` (`: BaseEntity`, part of the aggregate):
- `string PayPalOrderId`
- `string AuthorizationId`, `string AuthorizationStatus`
- `string? CaptureId`, `string? CaptureStatus`
- `decimal? CapturedAmount`, `decimal? PayPalFee`, `decimal? NetAmount`
- `string Currency`
- `string? CardBrand`, `string? CardLast4` (safe descriptors only)
- `private readonly List<PaymentRefund> _refunds` + `IReadOnlyCollection<PaymentRefund> Refunds`
- helpers: `decimal TotalRefunded()`, methods to record capture/refund and update statuses.

`src/ApplicationCore/Entities/OrderAggregate/PaymentRefund.cs` (`: BaseEntity`):
- `string RefundId`, `decimal Amount`, `string Status`, `string IdempotencyKey`, `DateTimeOffset CreatedAt`.

Extend `Order` (keep private setters; add intent-revealing methods rather than public setters):
- `public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;`
- `public Payment? Payment { get; private set; }`
- methods: `AttachAuthorization(payPalOrderId, authId, authStatus, brand, last4)` →
  `Status = Authorized`; `MarkAuthorizationRenewed(newAuthId, status)`; `RecordCapture(...)` →
  `Status = Fulfilled`; `RecordRefund(refund)` → set `PartiallyRefunded`/`Refunded` based on
  `TotalRefunded()` vs `CapturedAmount`; `Cancel()` → `Status = Cancelled`.
- A refund-guard: `TotalRefunded() + newRefund <= CapturedAmount` (see §7).

**EF config** (`src/Infrastructure/Data/Config/PaymentConfiguration.cs`,
`PaymentRefundConfiguration.cs`, auto-discovered): Payment 1:1 under Order (FK `OrderId`), Refunds as a
child collection of Payment; money `decimal(18,2)`; strings with sensible max lengths; map the `Order`
→ `Payment` navigation and `Order.Status` as an int/string. Update `OrderConfiguration.cs` to include
the `Payment` navigation. Add a **migration** (`dotnet ef migrations add AddOrderPayment -c CatalogContext
-p src/Infrastructure -s src/PublicApi`) — required for SQL Server correctness; ignored by the in-memory
provider used here. No `DbSet<Payment>` is strictly required (reachable via `Order`), but adding one is
harmless.

---

## 5. Domain model — saved cards

Add a persisted, shopper-scoped saved-card aggregate root (simpler and safer than reworking the
`Buyer` stub; stores **no** card number):

`src/ApplicationCore/Entities/SavedCardAggregate/SavedCard.cs` (`: BaseEntity, IAggregateRoot`):
- `string BuyerId` (owner = caller username)
- `string PayPalVaultId` (the vault/payment-token id)
- `string PayPalCustomerId`
- `string Brand`, `string Last4`, `string Expiry` (e.g. `"2027-02"`)
- `DateTimeOffset CreatedAt`
- ctor with guard clauses; a computed `Description => $"{Brand} ending {Last4} (exp {Expiry})"`.

`src/Infrastructure/Data/Config/SavedCardConfiguration.cs` (auto-discovered) + a `DbSet<SavedCard>`
on `CatalogContext` (needed because it is a directly repo-accessed root) + the same migration (or a
second `AddSavedCard` migration). Access via `IRepository<SavedCard>` / `IReadRepository<SavedCard>`.

Ownership specs in `src/ApplicationCore/Specifications/`:
- `SavedCardsByBuyerSpecification(buyerId)` → all of a buyer's cards.
- `SavedCardByIdAndBuyerSpecification(id, buyerId)` → a single card **scoped to the owner** (returns
  none for another shopper's id → 404).

---

## 6. Application services (in `ApplicationCore`, interfaces there, impls there or Infrastructure)

Keep PayPal HTTP concerns behind an interface so the domain/services stay testable.

**`IPayPalClient`** (`src/ApplicationCore/Interfaces/IPayPalClient.cs`) — thin, typed wrapper over the
REST calls in §2, returning small result records (ids/statuses/amounts), never leaking `HttpClient`:
`AuthorizeOrderWithCardAsync(amount, currency, custom_id, invoice_id, CardDetails, idempotencyKey)`,
`AuthorizeOrderWithVaultAsync(amount, currency, custom_id, invoice_id, vaultId, idempotencyKey)`,
`CaptureAsync(authId, amount, currency, invoice_id, idempotencyKey)`,
`ReauthorizeAsync(authId, amount, currency)`, `VoidAsync(authId)`,
`RefundAsync(captureId, amount?, currency, custom_id, invoice_id, idempotencyKey)`,
`CreateSetupTokenAsync(CardDetails, idempotencyKey)`, `CreatePaymentTokenAsync(setupTokenId, idempotencyKey)`,
`ListTransactionsAsync(startUtc, endUtc, page, pageSize)`.
Impl `src/Infrastructure/Services/PayPalClient.cs`: typed `HttpClient` (via `AddHttpClient`), OAuth
token cache (thread-safe, refresh before `expires_in`), base URL from `PayPalSettings.ResolvedBaseUrl()`,
sets `PayPal-Request-Id`/`Prefer` headers, parses responses with `System.Text.Json`, maps PayPal error
bodies (`{ name, message, details[] }`) to a typed `PayPalException` carrying `name`/`debug_id` so
callers can branch (e.g. expired authorization) and surface actionable messages.

**`IPaymentService`** (`src/ApplicationCore/Interfaces/`) — orchestrates domain + PayPal for pay/
fulfil/cancel/refund, updating the `Order`/`Payment` aggregate and persisting via `IRepository<Order>`.
Encapsulates the idempotency guards (§8) and the stale-auth renewal (§7).

**`ISavedCardService`** — save/list/delete cards via `IPayPalClient` + `IRepository<SavedCard>`.

**`IReconciliationService`** — window/page the Transaction Search API and match against orders (§9).

**Order placement:** add an overload to `IOrderService`/`OrderService`:
`Task<int> CreateOrderAsync(string buyerId, IEnumerable<(int catalogItemId,int quantity)> items,
Address shipToAddress)` that reads prices via `IRepository<CatalogItem>` +
`CatalogItemsSpecification(ids)`, builds `OrderItem`s exactly like the existing method (snapshotting
`CatalogItemOrdered` with `IUriComposer.ComposePicUri`), constructs the `Order`, `AddAsync`, and
returns `order.Id`. This **reuses the existing Order/OrderItem model** (mandate) instead of a parallel
one, and does not require a persisted basket.

---

## 7. Business rules & the fulfilment renewal path

- **Amounts to the cent.** `amount.value = Order.Total().ToString("F2", CultureInfo.InvariantCulture)`;
  `currency_code = PayPal:Currency`. The authorized/captured amount equals the order total exactly.
- **Fulfil (capture) with stale-auth renewal.** In `IPaymentService.FulfilAsync`:
  1. Try `CaptureAsync(authId, total)`.
  2. If it fails because the authorization is no longer capturable (PayPal error names such as
     `AUTHORIZATION_EXPIRED` / honor-period elapsed), call `ReauthorizeAsync(authId, total)`, store the
     **new** authorization id (`Order.MarkAuthorizationRenewed`), and retry the capture against it.
  3. If reauthorization is itself not possible (past the 29-day window, or PayPal rejects it), **do not
     fail silently** — return an actionable error the operator can act on, e.g. HTTP `409` with a
     message like *"Authorization can no longer be renewed; ask the shopper to pay the order again
     (POST /api/orders/{id}/pay) to create a fresh authorization."* Order stays `Authorized` (not
     `Fulfilled`).
  4. On success: persist `CaptureId`, `CaptureStatus`, and the `seller_receivable_breakdown`
     (`CapturedAmount`, `PayPalFee`, `NetAmount`); `Status = Fulfilled`.
  > In this environment (in-memory, single run) an authorization will not actually go stale, but the
  > renewal path must exist and be correct.
- **Cancel (before fulfilment only).** Allowed when `Status == Authorized` (not captured). `VoidAsync`
  the authorization; `Status = Cancelled`. If already captured → `409` ("cancel not allowed after
  fulfilment; use refund"). Idempotent: already-cancelled returns success.
- **Refund (after fulfilment only).** Requires a capture. Enforce
  `TotalRefunded() + requested <= CapturedAmount` **before** calling PayPal → reject over-refunds with
  `422`/`409`. Full refund omits `amount`. After success, append a `PaymentRefund` and set
  `PartiallyRefunded` or `Refunded`. A partly-refunded order is never refundable beyond the captured
  amount (the guard guarantees it).

---

## 8. Idempotency (double-click safety)

Layer PayPal's `PayPal-Request-Id` with our own persisted state so a repeat never charges twice.

- **`POST /pay` (authorize).** Before calling PayPal: if `Order.Payment` already exists and is
  `Authorized`, return the existing authorization (HTTP `200`, no new PayPal call). Otherwise use a
  **stable** `PayPal-Request-Id` derived from the order (e.g. `pay-{orderId}`) so even a genuine
  double-call collapses to one PayPal order/authorization. Persist the PayPal order + authorization ids.
- **`POST /fulfil` (capture).** If already `Fulfilled` (capture id present), return the existing capture
  result. Else use a stable `PayPal-Request-Id` (e.g. `capture-{authId}`).
- **`POST /refunds` (refund).** The **caller supplies an idempotency key** (required field, e.g.
  `idempotencyKey` in the request body). Before calling: if a stored `PaymentRefund` already has that
  key, return its `refundId` (no new refund). Else pass the key as `PayPal-Request-Id` and persist it
  with the resulting refund. Two **distinct** keys for two partial refunds are both honoured (subject to
  the cumulative-≤-captured guard) — so legitimate split refunds still work.
- **`POST /payment-methods` (save card).** Use a `PayPal-Request-Id` for the setup-token/payment-token
  calls (from the request correlation id) to avoid duplicate vault entries on retry.

---

## 9. Reconciliation report (`GET /api/reconciliation`)

Admin-only. Inputs `from`, `to` (ISO-8601 date-times).
1. **Window** `[from,to]` into consecutive ≤31-day slices (Transaction Search hard limit).
2. For each slice, **page** `page=1..total_pages` (or follow `links rel:"next"`) with
   `page_size=500`, accumulating every `transaction_info`. Never stop at page 1.
3. **Match** PayPal transactions to eShop orders. Primary key: PayPal `transaction_info.transaction_id`
   equals a stored **capture id** or **refund id** on our orders. Secondary: `custom_field`/`invoice_id`
   equals the eShop order id/reference we set on capture & refund (`custom_id`/`invoice_id` in §2.3/2.6).
4. **Output three buckets** so discrepancies are visible:
   - `matched`: PayPal txn ↔ eShop order (amounts, ids, status).
   - `inPayPalNotEShop`: PayPal knows a transaction eShop has no record of.
   - `inEShopNotPayPal`: an eShop capture/refund PayPal's report doesn't (yet) list.
5. Because of the up-to-3-hour lag, `inEShopNotPayPal` for very recent activity is **expected** and must
   not be treated as an error; state this in the response/report semantics. The report is correct over a
   range that already has settled data.

---

## 10. PublicApi endpoints & DI

Register in `src/PublicApi/Program.cs` (currently missing these):
```csharp
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ISavedCardService, SavedCardService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddHttpClient<IPayPalClient, PayPalClient>();
// PayPalSettings mapping + Configure<PayPalSettings> per §3
```
(`IRepository<>`/`IReadRepository<>` are already registered generically, so `IRepository<SavedCard>`
and `IRepository<Order>` resolve without extra wiring.)

All endpoints as `IEndpoint<...>` classes under new folders in `src/PublicApi/` (e.g.
`OrderEndpoints/`, `PaymentMethodEndpoints/`, `ReconciliationEndpoints/`). Shopper endpoints read
`ClaimsPrincipal.Identity.Name` as the owner key and filter every query/command by it. Admin endpoints
carry the `[Authorize(Roles = ...ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults...)]`
attribute. Each action is a **separate route** (no do-everything endpoint).

| Method & route | Auth | Purpose / notes | Returns |
|---|---|---|---|
| `POST /api/orders` | shopper | Place order from `[{catalogItemId, quantity}]` (+ optional shipping address; default to a placeholder like the Web flow if omitted). Buyer = token identity. `Status=AwaitingPayment`. | `{ orderId, ... }` |
| `POST /api/orders/{orderId}/pay` | shopper (own order) | Body carries **either** one-off card details **or** `paymentMethodId` of a saved card. Authorizes the order total (inline, §2.2). Idempotent (§8). 404 if not caller's order. STOP on browser challenge (§2.2). | `{ orderId, status, authorizationId, ... }` |
| `POST /api/orders/{orderId}/fulfil` | **admin** | Capture at fulfilment; renew stale auth (§7). | capture result (captured/fee/net) |
| `POST /api/orders/{orderId}/cancel` | **admin** | Void auth before fulfilment (§7). | status |
| `POST /api/orders/{orderId}/refunds` | shopper (own order) | Full/partial refund; requires caller `idempotencyKey`; cumulative-≤-captured guard (§7/§8). | `{ refundId, ... }` |
| `GET /api/my-orders` | shopper | Caller's orders + payment state. Scope by identity via `CustomerOrdersWithItemsSpecification`. | list |
| `GET /api/reconciliation?from=&to=` | **admin** | §9. | 3 buckets |
| `POST /api/payment-methods` | shopper | Save card via Vault v3 (§2.7). Response describes card safely (brand/last4/expiry), never full details. | `{ paymentMethodId, description, ... }` |
| `GET /api/payment-methods` | shopper | Caller's saved cards (safe descriptors). | list |
| `DELETE /api/payment-methods/{paymentMethodId}` | shopper (own card) | Remove card; afterwards it must not appear in the list and must not be usable to pay (the `/pay` handler rejects an unknown/other-owner `paymentMethodId`). 404 if not caller's. | 204/200 |

**Top-level response identifiers (mandate):** `orderId` from `POST /api/orders`, `paymentMethodId`
from `POST /api/payment-methods`, `refundId` from `POST /api/orders/{orderId}/refunds`. Everything else
about response shape is free (follow `BaseResponse` correlation-id convention).

**Ownership enforcement (mandate).** Every shopper endpoint loads the target by **id + buyerId** (via
the scoped specs) and returns `404` when the caller is not the owner — one shopper can never see, use,
or delete another's orders or cards. `DELETE` of a saved card removes it so it neither lists nor pays
afterward.

---

## 11. Security / PCI hygiene (mandate)

- **Never persist** card number or CVV in the app DB — only PayPal ids (order/auth/capture/refund/vault/
  customer) and safe descriptors (brand, last4, expiry).
- **Never log** card details or the raw bodies of `/pay`, `/payment-methods`, or the PayPal card/vault
  requests. In `PayPalClient`, log only method + path + status + `debug_id`; redact/omit `payment_source`.
  Ensure `[Log]`/request-logging middleware doesn't capture these bodies.
- Card details flow **through** the API to PayPal and are never stored; the request DTO holding them is
  transient.
- Secrets come only from env/config — never written to source, `appsettings.json`, logs, or `PLAN.md`.

---

## 12. Build order (suggested)

1. `PayPalSettings` + config mapping (§3); placeholder `PayPal` section in appsettings (no secrets).
2. Domain: `OrderStatus`, `Payment`, `PaymentRefund`, extend `Order`; `SavedCard`; specs (§4/§5).
3. EF configs + `DbSet<SavedCard>` + migration(s) (§4/§5).
4. `IPayPalClient` + `PayPalClient` (OAuth cache, all §2 calls, error mapping). Unit-test URL
   derivation and idempotency-header wiring with a stubbed handler.
5. `OrderService` catalog-ids overload; `IPaymentService`, `ISavedCardService`,
   `IReconciliationService`.
6. Endpoints (§10) + DI registrations in `Program.cs`.
7. Tests (§13); then live sandbox verification (§13).

---

## 13. Verification (self-verify, then hand the user a guide)

**Automated tests** — mirror existing patterns:
- Unit tests (`tests/UnitTests`, xUnit + NSubstitute): `PaymentService` idempotency & refund-guard;
  `PayPalSettings.ResolvedBaseUrl()`; reconciliation windowing/paging with a fake `IPayPalClient`.
- Endpoint/integration tests (`tests/PublicApiIntegrationTests`, MSTest, `WebApplicationFactory<Program>`;
  tokens via `ApiTokenHelper` — `GetAdminUserToken()` has role `Administrators`, `GetNormalUserToken()`
  none): auth gating (normal user → `403` on fulfil/cancel/reconciliation), cross-shopper isolation
  (shopper B → `404` on shopper A's order/card), response-identifier presence. For tests that must not
  hit PayPal, swap `IPayPalClient` for a fake in the factory (as `TestApiApplication` swaps DbContexts).

**Live sandbox end-to-end** (the real proof the task wants; run with `DOTNET_ROLL_FORWARD=Major`,
`UseOnlyInMemoryDatabase=true`, PayPal env vars set, all within one run):
1. `POST /api/authenticate` → bearer token.
2. `POST /api/orders` (catalog ids + quantities) → `orderId`.
3. `POST /api/orders/{orderId}/pay` with the sandbox card (`4111 1111 1111 1111`, any future expiry,
   any CVC, any name/address) → authorization created; hold == order total to the cent.
4. `POST /api/orders/{orderId}/fulfil` (admin token) → capture; response shows captured/fee/net.
5. `POST /api/orders/{orderId}/refunds` with an `idempotencyKey` (full or partial) → `refundId`;
   repeat the same key → same `refundId` (no double refund).
6. `POST /api/payment-methods` (save the card) → `paymentMethodId`; `GET` lists it (safe descriptor).
7. Place a **second** order, `POST /pay` with `{ paymentMethodId }` → authorized via the saved card;
   fulfil it. (Proves saved-card reuse.)
8. `DELETE /api/payment-methods/{id}` → gone from `GET`; a subsequent `/pay` with it is rejected.
9. `GET /api/reconciliation?from=&to=` over a range with settled data (a recent range may be empty due
   to the ≤3-hour lag — expected, not a gap).

Then give the user a concise, numbered curl/Postman walkthrough of steps 1–9 (with how to set the env
vars and the two run flags), stating expected status codes and the fields to look at.

---

## 14. Sources (re-confirm before coding, per the task)

- Orders v2 (create/authorize inline with card, statuses, links): developer.paypal.com/api/orders/v2,
  developer.paypal.com/api/rest/integration/orders-api/api-use-cases/advanced.
- 3DS response params (browser-challenge STOP signals):
  developer.paypal.com/docs/checkout/advanced/customize/3d-secure/response-parameters.
- Payments v2 (capture incl. `seller_receivable_breakdown`, reauthorize, void, refund):
  developer.paypal.com/api/payments/v2 (+ schema.json).
- Authorization honor period / reauthorization window (3-day / 4–29 days / 30-day):
  developer.paypal.com/docs/checkout/advanced/authorization-honor.
- Vault v3 setup-tokens → payment-tokens (save without purchase):
  developer.paypal.com/api/payment-tokens/save-without-purchase/cards,
  developer.paypal.com/docs/checkout/save-payment-methods/purchase-later/payment-tokens-api/cards.
- Transaction Search v1 (params, 31-day/3-year/3-hour limits, response, pagination):
  developer.paypal.com/api/transaction-search/v1 (+ schema.json),
  developer.paypal.com/docs/transaction-search.
- OAuth2 client-credentials token: developer.paypal.com/api/rest (Get an access token).

---

## 15. Gap check

No capability required by this task is unsupported by PayPal: order authorize (inline card), capture,
reauthorize (stale-auth renewal), void (cancel), full/partial refund, save-card-without-purchase
(Vault v3), and transaction reporting (reconciliation) are all first-class REST operations, confirmed
above. The only STOP conditions are the ones the task itself names — a card answered with a browser
approval challenge, or a saved-card setup token not auto-approved — which with the sandbox test card on
a direct-card-and-vault-enabled business account are not expected to occur; if they do, the build
session must STOP and report rather than build a browser round-trip. An empty recent reconciliation
range is an expected sandbox result (reporting lag), **not** a gap.
