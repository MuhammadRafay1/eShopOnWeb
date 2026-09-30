# PLAN — PayPal payments + saved cards for eShopOnWeb

This plan is the complete blueprint for a **later build session** that has the same task and the
same PayPal tooling but **not** this conversation. Everything the build session needs — the
verified PayPal contract, the domain model, the endpoint surface, and the run/verify recipe — is
here. Build in the order given in §12.

The capability is **additive**: it does not touch the existing catalog/basket/order browse flow.
It adds money movement (authorize → capture → refund/void), saved cards, and operator flows, all
exposed on **`src/PublicApi`** and drivable end-to-end through that API alone.

---

## 1. What already exists (facts established by inspection)

Paths are relative to the repo root.

### Endpoint style (`src/PublicApi`)
- The dominant pattern is the **`MinimalApi.Endpoint`** library. Each endpoint is one class
  implementing `IEndpoint<TResponse, TRequest, TDep...>` with `AddRoute(IEndpointRouteBuilder app)`
  + `HandleAsync(...)`. All are auto-registered: `builder.Services.AddEndpoints();` (Program.cs:28)
  and `app.MapEndpoints();` (Program.cs:176). **Follow this pattern for every new endpoint.**
- Representative: `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`:
  ```csharp
  public class CreateCatalogItemEndpoint : IEndpoint<IResult, CreateCatalogItemRequest, IRepository<CatalogItem>>
  {
      public void AddRoute(IEndpointRouteBuilder app) =>
          app.MapPost("api/catalog-items",
              [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                         AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
              (CreateCatalogItemRequest request, IRepository<CatalogItem> itemRepository) =>
                  await HandleAsync(request, itemRepository))
          .Produces<CreateCatalogItemResponse>()
          .WithTags("CatalogItemEndpoints");
  }
  ```
  - Routes are literal, **no leading slash**, kebab-case (`"api/catalog-items"`, `"api/catalog-items/{catalogItemId}"`).
  - Constructor injection for services (e.g. `IUriComposer`, `IMapper`); the last generic arg(s)
    of `IEndpoint<>` are resolved as route-handler parameters (repositories).
  - DTOs: one file per class, named `<EndpointName>.<TypeName>.cs`, same folder/namespace,
    deriving from `BaseRequest`/`BaseResponse` (both in `src/PublicApi/`, carrying a
    `CorrelationId()`). Responses are built as `new XxxResponse(request.CorrelationId())`.
  - Return `Task<IResult>`; use `Results.Ok/Created/NotFound/...`.
  - Swagger via `.Produces<T>()` + `.WithTags("...")`.
- The one exception is `AuthEndpoints/AuthenticateEndpoint.cs`, which uses `Ardalis.ApiEndpoints`
  (MVC-style `[HttpPost]`). **Do not** copy that style for the new endpoints — use `MinimalApi.Endpoint`.

### Auth & identity
- JWT bearer config in `src/PublicApi/Program.cs:54-70`: symmetric key from
  `AuthorizationConstants.JWT_SECRET_KEY`, `ValidateIssuer=false`, `ValidateAudience=false`.
- `POST api/authenticate` (`AuthenticateEndpoint`) returns a JWT for username/password. Token claims
  are built in `src/Infrastructure/Identity/IdentityTokenClaimService.cs`:
  `ClaimTypes.Name = userName` (+ `ClaimTypes.Role` per role). **The caller identity = `HttpContext.User.Identity.Name`, which is the username == email.**
- Admin restriction: attribute `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
  `Roles.ADMINISTRATORS == "Administrators"` (in `src/BlazorShared/Authorization/Constants.cs`).
- Seeded users (`src/Infrastructure/Identity/AppIdentityDbContextSeed.cs`), password
  `Pass@word1` (`AuthorizationConstants.DEFAULT_PASSWORD`):
  - `demouser@microsoft.com` — regular shopper.
  - `admin@microsoft.com` — in `Administrators` role (the operator).

### Domain model (all under `src/ApplicationCore/Entities/`)
- `OrderAggregate/Order.cs` — `Order : BaseEntity, IAggregateRoot`.
  `Order(string buyerId, Address shipToAddress, List<OrderItem> items)`; `BuyerId` (string == email),
  `OrderDate`, `ShipToAddress`, read-only `OrderItems`, `decimal Total()`.
- `OrderAggregate/OrderItem.cs` — `OrderItem(CatalogItemOrdered itemOrdered, decimal unitPrice, int units)`.
- `OrderAggregate/CatalogItemOrdered.cs` — value object `(int catalogItemId, string productName, string pictureUri)`
  (guards: id ≥ 1, name/pictureUri non-empty).
- `OrderAggregate/Address.cs` — value object `(street, city, state, country, zipcode)`.
- `Entities/CatalogItem.cs` — `CatalogItem : BaseEntity, IAggregateRoot`, has `.Price`, `.Name`, `.PictureUri`.
- `BuyerAggregate/Buyer.cs` + `BuyerAggregate/PaymentMethod.cs` exist but are **not persisted**
  (no `DbSet`, no EF config) and the checkout flow does not use them. `PaymentMethod` is a good
  reference for the *safe* card shape (`Alias`, `CardId`, `Last4`, with the comment
  *"actual card data must be stored in a PCI compliant system"*) but is a child of the Buyer
  aggregate, not an aggregate root — do **not** repurpose it directly (see §5, saved cards).

### Persistence
- Repository is **Ardalis.Specification**: `IRepository<T> : IRepositoryBase<T>` and
  `IReadRepository<T> : IReadRepositoryBase<T>` (both `where T : class, IAggregateRoot`), impl
  `EfRepository<T>` bound to `CatalogContext` (`src/Infrastructure/Data/EfRepository.cs`). Methods
  used across the code: `AddAsync`, `UpdateAsync`, `DeleteAsync`, `GetByIdAsync`,
  `FirstOrDefaultAsync(spec)`, `ListAsync(spec)`, `CountAsync(spec)`. Registered open-generic in
  `Program.cs:40-41` — `IRepository<Order>` and `IRepository<CatalogItem>` are already available.
- `CatalogContext` (`src/Infrastructure/Data/CatalogContext.cs`) holds `Orders`, `OrderItems`,
  `CatalogItems`, etc.; `OnModelCreating` calls `ApplyConfigurationsFromAssembly` so every
  `IEntityTypeConfiguration<T>` in `src/Infrastructure/Data/Config/` is picked up automatically.
  See `Config/OrderConfiguration.cs` for the `OwnsOne` pattern (used for `Address`).
- **`IOrderService`/`OrderService` is NOT registered in PublicApi** — only in Web. We will not use
  it (it is basket-based); we place orders from item ids directly (see §4).

### Config & run environment
- Config binding via `builder.Services.Configure<T>(section)` + `IOptions<T>` (e.g.
  `BaseUrlConfiguration`). `builder.Configuration.AddEnvironmentVariables()` is already called
  (Program.cs:86), so `PayPal__ClientId` etc. env vars bind to the `PayPal:` section automatically,
  **but** the env vars are named `PAYPAL_CLIENT_ID` (single underscore) — they will **not**
  auto-bind to `PayPal:ClientId`. See §7 for the exact binding wiring required.
- DbContext registration reads `configuration["UseOnlyInMemoryDatabase"]`
  (`src/Infrastructure/Dependencies.cs`). We must run with `UseOnlyInMemoryDatabase=true` on this
  machine (no LocalDB). In-memory ⇒ **migrations are ignored**, the model is created from code, and
  **each host has its own store** — so pay/fulfil/refund only the orders you place through PublicApi
  in the *same* run. This is why `POST /api/orders` is part of our surface.
- PublicApi dev ports: `https://localhost:37783;http://localhost:37784`
  (`src/PublicApi/Properties/launchSettings.json`). On this machine, bind only to the assigned
  `APP_PORT_BLOCK_BASE … +SIZE-1` block (launchSettings already targets it in the run harness).
- SDK/runtime: `global.json` pins 8.0.x but only .NET 10 SDK + no ASP.NET 8 runtime is installed.
  Run with `rollForward: latestMajor` / `DOTNET_ROLL_FORWARD=Major`, or install ASP.NET Core 8.0
  runtime (x64). **Do not** edit `global.json` as a code change unless necessary — prefer the env var.

---

## 2. PayPal contract — VERIFIED (research done in the planning session)

All of the following was confirmed against official PayPal developer documentation. The build
session must still keep the "confirm before you commit" rule in mind, but these are the pieces the
integration needs and they are all real, current PayPal REST capabilities. Sources are listed in §13.

We use **five** PayPal REST API families over plain HTTP (see §3 for the SDK-vs-HTTP decision):

1. **OAuth 2.0** — get an access token.
2. **Orders v2** — create an order with a direct card (or vaulted card) and **AUTHORIZE** intent.
3. **Payments v2** — capture / void / reauthorize the authorization; refund the capture.
4. **Payment Method Tokens v3 (Vault)** — save a card without a purchase; list/delete tokens.
5. **Transaction Search (Reporting) v1** — reconciliation report.

Base host: **`https://api-m.sandbox.paypal.com`** (sandbox). Live is `https://api-m.paypal.com`.
When `PayPal:BaseUrl` is set, use it **verbatim** as the base for **every** call including the token
call; otherwise derive the base from `PayPal:Environment` (`sandbox` → sandbox host, anything
indicating live/production → live host).

### 2.1 OAuth token
```
POST {base}/v1/oauth2/token
Authorization: Basic base64(clientId:clientSecret)
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
```
Response: `{ "access_token": "...", "token_type": "Bearer", "expires_in": 32400, ... }`.
Cache the token until shortly before `expires_in` (use `IMemoryCache`, already registered). On a
401 from any API call, refresh once and retry.

### 2.2 Create order + authorize with a direct card (the `POST /pay` core)
Passing `payment_source.card` **in the create-order call** makes PayPal process the payment inline;
with `intent: "AUTHORIZE"` the response contains the authorization. This is a single call — no
separate confirm step, no browser.
```
POST {base}/v2/checkout/orders
Authorization: Bearer {access_token}
Content-Type: application/json
PayPal-Request-Id: {idempotency-key}          # dedupe window ~6h; see §6
Prefer: return=representation                   # so the response includes payments.authorizations[]

{
  "intent": "AUTHORIZE",
  "purchase_units": [{
    "reference_id": "{eshopOrderId}",
    "custom_id": "{eshopOrderId}",              # <-- reconciliation key (see §2.7)
    "invoice_id": "eshop-{eshopOrderId}-{shortGuid}",  # unique per PayPal account
    "amount": {
      "currency_code": "{PayPal:Currency}",
      "value": "{order.Total() to 2 decimals}",
      "breakdown": { "item_total": { "currency_code": "{cur}", "value": "{same}" } }
    }
  }],
  "payment_source": {
    "card": {
      "number": "4111111111111111",
      "expiry": "2030-01",                       # format YYYY-MM
      "security_code": "123",
      "name": "John Doe",
      "billing_address": {
        "address_line_1": "123 Main St.",
        "admin_area_2": "Anytown",               # city
        "admin_area_1": "CA",                    # state
        "postal_code": "12345",
        "country_code": "US"
      },
      "attributes": {
        "verification": { "method": "SCA_WHEN_REQUIRED" }
      }
    }
  }
}
```
**Paying with a saved card** (Flow 2): replace the whole `card` object with just the vault id:
```json
"payment_source": { "card": { "vault_id": "{paypal-payment-token-id}" } }
```
Response (relevant path): `status` (expect `COMPLETED`) and
`purchase_units[0].payments.authorizations[0]` → `{ id, status: "CREATED", expiration_time, amount }`.
**Persist** `id` (=authorization id) and the top-level order `id` (=PayPal order id).

**Amount = order total to the cent:** compute `order.Total()` (sum of `UnitPrice*Units`), format
with `InvariantCulture` to the currency's minor-unit precision (2 for USD/EUR/GBP; 0 for
zero-decimal currencies like JPY). Send that same string in `amount.value` (and `item_total`).

### 2.3 Capture the authorization (the `POST /fulfil` core)
```
POST {base}/v2/payments/authorizations/{authorization_id}/capture
Authorization: Bearer {access_token}
PayPal-Request-Id: {idempotency-key}
Prefer: return=representation
Content-Type: application/json

{ "final_capture": true }        # omit amount ⇒ capture full authorized amount
```
Response includes what PayPal reported — persist all three:
```json
{
  "id": "{capture_id}", "status": "COMPLETED", "final_capture": true,
  "seller_receivable_breakdown": {
    "gross_amount": { "currency_code": "USD", "value": "100.00" },   # captured amount
    "paypal_fee":   { "currency_code": "USD", "value": "3.48" },     # PayPal fee
    "net_amount":   { "currency_code": "USD", "value": "96.52" }     # net to merchant
  }
}
```

### 2.4 Stale-authorization handling at fulfil (mandatory)
PayPal authorizations have a **3-day honor period** and a **29-day total validity**; a
reauthorization *generates a new authorization id and restarts the 3-day honor period* (and is only
possible within the 29-day window). At fulfil:
1. Attempt capture on the stored authorization id.
2. If PayPal rejects because the authorization is stale/expired (HTTP 422 with
   `issue: "AUTH_CAPTURE_CURRENCY_MISMATCH"`-style errors are different; the relevant ones are
   `issue: "AUTHORIZATION_EXPIRED"` / `"INVALID_RESOURCE_ID"` / order/auth status `EXPIRED`), call
   **reauthorize**, then capture the *new* authorization id:
   ```
   POST {base}/v2/payments/authorizations/{authorization_id}/reauthorize
   PayPal-Request-Id: {key}
   { "amount": { "currency_code": "{cur}", "value": "{order total}" } }
   ```
   Persist the new authorization id (replace the stored one) before capturing.
3. If reauthorization is **not** possible (past the 29-day window, or PayPal returns a terminal
   `DENIED`/`AUTHORIZATION_ALREADY_CAPTURED`/`INVALID_RESOURCE_ID`), do **not** silently fail the
   fulfilment: return an operator-actionable error (HTTP 409/422) with a clear message, e.g.
   *"The payment authorization for order {id} has expired and can no longer be renewed. Ask the
   shopper to pay again before fulfilling."* (See §8.)

Optionally `GET {base}/v2/payments/authorizations/{authorization_id}` first to read `status`
(`CREATED`/`CAPTURED`/`DENIED`/`EXPIRED`/`VOIDED`/`PENDING`) and decide, rather than capturing
blindly — either approach is fine; both must end in the actionable-error behavior above.

### 2.5 Void the authorization (the `POST /cancel` core)
```
POST {base}/v2/payments/authorizations/{authorization_id}/void
Authorization: Bearer {access_token}
PayPal-Request-Id: {key}
```
Success ⇒ held funds released, no money moved. Only valid **before** capture; if the order is
already captured, reject with an actionable message (use refund instead).

### 2.6 Refund the capture (the `POST /refunds` core)
```
POST {base}/v2/payments/captures/{capture_id}/refund
Authorization: Bearer {access_token}
PayPal-Request-Id: {caller-idempotency-key}     # <-- caller-supplied; see §6
Content-Type: application/json

{ "amount": { "currency_code": "{cur}", "value": "{partial amount}" } }   # omit body ⇒ full refund
```
Response: `{ "id": "{refund_id}", "status": "COMPLETED", "seller_payable_breakdown": { ... "total_refunded_amount": {...} } }`.
Return `refund_id` as the top-level `refundId`. **Guard before calling:** `sum(existing refunds) +
requested ≤ captured amount`; reject over-refunds with an actionable error so a partly-refunded
order never becomes refundable beyond what was captured.

### 2.7 Reconciliation (the `GET /reconciliation` core)
```
GET {base}/v1/reporting/transactions
    ?start_date={ISO-8601}&end_date={ISO-8601}
    &fields=all&page_size=500&page={n}
Authorization: Bearer {access_token}
```
Verified constraints that shape the implementation:
- **Max 31 days per request.** For a range wider than 31 days, split into ≤31-day windows and query
  each. The report must cover the **whole** requested range, not one window.
- **Pagination:** `page_size` max **500**; response has `total_pages` — loop `page = 1..total_pages`
  for each window and accumulate. (Max 10,000 records/request overall.)
- **Reporting lag: up to ~3 hours** before a transaction appears; history goes back ~3 years.
- Each entry: `transaction_details[].transaction_info` → `transaction_id`, `transaction_status`,
  `transaction_amount {currency_code,value}`, `fee_amount`, `invoice_id`, `custom_field`
  (this carries our `custom_id` = eShop order id when `fields=all` is requested).

Reconciliation logic: pull all PayPal transactions in range; pull eShop orders whose payment
activity falls in range (captures/refunds with PayPal ids). Then line them up:
- **Matched** — PayPal transaction whose `custom_field`/`invoice_id` maps to an eShop order (and the
  eShop order references that capture/refund id).
- **In PayPal, not in eShop** — a transaction PayPal knows about with no matching eShop order.
- **In eShop, not in PayPal** — an eShop capture/refund with no PayPal transaction in range
  (expected for very recent activity due to the ~3h lag — **not** a gap; report it as
  "pending in PayPal reporting", do not treat an empty recent range as a failure).

Match key: set `custom_id` (and `invoice_id`) on the purchase unit at authorize time (§2.2) so the
eShop order id survives into PayPal's reporting `custom_field`, giving a stable join.

### 2.8 Save a card without a purchase (the `POST /payment-methods` core)
For cards, PayPal requires the **two-step** setup-token → payment-token flow (you cannot mint a
card payment token directly). Both steps are synchronous — **no webhook needed**.
```
# Step 1 — setup token from raw card
POST {base}/v3/vault/setup-tokens
Content-Type: application/json
{
  "payment_source": { "card": {
    "number": "4111111111111111", "expiry": "2030-01", "security_code": "123",
    "name": "John Doe",
    "billing_address": { "address_line_1": "...", "admin_area_1": "CA", "admin_area_2": "San Jose",
                         "postal_code": "95131", "country_code": "US" }
  }}
}
# → { "id": "{setup_token_id}", "status": "APPROVED", "customer": { "id": "{customer_id}" } }

# Step 2 — permanent payment token from the setup token
POST {base}/v3/vault/payment-tokens
Content-Type: application/json
PayPal-Request-Id: {key}
{ "payment_source": { "token": { "id": "{setup_token_id}", "type": "SETUP_TOKEN" } } }
# → { "id": "{payment_token_id}",           # <-- this is the vault_id used to pay (§2.2)
#     "customer": { "id": "{customer_id}" },
#     "payment_source": { "card": { "brand": "VISA", "last_digits": "1111", "expiry": "2030-01" } } }
```
Persist `payment_token_id` (the vault id), `customer.id`, and the **safe** descriptor
(`brand`, `last_digits`, `expiry`). Never persist PAN/CVC.

- **List:** `GET {base}/v3/vault/payment-tokens?customer_id={id}` (we drive listing from our own DB;
  the PayPal list is a cross-check, optional).
- **Delete:** `DELETE {base}/v3/vault/payment-tokens/{payment_token_id}` (returns 204). After delete,
  remove our record and reject any pay-with-that-card attempt.

### 2.9 The 3DS / challenge stop-condition (mandatory)
Use `payment_source.card.attributes.verification.method = "SCA_WHEN_REQUIRED"`. With the sandbox
business account (enabled for direct card + vaulting) and the sandbox test card
`4111 1111 1111 1111`, authorization completes without a challenge. **If** PayPal returns a
challenge that needs a shopper's browser approval — i.e. order/authorization `status` is
`PAYER_ACTION_REQUIRED`, or the response contains a `links[]` entry with `rel: "payer-action"` /
`rel: "3ds-contingency"` — the integration must **STOP and surface that as an error to the operator/
caller**, not build a browser approval round-trip. (This should not happen with the test card; it is
a guard, per the task.)

---

## 3. SDK vs. plain HTTP — decision

**Use plain HTTP via a typed `HttpClient`** (one gateway class), not the PayPal .NET Server SDK.
Rationale (this is a decision, not a gap): the official `PayPal-Dotnet-Server-SDK` covers Orders v2
and Payments v2 but **not** Payment Method Tokens v3 (vault) or Transaction Search v1 — both of
which this task requires. A single HTTP gateway covers all five families uniformly, gives direct
control over the `PayPal-Request-Id` / `Prefer: return=representation` headers we rely on for
idempotency and representation, and honors the `PayPal:BaseUrl` verbatim-override cleanly. Add
`System.Net.Http.Json` usage (part of the shared framework) for JSON (de)serialization; no new
third-party package is strictly required. (`Microsoft.Extensions.Http` for `AddHttpClient` is part
of the ASP.NET shared framework and is available; add the package reference to `PublicApi.csproj`
only if the build complains.)

---

## 4. Architecture & layering

Keep the existing Clean-Architecture layering (ApplicationCore = domain + ports; Infrastructure =
adapters; PublicApi = HTTP).

- **ApplicationCore (domain + ports):**
  - Extend the `Order` aggregate with payment + fulfilment state (§5).
  - New enums, new specifications.
  - A **port** interface `IPayPalGateway` (in `ApplicationCore/Interfaces/`) describing the
    operations the domain needs, expressed in domain terms (not PayPal JSON): `GetAccessToken` is
    internal to the adapter; the port exposes e.g. `AuthorizeAsync`, `CaptureAsync`,
    `ReauthorizeAsync`, `VoidAsync`, `RefundAsync`, `VaultCardAsync`, `DeleteVaultedCardAsync`,
    `SearchTransactionsAsync`. Each returns a small domain result record (ids, statuses, amounts,
    fee/net) or throws a typed `PayPalException` carrying an operator-actionable message + issue code.
  - Application services orchestrating domain + port + repositories (thin, unit-testable):
    - `IOrderPlacementService` — build an `Order` from `(buyerId, items[], address)`.
    - `IPaymentService` — `Authorize`, `Fulfil(Capture)`, `Cancel(Void)`, `Refund`.
    - `ISavedCardService` — save / list / delete cards.
    - `IReconciliationService` — build the report.
- **Infrastructure (adapters):**
  - `PayPalGateway : IPayPalGateway` — the typed `HttpClient` implementation with all HTTP, JSON
    DTOs, header handling, token caching, base-URL resolution, and PayPal→domain error mapping.
    Put it in `src/Infrastructure/Services/PayPal/` (new folder).
  - EF `IEntityTypeConfiguration<>` for the new persisted state (§5) in
    `src/Infrastructure/Data/Config/` (auto-discovered).
- **PublicApi (HTTP):**
  - New endpoint folders mirroring the existing style:
    `src/PublicApi/OrderEndpoints/`, `src/PublicApi/PaymentMethodEndpoints/`,
    `src/PublicApi/ReconciliationEndpoints/` (namespaces `Microsoft.eShopWeb.PublicApi.*`).
  - Thin endpoints: read caller `ClaimsPrincipal`, validate ownership, call an application service,
    map to a response DTO. Add a `ClaimsPrincipal user` (or `HttpContext http`) parameter to the
    route-handler lambda to get `user.Identity.Name`.
  - Register the new services, the typed `HttpClient`, and `PayPalSettings` in `Program.cs` (§7).

Each endpoint stays **separately invocable** — no do-everything route. Pay/fulfil/cancel/refund are
distinct endpoints.

---

## 5. Domain model changes

Extend the **existing** `Order` aggregate (per the task: "Order carries no payment or fulfilment
state at all. This adds the money movement…"). Do **not** create a parallel order model.

Add to `src/ApplicationCore/Entities/OrderAggregate/`:

- **`OrderStatus`** enum: `AwaitingPayment`, `Authorized`, `Fulfilled`, `Cancelled`,
  `PartiallyRefunded`, `Refunded`. `Order` starts `AwaitingPayment`.
- **`Payment`** — an **owned** entity of `Order` (mapped with `OwnsOne`, like `Address`). Fields
  capture the state PayPal owns so later requests can act on it:
  - `Currency` (string)
  - `PayPalOrderId` (string) — the checkout order id from §2.2
  - `AuthorizationId` (string), `AuthorizationStatus` (string), `AuthorizationExpiresAt` (DateTimeOffset?)
  - `CaptureId` (string?), `CaptureStatus` (string?)
  - `CapturedAmount` (decimal?), `PayPalFee` (decimal?), `NetAmount` (decimal?) — from §2.3
  - Refunds: an owned collection `Refund` (mapped with `OwnsMany`), each:
    `PayPalRefundId` (string), `Amount` (decimal), `Status` (string), `IdempotencyKey` (string),
    `CreatedAt`.
- **Behavior methods on `Order`** (keep invariants inside the aggregate):
  - `SetAwaitingPayment()` (or default in ctor).
  - `MarkAuthorized(currency, paypalOrderId, authorizationId, status, expiresAt)` → status `Authorized`.
  - `ReplaceAuthorization(newAuthorizationId, status, expiresAt)` (after reauthorize).
  - `MarkFulfilled(captureId, capturedAmount, paypalFee, netAmount)` → status `Fulfilled`.
  - `MarkCancelled()` → status `Cancelled` (only from `Authorized`).
  - `AddRefund(paypalRefundId, amount, idempotencyKey)` → sets `PartiallyRefunded` or `Refunded`;
    **guards** `TotalRefunded + amount ≤ CapturedAmount`.
  - Helpers: `TotalRefunded()`, `RefundableRemaining()`, `FindRefundByKey(key)`.

**Persistence:** add `Config/PaymentConfiguration`-style mapping. Because `Payment`/`Refund` are
owned by `Order`, extend `OrderConfiguration.cs` (`builder.OwnsOne(o => o.Payment, p => { … p.OwnsMany(x => x.Refunds, …); })`)
rather than a separate root. This works with the in-memory provider directly (model-driven).

> **Migrations:** The verification path uses the in-memory provider, which **ignores migrations**
> and builds the schema from the model — so no migration is needed to pass verification. For SQL
> completeness, generating an EF migration (`dotnet ef migrations add AddOrderPayment -c CatalogContext`)
> is a nice-to-have; it is **not** blocking and can be skipped if `dotnet ef` is unavailable. Do not
> let a migration failure block the in-memory verification.

### Saved cards
Add **`SavedPaymentMethod : BaseEntity, IAggregateRoot`** (new aggregate; use the repository).
Prefer a dedicated aggregate over reworking the unused `Buyer`/`PaymentMethod` types (which are not
persisted and are non-root). Place at `src/ApplicationCore/Entities/PaymentMethodAggregate/SavedPaymentMethod.cs`.
Fields — **safe data only**:
- `BuyerId` (string == owner email) — ownership scope.
- `PayPalVaultId` (string) — the payment-token id (§2.8), used as `vault_id` to pay.
- `PayPalCustomerId` (string).
- `Brand` (string), `Last4` (string), `Expiry` (string `YYYY-MM`).
- `Label` (string?, optional shopper-friendly name).
- `CreatedAt`.
Add a `DbSet<SavedPaymentMethod>` to `CatalogContext` and a
`Config/SavedPaymentMethodConfiguration.cs`. **Never** add PAN/CVC fields.

New specifications (`src/ApplicationCore/Specifications/`):
- `CustomerOrdersWithPaymentSpecification(buyerId)` — orders for a buyer incl. items + payment
  (extend the existing `CustomerOrdersWithItemsSpecification` pattern; owned types load with the root).
- `OrderWithPaymentByIdSpec(orderId)` — single order incl. items + payment.
- `SavedCardsByBuyerSpecification(buyerId)` and `SavedCardByIdAndBuyerSpecification(id, buyerId)`.

---

## 6. Idempotency & concurrency (mandatory)

Payment operations must be idempotent in effect; a double-click must never authorize or capture
twice. Layered defense:

1. **Domain state guards (primary):** every operation checks the order's current `OrderStatus`
   and stored PayPal ids before calling PayPal:
   - `Authorize`: if the order already has an `AuthorizationId` and status `Authorized` (or beyond),
     **return the existing authorization** instead of re-authorizing.
   - `Fulfil`: if already `Fulfilled` (has `CaptureId`), return the existing capture result.
   - `Cancel`: if already `Cancelled`, return success idempotently; if `Fulfilled`, reject
     (use refund).
2. **PayPal-Request-Id (durable backstop):** send a **deterministic** `PayPal-Request-Id` per
   logical operation so retries dedupe at PayPal (dedupe window ~6h for orders):
   - authorize: `auth-{eshopOrderId}`
   - capture: `capture-{authorizationId}`
   - void: `void-{authorizationId}`
   - reauthorize: `reauth-{authorizationId}`
3. **Per-order serialization:** guard the authorize/fulfil/cancel/refund critical section with an
   in-process lock keyed by order id (a `ConcurrentDictionary<int, SemaphoreSlim>`), so concurrent
   double-clicks in the single PublicApi host don't race between the state check and the PayPal
   call. (Single-host in-memory run ⇒ in-process lock suffices; `PayPal-Request-Id` is the
   cross-process backstop.)

**Refund idempotency (caller-supplied key):** `POST /refunds` carries a caller idempotency key
(accept it as a body field `idempotencyKey` **and/or** an `Idempotency-Key` header; body field is
canonical). Before refunding:
- If a stored `Refund` for this order already has that key, **return its `refundId`** (no second
  refund).
- Otherwise call PayPal with `PayPal-Request-Id = {that key}`, persist the new `Refund` with the
  key, return the new `refundId`.
- Two **different** keys ⇒ two legitimate partial refunds (subject to the ≤ captured guard). Keys
  are scoped per capture/order.

---

## 7. Configuration & DI

### Settings POCO
`src/ApplicationCore/` (or `src/Infrastructure/Services/PayPal/`) — `PayPalSettings`:
```csharp
public class PayPalSettings
{
    public const string CONFIG_NAME = "PayPal";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Environment { get; set; }   // "sandbox" | "live"
    public string? Currency { get; set; }       // e.g. "USD"
    public string? BaseUrl { get; set; }        // optional verbatim override
}
```

### Binding (exact keys `PayPal:ClientId`, `PayPal:ClientSecret`, `PayPal:Environment`, `PayPal:Currency`, `PayPal:BaseUrl`)
The env vars are `PAYPAL_CLIENT_ID` etc. (single underscore) — these do **not** auto-map to
`PayPal:ClientId`. Wire an explicit mapping in `src/PublicApi/Program.cs` (before `Build()`), so the
same build runs against any account with **no hard-coded values**:
```csharp
builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
{
    ["PayPal:ClientId"]     = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID"),
    ["PayPal:ClientSecret"] = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET"),
    ["PayPal:Environment"]  = Environment.GetEnvironmentVariable("PAYPAL_ENVIRONMENT"),
    ["PayPal:Currency"]     = Environment.GetEnvironmentVariable("PAYPAL_CURRENCY"),
    // PayPal:BaseUrl comes from config (appsettings) if provided; add an env fallback if desired.
});
builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection(PayPalSettings.CONFIG_NAME));
```
Add a `"PayPal": { "BaseUrl": "" }` placeholder to `src/PublicApi/appsettings.json` for the optional
override key (leave the secret keys **absent** from appsettings — they come from env only).
**Never** write credential values into any file (appsettings, PLAN.md, code).

`PayPal:BaseUrl` rule: if non-empty, the gateway uses it verbatim as the base for **all** calls
(token + orders + payments + vault + reporting). Else it derives the host from `PayPal:Environment`.

### Service registration (in `Program.cs`, inline with the existing `builder.Services...` calls)
```csharp
builder.Services.AddMemoryCache();  // already present
builder.Services.AddHttpClient<IPayPalGateway, PayPalGateway>();   // typed client
builder.Services.AddScoped<IOrderPlacementService, OrderPlacementService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ISavedCardService, SavedCardService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
```
(`IRepository<Order>`, `IRepository<CatalogItem>`, and — once added —
`IRepository<SavedPaymentMethod>` come from the existing open-generic registration.)

---

## 8. Endpoint surface (all under `/api/`, `MinimalApi.Endpoint` style)

Auth mapping per the task: **operator (admin) actions = fulfil, cancel, reconciliation**; every
other endpoint is **shopper-scoped** and acts only on the caller's own data. (This is the task's
explicit split; note refunds are shopper-scoped by that wording, but still act only on the caller's
own order and are bounded by the ≤-captured guard.)

All endpoints require JWT (`AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme`).
Admin ones add `Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS`.
Shopper endpoints resolve `buyerId = user.Identity.Name` and must filter/verify by it — return
`404` (not `403`) when an order/card exists but belongs to someone else, to avoid leaking existence.

| Method & route | Auth | Body → | Returns (top-level id in **bold**) |
|---|---|---|---|
| `POST api/orders` | shopper | `{ items:[{catalogItemId,quantity}], shipToAddress? }` | **`orderId`** (int), status `AwaitingPayment` |
| `POST api/orders/{orderId}/pay` | shopper (owns order) | `{ card:{number,expiry,cvc,name,billingAddress{street,city,state,country,zipCode}} }` **or** `{ savedPaymentMethodId:int }` (exactly one) | authorization id + status; order → `Authorized` |
| `POST api/orders/{orderId}/fulfil` | **admin** | (none) | capture id, capturedAmount, paypalFee, netAmount; order → `Fulfilled` |
| `POST api/orders/{orderId}/cancel` | **admin** | (none) | void confirmation; order → `Cancelled` |
| `POST api/orders/{orderId}/refunds` | shopper (owns order) | `{ amount?, idempotencyKey }` (amount omitted ⇒ full) | **`refundId`** (PayPal refund id) |
| `GET api/my-orders` | shopper | — | caller's orders + payment state |
| `GET api/reconciliation?from={iso}&to={iso}` | **admin** | — | matched / paypal-only / eshop-only buckets |
| `POST api/payment-methods` | shopper | `{ card:{number,expiry,cvc,name,billingAddress{...}}, label? }` | **`paymentMethodId`** (int) + safe descriptor `{brand,last4,expiry}` |
| `GET api/payment-methods` | shopper | — | caller's saved cards (safe descriptors only) |
| `DELETE api/payment-methods/{paymentMethodId}` | shopper (owns card) | — | 204/confirmation |

Notes:
- `POST api/orders`: look up each `catalogItemId` via `IRepository<CatalogItem>`; build
  `CatalogItemOrdered`/`OrderItem` using the **catalog price** as `UnitPrice` (never trust a
  client-supplied price); `ShipToAddress` from `shipToAddress` or a sensible default (Order requires
  a non-null address — mirror the Web checkout default if none supplied). `BuyerId = caller name`.
- `POST .../pay`: validate exactly one of `card` / `savedPaymentMethodId`. For a saved card, load it
  with `SavedCardByIdAndBuyerSpecification` (ownership); if not found (or deleted) reject. Build the
  §2.2 request; enforce the §2.9 challenge stop-condition; persist authorization + PayPal order id.
- `POST .../fulfil`: §2.3 capture with §2.4 stale-auth handling; persist capture/fee/net.
- `POST .../cancel`: §2.5 void; only from `Authorized`.
- `POST .../refunds`: §2.6 + §6 refund idempotency + ≤-captured guard.
- `GET api/my-orders`: `CustomerOrdersWithPaymentSpecification(caller)`; project to a DTO exposing
  order id, total, `OrderStatus`, and payment fields (auth/capture/refund ids & statuses, fee, net).
- Response DTOs derive from `BaseResponse`; each creation response sets the required top-level id
  field exactly as named (`orderId`, `paymentMethodId`, `refundId`).

### Error handling
Extend `src/PublicApi/Middleware/ExceptionMiddleware.cs` (or throw domain exceptions it maps) to
translate the new failure modes into clear JSON with appropriate status codes:
- `PayPalException` (auth expired & non-renewable, void-after-capture, over-refund, challenge
  required, PayPal 4xx/5xx) → `409`/`422` with an **operator-actionable** message (§2.4, §2.6, §2.9).
- Not-found / not-owned → `404`.
- Validation (bad body, both/neither payment instrument) → `400`.
Keep the existing `DuplicateException → 409` behavior. Follow the existing `ErrorDetails` response
shape.

---

## 9. Security & PCI (mandatory)

- **Full card details never touch our DB and never hit logs.** PAN/CVC are only ever placed in the
  outbound PayPal request body and then discarded. We persist only `brand`, `last4`, `expiry`,
  vault id, customer id.
- The `PayPalGateway` must **not** log request/response bodies for calls that carry card data
  (create-order-with-card, setup-token). If logging HTTP, redact `number`, `security_code`,
  `expiry`, `name`, `billing_address` (log only status code + PayPal ids). Do **not** register the
  default `HttpClient` logging that dumps bodies for these calls.
- Credentials come only from env → `PayPal:` config; never written to any repo file.
- Ownership enforced on every shopper endpoint (`BuyerId == caller`); admin endpoints gated by role.
- Deleted saved cards are unusable to pay (record removed; pay path rejects unknown/foreign vault id).

---

## 10. Currency & amount formatting

- Currency from `PayPal:Currency` (config). Amounts derived from catalog prices only.
- Format `amount.value` with `InvariantCulture` to the currency's minor units: 2 decimals for
  common currencies (USD/EUR/GBP…), 0 for zero-decimal currencies (JPY, KRW…). A small currency
  helper mapping currency → decimal places keeps "equal to the order total to the cent" correct.
- Capture with no `amount` ⇒ full authorized amount (avoids rounding drift); the authorized amount
  already equals the order total from authorize time.

---

## 11. Files to add / change (checklist for the build session)

Add:
- `src/ApplicationCore/Entities/OrderAggregate/OrderStatus.cs`
- `src/ApplicationCore/Entities/OrderAggregate/Payment.cs`, `Refund.cs`
- (Order.cs) extend with `Payment`, `OrderStatus`, behavior methods
- `src/ApplicationCore/Entities/PaymentMethodAggregate/SavedPaymentMethod.cs`
- `src/ApplicationCore/Interfaces/IPayPalGateway.cs` (+ domain result records / `PayPalException`)
- `src/ApplicationCore/Interfaces/IOrderPlacementService.cs`, `IPaymentService.cs`,
  `ISavedCardService.cs`, `IReconciliationService.cs`
- `src/ApplicationCore/Services/OrderPlacementService.cs`, `PaymentService.cs`,
  `SavedCardService.cs`, `ReconciliationService.cs`
- `src/ApplicationCore/Specifications/` — the four specs in §5
- `src/Infrastructure/Services/PayPal/PayPalGateway.cs` (+ internal JSON DTOs, token cache,
  base-url resolver, error mapping)
- `src/Infrastructure/Data/Config/SavedPaymentMethodConfiguration.cs`
- `src/PublicApi/OrderEndpoints/*` (PlaceOrder, Pay, Fulfil, Cancel, Refund, MyOrders) + request/response DTOs
- `src/PublicApi/PaymentMethodEndpoints/*` (Create, List, Delete) + DTOs
- `src/PublicApi/ReconciliationEndpoints/*` + DTOs
- `PayPalSettings.cs`

Change:
- `src/Infrastructure/Data/CatalogContext.cs` — add `DbSet<SavedPaymentMethod>`
- `src/Infrastructure/Data/Config/OrderConfiguration.cs` — `OwnsOne(Payment)` + `OwnsMany(Refunds)`
- `src/PublicApi/Program.cs` — PayPal env→config mapping, `Configure<PayPalSettings>`,
  `AddHttpClient<IPayPalGateway,PayPalGateway>`, register the four services
- `src/PublicApi/appsettings.json` — `"PayPal": { "BaseUrl": "" }` placeholder
- `src/PublicApi/Middleware/ExceptionMiddleware.cs` — map `PayPalException`/not-found/validation
- `src/PublicApi/PublicApi.csproj` — add `Microsoft.Extensions.Http` only if the build requires it

---

## 12. Build order

1. **Domain first:** `OrderStatus`, `Payment`, `Refund`, extend `Order` with behavior + guards;
   `SavedPaymentMethod`; specs. Build ApplicationCore.
2. **Port + gateway:** `IPayPalGateway` (+ result records, `PayPalException`); `PayPalGateway` with
   token cache, base-url resolution, all five API families, header handling, error mapping,
   card-safe logging. Build Infrastructure.
3. **EF wiring:** `CatalogContext` DbSet + `OrderConfiguration`/`SavedPaymentMethodConfiguration`.
   (Optional migration; in-memory needs none.)
4. **Application services:** placement, payment (authorize/fulfil/cancel/refund + idempotency +
   stale-auth), saved cards, reconciliation (31-day windowing + pagination + join).
5. **Program.cs wiring:** config mapping, options, HttpClient, service registrations.
6. **Endpoints + DTOs:** orders, payments, payment-methods, reconciliation; ExceptionMiddleware.
7. **Build the solution**, then **self-verify** (§14) against the PayPal sandbox with the test card.

---

## 13. PayPal sources (verified during planning)

- Orders v2 API overview & create order — https://developer.paypal.com/docs/api/orders/v2/ ;
  https://developer.paypal.com/api/rest/integration/orders-api/
- Expanded Checkout (direct card `payment_source.card`) — https://developer.paypal.com/api/rest/integration/orders-api/api-use-cases/advanced
- Save card during purchase (vault ON_SUCCESS) — https://developer.paypal.com/docs/checkout/save-payment-methods/during-purchase/orders-api/cards/
- Payments v2 (authorizations capture/void/reauthorize; captures refund) —
  https://developer.paypal.com/api/payments/v2 ;
  https://developer.paypal.com/api/payments/v2/captures-refund ;
  https://developer.paypal.com/api/payments/v2/authorizations-capture
- Authorize-and-capture-later timing (3-day honor / 29-day validity / reauthorize) — https://developer.paypal.com/docs/checkout/standard/customize/authorization/
- Payment Method Tokens v3 (setup token → payment token, list, delete) —
  https://developer.paypal.com/api/payment-tokens/v3 ;
  https://developer.paypal.com/api/payment-tokens/save-without-purchase/cards
- Transaction Search v1 (31-day window, page_size 500, ~3h lag, 3-yr history, fields) —
  https://developer.paypal.com/docs/transaction-search/ ;
  https://developer.paypal.com/docs/api/transaction-search/v1/
- OAuth token — https://developer.paypal.com/api/rest/authentication/
- .NET Server SDK (coverage reference for the SDK-vs-HTTP decision) — https://github.com/paypal/PayPal-Dotnet-Server-SDK

---

## 14. Self-verification recipe (for the build session's finish)

Preconditions: env vars `PAYPAL_CLIENT_ID/SECRET/ENVIRONMENT/CURRENCY` set; dev cert trusted
(`dotnet dev-certs https --check`, `--trust` if needed).

Run PublicApi (in-memory, roll-forward), binding to the assigned port block:
```bash
DOTNET_ROLL_FORWARD=Major \
ASPNETCORE_ENVIRONMENT=Development \
UseOnlyInMemoryDatabase=true \
dotnet run --project src/PublicApi
# stop any prior instance first; bind only to APP_PORT_BLOCK_BASE..+SIZE-1 (launchSettings targets it)
```
Then, all against `https://localhost:{PublicApi-port}` (Swagger at `/swagger`):

1. **Token (shopper):** `POST /api/authenticate` `{ "username":"demouser@microsoft.com", "password":"Pass@word1" }` → save bearer `SHOP`.
2. **Token (operator):** same with `admin@microsoft.com` / `Pass@word1` → save bearer `ADMIN`.
3. **Place order:** `POST /api/orders` (SHOP) with a couple of seeded `catalogItemId`s+quantities → capture `orderId` (order A).
4. **Save a card:** `POST /api/payment-methods` (SHOP) with card `4111111111111111`, expiry any future `YYYY-MM`, any CVC/name/address → capture `paymentMethodId`; confirm response shows only `brand/last4/expiry`.
5. **Authorize order A (one-off card):** `POST /api/orders/{A}/pay` (SHOP) with `card{...}` → expect `Authorized`; **double-click the same call** → same authorization (no second hold).
6. **Fulfil order A:** `POST /api/orders/{A}/fulfil` (ADMIN) → expect `Fulfilled` with capturedAmount == order total, plus PayPal fee + net.
7. **Partial refund:** `POST /api/orders/{A}/refunds` (SHOP) `{ "amount":<part>, "idempotencyKey":"k1" }` → `refundId`; **repeat with `k1`** → same `refundId` (no double refund); an over-refund beyond captured is rejected.
8. **Second order + saved card + cancel:** `POST /api/orders` (order B) → `POST /api/orders/{B}/pay` with `{ "savedPaymentMethodId": <id> }` → `Authorized`; then `POST /api/orders/{B}/cancel` (ADMIN) → `Cancelled` (funds released, no capture).
9. **My orders:** `GET /api/my-orders` (SHOP) → A shows Fulfilled+PartiallyRefunded with ids/fee/net; B shows Cancelled.
10. **Delete card:** `DELETE /api/payment-methods/{id}` (SHOP) → gone from `GET /api/payment-methods`; a subsequent pay with it is rejected.
11. **Ownership:** repeat a `GET /api/my-orders`/pay as a different user → cannot see/act on the first shopper's order or card.
12. **Reconciliation:** `GET /api/reconciliation?from=...&to=...` (ADMIN) over a range — a very recent range may return empty due to the ~3h PayPal reporting lag (**expected**, not a gap); a wider/older range with data lines transactions up against eShop orders.

Provide the user a concise numbered guide (curl/Swagger) reproducing steps 1-12.

---

## 15. Explicitly out of scope / decided (not gaps)
- **No storefront UI**, no webhooks (all flows are synchronous with the sandbox business account),
  no browser approval round-trip (challenge ⇒ STOP-and-report, §2.9).
- **No new infra** (no Docker/broker/Postgres). In-memory DB only on this machine.
- **Refunds are shopper-scoped** per the task's explicit operator list (fulfil/cancel/reconciliation
  only), still bounded by ownership + ≤-captured guards.
- **Migrations optional** (in-memory ignores them); do not let migration tooling block verification.
- **Plain HTTP over the SDK** — decided in §3 (SDK lacks vault + reporting coverage).

No capability required by this task was found to be unsupported by PayPal. If, during the build,
PayPal genuinely does not cover a required capability (or returns a browser-challenge for the test
card), **STOP and report the gap** rather than inventing a workaround.
