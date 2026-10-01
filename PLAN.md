# PLAN — PayPal payments + saved cards for eShopOnWeb

This is the plan for a later build session. It merges the PayPal Server SDK contract sheet that
`dotnet-integrate-pay-pal-server-sdk` requires (normally written to `pay-pal-server-sdk-plan.md`) with the
application design for the task. No code was written in this session; `PLAN.md` is the only file it
touched. Grounding: a fresh shallow clone of `https://github.com/Darker98/paypal-csharp-sdk` (branch
`main`, API spec version `2.29`) in the system temp directory, read via `sdk-map.md` and the map pages/
source files it names. The clone path is intentionally not recorded here — the build session clones its
own copy per `dotnet-getting-started`.

---

## 1. Scope & sequence

Build in this order; each step names the PayPal operations it uses.

1. **Project wiring** — add `Darker98.PayPalServerSdk` (version-less) to `src/Infrastructure`. Add
   `PayPalOptions` + startup validation. Register the SDK client via DI in `src/PublicApi/Program.cs`
   (the client is only *called* from Infrastructure services, but registration needs the same
   configuration pipeline `Program.cs` already builds). No PayPal operation calls yet.
2. **Domain model** — extend `Order` (status machine) and add `OrderPayment`, `OrderRefund`,
   `PaymentMethod` entities + EF configuration + specifications. No PayPal calls.
3. **Gateway abstraction** — `IPayPalGateway` in `ApplicationCore.Interfaces`, implemented in
   `Infrastructure.PayPal` wrapping the SDK client. One method per capability below; this is where every
   PayPal operation call in this plan lives.
4. **`POST /api/orders`** — no PayPal call; creates the local `Order` + `OrderItem`s + `OrderPayment` row
   in `AwaitingPayment`.
5. **`POST /api/orders/{orderId}/pay`** — `Orders.CreateOrder` (one call does both "create" and
   "authorize" because a `payment_source` is supplied — see Cross-operation invariants and Trap notes).
6. **`POST /api/orders/{orderId}/fulfil`** — `Payments.GetAuthorizedPayment`, conditionally
   `Payments.ReauthorizePayment`, then `Payments.CaptureAuthorizedPayment`.
7. **`POST /api/orders/{orderId}/cancel`** — `Payments.VoidPayment`.
8. **`POST /api/orders/{orderId}/refunds`** — `Payments.RefundCapturedPayment`.
9. **`GET /api/my-orders`** — no PayPal call; reads local state.
10. **Saved cards** — `POST /api/payment-methods` (`Vault.CreateSetupToken` then `Vault.CreatePaymentToken`),
    `GET /api/payment-methods` (`Vault.ListCustomerPaymentTokens`), `DELETE /api/payment-methods/{id}`
    (`Vault.DeletePaymentToken`). Then re-run step 5 against a second order using a saved card.
11. **`GET /api/reconciliation`** — `TransactionSearch.SearchTransactions`, paged.
12. **Tests** (per `dotnet-testing`) + manual sandbox verification (§8) + the user-facing verification guide.

---

## 2. CONTRACT SHEET

⚠ Signatures below are generated code, verbatim. Every operation that takes input takes **one** request
record as its first parameter, built with an object initializer using that record's own property names —
never flat arguments. Every SDK type is written fully-qualified with the namespace its declaring file's
path implies (take it from the **Source** column for that exact type, never from a neighbouring type).

Root namespace `PayPalServerSdk`. Client: `PayPalServerSdkClient(HttpClient, PayPalServerSdkClientOptions)`.
Auth: `options.Oauth2 = new PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials { ClientId, ClientSecret }`.
One server group (`Default`), one environment (`ServerEnvironment.Sandbox` — the **only** constant this SDK
build declares; see Trap notes). Override point: `options.Server.Default.Sandbox.BaseUrl` — confirmed
(source: `AuthSchemes.cs` + `PayPalServerSdkClient.cs`) that the OAuth2 token request (`POST
{BaseUrl}/v1/oauth2/token`) resolves through the **same** `Server.Default(...)` helper as every operation,
so overriding `BaseUrl` redirects the token call too, satisfying the task's `PayPal:BaseUrl` requirement.

### 2.1 Operations used

| Controller.Operation | Signature (request record · required members) | Body model (· required/optional, wire name) | Response (· fields this integration reads) | Error case · accessors | Pagination | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `Orders.CreateOrder` | `CreateOrderRequest { Body, PayPalRequestId }` · `Body` required | `OrderRequest { Intent (required, `intent`), PurchaseUnits (required, `purchase_units`), PaymentSource (`payment_source`) }` | `Order { Id, Status, PurchaseUnits[0].Payments.Authorizations[0].{Id,Status,Amount,ExpirationTime} }` | A: `CreateOrderError` → `TryGetError(out Error)` [400,401,422] · `TryGetRawError` [fallback] | none | `Requests/Orders/CreateOrderRequest.cs`, `Models/OrderRequest.cs`, `Models/Order.cs`, `Errors/CreateOrderError.cs` |
| `Payments.GetAuthorizedPayment` | `GetAuthorizedPaymentRequest { AuthorizationId (required) }` | — (no body) | `PaymentAuthorization { Id, Status, ExpirationTime, Amount }` | A: `GetAuthorizedPaymentError` → `TryGetError(out Error)` [401,403,404] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback] | none | `Requests/Payments/GetAuthorizedPaymentRequest.cs`, `Models/PaymentAuthorization.cs` |
| `Payments.ReauthorizePayment` | `ReauthorizePaymentRequest { AuthorizationId (required), Body, PayPalRequestId }` | `ReauthorizeRequest { Amount (`amount`) }` (amount optional — omit to reauthorize the same amount) | `PaymentAuthorization { Id, Status, ExpirationTime }` — **take the id to capture from this response's own `Id`, never assume it equals the id you reauthorized** (see Trap notes, UNVERIFIED) | A: `ReauthorizePaymentError` → `TryGetError(out Error)` [400,401,403,404,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback] | none | `Requests/Payments/ReauthorizePaymentRequest.cs`, `Models/ReauthorizeRequest.cs`, `Models/PaymentAuthorization.cs` |
| `Payments.CaptureAuthorizedPayment` | `CaptureAuthorizedPaymentRequest { AuthorizationId (required), Body, PayPalRequestId }` | `CaptureRequest { Amount (`amount`), FinalCapture (`final_capture`, default `false`), NoteToPayer, SoftDescriptor }` | `CapturedPayment { Id, Status, Amount, SellerReceivableBreakdown.{GrossAmount(required),PaypalFee,NetAmount} }` | A: `CaptureAuthorizedPaymentError` → `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback] | none | `Requests/Payments/CaptureAuthorizedPaymentRequest.cs`, `Models/CaptureRequest.cs`, `Models/CapturedPayment.cs`, `Models/SellerReceivableBreakdown.cs` |
| `Payments.VoidPayment` | `VoidPaymentRequest { AuthorizationId (required), PayPalRequestId }` | — (no body — `EmptyBody.Instance`, confirmed in `Api/Payments.cs`) | `PaymentAuthorization { Id, Status }` | A: `VoidPaymentError` → `TryGetError(out Error)` [401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback] | none | `Requests/Payments/VoidPaymentRequest.cs`, `Models/PaymentAuthorization.cs` |
| `Payments.RefundCapturedPayment` | `RefundCapturedPaymentRequest { CaptureId (required), Body, PayPalRequestId }` | `RefundRequest { Amount (`amount`, omit for full refund), InvoiceId, NoteToPayer }` | `Refund { Id, Status, Amount, SellerPayableBreakdown }` | A: `RefundCapturedPaymentError` → `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback] | none | `Requests/Payments/RefundCapturedPaymentRequest.cs`, `Models/RefundRequest.cs`, `Models/Refund.cs` |
| `Payments.GetRefund` | `GetRefundRequest { RefundId (required) }` | — | `Refund { Id, Status, Amount }` | A: `GetRefundError` → `TryGetError(out Error)` [401,403,404] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback] | none | `Requests/Payments/GetRefundRequest.cs`, `Models/Refund.cs` |
| `Vault.CreateSetupToken` | `CreateSetupTokenRequest { Body (required), PayPalRequestId }` | `SetupTokenRequest { Customer (`customer`), PaymentSource (required, `payment_source`) }`; `PaymentSource.Card` → `SetupTokenRequestCard { Number, Expiry, Name, SecurityCode, BillingAddress, VerificationMethod }` | `SetupTokenResponse { Id, Status, Customer.Id }` | A: `CreateSetupTokenError` → `TryGetError(out Error)` [400,403,422,500] · `TryGetRawError` [fallback] | none | `Requests/Vault/CreateSetupTokenRequest.cs`, `Models/SetupTokenRequest.cs`, `Models/SetupTokenRequestCard.cs`, `Models/SetupTokenResponse.cs` |
| `Vault.CreatePaymentToken` | `CreatePaymentTokenRequest { Body (required), PayPalRequestId }` | `PaymentTokenRequest { Customer, PaymentSource (required) }`; `PaymentSource.Token` → `VaultTokenRequest { Id (required), Type (required, = `VaultTokenRequestType.SetupToken`) }` | `PaymentTokenResponse { Id, Customer.Id, PaymentSource.Card (`CardPaymentTokenEntity`) .{LastDigits,Brand,Expiry} }` | A: `CreatePaymentTokenError` → `TryGetError(out Error)` [400,403,404,422,500] · `TryGetRawError` [fallback] | none | `Requests/Vault/CreatePaymentTokenRequest.cs`, `Models/PaymentTokenRequest.cs`, `Models/PaymentTokenRequestPaymentSource.cs` (`.Token`), `Models/VaultTokenRequest.cs`, `Models/PaymentTokenResponse.cs`, `Models/CardPaymentTokenEntity.cs` |
| `Vault.ListCustomerPaymentTokens` | `ListCustomerPaymentTokensRequest { CustomerId (required), PageSize (default 5), Page (default 1), TotalRequired (default false) }` | — | `CustomerVaultPaymentTokensResponse { PaymentTokens[], TotalItems, TotalPages }` | A: `ListCustomerPaymentTokensError` → `TryGetError(out Error)` [400,403,500] · `TryGetRawError` [fallback] | page-number (manual — see §2.3 Paged reads) | `Requests/Vault/ListCustomerPaymentTokensRequest.cs`, `Models/CustomerVaultPaymentTokensResponse.cs` |
| `Vault.GetPaymentToken` | `GetPaymentTokenRequest { Id (required) }` | — | `PaymentTokenResponse` | A: `GetPaymentTokenError` → `TryGetError(out Error)` [403,404,422,500] · `TryGetRawError` [fallback] | none | `Requests/Vault/GetPaymentTokenRequest.cs` |
| `Vault.DeletePaymentToken` | `DeletePaymentTokenRequest { Id (required) }` | — | `void` (Task) | A: `DeletePaymentTokenError` → `TryGetError(out Error)` [400,403,500] · `TryGetRawError` [fallback] | none | `Requests/Vault/DeletePaymentTokenRequest.cs` |
| `TransactionSearch.SearchTransactions` | `SearchTransactionsRequest { StartDate (required), EndDate (required), PageSize (default 100, max 500), Page (default 1), Fields (default "transaction_info"), BalanceAffectingRecordsOnly (default "Y") }` | — | `SearchResponse { TransactionDetails[].TransactionInfo, Page, TotalItems, TotalPages }` | **B (raw)** — `ApiException<RawError>`, no typed accessors | page-number, **manual** (no auto-`Pageable`; map's pagination default note applies — see §2.3) | `Requests/TransactionSearch/SearchTransactionsRequest.cs`, `Models/SearchResponse.cs`, `Models/TransactionDetails.cs` |

Not used: `Orders.AuthorizeOrder` / `ConfirmOrder` / `GetOrder` / `PatchOrder` / `CreateOrderTracking` /
`UpdateOrderTracking` (this integration never redirects a payer for approval, so it never reaches a
pre-existing, payer-approved order — see Trap notes); `TransactionSearch.SearchBalances` (not needed for
reconciliation); all 17 `Subscriptions` operations (out of scope — no subscriptions in this task).

### 2.2 Enum values needed

| Enum | Values used | Source |
| --- | --- | --- |
| `CheckoutPaymentIntent` | `Authorize` (wire `AUTHORIZE`) — never `Capture`: this integration always holds funds first | `Models/Enums/CheckoutPaymentIntent.cs` |
| `OrderStatus` (on `Order.Status`) | `Completed` (payment processed immediately — expected on every direct-card create), `PayerActionRequired` (STOP condition, see Trap notes) | `Models/Enums/OrderStatus.cs` |
| `AuthorizationStatus` | `Created` (holds, capturable), `Captured`/`PartiallyCaptured` (already acted on — treat as already-fulfilled, not an error, on a retried `/fulfil`), `Denied`, `Voided`, `Pending` | `Models/Enums/AuthorizationStatus.cs` |
| `CaptureStatus` | `Completed`, `Declined`, `Pending`, `PartiallyRefunded`, `Refunded`, `Failed` | `Models/Enums/CaptureStatus.cs` |
| `RefundStatus` | `Completed`, `Pending`, `Failed`, `Cancelled` | `Models/Enums/RefundStatus.cs` |
| `PaymentTokenStatus` (on `SetupTokenResponse.Status`) | `Created`, `Approved`, `Vaulted` (happy path) — `PayerActionRequired` is the vaulting STOP condition | `Models/Enums/PaymentTokenStatus.cs` |
| `VaultCardVerificationMethod` | `ScaWhenRequired` — set explicitly on `SetupTokenRequestCard.VerificationMethod` to minimize (not eliminate) the chance of a payer-action contingency; **not a guarantee** — see Trap notes | `Models/Enums/VaultCardVerificationMethod.cs` |
| `VaultTokenRequestType` | `SetupToken` (the only declared value) | `Models/Enums/VaultTokenRequestType.cs` |

### 2.3 Cross-operation invariants

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| The `authorization_id` captured/voided/reauthorized at `/fulfil`, `/cancel` must be one this app's own `CreateOrder` (or a prior `ReauthorizePayment`) call returned for *this* order | `CaptureAuthorizedPayment`, `VoidPayment`, `ReauthorizePayment` ← `CreateOrder`, `ReauthorizePayment` | read `OrderPayment.PayPalAuthorizationId` for the order row (never accept an authorization id from the HTTP caller) |
| The `capture_id` refunded must be the one *this order's* `CaptureAuthorizedPayment` call returned | `RefundCapturedPayment` ← `CaptureAuthorizedPayment` | read `OrderPayment.PayPalCaptureId` for the order row (never accept a capture id from the HTTP caller) |
| A refund's requested amount must never exceed `capturedAmount − sum(previous successful refunds)` for that capture | `RefundCapturedPayment` ← `CaptureAuthorizedPayment`, prior `RefundCapturedPayment`s | application-layer check in the refund handler, computed from `OrderPayment.CapturedAmount` and the sum of `OrderRefund` rows with `Status = Completed`/`Pending`, **before** calling PayPal (PayPal also enforces this server-side — 422 — but the app must not rely on that alone, since the task requires the app to prevent over-refund) |
| The saved card paid with (`payment_source.card.vault_id`) must be one `ListCustomerPaymentTokens` (or the `CreatePaymentToken` response at save time) returned for *this signed-in shopper* | `CreateOrder` (pay-with-saved-card path) ← `CreatePaymentToken` / `ListCustomerPaymentTokens` | `PayOrderEndpoint` resolves the caller-supplied `paymentMethodId` (the app's own int id) to a `PaymentMethod` row filtered by `BuyerId == caller`, then reads its `PayPalVaultId` — never accepts a raw PayPal vault id from the HTTP caller |
| The `customer_id` used on `CreateSetupToken`/`CreatePaymentToken`/`ListCustomerPaymentTokens` for a given shopper must be the **same** PayPal-generated id across calls, so a shopper's saved cards all land under one PayPal customer | `CreateSetupToken`, `CreatePaymentToken` → `ListCustomerPaymentTokens` | `SavePaymentMethodEndpoint` looks up any existing `PaymentMethod` row for `BuyerId`; if one exists, its stored `PayPalCustomerId` is sent as `Customer.Id` on the new `CreateSetupToken`/`CreatePaymentToken` calls; if none exists, `Customer` is sent with only `MerchantCustomerId = BuyerId` (letting PayPal mint a new customer id), and the id PayPal returns is persisted on the new `PaymentMethod` row for next time (see Trap notes — `ListCustomerPaymentTokensRequest.CustomerId`'s format rules out passing the shopper's email directly) |

---

## 3. Trap notes

- **Why `CreateOrder` alone is enough (no `AuthorizeOrder` call).** `Api/Orders.cs`'s `<remarks>` on both
  `AuthorizeOrder` and `CaptureOrder` state: *"the buyer must first approve the order **or** a valid
  payment_source must be provided in the request."* Supplying `payment_source.card` (raw or `vault_id`) on
  `CreateOrder` with `Intent = Authorize` is that "valid payment_source" path — the single call creates
  **and** authorizes, with the resulting `AuthorizationWithAdditionalData` already in the response's
  `PurchaseUnits[0].Payments.Authorizations`. `MUST load dotnet-calling-endpoints` for how to read a
  response that nests a resource (here, nested three levels deep) rather than returning it directly.
- **Reauthorization may not keep the same authorization id.** The operation is invoked "by ID" but its
  `<remarks>` does not state whether the *returned* `PaymentAuthorization.Id` is guaranteed identical to
  the one supplied — labeled `UNVERIFIED` on the sheet. Defensive directive: after every
  `ReauthorizePayment` call, overwrite `OrderPayment.PayPalAuthorizationId` with the response's own `Id`
  before the following `CaptureAuthorizedPayment` call, rather than reusing the id that was reauthorized.
- **The payer-action / 3DS STOP condition is a real, checkable value, not a hypothetical.** Both
  `OrderStatus.PayerActionRequired` (on `CreateOrder`'s response) and `PaymentTokenStatus.PayerActionRequired`
  (on `CreateSetupToken`'s response) are declared enum members this SDK build knows about — not an
  undeclared/`otherwise` branch. Per the task, if either is observed on the sandbox test card, **stop and
  report it** rather than building a browser-approval round trip; do not treat `ScaWhenRequired` as
  eliminating the possibility, only as reducing it. `MUST load dotnet-models` for how to branch on the
  returned enum via `Match`/`TryGetKnownValue` rather than a raw string compare.
- **The `Error` typed-error accessor name is `TryGetError` on every operation in this plan, but the class
  behind it is per-operation** (`CreateOrderError`, `CaptureAuthorizedPaymentError`, …) — same accessor
  name, different declared type per catch, and only one operation here (`SearchTransactions`) is Case B.
  `MUST load dotnet-error-handling` before writing any catch block — including the mandatory
  `ResponseDeserializationException` arm (a drifted 2xx or an error body that doesn't match its
  `{Operation}Error` shape surfaces there, not in a typed catch).
- **`PayPal-Request-Id` is the real idempotency key here — the generator-injected `Idempotency-Key` header
  is not.** Every write operation in §2.1 sends both: `request.PayPalRequestId` (a real member on the
  request record, `null` unless the caller sets it) as the `PayPal-Request-Id` header, **and** a
  `Guid.NewGuid()` fresh on every call as `Idempotency-Key` (confirmed in `Api/Orders.cs` and
  `Api/Payments.cs` — e.g. `new HeaderParam("Idempotency-Key", Guid.NewGuid())` sitting right next to `new
  HeaderParam("PayPal-Request-Id", request.PayPalRequestId)`). Only `PayPalRequestId` is ours to use; the
  SDK's injected key must never be cited as an idempotency mechanism. `MUST load
  dotnet-configuration-resilience` for the full idempotency-key reasoning and for why a header's mere
  presence on the wire proves nothing.
- **Vault.ListCustomerPaymentTokens' `CustomerId` cannot be the shopper's email/username.** Its pattern
  (`^[0-9a-zA-Z_-]+$`, 7–36 chars, `Requests/Vault/ListCustomerPaymentTokensRequest.cs`) matches
  `Customer.Id`'s own pattern (`Models/Customer.cs`), not `Customer.MerchantCustomerId`'s more permissive
  one — and this app's JWT only carries a username/email claim (see §6 Assumptions). The cross-operation
  invariant above is the resolution: persist the PayPal-returned `customer.id` locally and use *that*,
  never the shopper's own identifier, anywhere `CustomerId` is a parameter.
- **`SearchTransactions` pagination is manual, not an auto-`Pageable`.** The map's operations page for this
  controller carries no "Pagination" bullet distinct from the plain `page`/`page_size` query params on the
  request record, and `dotnet-configuration-resilience`'s `Pageable<TPage,TItem>`/`.AsPages()` shape applies
  only to operations the map marks as paginated via that mechanism — this one is a plain list call with its
  own `page`/`page_size`/`SearchResponse.TotalPages` fields, so the loop (stop condition, page cap) must be
  hand-written. `MUST load dotnet-configuration-resilience` § *Never leave a page loop unbounded* before
  writing it.
- **PayPal's sandbox transaction reporting lags live activity (task-given fact, not an SDK fact)** — a
  reconciliation range covering orders created in the same run can legitimately return zero transactions.
  Do not treat that as a bug during manual verification; verify the reconciliation report's *logic* (does it
  page correctly, does it line up the two sides correctly) against a date range from an **earlier** run, or
  accept an empty-but-well-formed result for a just-created range.

---

## 4. REQUIRED READING

Load every `dotnet-*` skill below **before implementation starts** — this sheet deliberately does not carry
their content, only the hazard and which step it governs.

| Skill | Governs |
| --- | --- |
| `dotnet-client-initialization` | Registering `PayPalServerSdkClient` via DI in `src/Infrastructure`/`src/PublicApi` — `HttpClient` lifetime, named-`HttpClient` registration, `PooledConnectionLifetime` |
| `dotnet-authentication` | Binding `PayPal:ClientId`/`PayPal:ClientSecret` to `options.Oauth2`, and the startup fail-fast check (§5 row 1) |
| `dotnet-calling-endpoints` | Every call in §2.1 — building the one request record per call, reading nested response envelopes (`Order.PurchaseUnits[0].Payments.Authorizations[0]`) |
| `dotnet-models` | Branching on `OrderStatus`/`PaymentTokenStatus`/`AuthorizationStatus`/etc. via `Match`, not string compares; `Money.Value` formatting |
| `dotnet-error-handling` | Every catch block in `IPayPalGateway`'s implementation — the Case A ladder per operation, the mandatory `ResponseDeserializationException` arm, and the Case B ladder for `SearchTransactions` |
| `dotnet-configuration-resilience` | Retry/timeout tuning, the idempotency-key reasoning (§3), the hand-written pagination loop for `SearchTransactions`, the unknown-outcome recovery pattern (§5 Unknown outcomes) |
| `dotnet-testing` | Unit tests for `IPayPalGateway`'s implementation — stub `HttpMessageHandler`, assert outgoing `PayPal-Request-Id`/body, assert each error-case catch |

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | **Credential fail-fast** | `PayPalOptions { ClientId, ClientSecret, Environment, Currency, BaseUrl }` bound from the `PayPal` section via `AddOptions<PayPalOptions>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` in `PublicApi/Program.cs`. `[Required]` on `ClientId`, `ClientSecret`, `Environment`, `Currency` (not `BaseUrl` — optional per task). A blank (whitespace-only) string must fail `[Required]` too — use a custom `[Required(AllowEmptyStrings = false)]` or a `ValidateOnStart` delegate that also calls `string.IsNullOrWhiteSpace`, since `[Required]` alone only rejects `null`/empty, not whitespace. |
| 2 | **Secret sourcing & rotation** | Values come from env vars `PAYPAL_CLIENT_ID`/`PAYPAL_CLIENT_SECRET`/`PAYPAL_ENVIRONMENT`/`PAYPAL_CURRENCY`, which `Program.cs`'s existing `builder.Configuration.AddEnvironmentVariables()` maps onto `PayPal:ClientId` etc. via the standard `__`/`:` double-underscore convention — e.g. set `PayPal__ClientId`, or rely on ASP.NET Core's env-var provider which also accepts a direct `PayPal:ClientId` key depending on OS. State explicitly in the Program.cs wiring which form is used. `services.AddPayPalServerSdkClient(options => { options.Oauth2 = new(...) using the bound PayPalOptions; })` builds the options object once at registration, per `dotnet-client-initialization` — a rotated secret takes effect only on process restart; this is acceptable (no live-rotation requirement in the task). |
| 3 | **Total timeout budget** | `options.Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) }`; the named `HttpClient`'s own `Timeout = TimeSpan.FromSeconds(15)` as a backstop (per `dotnet-client-initialization`/`dotnet-configuration-resilience`). Each PublicApi payment endpoint wraps its PayPal call(s) in a `CancellationTokenSource` linked to `HttpContext.RequestAborted` with `CancelAfter(TimeSpan.FromSeconds(30))` — the real, enforced per-request budget, set once in a shared `IPayPalGateway` base/helper, not per call site. |
| 4 | **Write-retry ownership** | Every write this app makes (`CreateOrder`, `CaptureAuthorizedPayment`, `VoidPayment`, `ReauthorizePayment`, `RefundCapturedPayment`, `CreateSetupToken`, `CreatePaymentToken`, `DeletePaymentToken`) is `POST`/`DELETE`. The SDK's default `HttpMethodsToRetry` (`GET, HEAD, PUT, OPTIONS`) already excludes all of them from SDK-level resend — do not widen that list. The reads (`GetAuthorizedPayment`, `GetCapturedPayment`, `GetRefund`, `GetPaymentToken`, `GetSetupToken`, `ListCustomerPaymentTokens`, `SearchTransactions`) are `GET`, safe under the default retry behavior; leave it as-is. |
| 5 | **Idempotency & ambiguous writes** | Every write operation's request record carries a real `PayPalRequestId` member (confirmed per-file in §2.1) — this app always sets it to a value deterministic in the *local* claim (below), so a resend after an unknown outcome reuses the same key rather than minting a new one: `CreateOrder` → `$"pay-{orderId}-{attempt}"`; `CaptureAuthorizedPayment` → `$"fulfil-{orderId}"`; `VoidPayment` → `$"cancel-{orderId}"`; `ReauthorizePayment` → `$"reauth-{orderId}-{attempt}"`; `RefundCapturedPayment` → the **caller-supplied** idempotency key from the `POST /api/orders/{orderId}/refunds` request body, prefixed with the order id (`$"refund-{orderId}-{callerKey}"`) so one caller's key can't collide with another order's. `CreateSetupToken`/`CreatePaymentToken` → a fresh server-generated GUID per call (the task does not require idempotency for saving a card; a double-click here produces at most a duplicate saved card, which the shopper can delete). See Duplicate claims below for the local (pre-PayPal) half of this. |
| 6 | **Observability** | `options.Logging.LoggerFactory` is **always explicitly assigned** (via `AddPayPalServerSdkClient`, which fills it from the container's `ILoggerFactory` automatically) so the `PAYPALSERVERSDKCLIENT_LOG` env var can never silently turn on body logging in any deployment. `LogRequestBody` stays `false` always (row 7). On every PayPal error this app logs (at `Warning`/`Error`): the operation name, the local order/refund id, the HTTP status, and — for Case A errors — `Error.Name`/`Error.DebugId` (never `Error.Message` verbatim to the HTTP caller without checking it doesn't echo input; `DebugId` is the correlation id to hand to PayPal support and to surface to the operator in the `/fulfil`-stale-authorization-can't-renew response). |
| 7 | **Sensitive data** | `CardRequest`/`SetupTokenRequestCard`/`PaymentTokenRequestCard` carry `Number` (full PAN) and `SecurityCode` (CVV) — confirmed fields on these request models (§2.1). These values: never persisted (not even transiently beyond the single request scope — they are read from the incoming HTTP request body, used to build the PayPal SDK request, and discarded), never logged, and `LogRequestBody` stays `false` on the SDK client unconditionally (not just for this endpoint — a single shared client options object, so there is no "turn it on for one endpoint" path here; keep it off everywhere, consistent with row 6). The response-side card data this app *does* store (`CardPaymentTokenEntity`/`CardFromRequest`/`CardResponse`'s `LastDigits`/`Brand`/`Expiry`) is already masked by PayPal — confirmed by reading those three response model files — so persisting and returning them for `POST /api/payment-methods`'s "describe it safely" requirement is safe. |
| 8 | **Environment selection** | This SDK build declares exactly one `ServerEnvironment` constant — `Sandbox` (`ClosedStringEnum<ServerEnvironment>`, source: `Servers/ServerEnvironment.cs`) — there is no `Production`/`Live` constant compiled into this package version, so there is no way for this integration to reach a live PayPal endpoint through the SDK's environment selector at all. Startup validation still checks `PayPal:Environment` (from `PAYPAL_ENVIRONMENT`) case-insensitively equals `"sandbox"` and fails fast with a named-config-key message otherwise, both to catch a misconfigured deployment early and to document the constraint for whoever reads the config later. `PayPal:BaseUrl` (from an unnamed env var — task says "optional override"), when non-empty, is applied verbatim to `options.Server.Default.Sandbox.BaseUrl` — this also redirects the OAuth2 token call (confirmed in §2 preamble). |
| 9 | **Duplicate prevention under concurrency** | See **Duplicate claims** table below. |
| 10 | **Partial results** | See **Paged reads** table below. |
| 11 | **Unknown outcomes** | See **Unknown outcomes** table below. |

### Duplicate claims

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `POST /api/orders/{id}/pay` | `OrderPayment.Status` column, with a concurrency token (`OrderPayment.Version`, `int`, `.IsConcurrencyToken()`) — load the row, assert `Status == AwaitingPayment`, set `Status = Authorizing`, `SaveChangesAsync()` | EF Core's optimistic-concurrency check on the token — the InMemory provider enforces concurrency tokens (throws `DbUpdateConcurrencyException`) even without a real database rowversion | `catch (DbUpdateConcurrencyException)` in `PayOrderEndpoint` → re-read the row; if another request already moved it past `AwaitingPayment`, return the **current** state instead of retrying the claim | `TBD` |
| `POST /api/orders/{id}/fulfil` | Same mechanism, `Status == Authorized → Capturing` | same | same pattern in `FulfilOrderEndpoint` | `TBD` |
| `POST /api/orders/{id}/cancel` | Same mechanism, `Status ∈ {AwaitingPayment, Authorized} → Cancelling` | same | same pattern in `CancelOrderEndpoint` | `TBD` |
| `POST /api/orders/{id}/refunds` | A new `OrderRefund` row, **inserted before** calling PayPal, with a unique index on `(OrderPaymentId, IdempotencyKey)` | The unique index — EF Core's InMemory provider enforces unique indexes (throws `DbUpdateException`) | `catch (DbUpdateException)` in `RefundOrderEndpoint` → load the existing `OrderRefund` row for that key and return **its** stored result instead of calling PayPal again | `TBD` |
| `POST /api/payment-methods` | None — not required by the task for this write; a double-click can produce two saved cards, which is a correctness nuisance, not a money-safety issue | n/a | n/a | n/a |

### Paged reads

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `GET /api/reconciliation` (`TransactionSearch.SearchTransactions`, looped by `page`) | A hard `MaxPages` constant (e.g. 200, at `page_size = 500` — the max the request record allows — i.e. up to 100,000 transactions, far beyond a 31-day window's realistic volume for this sandbox account) **in addition to** the natural stop condition `page >= SearchResponse.TotalPages` | A `truncated: bool` field on the reconciliation response, set `true` only if the `MaxPages` cap was hit before `TotalPages` was reached | `TBD` |
| `GET /api/payment-methods` (`Vault.ListCustomerPaymentTokens`, looped by `Page`/`PageSize`) | `PageSize` max is 5 per the request record's own `[Maximum(5)]`; loop while `Page <= CustomerVaultPaymentTokensResponse.TotalPages` (request `TotalRequired = true` to get it), with the same kind of hard page cap as a backstop | A `truncated: bool` field on the list response | `TBD` |

### Unknown outcomes

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `CreateOrder` (pay) | `CreateOrder` itself, resent | the same `PayPal-Request-Id` (`$"pay-{orderId}-{attempt}"`) — PayPal's documented 6-hour key retention for this operation means a resend with the same key returns the original outcome rather than creating a second authorization | `TBD` | stub `HttpMessageHandler` throws `HttpRequestException` on the `CreateOrder` call; assert the gateway's retry carries an identical `PayPal-Request-Id` header value to the first attempt |
| `CaptureAuthorizedPayment` (fulfil) | `CaptureAuthorizedPayment` itself, resent | same `PayPal-Request-Id` (`$"fulfil-{orderId}"`, 45-day retention) | `TBD` | same pattern |
| `VoidPayment` (cancel) | `GetAuthorizedPayment(authorizationId)` | the `AuthorizationId` (already known locally — no key needed since `Status == Voided` is directly observable) | `TBD` | same pattern, asserting the gateway falls back to `GetAuthorizedPayment` and reads `Status` |
| `RefundCapturedPayment` (refund) | `RefundCapturedPayment` itself, resent | same `PayPal-Request-Id` (`$"refund-{orderId}-{callerKey}"`, 45-day retention) | `TBD` | same pattern |

All four follow §*⚠⚠ A write whose outcome is unknown* from `dotnet-configuration-resilience`: on
`SdkConnectionException`/`SdkTimeoutException` from the write, the gateway method re-runs the recovery call
above **once** before giving up and marking the local row `Status = Unknown` (a new status value on both
`Order`/`OrderPayment` state machines) for a human/sweep to resolve — never silently reported as a hard
failure to the shopper, and never silently reported as success either.

---

## 6. Assumptions & Blockers

**Assumptions** (decided, not blocking):

- The PublicApi JWT (`IdentityTokenClaimService.GetTokenAsync`, confirmed by reading
  `src/Infrastructure/Identity/IdentityTokenClaimService.cs`) carries only `ClaimTypes.Name` (the
  username/email) and one `ClaimTypes.Role` claim per role — **no** stable numeric/GUID user id claim
  exists today. Every new endpoint uses `User.Identity!.Name!` as the shopper's identity, exactly like the
  existing `Order.BuyerId`/`Basket.BuyerId` convention (confirmed via `CustomerOrdersSpecification`,
  `BasketWithItemsSpecification`) — not a new claim type, to stay additive and not touch the auth pipeline.
- `Order`'s existing constructor requires a `ShipToAddress` (`Address`, confirmed in
  `src/ApplicationCore/Entities/OrderAggregate/Order.cs`/`Address.cs`) with a free-text `Country` (not
  ISO-2). `POST /api/orders`'s request DTO therefore requires a ship-to address in that same free-text
  shape. Card billing address (`CardRequest.BillingAddress`/`SetupTokenRequestCard.BillingAddress`) needs
  PayPal's own `Address` model with a **2-letter ISO `CountryCode`** (confirmed regex in
  `Models/Address.cs`) — a structurally different shape — so `POST /api/orders/{orderId}/pay`'s one-off-card
  request DTO carries its **own** billing-address fields (including a 2-letter country code), independent of
  the order's ship-to address. This is a deliberate, task-consistent choice ("any name and billing address"
  for the sandbox test card), not a gap.
- `PublicApi` currently mixes two endpoint conventions side by side — `MinimalApi.Endpoint`'s
  `IEndpoint<TResult, TRequest, ...>` (all `CatalogItemEndpoints`) and `Ardalis.ApiEndpoints`'
  `EndpointBaseAsync.WithRequest<T>.WithActionResult<T>` (`AuthEndpoints`). New endpoints follow
  `MinimalApi.Endpoint` (the dominant convention) for consistency with the majority of the project.
- Currency amounts: `CatalogItem.Price` is `decimal`. All `Money`/`AmountWithBreakdown` values sent to
  PayPal are formatted via `decimal.ToString("F2", CultureInfo.InvariantCulture)` (2 decimal places,
  invariant culture) — matching `Money.Value`'s regex (`Models/Money.cs`) and the task's "to the cent"
  requirement. `CurrencyCode` always comes from the bound `PayPal:Currency` option, never hardcoded.
- No existing `OrdersController`/order endpoints exist in `src/PublicApi` today (confirmed — the directory
  search found none) — every endpoint in this plan is new.

**Blockers:** none. Every capability the task requires has a grounded PayPal operation in §2.1: hold
(`CreateOrder` with `Intent=Authorize`), capture-with-fee-breakdown (`CaptureAuthorizedPayment`), stale-hold
renewal (`ReauthorizePayment`) with an explicit, checkable "can no longer be renewed" signal (its own
typed 422 error, or `GetAuthorizedPayment` reporting an un-renewable status), release-before-capture
(`VoidPayment`), full/partial refund with an app-enforced over-refund guard (`RefundCapturedPayment` +
§2.3 invariant), saved-card vaulting with masked display data (`CreateSetupToken`/`CreatePaymentToken`/
`CardPaymentTokenEntity`), and a transaction-level reconciliation feed (`SearchTransactions`). The one
run-time contingency the task tells us to stop on (a payer-action/3DS challenge) is representable and
checkable (§3), not unrepresentable.

---

## 7. Application design

### 7.1 Domain model changes (`src/ApplicationCore/Entities/OrderAggregate/`)

- **`Order`** (extend, keep private setters / behavior-method style already used): add
  `OrderStatus Status` (new enum: `AwaitingPayment`, `Authorizing`, `Authorized`, `Capturing`, `Fulfilled`,
  `Cancelling`, `Cancelled`, `PartiallyRefunded`, `Refunded`, `Unknown`), defaulted to `AwaitingPayment` in
  the constructor; a one-to-one `OrderPayment? Payment` navigation; behavior methods
  (`BeginAuthorizing()`, `MarkAuthorized(...)`, `BeginCapturing()`, `MarkFulfilled(...)`, `BeginCancelling()`,
  `MarkCancelled()`, `MarkRefunded(bool partial)`, `MarkUnknown()`) that enforce legal transitions
  (throwing a new `InvalidOrderStateException` on an illegal one) — this is where the "awaiting payment"
  and "operator fulfils/cancels/refunds" states the task describes actually live.
- **`OrderPayment`** (new, `BaseEntity`, 1:1 with `Order` via `OrderId` FK): `Status` (own small enum
  mirroring the PayPal side: `AwaitingPayment`, `Authorizing`, `Authorized`, `Capturing`, `Captured`,
  `Cancelling`, `Cancelled`, `Unknown`), `Version` (concurrency token, `int`), `Currency`,
  `PayPalOrderId`, `PayPalAuthorizationId`, `AuthorizationStatus` (raw wire string, e.g. `"CREATED"`),
  `AuthorizationExpiresAt` (`DateTimeOffset?`), `PayPalCaptureId`, `CaptureStatus`, `CapturedAmount`,
  `PayPalFeeAmount`, `NetAmount`, `AuthorizePayPalRequestId`, `CapturePayPalRequestId`,
  `VoidPayPalRequestId`. A `IReadOnlyCollection<OrderRefund> Refunds` child collection (same
  private-field/`AsReadOnly()` pattern as `Order.OrderItems`).
- **`OrderRefund`** (new, `BaseEntity`, child of `OrderPayment`): `OrderPaymentId` FK, `IdempotencyKey`
  (the caller-supplied key, prefixed per §5 row 5), `PayPalRefundId`, `Amount`, `Status` (raw wire string),
  `CreatedAt`. Unique index on `(OrderPaymentId, IdempotencyKey)` — the duplicate-claim mechanism (§5).
- **`PaymentMethod`** (new, `BaseEntity`, its own small aggregate, not a child of `Order`): `BuyerId`,
  `PayPalVaultId` (unique index), `PayPalCustomerId`, `Brand`, `LastDigits`, `Expiry`, `CreatedAt`.

### 7.2 EF configuration & specifications

- `src/Infrastructure/Data/Config/OrderPaymentConfiguration.cs`, `OrderRefundConfiguration.cs`,
  `PaymentMethodConfiguration.cs` — same `IEntityTypeConfiguration<T>` pattern as the existing
  `OrderConfiguration.cs`/`OrderItemConfiguration.cs`, picked up automatically by
  `CatalogContext.OnModelCreating`'s `ApplyConfigurationsFromAssembly` (no change needed there). Configure
  the `OrderPayment.Version` concurrency token and the two unique indexes from §5/§7.1.
- Add `DbSet<OrderPayment> OrderPayments`, `DbSet<OrderRefund> OrderRefunds`,
  `DbSet<PaymentMethod> PaymentMethods` to `CatalogContext` (same DbContext that already holds `Order` —
  per the task's in-memory-provider note, this stays a single store per host, no new DbContext needed).
- New specifications in `src/ApplicationCore/Specifications/`: `OrderByIdForBuyerSpec(int orderId, string
  buyerId)` (includes `OrderItems` + `Payment.Refunds`, filters by both id and buyer — the ownership check
  for every shopper-scoped order endpoint), `OrdersForBuyerWithPaymentSpec(string buyerId)` (for
  `GET /api/my-orders`), `PaymentMethodByIdForBuyerSpec`, `PaymentMethodsForBuyerSpec`
  (mirrors `CustomerOrdersSpecification`'s existing shape).
- Operator endpoints (`/fulfil`, `/cancel`, `/reconciliation`) use an id-only specification (no buyer
  filter) since `[Authorize(Roles = Administrators)]` already restricts the caller.

### 7.3 `IPayPalGateway` (new interface, `src/ApplicationCore/Interfaces/IPayPalGateway.cs`; implementation
`src/Infrastructure/PayPal/PayPalGateway.cs`)

One method per capability, each wrapping exactly the PayPal operation(s) named, translating SDK
exceptions per `dotnet-error-handling` into a small set of this app's own result/exception types (never an
SDK type crossing out of `Infrastructure`):

- `AuthorizeAsync(orderPayment, purchaseUnit, paymentSource, idempotencyKey, ct)` → `CreateOrder`
- `GetAuthorizationAsync(authorizationId, ct)` → `GetAuthorizedPayment`
- `ReauthorizeAsync(authorizationId, amount, idempotencyKey, ct)` → `ReauthorizePayment`
- `CaptureAsync(authorizationId, idempotencyKey, ct)` → `CaptureAuthorizedPayment`
- `VoidAsync(authorizationId, idempotencyKey, ct)` → `VoidPayment`
- `RefundAsync(captureId, amount?, idempotencyKey, ct)` → `RefundCapturedPayment`
- `SaveCardAsync(existingPayPalCustomerId?, merchantCustomerId, card, ct)` → `CreateSetupToken` then
  `CreatePaymentToken` (sequential; stop and surface `PayerActionRequired` per §3 if either response
  carries it)
- `ListSavedCardsAsync(payPalCustomerId, ct)` → `ListCustomerPaymentTokens`, paged per §5
- `DeleteSavedCardAsync(vaultId, ct)` → `DeletePaymentToken`
- `SearchTransactionsAsync(from, to, ct)` → `SearchTransactions`, paged per §5

### 7.4 PublicApi endpoints (`MinimalApi.Endpoint` convention, new folders)

`src/PublicApi/OrderEndpoints/`:
- `CreateOrderEndpoint` — `POST api/orders`, `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`, no role. Body: catalog item ids + quantities + ship-to address. Response: `{ orderId, ... }`.
- `PayOrderEndpoint` — `POST api/orders/{orderId}/pay`, shopper-scoped (buyer-id check via spec). Body: either one-off card fields + billing address, **or** `paymentMethodId` (int, resolved per the cross-operation invariant in §2.3 — never a raw vault id from the caller).
- `FulfilOrderEndpoint` — `POST api/orders/{orderId}/fulfil`, `[Authorize(Roles = Administrators)]`.
- `CancelOrderEndpoint` — `POST api/orders/{orderId}/cancel`, `[Authorize(Roles = Administrators)]`.
- `RefundOrderEndpoint` — `POST api/orders/{orderId}/refunds`, `[Authorize(Roles = Administrators)]`. Body: `{ amount?, idempotencyKey }` (full refund when `amount` omitted, per `RefundRequest`'s own documented behavior). Response: `{ refundId, ... }`.
- `GetMyOrdersEndpoint` — `GET api/my-orders`, shopper-scoped.
- `ReconciliationEndpoint` — `GET api/reconciliation?from&to`, `[Authorize(Roles = Administrators)]`.

`src/PublicApi/PaymentMethodEndpoints/`:
- `SavePaymentMethodEndpoint` — `POST api/payment-methods`, shopper-scoped. Response: `{ paymentMethodId, brand, lastDigits, expiry }` (never full PAN).
- `ListPaymentMethodsEndpoint` — `GET api/payment-methods`, shopper-scoped.
- `DeletePaymentMethodEndpoint` — `DELETE api/payment-methods/{paymentMethodId}`, shopper-scoped (ownership check before calling `DeletePaymentToken`, so one shopper can never delete another's).

Every shopper-scoped endpoint loads its `Order`/`PaymentMethod` row through a buyer-filtered specification
(§7.2) and returns `404`/`403` (project convention — check an existing not-found example, e.g.
`CatalogItemGetByIdEndpoint`'s `Results.NotFound()`) rather than leaking another buyer's row's existence.

### 7.5 Configuration & DI wiring (`src/PublicApi/Program.cs`, `src/Infrastructure/Dependencies.cs` or a new
`src/Infrastructure/PayPalServiceCollectionExtensions.cs`)

- `appsettings.json` gets an empty `"PayPal": {}` section (documenting the keys without values — no
  secrets committed); real values come from env vars at runtime as described in §5 row 2.
- `AddOptions<PayPalOptions>().Bind(configuration.GetSection("PayPal")).ValidateDataAnnotations().ValidateOnStart()`.
- `services.AddPayPalServerSdkClient(options => { ... })` per `dotnet-client-initialization` — registered
  over a **named** `HttpClient` (not the shared default one), with `Timeout`/`PooledConnectionLifetime` set
  per §5 row 3, and `options.Environment`/`options.Server.Default.Sandbox.BaseUrl`/`options.Oauth2` built
  from the validated `PayPalOptions` (read via `IOptions<PayPalOptions>` resolved once at registration time
  — the options callback runs once at startup, consistent with `dotnet-client-initialization`'s "captured
  once, at registration" note).
- `services.AddScoped<IPayPalGateway, PayPalGateway>()`.

### 7.6 Tests

- `IPayPalGateway` implementation: one stub-`HttpMessageHandler` test per operation in §2.1 — success path
  (assert the deserialized fields this app reads), each declared Case-A accessor, the Case-B ladder for
  `SearchTransactions`, `ResponseDeserializationException`, and a transport-failure test per the four rows
  in §5's Unknown outcomes table (per `dotnet-testing`).
- Application-layer: the four Duplicate-claims rows (§5) as integration tests against the EF InMemory
  provider — assert the second concurrent call is rejected/short-circuited, not double-processed.
- Endpoint-layer: ownership checks (shopper A cannot see/act on shopper B's order or saved card;
  non-administrator cannot call `/fulfil`, `/cancel`, `/refunds`, `/reconciliation`).

---

## 8. Manual sandbox verification (for the build session to run and then write up)

Sequence to actually execute against the PayPal sandbox once built (bearer token from
`POST api/authenticate` first, then):

1. `POST /api/orders` with one or more catalog item ids/quantities + a ship-to address → note `orderId`.
2. `POST /api/orders/{orderId}/pay` with the sandbox Visa `4111 1111 1111 1111`, a future expiry, any
   CVC/name/billing address → expect `Order.Status = COMPLETED` and an authorization in the response;
   confirm no `payer-action` link/status appeared (if it did: stop, per the task, and report it instead of
   continuing).
3. `POST /api/orders/{orderId}/fulfil` (as an administrator) → expect `CaptureAuthorizedPayment`'s
   `SellerReceivableBreakdown` fields populated (gross/fee/net).
4. `POST /api/orders/{orderId}/refunds` with a partial `amount` and an idempotency key → repeat the exact
   same request (same key) → expect the second call to return the same refund, not a second one. Then a
   second, *distinct* partial refund with a different key → expect it to succeed, up to the captured total.
5. A second order: `POST /api/payment-methods` with the same test card → `POST /api/orders` (new order) →
   `POST /api/orders/{orderId}/pay` with `paymentMethodId` from step 5 instead of raw card fields → confirm
   it authorizes without re-entering card data.
6. A third order, cancelled before fulfilment: create → pay → `POST /api/orders/{orderId}/cancel` (as
   administrator) → confirm `GetAuthorizedPayment`/local state shows voided, and that `/fulfil` on it now
   fails cleanly (already-cancelled).
7. `GET /api/reconciliation?from=...&to=...` over the time window containing the above activity (expect it
   may come back empty per the task's lag note — re-run it later in the same session, or widen the window
   to include a prior day's manual sandbox activity, to see non-empty output and confirm the paging/lineup
   logic).

The build session must turn the above into the concrete, copy-pasteable step-by-step guide the task asks
for (exact curl/Postman calls, exact JSON bodies, exact expected fields) once the endpoints exist — this
section is the script to adapt, not the deliverable itself.

---

## 9. Skills loaded this session

- `paypal-sdk:dotnet-integrate-pay-pal-server-sdk` (workflow — loaded first, per its own requirement)
- `paypal-sdk:dotnet-getting-started` (map + SDK source location)
- `paypal-sdk:dotnet-client-initialization`
- `paypal-sdk:dotnet-authentication`
- `paypal-sdk:dotnet-calling-endpoints`
- `paypal-sdk:dotnet-models`
- `paypal-sdk:dotnet-error-handling`
- `paypal-sdk:dotnet-configuration-resilience`
- `paypal-sdk:dotnet-testing`
