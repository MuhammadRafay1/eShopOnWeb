# PayPal payments + saved cards for eShopOnWeb — implementation plan

This file is both the task plan and the PayPal Server SDK **contract sheet** required by the
`dotnet-integrate-pay-pal-server-sdk` skill (written here, at the repo root, instead of
`pay-pal-server-sdk-plan.md`, per this session's instructions). It is the sole deliverable of
this planning session — no project file was created or edited.

Grounding: every PayPal fact below was read this session from the SDK map and the declaring
source files in a fresh shallow clone of `https://github.com/Darker98/paypal-csharp-sdk`
(branch `main`), obtained per `dotnet-getting-started`. The clone lives in the system temp
directory and is not referenced by path anywhere below, per that skill's portability rule.
Repo conventions (project layout, endpoint pattern, auth, EF setup, test stack) were read from
`src/` and `tests/` in this repo by a read-only survey this session.

---

## 1. Design overview

### 1.1 Why this shape

- **Additive, not a replacement.** Nothing about `BasketService`/`OrderService`/the existing
  `Order` checkout flow changes. A new, independent order-creation path is added to PublicApi
  that builds an `Order` directly from catalog item ids + quantities (no `Basket` involved),
  because the task requires the whole flow to be drivable through PublicApi alone, and baskets
  are per-host in-memory state that Web and PublicApi don't share anyway (see environment
  gotchas).
- **Clean Architecture is kept.** `ApplicationCore` gets the new entities and an
  `IPaymentGateway` interface (plus a few narrow supporting interfaces) but **never references
  `PayPalServerSdk`**. `Infrastructure` gets a `PayPal/` folder implementing those interfaces
  against the SDK. `PublicApi` wires DI and exposes endpoints, following the existing
  `MinimalApi.Endpoint` (`IEndpoint<TResult, TRequest, TDep>`) pattern used throughout
  `src/PublicApi/CatalogItemEndpoints/*` (e.g. `CreateCatalogItemEndpoint.cs`).
- **PayPal state lives in new entities, not bolted onto `Order`.** `Order` gets a navigation
  property to a new `Payment` aggregate (1:1) that carries every PayPal id/status the task
  asks for (hold id, capture id, refund ids, amounts). `Order` itself only gains two
  timestamps (`FulfilledAt`, `CancelledAt`) — the task's "fulfil", "cancel", "refund" verbs are
  order-level events, but the *money* state (authorize/capture/refund ids and statuses) is
  PayPal's own state mirrored on `Payment`.
- **Saved cards extend the existing skeleton.** `src/ApplicationCore/Entities/BuyerAggregate/PaymentMethod.cs`
  already exists with `Alias`/`CardId`/`Last4` and the comment "actual card data must be stored
  in a PCI compliant system" — this is precisely PayPal Vault's job. `PaymentMethod` is
  extended (not replaced) with the PayPal payment-token id and a few more safe display fields
  (brand, expiry). `CardId` becomes the PayPal-generated vault/payment-token id.

### 1.2 New entities (ApplicationCore)

- `src/ApplicationCore/Entities/BuyerAggregate/PaymentMethod.cs` (existing file, extended):
  add `PayPalPaymentTokenId` (the field `CardId` already half-describes — reuse `CardId` for
  this, per the file's own comment), `Brand` (string, PayPal's card brand wire value), `Expiry`
  (string, `YYYY-MM`), `IsDeleted`/`DeletedAt` (soft delete — see §4.3). `Alias`/`Last4` already
  exist. Add a public factory/constructor and the mutation methods `Buyer` needs
  (`Buyer.AddPaymentMethod(...)`, `PaymentMethod.MarkDeleted()`), matching the existing
  `BaseEntity`/private-setter style used by `Order`/`Buyer`.
- `src/ApplicationCore/Entities/OrderAggregate/Order.cs` (existing file, extended): add
  `OrderStatus Status` (new enum, default `AwaitingPayment`), `DateTimeOffset? FulfilledAt`,
  `DateTimeOffset? CancelledAt`, and a `Payment? Payment` navigation (EF: one-to-one via
  `Payment.OrderId`). Add state-transition methods (`MarkAwaitingPayment` is the ctor default;
  `MarkFulfilled()`, `MarkCancelled()`) following the existing private-setter + public-method
  style (`AddOrderItem` doesn't exist today but `Order` already uses this pattern elsewhere in
  the codebase's other aggregates — mirror it).
- New `src/ApplicationCore/Entities/OrderAggregate/OrderStatus.cs`: plain enum —
  `AwaitingPayment, Authorizing, Authorized, Capturing, Fulfilled, Cancelling, Cancelled,
  Refunding, PartiallyRefunded, Refunded, PaymentFailed`. (Separate from, and never confused
  with, PayPal's own `OrderStatus`/`AuthorizationStatus`/`CaptureStatus` enums, which never
  leave `Infrastructure`.)
- New `src/ApplicationCore/Entities/OrderAggregate/Payment.cs` (new aggregate root or owned
  entity keyed by `OrderId`, 1:1 with `Order`):
  - `OrderId` (FK, unique)
  - `Status` — same `OrderStatus`-shaped lifecycle as above, or a dedicated `PaymentStatus`
    enum mirroring it 1:1 (`Authorizing, Authorized, Capturing, Captured, Cancelling,
    Cancelled, Refunding, PartiallyRefunded, Refunded, Failed`) — pick one during
    implementation; don't maintain two independent state machines that can disagree.
  - `PayPalOrderId` (string) — the `/v2/checkout/orders` id
  - `PayPalAuthorizationId` (string?), `AuthorizationStatus` (string? — raw PayPal wire value,
    e.g. `CREATED`/`CAPTURED`/`VOIDED`/`PENDING`/`DENIED`/`PARTIALLY_CAPTURED`),
    `AuthorizationExpiresAt` (`DateTimeOffset?`)
  - `PayPalCaptureId` (string?), `CaptureStatus` (string?), `CapturedAmount` (decimal?),
    `PayPalFeeAmount` (decimal?), `NetAmount` (decimal?)
  - `CurrencyCode` (string, from `PayPal:Currency`, snapshotted per-payment so a later config
    change can't reinterpret historical amounts)
  - `CreatedAt`/`UpdatedAt` (`DateTimeOffset`)
  - `Refunds` nav collection (see next)
- New `src/ApplicationCore/Entities/OrderAggregate/Refund.cs`:
  - `PaymentId` (FK)
  - `IdempotencyKey` (string, caller-supplied) — **unique together with `PaymentId`** (DB
    index; see DUPLICATE CLAIMS §7)
  - `PayPalRefundId` (string?) — null while the claim row exists but PayPal hasn't answered yet
  - `Status` (string? — raw PayPal `RefundStatus` wire value)
  - `Amount` (decimal), `CurrencyCode` (string)
  - `CreatedAt`/`UpdatedAt`

### 1.3 New interfaces (ApplicationCore) / implementations (Infrastructure)

ApplicationCore defines what it needs without knowing PayPal exists:

```csharp
namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IPaymentGateway
{
    Task<AuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken ct);
    Task<CaptureResult> CaptureAsync(string authorizationId, Money amount, string paypalRequestId, CancellationToken ct);
    Task<ReauthorizationResult> ReauthorizeAsync(string authorizationId, Money amount, string paypalRequestId, CancellationToken ct);
    Task VoidAsync(string authorizationId, string paypalRequestId, CancellationToken ct);
    Task<RefundResult> RefundAsync(string captureId, Money? amount, string paypalRequestId, CancellationToken ct);
    Task<OrderSnapshot> GetOrderSnapshotAsync(string paypalOrderId, CancellationToken ct); // unknown-outcome re-read
}

public interface ICardVault
{
    Task<SavedCardResult> SaveCardAsync(CardDetails card, string buyerId, string paypalRequestId, CancellationToken ct);
    Task DeletePaymentTokenAsync(string paymentTokenId, CancellationToken ct);
}

public interface ITransactionReportReader
{
    IAsyncEnumerable<TransactionRecord> SearchAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
```

(`Money`, `AuthorizationResult`, etc. are small plain DTOs in `ApplicationCore` — exact shape
is an implementation call, not a PayPal fact; keep them provider-agnostic, e.g. no PayPal enum
types, only strings/decimals.) `Infrastructure/PayPal/PayPalGateway.cs`,
`PayPalCardVault.cs`, `PayPalTransactionReportReader.cs` implement these against
`client.Orders`, `client.Payments`, `client.Vault`, `client.TransactionSearch` respectively,
translating PayPal SDK types to/from the DTOs above and doing all the error/retry handling
described in this document. **No PayPal SDK type crosses into ApplicationCore or PublicApi's
endpoint/request/response DTOs.**

### 1.4 Endpoints (PublicApi, follow `src/PublicApi/CatalogItemEndpoints/*` conventions)

New folder `src/PublicApi/OrderEndpoints/` and `src/PublicApi/PaymentMethodEndpoints/` and
`src/PublicApi/ReconciliationEndpoints/`, one `IEndpoint<,,>` class per route, each with its
own `*Request`/`*Response` partial files inheriting `BaseRequest`/`BaseResponse`:

| Route | Method | Auth | Endpoint class (suggested) |
| --- | --- | --- | --- |
| `/api/orders` | POST | any authenticated user; acts on caller only | `CreateOrderEndpoint` |
| `/api/orders/{orderId}/pay` | POST | caller must own the order | `PayOrderEndpoint` |
| `/api/orders/{orderId}/fulfil` | POST | `Administrators` | `FulfilOrderEndpoint` |
| `/api/orders/{orderId}/cancel` | POST | `Administrators` | `CancelOrderEndpoint` |
| `/api/orders/{orderId}/refunds` | POST | `Administrators` | `RefundOrderEndpoint` |
| `/api/my-orders` | GET | any authenticated user; returns caller's own orders only | `GetMyOrdersEndpoint` |
| `/api/reconciliation` | GET | `Administrators` | `GetReconciliationEndpoint` |
| `/api/payment-methods` | POST | any authenticated user | `CreatePaymentMethodEndpoint` |
| `/api/payment-methods` | GET | any authenticated user; caller's own only | `GetPaymentMethodsEndpoint` |
| `/api/payment-methods/{paymentMethodId}` | DELETE | any authenticated user; caller's own only | `DeletePaymentMethodEndpoint` |

The task says cancel/fulfil/reconciliation are operator actions, restricted to the
"administrator role this project already uses" — that's
`[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`, the exact attribute already
on `CreateCatalogItemEndpoint.cs`/`DeleteCatalogItemEndpoint.cs`. Every other endpoint uses
`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` with no role, and
resolves the caller's own `Buyer`/`Order` ownership from the JWT inside `HandleAsync` — no
endpoint in PublicApi does this today (per the survey), so this is new plumbing: read
`ClaimTypes.Name` off `HttpContext.User`, and use it as `Order.BuyerId`/`Buyer.IdentityGuid`
exactly as `OrderService`/`CustomerOrdersSpecification` already do elsewhere in the codebase —
confirm the exact claim/field names against `src/Infrastructure/Identity/IdentityTokenClaimService.cs`
and `OrderController.cs` before writing this, since the survey only located the precedent, not
the exact member names.

Response identifiers (task requirement): `CreateOrderEndpoint` response has top-level
`OrderId`; `CreatePaymentMethodEndpoint` response has top-level `PaymentMethodId`;
`RefundOrderEndpoint` response has top-level `RefundId`.

### 1.5 Configuration

`src/PublicApi/appsettings.json` gets a new `PayPal` section; bind it the same way
`BaseUrlConfiguration` is bound in `Program.cs` (`GetRequiredSection` + `Configure<PayPalOptions>`):

```csharp
public class PayPalOptions
{
    public const string CONFIG_NAME = "PayPal";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Environment { get; set; } = "Sandbox";
    public string Currency { get; set; } = "";
    public string? BaseUrl { get; set; }
}
```

Bound from `PAYPAL_CLIENT_ID`/`PAYPAL_CLIENT_SECRET`/`PAYPAL_ENVIRONMENT`/`PAYPAL_CURRENCY` via
standard ASP.NET Core environment-variable configuration (`:`-separated keys map to
double-underscore env vars, i.e. `PayPal__ClientId` — **the task's env var names
(`PAYPAL_CLIENT_ID` etc.) are flat, not `PayPal__`-prefixed**, so either the host's launch
profile / provisioning maps them explicitly to `PayPal:ClientId` etc. in `appsettings` /
`launchSettings.json` `environmentVariables`, or `Program.cs` reads the flat env vars directly
and assigns them onto the bound `PayPalOptions` after the `Configure<>()` call, before
`ValidateOnStart()` runs. Confirm which env vars are actually present in the build/verification
environment and wire whichever mapping is needed — this is plumbing, not a PayPal fact.

**Fail-fast at startup** (per `dotnet-authentication` — see trap notes and PRODUCTION READINESS
§6 row 1): `ValidateDataAnnotations()` + `ValidateOnStart()` on `PayPalOptions`
(`[Required]` on `ClientId`/`ClientSecret`/`Currency`), plus an explicit check that
`Environment` case-insensitively equals `"Sandbox"` — **this SDK version's `ServerEnvironment`
enum ships only a `Sandbox` constant** (confirmed from `Servers/ServerEnvironment.cs` — no
`Production`/`Live` member exists), so any other configured value is a deployment fault and
must fail boot with a clear message naming the config key, not silently default to sandbox or
throw a confusing runtime error. The task only ever targets sandbox, so this is not a
limitation in practice — `PayPal:ClientId`/`ClientSecret` are what changes between PayPal
accounts, never `PayPal:Environment`.

### 1.6 DI wiring (Infrastructure or PublicApi `Program.cs`)

```csharp
services.AddPayPalServerSdkClient(options =>
{
    var o = configuration.GetSection(PayPalOptions.CONFIG_NAME).Get<PayPalOptions>()!; // already validated at startup
    options.Oauth2 = new OAuth2ClientCredentials { ClientId = o.ClientId, ClientSecret = o.ClientSecret };
    options.Environment = ServerEnvironment.Sandbox;
    if (!string.IsNullOrWhiteSpace(o.BaseUrl))
        options.Server.Default.Sandbox.BaseUrl = o.BaseUrl; // overrides EVERY call on this SDK incl. the token endpoint —
                                                              // there is exactly one server group ("Default") in this SDK
                                                              // (sdk-map.md "Servers & auth": 1 server group), so there is
                                                              // no separate auth base URL to override.
    options.Logging.LoggerFactory = loggerFactory;  // explicit — see PRODUCTION READINESS §6 rows 6/7
    options.Logging.LogRequestBody = false;          // explicit — card data must never be logged (task constraint)
    options.Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) }; // see §6 row 3
});
services.AddScoped<IPaymentGateway, PayPalGateway>();
services.AddScoped<ICardVault, PayPalCardVault>();
services.AddScoped<ITransactionReportReader, PayPalTransactionReportReader>();
```

`AddPayPalServerSdkClient` registers the client as a singleton over the default
`IHttpClientFactory` client (per `dotnet-client-initialization`); set
`PooledConnectionLifetime` on that default client's primary handler so a sandbox DNS change
doesn't get cached indefinitely for the process lifetime (same section, "⚠ The extension
registers the client as a `singleton`").

---

## 2. Flow-by-flow sequence → PayPal operations

1. **`POST /api/orders`** — no PayPal call. Looks up each catalog item id via
   `IRepository<CatalogItem>`, builds `OrderItem`s at current catalog price, constructs
   `Order` (reusing the existing `Order(buyerId, shipToAddress, items)` ctor — `ShipToAddress`
   supplied in the request body, since there's no basket/checkout UI feeding it here), status
   `AwaitingPayment`. Returns `OrderId`.
2. **`POST /api/orders/{orderId}/pay`** — load `Order` (must belong to caller, status
   `AwaitingPayment`), compute the order total server-side from `Order.OrderItems` (never trust
   a client-supplied amount — task: "Amounts come from catalog prices"). Claim the transition
   `AwaitingPayment → Authorizing` (§7 DUPLICATE CLAIMS row 1). Resolve the card: either the
   request's inline card fields (one-off) or a `paymentMethodId` naming one of the caller's own
   saved `PaymentMethod`s (§3 cross-operation invariant — never trust a raw vault id from the
   request). Call **`Orders.CreateOrder`** with `Intent = Authorize` and `PaymentSource.Card`
   set (full card fields, or `VaultId` from the resolved `PaymentMethod.CardId`), amount =
   order total, `PayPalRequestId` = a deterministic key derived from `OrderId` (e.g.
   `$"order-{orderId}-authorize"` — stable across retries of the *same* pay attempt, task:
   "idempotent in effect: a double-click never authorizes... twice").
   - If the response `Order.Status == PayerActionRequired`: **this is the sandbox-card 3DS
     challenge case the task says to stop and report** — do not attempt any redirect/approval
     round-trip; fail the pay attempt with a clear "payer action required, not supported by
     this integration" error, revert the claim to `AwaitingPayment` (or a terminal
     `PaymentFailed`), and surface this to the human running the build/verification session as
     a stop-and-report condition per the task if it is ever actually observed against the
     sandbox test card.
   - Else if `purchase_units[0].payments.authorizations` is already populated (grounded:
     `OrderStatus.Completed`'s doc says a completed order can mean "a payment was authorized"
     — i.e. CreateOrder can finish the authorization synchronously when a valid
     `payment_source` was supplied): use that `AuthorizationWithAdditionalData` directly — no
     further call.
   - Else (status `Created`/`Approved`, no authorization yet present): call
     **`Orders.AuthorizeOrder`** with `Id` = the PayPal order id, `PayPalRequestId` =
     `$"order-{orderId}-authorize-step"`. Read the authorization off
     `OrderAuthorizeResponse.PurchaseUnits[0].Payments.Authorizations[0]`.
     *(Which of these two branches actually fires against the sandbox test card is
     UNVERIFIED from the map/source — both are handled, so no guess is load-bearing; see trap
     note and §9.)*
   - Persist `PayPalOrderId`, `PayPalAuthorizationId` = `AuthorizationWithAdditionalData.Id`,
     `AuthorizationStatus` = `.Status.Value`, `AuthorizationExpiresAt` = parsed
     `.ExpirationTime`. Set `Payment.Status = Authorized`, `Order.Status = Authorized`.
3. **`POST /api/orders/{orderId}/fulfil`** (admin) — load `Order`+`Payment` (status must be
   `Authorized`). Claim `Authorized → Capturing`. If `AuthorizationExpiresAt` has passed (or
   the capture call below fails with an expiry-shaped error), call
   **`Payments.ReauthorizePayment`** (`AuthorizationId`, `Body.Amount` = order total,
   `PayPalRequestId` = `$"order-{orderId}-reauth"`) **once**; on success, overwrite
   `PayPalAuthorizationId`/`AuthorizationStatus`/`AuthorizationExpiresAt` from the *returned*
   `PaymentAuthorization` (never assume the id is unchanged — UNVERIFIED, see trap note), then
   proceed. On reauthorize failure (e.g. past the 29-day window, or already reauthorized once —
   both documented in `ReauthorizeRequest`'s XML remarks), map `ex.Error.Name`/`.Message`
   straight into the response as an operator-actionable message (task: "must say so in terms
   an operator can act on") and leave `Payment.Status` at a reauthorization-failed terminal
   state; do not capture. Otherwise call **`Payments.CaptureAuthorizedPayment`**
   (`AuthorizationId`, `Body.Amount` = order total, `Body.FinalCapture = true`,
   `PayPalRequestId` = `$"order-{orderId}-capture"`). On success, persist
   `PayPalCaptureId` = `CapturedPayment.Id`, `CaptureStatus` = `.Status.Value`,
   `CapturedAmount` = `.Amount.Value`, `PayPalFeeAmount` =
   `.SellerReceivableBreakdown.PaypalFee.Value`, `NetAmount` =
   `.SellerReceivableBreakdown.NetAmount.Value`; `Payment.Status = Captured`;
   `Order.Status = Fulfilled`, `Order.FulfilledAt = now`.
4. **`POST /api/orders/{orderId}/cancel`** (admin) — load `Order`+`Payment` (status must be
   `AwaitingPayment` or `Authorized` — i.e. before fulfilment). Claim `→ Cancelling`. Call
   **`Payments.VoidPayment`** (`AuthorizationId`, `PayPalRequestId` =
   `$"order-{orderId}-void"`). A `409` here (already voided/captured) is read as "already in
   the target state" and treated as idempotent success rather than surfaced as an error — the
   exact `Error.Name` PayPal sends for this is UNVERIFIED, so match defensively on the 409
   status plus a best-effort name check, confirm the real value from the first live 409 and
   tighten the check (see trap note). On success, `Payment.Status = Cancelled`,
   `Order.Status = Cancelled`, `Order.CancelledAt = now`. If `Order.Status` was still
   `AwaitingPayment` (no authorization exists yet), there is nothing to void — just transition
   local state.
5. **`POST /api/orders/{orderId}/refunds`** (admin) — load `Order`+`Payment` (status must be
   `Captured` or `PartiallyRefunded`). Claim by inserting a `Refund` row keyed on
   `(PaymentId, IdempotencyKey)` with `Status = "PENDING"` and no `PayPalRefundId` yet (§7 row
   4) — a unique-constraint violation on this insert means the same key was already used:
   return the **existing** `Refund` row's result instead of calling PayPal again (task:
   "repeating a request under the same key must not refund twice"). Compute the refundable
   remainder server-side from `Payment.CapturedAmount` minus the sum of previously-completed
   refunds for this `Payment` (task: "must never become refundable beyond what was captured")
   and reject a request whose amount exceeds it *before* calling PayPal. Call
   **`Payments.RefundCapturedPayment`** (`CaptureId` = `Payment.PayPalCaptureId`, `Body.Amount`
   = the requested amount or omitted for a full refund, `PayPalRequestId` = the caller's own
   idempotency key — forwarding it verbatim is exactly what the task's "caller-supplied
   idempotency key" maps onto on the wire). On success, fill in the claim row's
   `PayPalRefundId`/`Status`/amounts from the returned `Refund`; set `Payment.Status` to
   `Refunded` (sum of refunds == captured amount) or `PartiallyRefunded` otherwise;
   `RefundId` is the endpoint's top-level response field.
6. **`GET /api/my-orders`** — no PayPal call; reads `Order`+`Payment`+`Refund`s for
   `BuyerId == caller` from the local DB.
7. **`GET /api/reconciliation?from=&to=`** (admin) — calls
   **`TransactionSearch.SearchTransactions`** (see §5/§8 for chunking/paging) for the PayPal
   side, and reads local `Payment`/`Refund` rows created/updated in `[from, to]` for the eShop
   side, then lines them up by `TransactionInformation.TransactionId` against
   `Payment.PayPalCaptureId` / `Refund.PayPalRefundId` (a capture's and its refund's PayPal
   transaction id is the same id the Payments API returned — both are "the PayPal-generated
   ID" for that capture/refund resource). Rows present on one side only are surfaced as
   mismatches, in both directions, per the task.
8. **`POST /api/payment-methods`** — call **`Vault.CreatePaymentToken`** with
   `Body.PaymentSource.Card` = the submitted card fields, `Body.Customer.MerchantCustomerId` =
   caller's `BuyerId`, `PayPalRequestId` = a fresh value per call (not reused across distinct
   save attempts — saving the same card twice under two aliases is legitimate, see §7 row 5).
   Persist a new `PaymentMethod` under the caller's `Buyer` with `CardId` =
   `PaymentTokenResponse.Id`, `Last4` = `.PaymentSource.Card.LastDigits`, `Brand` =
   `.PaymentSource.Card.Brand.Value`, `Expiry` = `.PaymentSource.Card.Expiry`. Full card number
   is never persisted and never logged (see §6 row 7). Response top-level `PaymentMethodId`.
9. **`GET /api/payment-methods`** — no PayPal call; reads the caller's own non-deleted
   `PaymentMethod`s.
10. **`DELETE /api/payment-methods/{paymentMethodId}`** — load the `PaymentMethod` (must
    belong to caller). Call **`Vault.DeletePaymentToken`** (`Id` = `CardId`); a `404` (already
    gone server-side) is treated as idempotent success. Soft-delete locally (`IsDeleted = true`)
    rather than hard-delete, so `Payment`/`Order` history that doesn't FK into `PaymentMethod`
    is unaffected either way, and a second `DELETE` of the same id is a no-op success.

---

## 3. CONTRACT SHEET

⚠ Signatures below are generated code, verbatim from the SDK source read this session. Every
operation that takes input takes **one** request record as its first parameter, built with an
object initializer using that record's own property names — never flat arguments. Every SDK
type below is written fully-qualified with the namespace implied by its source path (map's
*Namespaces by content type* table): `Requests.*` types → `PayPalServerSdk.Requests[.Controller]`,
`Models.*` → `PayPalServerSdk.Models`, `Models.Enums.*` → `PayPalServerSdk.Models.Enums`,
`Errors.*` → `PayPalServerSdk.Errors`.

All operations: **Auth** `options.Oauth2` · **Server group** `Default` (only group in this SDK)
· **No no-throw `…Result` sibling** (this SDK is throw-only — confirmed, sdk-map.md: "No-throw
variants: absent across this SDK").

### 3.1 `Orders` (`client.Orders`) — source `Api/Orders.cs`

| Operation | Request record (required members) | Body model (required/key fields) | Returns (fields read) | Error case | Pagination | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `CreateOrder` | `Requests.Orders.CreateOrderRequest { Body (required), PayPalRequestId, Prefer="return=minimal" }` | `Models.OrderRequest { Intent (required, CheckoutPaymentIntent), PurchaseUnits (required, IReadOnlyList<PurchaseUnitRequest>), PaymentSource }` → `PurchaseUnitRequest { Amount (required, AmountWithBreakdown{CurrencyCode,Value both required}) }` → `PaymentSource { Card: CardRequest{Name,Number,Expiry,SecurityCode,BillingAddress,VaultId} }` | `Models.Order { Id, Status (OrderStatus), PurchaseUnits[].Payments.Authorizations[] (AuthorizationWithAdditionalData{Id,Status,Amount,ExpirationTime}) }` | A: `ApiException<CreateOrderError>` — `TryGetError(out Error)`[400,401,422] · `TryGetRawError`[fallback] | none | `Requests/Orders/CreateOrderRequest.cs`, `Models/OrderRequest.cs`, `Models/Order.cs`, `Errors/CreateOrderError.cs` |
| `AuthorizeOrder` | `Requests.Orders.AuthorizeOrderRequest { Id (required), Body (OrderAuthorizeRequest, optional), PayPalRequestId, Prefer }` | `Models.OrderAuthorizeRequest { PaymentSource (optional) }` — left unset; payment source already supplied at `CreateOrder` | `Models.OrderAuthorizeResponse { Id, Status, PurchaseUnits[].Payments.Authorizations[] }` | A: `ApiException<AuthorizeOrderError>` — `TryGetError(out Error)`[400,401,403,404,422,500] · `TryGetRawError`[fallback] | none | `Requests/Orders/AuthorizeOrderRequest.cs`, `Models/OrderAuthorizeRequest.cs`, `Models/OrderAuthorizeResponse.cs`, `Errors/AuthorizeOrderError.cs` |
| `GetOrder` | `Requests.Orders.GetOrderRequest { Id (required), Fields (query, optional) }` | — | `Models.Order` (same shape as above — unknown-outcome re-read) | A: `ApiException<GetOrderError>` — `TryGetError(out Error)`[401,404] · `TryGetRawError`[fallback] | none | `Requests/Orders/GetOrderRequest.cs`, `Models/Order.cs`, `Errors/GetOrderError.cs` |

### 3.2 `Payments` (`client.Payments`) — source `Api/Payments.cs`

| Operation | Request record (required) | Body model | Returns (fields read) | Error case | Pagination | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `CaptureAuthorizedPayment` | `CaptureAuthorizedPaymentRequest { AuthorizationId (required), Body (CaptureRequest, optional), PayPalRequestId, Prefer }` | `Models.CaptureRequest { Amount: Money?, FinalCapture: bool?=false, NoteToPayer }` | `Models.CapturedPayment { Id, Status (CaptureStatus), Amount (Money), SellerReceivableBreakdown.PaypalFee, .NetAmount }` | A: `ApiException<CaptureAuthorizedPaymentError>` — `TryGetError(out Error)`[400,401,403,404,409,422] · `TryGetNoContent(out RawError)`[500] · `TryGetRawError`[fallback] | none | `Requests/Payments/CaptureAuthorizedPaymentRequest.cs`, `Models/CaptureRequest.cs`, `Models/CapturedPayment.cs`, `Models/SellerReceivableBreakdown.cs`, `Errors/CaptureAuthorizedPaymentError.cs` |
| `VoidPayment` | `VoidPaymentRequest { AuthorizationId (required), PayPalRequestId, Prefer }` (no Body) | — | `Models.PaymentAuthorization { Id, Status }` | A: `ApiException<VoidPaymentError>` — `TryGetError(out Error)`[401,403,404,409,422] · `TryGetNoContent(out RawError)`[500] · `TryGetRawError`[fallback] | none | `Requests/Payments/VoidPaymentRequest.cs`, `Models/PaymentAuthorization.cs`, `Errors/VoidPaymentError.cs` |
| `ReauthorizePayment` | `ReauthorizePaymentRequest { AuthorizationId (required), Body (ReauthorizeRequest, optional), PayPalRequestId, Prefer }` | `Models.ReauthorizeRequest { Amount: Money? }` — "Supports only the amount request parameter" (XML remarks) | `Models.PaymentAuthorization { Id, Status, ExpirationTime }` | A: `ApiException<ReauthorizePaymentError>` — `TryGetError(out Error)`[400,401,403,404,422] · `TryGetNoContent(out RawError)`[500] · `TryGetRawError`[fallback] | none | `Requests/Payments/ReauthorizePaymentRequest.cs`, `Models/ReauthorizeRequest.cs`, `Models/PaymentAuthorization.cs`, `Errors/ReauthorizePaymentError.cs` |
| `RefundCapturedPayment` | `RefundCapturedPaymentRequest { CaptureId (required), Body (RefundRequest, optional), PayPalRequestId, Prefer }` | `Models.RefundRequest { Amount: Money?, NoteToPayer }` — "For a full refund, include an empty request body. For a partial refund, include an amount object" (XML remarks) | `Models.Refund { Id, Status (RefundStatus), Amount, SellerPayableBreakdown }` | A: `ApiException<RefundCapturedPaymentError>` — `TryGetError(out Error)`[400,401,403,404,409,422] · `TryGetNoContent(out RawError)`[500] · `TryGetRawError`[fallback] | none | `Requests/Payments/RefundCapturedPaymentRequest.cs`, `Models/RefundRequest.cs`, `Models/Refund.cs`, `Errors/RefundCapturedPaymentError.cs` |
| `GetAuthorizedPayment` | `GetAuthorizedPaymentRequest { AuthorizationId (required) }` | — | `Models.PaymentAuthorization` | A: `ApiException<GetAuthorizedPaymentError>` — `TryGetError(out Error)`[401,403,404] · `TryGetNoContent(out RawError)`[500] · `TryGetRawError`[fallback] | none | `Requests/Payments/GetAuthorizedPaymentRequest.cs`, `Errors/GetAuthorizedPaymentError.cs` |
| `GetCapturedPayment` | `GetCapturedPaymentRequest { CaptureId (required) }` | — | `Models.CapturedPayment` | A: `ApiException<GetCapturedPaymentError>` — `TryGetError(out Error)`[401,403,404] · `TryGetNoContent(out RawError)`[500] · `TryGetRawError`[fallback] | none | `Requests/Payments/GetCapturedPaymentRequest.cs`, `Errors/GetCapturedPaymentError.cs` |
| `GetRefund` | `GetRefundRequest { RefundId (required) }` | — | `Models.Refund` | A: `ApiException<GetRefundError>` — `TryGetError(out Error)`[401,403,404] · `TryGetNoContent(out RawError)`[500] · `TryGetRawError`[fallback] | none | `Requests/Payments/GetRefundRequest.cs`, `Errors/GetRefundError.cs` |

### 3.3 `Vault` (`client.Vault`) — source `Api/Vault.cs`

| Operation | Request record (required) | Body model | Returns (fields read) | Error case | Pagination | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `CreatePaymentToken` | `CreatePaymentTokenRequest { Body (required), PayPalRequestId }` | `Models.PaymentTokenRequest { Customer: Customer?, PaymentSource (required): PaymentTokenRequestPaymentSource { Card: PaymentTokenRequestCard{Name,Number,Expiry,SecurityCode,Brand,BillingAddress} } }` | `Models.PaymentTokenResponse { Id, PaymentSource.Card (CardPaymentTokenEntity){LastDigits,Brand,Expiry,Name} }` | A: `ApiException<CreatePaymentTokenError>` — `TryGetError(out Error)`[400,403,404,422,500] · `TryGetRawError`[fallback] | none | `Requests/Vault/CreatePaymentTokenRequest.cs`, `Models/PaymentTokenRequest.cs`, `Models/PaymentTokenRequestPaymentSource.cs`, `Models/PaymentTokenRequestCard.cs`, `Models/PaymentTokenResponse.cs`, `Models/CardPaymentTokenEntity.cs`, `Errors/CreatePaymentTokenError.cs` |
| `DeletePaymentToken` | `DeletePaymentTokenRequest { Id (required) }` | — | `void` (Task) | A: `ApiException<DeletePaymentTokenError>` — `TryGetError(out Error)`[400,403,500] · `TryGetRawError`[fallback] (a 404 surfaces only via `TryGetRawError`) | none | `Requests/Vault/DeletePaymentTokenRequest.cs`, `Errors/DeletePaymentTokenError.cs` |

(`GetPaymentToken`, `CreateSetupToken`, `GetSetupToken`, `ListCustomerPaymentTokens` are in
scope of the SDK but **out of scope of this integration** — `GET`/`DELETE /api/payment-methods`
are served entirely from the local `PaymentMethod` table, never from PayPal's vault list, so no
PayPal call is needed to read them back. `YOUR CALL — not in the map`: simpler, avoids resolving
`ListCustomerPaymentTokens`'s `customer_id` semantics, and the task only requires the caller's
own saved cards to be listable, which the local table already scopes by `BuyerId`.)

### 3.4 `TransactionSearch` (`client.TransactionSearch`) — source `Api/TransactionSearch.cs`

| Operation | Request record (required) | Returns (fields read) | Error case | Pagination | Source |
| --- | --- | --- | --- | --- | --- |
| `SearchTransactions` | `SearchTransactionsRequest { StartDate (required), EndDate (required), Page=1, PageSize=100, Fields="transaction_info", BalanceAffectingRecordsOnly="Y" }` | `Models.SearchResponse { TransactionDetails[].TransactionInfo (TransactionInformation){TransactionId,TransactionAmount,TransactionStatus,TransactionInitiationDate}, Page, TotalPages, TotalItems }` | **B**: `ApiException<RawError>` — `StatusCode`/`ReadAsString()`/`ReadAsJson<T>()` | **none** (plain list call — map states no pagination metadata; `page`/`page_size` are driven by hand, see §8) | `Requests/TransactionSearch/SearchTransactionsRequest.cs`, `Models/SearchResponse.cs`, `Models/TransactionDetails.cs`, `Models/TransactionInformation.cs` |

Contract fact worth flagging: `SearchTransactionsRequest`'s XML doc on `EndDate` states "The
maximum supported range is 31 days" — a `[from, to]` wider than 31 days **must be chunked**
into ≤31-day windows before calling `SearchTransactions`, each window paged independently (see
§8 PAGED READS). This is a provider constraint, not a client-side choice.

### 3.5 Enum value tables actually used

- `CheckoutPaymentIntent` (`Models/Enums/CheckoutPaymentIntent.cs`): `Capture` ("CAPTURE"),
  **`Authorize`** ("AUTHORIZE") — used for every `CreateOrder` call in this integration.
- `OrderStatus` (`Models/Enums/OrderStatus.cs`): `Created`, `Saved`, `Approved`, `Voided`,
  `Completed`, **`PayerActionRequired`** — the last is the task's "challenge requiring a
  browser" stop condition.
- `AuthorizationStatus` (`Models/Enums/AuthorizationStatus.cs`): `Created`, `Captured`,
  `Denied`, `PartiallyCaptured`, `Voided`, `Pending`.
- `CaptureStatus` (`Models/Enums/CaptureStatus.cs`): `Completed`, `Declined`,
  `PartiallyRefunded`, `Pending`, `Refunded`, `Failed`.
- `RefundStatus` (`Models/Enums/RefundStatus.cs`): `Cancelled`, `Failed`, `Pending`, `Completed`.

### 3.6 Client construction / auth / server facts

- Client class `PayPalServerSdkClient(HttpClient, PayPalServerSdkClientOptions)`; DI via
  `services.AddPayPalServerSdkClient(options => ...)` (source: `sdk-map.md` "Getting a
  client", `ServiceCollectionExtensions.cs`).
- Auth: `options.Oauth2 = new OAuth2ClientCredentials { ClientId, ClientSecret }` (namespace
  `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials`). Token endpoint
  `https://api-m.sandbox.paypal.com/v1/oauth2/token`, cached per client instance, re-acquired
  on `401` (`dotnet-authentication`).
- Environment: `ServerEnvironment.Sandbox` (`Servers/ServerEnvironment.cs`) — **the only
  constant this SDK version declares**.
- Server group: `Default`, base URL `https://api-m.sandbox.paypal.com`, override point
  `options.Server.Default.Sandbox.BaseUrl` (`sdk-map.md` "Servers & auth" — 1 server group
  total).

### 3.7 CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| A `payment_source.card.vault_id` used in `CreateOrder` must be a PayPal payment-token id this buyer actually owns | `Orders.CreateOrder` ← `Vault.CreatePaymentToken` | `PayOrderEndpoint.HandleAsync` — resolves `paymentMethodId` to `PaymentMethod` filtered by `BuyerId == caller` *and* `IsDeleted == false` before reading `.CardId`; never accepts a raw vault id string from the request body |
| An `authorization_id` used in `CaptureAuthorizedPayment`/`VoidPayment`/`ReauthorizePayment`/`GetAuthorizedPayment` must be the one this order's own authorize step returned | `Payments.CaptureAuthorizedPayment`, `Payments.VoidPayment`, `Payments.ReauthorizePayment` ← `Orders.CreateOrder`/`Orders.AuthorizeOrder`/`Payments.ReauthorizePayment` | `FulfilOrderEndpoint`/`CancelOrderEndpoint`.`HandleAsync` — always reads `Payment.PayPalAuthorizationId` from the local `Payment` row for that `Order`, never from caller input (no endpoint accepts an authorization id as a parameter) |
| A `capture_id` used in `RefundCapturedPayment`/`GetCapturedPayment` must be the one this order's own fulfil step returned | `Payments.RefundCapturedPayment` ← `Payments.CaptureAuthorizedPayment` | `RefundOrderEndpoint.HandleAsync` — always reads `Payment.PayPalCaptureId` from the local `Payment` row; no endpoint accepts a capture id as a parameter |

---

## 4. Trap notes (hazard named, resolution deferred to the named skill)

1. **Client/DI lifetime** — registering `PayPalGateway` as `AddScoped` while the SDK client
   itself is a DI singleton is fine, but constructing `PayPalServerSdkClient` by hand anywhere
   (e.g. a console verification script) risks a fresh token fetch per instance and loses the
   pooled-connection lifetime fix. **MUST load `dotnet-client-initialization`** before writing
   client construction/registration code.
2. **Credential validation ordering** — setting `options.Oauth2` with a blank `ClientSecret`
   produces no exception at registration time, only an eventual `401` on first call, which is
   indistinguishable from a real auth problem unless startup validation already ran first.
   **MUST load `dotnet-authentication`** before wiring `PayPalOptions` validation.
3. **`PayPalRequestId` vs the generator-injected `Idempotency-Key` header** — every write
   operation in scope carries a *real*, caller-supplied `PayPalRequestId` member on its request
   record (confirmed per-operation above), which is different from the generator's own
   `Idempotency-Key: Guid.NewGuid()` header injected on every non-GET call regardless. Setting
   `PayPalRequestId` is necessary for the idempotency design in §7; the injected header must
   not be mistaken for it. **MUST load `dotnet-configuration-resilience`** (§ *Making a write
   safe under retries*) before implementing any of the write paths.
4. **Enum/typed-error reading** — every operation above is Case A except `SearchTransactions`
   (Case B). Writing the `catch` ladder for a Case A operation from this sheet's accessor list
   alone (without re-deriving method bodies from memory) and getting `TryGetRawError`'s
   "fallback only, not catch-all" semantics right is easy to get backwards. **MUST load
   `dotnet-error-handling`** before writing any `catch` block in `PayPalGateway`/`PayPalCardVault`.
5. **`CreateOrder` outcome branching (§2 step 2)** — whether PayPal finishes the authorization
   synchronously inside `CreateOrder` or requires the follow-up `AuthorizeOrder` call for a
   direct-card, non-interactive payment is not stated anywhere in the map or source read this
   session (only `OrderStatus`'s doc comments, which are consistent with either reading). The
   three-way branch in §2 step 2 handles both without guessing. **MUST load
   `dotnet-calling-endpoints`** (return-type/response-reading section) when implementing the
   branch, and **MUST load `dotnet-models`** for reading the `OrderStatus` open-enum value
   safely (`Match`/`TryGetKnownValue`, not a C# `switch`).
6. **Reauthorized-authorization identity (§2 step 3)** — whether `ReauthorizePayment` returns
   the same `authorization_id` or a new one is not stated in the XML remarks read this session.
   The plan always takes the id from the *response*, never assumes continuity. **MUST load
   `dotnet-models`** for correct `PaymentAuthorization.Id`/`Status` reading.
7. **Money formatting** — `AmountWithBreakdown.Value`/`Money.Value` are `string`, not
   `decimal`, with a documented regex shape; a `decimal.ToString()` under a non-invariant
   culture (comma decimal separators) would corrupt the wire value silently (passes the SDK's
   own validation attributes, which are never enforced — `dotnet-models` § *Validation
   attributes are documentation, not enforcement*). **MUST load `dotnet-models`** before
   writing the decimal↔string conversion helper.
8. **Logging of card data** — `CardRequest`/`PaymentTokenRequestCard` carry raw PAN/CVV in the
   request body; `LogRequestBody` and the `PAYPALSERVERSDKCLIENT_LOG` environment variable are
   two independent ways this can end up in logs unredacted. **MUST load
   `dotnet-configuration-resilience`** (§ *Logging*, § *Sensitive data*) before finalizing the
   client registration in §1.6.
9. **Reconciliation paging/chunking** — `SearchTransactions` is a plain (non-`Pageable`) list
   call with a documented 31-day range cap and manual `page`/`page_size`; naively driving it
   with only the provider's "fewer than `page_size` back" signal as the stop condition is an
   unbounded-loop risk. **MUST load `dotnet-configuration-resilience`** (§ *Never leave a page
   loop unbounded*) before implementing `GetReconciliationEndpoint`.
10. **Unknown outcomes on connection failure** — a transport failure on any of the six writes
    in §7/§8 does not mean the write didn't reach PayPal. **MUST load
    `dotnet-configuration-resilience`** (§ *A write whose outcome is unknown*) and
    **`dotnet-error-handling`** (§ *Connection failures*) before implementing `PayPalGateway`.

---

## 5. REQUIRED READING (load before implementation starts)

- `dotnet-client-initialization` — client construction, DI registration, `HttpClient`/pooled
  connection lifetime (trap note 1; §1.6).
- `dotnet-authentication` — credential wiring and fail-fast startup validation (trap note 2;
  §1.5 PRODUCTION READINESS row 1).
- `dotnet-calling-endpoints` — request-record construction and response reading for all 13
  operations in §3 (trap note 5).
- `dotnet-models` — `OpenStringEnum`/`Match`/`TryGetKnownValue` for every enum read in §3.5,
  decimal↔`Money.Value` string conversion (trap notes 5, 6, 7).
- `dotnet-error-handling` — the full Case A/B catch ladder for all 12 Case-A operations plus
  the one Case-B operation, and the boundary-mapping rules used throughout §2/§6 (trap note 4).
  Included even if the trap notes above were few, per this skill's own rule.
- `dotnet-configuration-resilience` — retries/timeouts (§6 rows 3–4), idempotency-key
  placement (trap note 3), logging/sensitive-data (trap note 8), pagination bounding (trap note
  9), unknown-outcome re-reads (trap note 10).
- `dotnet-testing` — the `HttpClient`-seam stub pattern for testing `PayPalGateway`/
  `PayPalCardVault`/`PayPalTransactionReportReader` without live sandbox calls in unit tests
  (integration-style tests against the real sandbox are additionally expected per the task's
  self-verification requirement).

⚠ Mandatory hazard carried verbatim from the skill: a body that does not match its declared
type — a drifted/malformed 2xx response or a non-2xx body that doesn't match its operation's
`{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException`
that is **not** an `ApiException<TError>`. Every catch ladder in `PayPalGateway`/
`PayPalCardVault`/`PayPalTransactionReportReader` must also catch `ResponseDeserializationException`
(or the non-generic `ApiException` base) alongside the typed/raw catches in §3, or a drifted
response escapes every typed handler.

---

## 6. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | **Credential fail-fast** | `PayPalOptions` bound via `IOptions<T>` with `[Required]` on `ClientId`/`ClientSecret`/`Currency`, `ValidateDataAnnotations()` + `ValidateOnStart()` (§1.5). Additionally, an explicit startup check that `Environment` case-insensitively equals `"Sandbox"` (this SDK ships no other environment constant — §1.5) — a check `ValidateDataAnnotations` alone can't express, done via a custom `IValidateOptions<PayPalOptions>`. Both parts of the credential (`ClientId` *and* `ClientSecret`) are checked individually — a blank secret with a present id is still a failure. |
| 2 | **Secret sourcing & rotation** | Sourced from `PayPal:ClientId`/`PayPal:ClientSecret` config keys (ultimately `PAYPAL_CLIENT_ID`/`PAYPAL_CLIENT_SECRET` env vars — mapping confirmed/adjusted at implementation time, §1.5). `AddPayPalServerSdkClient`'s `configure` callback reads `IConfiguration` once at registration and captures it in the singleton client (per `dotnet-client-initialization`), so a rotated secret takes effect only on process restart. Acceptable for this task (sandbox credentials, single verification run); no hot-rotation requirement stated. |
| 3 | **Total timeout budget** | `options.Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) }` bounds each attempt; an explicit per-request `CancellationToken` budget (`CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30))`, linked to `HttpContext.RequestAborted`) is applied once, in a single `Bounded(...)` helper inside `PayPalGateway`/`PayPalCardVault`, that every PayPal call goes through — not per call site. All *write* operations in scope use non-retried verbs (`POST`/`DELETE`) by default (`HttpMethodsToRetry` = `GET,HEAD,PUT,OPTIONS`), so their worst case under the 15s per-attempt timeout is one attempt ≈15s, well under the 30s call budget. The one `GET`-shaped read in the hot path, `TransactionSearch.SearchTransactions`, is retryable and bounded by the same 30s token. |
| 4 | **Write-retry ownership** | `HttpMethodsToRetry` is left at its default (`GET,HEAD,PUT,OPTIONS`) — every write this integration performs (`CreateOrder`, `AuthorizeOrder`, `CaptureAuthorizedPayment`, `VoidPayment`, `ReauthorizePayment`, `RefundCapturedPayment`, `CreatePaymentToken`, `DeletePaymentToken`) is `POST`/`DELETE` and is therefore **never** resent by the SDK itself on any trigger (status, transport fault, or per-attempt timeout). No write in this integration uses `PUT`. |
| 5 | **Idempotency & ambiguous writes** | See §7 DUPLICATE CLAIMS and §9 UNKNOWN OUTCOMES — every write carries a real `PayPalRequestId` (trap note 3), and every write is additionally guarded by a local conditional-state-transition or unique-constraint claim taken *before* the PayPal call. |
| 6 | **Observability** | Default SDK logging (`Information` request/response lines, `Warning` on retry/failure) is left on via `options.Logging.LoggerFactory` set explicitly to the host's `ILoggerFactory` (not left null — see row 7). `LogRequestHeaders`/`LogResponseHeaders`/`LogRequestBody` all stay `false`. `Error.DebugId` (PayPal's own correlation id) is captured into the translated exception/error DTO at the `PayPalGateway` boundary and logged alongside our own `OrderId`/`CaptureId` so a sandbox support request can be correlated. |
| 7 | **Sensitive data** | `CardRequest`/`PaymentTokenRequestCard` carry raw PAN (`Number`) and `SecurityCode` (trap note 8) — both request models read this session. Consequently: `LogRequestBody` stays `false` in every environment, and `options.Logging.LoggerFactory` is **always** assigned explicitly at registration (never left null), so the `PAYPALSERVERSDKCLIENT_LOG` environment variable can never switch body logging on from outside the code (per `dotnet-getting-started` § *Sensitive data*). Separately, at the application level: the card-bearing request DTOs on `PayOrderEndpoint`/`CreatePaymentMethodEndpoint` are excluded from any ASP.NET Core request-logging/correlation middleware this project may add, and are never persisted (only PayPal-returned ids/last4/brand are). |
| 8 | **Environment selection** | Single server group (`Default`), single environment this SDK exposes (`Sandbox`) — see §1.5. `PayPal:BaseUrl`, when set, overrides `options.Server.Default.Sandbox.BaseUrl`, which is the only override point this SDK exposes and covers every call including the OAuth2 token request (only one server group exists, §1.6). There is no live/production environment to accidentally target — the SDK itself cannot construct one with this plugin version. |
| 9 | **Duplicate prevention under concurrency** | See §7 DUPLICATE CLAIMS — claims are stored in the application's own EF Core-backed store (`Payment`/`Refund`/`PaymentMethod` tables), the same store already used for `Order`/`Buyer`. |
| 10 | **Partial results** | See §8 PAGED READS — `GetReconciliationEndpoint`'s response carries a `Truncated: bool` (and `NextFrom`/`NextPage` hint) field when a page/chunk cap is hit. |
| 11 | **Unknown outcomes** | See §9 UNKNOWN OUTCOMES. |

---

## 7. DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `POST /orders/{id}/pay` → `Orders.CreateOrder`(+`AuthorizeOrder`) | `Payment.Status` column on the `Payment` row for this `Order` (EF Core, same `DbContext` as `Order`) | A conditional update `WHERE OrderId=@id AND Status='AwaitingPayment'` (EF Core `ExecuteUpdateAsync` with that predicate, or an optimistic-concurrency token on `Payment.Status`) affecting 0 rows | The 0-rows-affected result in `PayOrderEndpoint.HandleAsync` | TBD |
| `POST /orders/{id}/fulfil` → `Payments.CaptureAuthorizedPayment` (+ optional `ReauthorizePayment`) | `Payment.Status` | Conditional update `WHERE OrderId=@id AND Status='Authorized'` affecting 0 rows | 0-rows-affected result in `FulfilOrderEndpoint.HandleAsync` | TBD |
| `POST /orders/{id}/cancel` → `Payments.VoidPayment` | `Payment.Status` | Conditional update `WHERE OrderId=@id AND Status IN ('AwaitingPayment','Authorized')` affecting 0 rows | 0-rows-affected result in `CancelOrderEndpoint.HandleAsync` | TBD |
| `POST /orders/{id}/refunds` → `Payments.RefundCapturedPayment` | New `Refund` row, unique index on `(PaymentId, IdempotencyKey)` | `DbUpdateException` (unique constraint violation) on the claim `INSERT` | `catch (DbUpdateException)` around the claim insert in `RefundOrderEndpoint.HandleAsync`, which then loads and returns the pre-existing `Refund` row instead of calling PayPal | TBD |
| `POST /payment-methods` → `Vault.CreatePaymentToken` | none (deliberate — see note) | n/a | n/a | n/a |
| `DELETE /payment-methods/{id}` → `Vault.DeletePaymentToken` | `PaymentMethod.IsDeleted` column | Conditional update `WHERE Id=@id AND BuyerId=@caller AND IsDeleted=0` affecting 0 rows | 0-rows-affected result in `DeletePaymentMethodEndpoint.HandleAsync` treated as idempotent success (already deleted) | TBD |

Note on `CreatePaymentToken`: unlike the money-moving writes, a double-click here creates two
distinct, independently-valid saved cards rather than moving money twice or refunding twice —
the task does not require single-token dedup, and a shopper may legitimately want to save the
same physical card under two aliases. No claim is taken. If stronger dedup is wanted later, the
real key is already available: `CreatePaymentTokenRequest.PayPalRequestId` is a genuine
provider-side idempotency key (§3.3) and could be set deterministically from a client-supplied
request header.

---

## 8. PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `GET /api/reconciliation` → `TransactionSearch.SearchTransactions`, hand-driven paging per ≤31-day chunk of `[from, to]` | A page cap (`MaxPages` per chunk, e.g. 50) **and** a chunk cap (`MaxChunks`, e.g. 24 — enough for a year at 31 days/chunk) — both backstops independent of `TotalPages`/`TotalItems` in the response | `GetReconciliationResponse.Truncated: bool` (set `true` if either cap is hit) plus `TruncatedAfter: DateTimeOffset?` naming the last fully-processed chunk boundary, so the caller can resume with a narrower `from` | TBD |

---

## 9. UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `Orders.CreateOrder` | Re-issue `Orders.CreateOrder` with the **same** `PayPalRequestId` (`$"order-{orderId}-authorize"`) — `CreateOrderRequest.PayPalRequestId`'s own doc states the server stores and dedupes on this key for exactly this call shape (payment source supplied at creation) | The deterministic `PayPalRequestId` itself | `PayPalGateway.AuthorizeAsync`, around the `CreateOrder` call | TBD |
| `Orders.AuthorizeOrder` | `Orders.GetOrder(paypalOrderId)` — by this point the PayPal order id is already known locally (persisted after `CreateOrder` succeeded), so a GET-based re-read is possible; check `purchase_units[].payments.authorizations[]` for an authorization already present | `Payment.PayPalOrderId` | `PayPalGateway.AuthorizeAsync`, around the `AuthorizeOrder` call | TBD |
| `Payments.CaptureAuthorizedPayment` | `Orders.GetOrder(paypalOrderId)` — reads `purchase_units[].payments.captures[]`; alternatively re-issue `CaptureAuthorizedPayment` with the same `PayPalRequestId` | `Payment.PayPalOrderId` (or the deterministic `PayPalRequestId`) | `PayPalGateway.CaptureAsync`, around the `CaptureAuthorizedPayment` call | TBD |
| `Payments.VoidPayment` | Re-issue `Payments.VoidPayment` with the same `PayPalRequestId`, or `Payments.GetAuthorizedPayment(authorizationId)` and check `Status == Voided` | `Payment.PayPalAuthorizationId` (or the deterministic `PayPalRequestId`) | `PayPalGateway.VoidAsync` | TBD |
| `Payments.ReauthorizePayment` | `Payments.GetAuthorizedPayment(authorizationId)` — if the *original* authorization now shows `Voided`/superseded, or a fresh capture attempt with the possibly-new id succeeds, the reauthorization landed | `Payment.PayPalAuthorizationId` | `PayPalGateway.ReauthorizeAsync` | TBD |
| `Payments.RefundCapturedPayment` | Re-issue `Payments.RefundCapturedPayment` with the same `PayPalRequestId` (the caller's own idempotency key) — PayPal's documented 45-day key storage means this returns the existing refund rather than creating a second one | The caller-supplied idempotency key (already persisted on the claim `Refund` row before the first attempt, §7) | `PayPalGateway.RefundAsync` | TBD |
| `Vault.CreatePaymentToken` | No PayPal-side re-read attempted — on a connection failure, the local `PaymentMethod` row is simply never created (no claim was taken to roll back, §7), and the endpoint returns a transient error for the caller to retry; a retry creates at most one extra vaulted card, which is an accepted, non-harmful duplicate per §7 | n/a | `PayPalCardVault.SaveCardAsync` | TBD |

---

## 10. Assumptions & Blockers

**Blockers: none.** Every PayPal capability the task requires (authorize, capture-at-fulfilment
with fee/net breakdown, void, partial/full refund with a caller idempotency key, vault a card,
delete a vaulted card, search transactions for reconciliation) maps onto an operation this SDK
exposes, grounded from the map/source as cited throughout §3.

**Assumptions** (decisions made where the task or the SDK left the call to this session's
judgment — none of these block planning, listed so the build session doesn't have to
re-litigate them):

1. `POST /api/orders` requires the caller to supply a shipping address in the request body
   (reusing `Order`'s existing `Address` value object) since there is no basket/checkout UI in
   this flow to source one from.
2. Amount formatting assumes a 2-decimal-place currency (consistent with the task's sandbox
   verification currency). Zero-decimal (e.g. `JPY`) or three-decimal (e.g. `KWD`) currencies
   are not specially handled — `Money.Value`/`AmountWithBreakdown.Value` formatting should use
   invariant-culture `"0.00"` unless `PayPal:Currency` is later extended beyond 2-decimal
   currencies, which the task's fixtures don't exercise.
3. `Order`/`Payment`/`Refund`/extended `PaymentMethod` are added as new EF Core entities with
   ordinary configuration classes alongside `OrderConfiguration.cs`/`OrderItemConfiguration.cs`
   in `src/Infrastructure/Data/Config/`; under `UseOnlyInMemoryDatabase=true` no migration is
   needed to verify the flows (task's environment gotchas), but a real migration should still
   be added for the SQL Server path so the project isn't left inconsistent.
4. The exact mapping from `PAYPAL_CLIENT_ID`/`PAYPAL_CLIENT_SECRET`/`PAYPAL_ENVIRONMENT`/
   `PAYPAL_CURRENCY` (flat env var names) onto the `PayPal:*` config keys the task mandates is
   left to the build session to wire against whatever shape those env vars actually arrive in
   (`launchSettings.json` `environmentVariables`, a `.env`-style loader, or direct
   `Environment.GetEnvironmentVariable` reads assigned onto `PayPalOptions` after binding) —
   this is host plumbing, not a PayPal contract fact.
5. Whether `CreateOrder` finishes the authorization synchronously for a direct-card payment, or
   requires the follow-up `AuthorizeOrder` call, is UNVERIFIED from the map/source (trap note
   5) — both branches are implemented so the real sandbox behavior, observed during the build
   session's self-verification, decides which path actually executes; this does not change the
   external contract (`POST /pay` either way authorizes and returns the same shape).
6. Whether a second `VoidPayment`/an attempted double-capture surfaces with a specific,
   recognizable `Error.Name` is UNVERIFIED — handled defensively by treating the documented
   `409` status itself as "already in the target state" (trap note under §2 step 4), to be
   tightened with the real `Error.Name` once observed against the sandbox.

---

## 11. Testing plan

- **Unit tests** (new `tests/UnitTests/PayPal/` or similar, xUnit per existing `tests/UnitTests`
  convention): `PayPalGateway`/`PayPalCardVault`/`PayPalTransactionReportReader` tested against
  the `HttpClient`-seam stub pattern from `dotnet-testing` — no live sandbox calls. Cover: the
  `CreateOrder` 3-way branch (Completed+authorized / needs-AuthorizeOrder / PayerActionRequired
  stop), the Case A error ladders (every `TryGet…` accessor in §3 gets at least one test),
  `ResponseDeserializationException` handling, the reauthorize-then-capture sequence including
  the "cannot be renewed" terminal path, and the 31-day chunking/paging logic for
  reconciliation with a forced page/chunk cap.
- **Integration tests** (`tests/PublicApiIntegrationTests`, MSTest + `WebApplicationFactory<Program>`
  per existing convention in `ProgramTest.cs`): endpoint-level tests for auth (non-admin token
  gets `Forbidden` on fulfil/cancel/refunds/reconciliation, per the existing
  `CreateCatalogItemEndpointTest.cs` pattern), ownership scoping (buyer A cannot see/act on
  buyer B's orders or payment methods), and the full happy-path sequence
  create→pay→fulfil→refund driven through the test server with the stub `HttpClient` swapped
  in for `PayPalServerSdkClient` (per `dotnet-testing` § DI-based stubbing).
- **Live sandbox self-verification** (task's "Rules of engagement" — performed by the build
  session, not this planning session): drive the full flow through `PublicApi`'s real HTTP
  endpoints against the actual PayPal sandbox using the Visa `4111 1111 1111 1111` test card —
  create → pay (one-off card) → fulfil (real capture, check fee/net on the response) → a second
  order paid with a saved card (`POST /payment-methods` → `POST /pay` with `paymentMethodId`) →
  partial refund (check idempotency key re-send doesn't double-refund) → cancel on a third,
  unfulfilled order → reconciliation over a date range known to contain the created
  transactions (expect PayPal reporting lag — do not treat an empty window near "now" as a
  failure, per the task).

---

## 12. Skills loaded this session

- `paypal-sdk:dotnet-integrate-pay-pal-server-sdk` (workflow; loaded first)
- `paypal-sdk:dotnet-getting-started` (SDK map/source location and conventions)
- `paypal-sdk:dotnet-authentication`
- `paypal-sdk:dotnet-client-initialization`
- `paypal-sdk:dotnet-calling-endpoints`
- `paypal-sdk:dotnet-configuration-resilience`
- `paypal-sdk:dotnet-error-handling`
- `paypal-sdk:dotnet-models`
- `paypal-sdk:dotnet-testing`
