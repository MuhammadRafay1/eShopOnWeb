# PayPal Server SDK (.NET) integration plan — eShopOnWeb payments & saved cards

SDK: `Darker98.PayPalServerSdk`, root namespace `PayPalServerSdk`, OAuth2 client credentials, `ServerEnvironment.Sandbox`.
All contract facts below were taken from the SDK map (`sdk-map.md` + `map/operations/*`) and the declaring source files it names, this session. Nothing here is from memory.

---

## 1. Scope & sequence

Everything is exposed on **`src/PublicApi`** (JWT), MinimalApi.Endpoint convention, routed under `/api/`. New domain lives in `ApplicationCore`; EF config + SDK wiring in `Infrastructure`/`PublicApi`.

1. **Config & client** — bind `PayPal:` section → `PayPalSettings`; fail-fast on blank credentials; register `PayPalServerSdkClient` (singleton, `IHttpClientFactory`-owned HttpClient). (client-initialization, authentication, configuration-resilience)
2. **Domain** — `OrderPayment` aggregate (+ `PaymentRefund` child) and `SavedCard` aggregate in `ApplicationCore`; EF configs + DbSets + in-memory-friendly keys in `Infrastructure`.
3. **PayPal gateway** — one `IPayPalPaymentGateway` wrapping the SDK: authorize, (re)authorize, capture, void, refund, vault-save, vault-delete, search-transactions. Translates SDK models ↔ domain; owns the error boundary. (calling-endpoints, models, error-handling)
4. **Flow-1 endpoints** — `POST /api/orders`, `/pay`, `/fulfil`, `/cancel`, `/refunds`; `GET /api/my-orders`; `GET /api/reconciliation`.
5. **Flow-2 endpoints** — `POST`/`GET`/`DELETE /api/payment-methods`.
6. **Tests** + end-to-end self-verify against the sandbox card.

Operations used (map): `Orders.CreateOrder`, `Orders.AuthorizeOrder`, `Payments.CaptureAuthorizedPayment`, `Payments.GetAuthorizedPayment`, `Payments.ReauthorizePayment`, `Payments.VoidPayment`, `Payments.RefundCapturedPayment`, `Vault.CreatePaymentToken`, `Vault.DeletePaymentToken`, `TransactionSearch.SearchTransactions`.

---

## 2. CONTRACT SHEET

> ⚠ Signatures are generated code, verbatim. **Each operation that takes input takes ONE request record as its first parameter**, built with an object initializer whose property names are the record's own (never flat arguments). An operation with no inputs takes none.
> ⚠ Every SDK type is written fully-qualified with the namespace its source path implies (`Models/` → `PayPalServerSdk.Models`, `Models/Enums/` → `PayPalServerSdk.Models.Enums`, `Requests/<Ctrl>/` → `PayPalServerSdk.Requests.<Ctrl>`, `Errors/` → `PayPalServerSdk.Errors`), taken from THAT type's own path.

| Operation | Controller · method | Request record + members (name: type, required?) | Body model + fields read/written (name (wire): type, req?) | Response envelope → fields read | Error case + accessors + payload | Pagination | Source |
|---|---|---|---|---|---|---|---|
| CreateOrder | `client.Orders.CreateOrder(CreateOrderRequest)` | `Body: OrderRequest, REQUIRED`; `PayPalRequestId: string?` (idempotency, 6h); `Prefer: string?` (set `"return=representation"`) | `OrderRequest`: `Intent (intent): CheckoutPaymentIntent, REQ`; `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnitRequest>, REQ`; `PaymentSource (payment_source): PaymentSource?` | `Order` → `Id`, `Status` (OrderStatus), `PurchaseUnits[].Payments.Authorizations[]` (`.Id`,`.Status`,`.ExpirationTime`,`.Amount`), `Links[]` (rel) | A: `ApiException<CreateOrderError>`; `TryGetError(out Error)` [400,401,422] · `TryGetRawError` | none | map/operations/Orders.md; Models/OrderRequest.cs; Models/Order.cs |
| AuthorizeOrder | `client.Orders.AuthorizeOrder(AuthorizeOrderRequest)` | `Id: string, REQUIRED`; `PayPalRequestId: string?`; `Prefer: string?` (`"return=representation"`); `Body: OrderAuthorizeRequest?` | `OrderAuthorizeRequest`: `PaymentSource (payment_source): OrderAuthorizeRequestPaymentSource?` (has `Card: CardRequest?`, `Token: Token?`) | `OrderAuthorizeResponse` → `Id`, `Status`, `PurchaseUnits[].Payments.Authorizations[].Id/.Status/.ExpirationTime/.Amount` | A: `ApiException<AuthorizeOrderError>`; `TryGetError(out Error)` [400,401,403,404,422,500] · `TryGetRawError` | none | map/operations/Orders.md; Models/OrderAuthorizeRequest.cs; Models/OrderAuthorizeResponse.cs |
| CaptureAuthorizedPayment | `client.Payments.CaptureAuthorizedPayment(CaptureAuthorizedPaymentRequest)` | `AuthorizationId: string, REQUIRED`; `PayPalRequestId: string?` (45d); `Prefer: string?` (`"return=representation"`); `Body: CaptureRequest?` (omit → full capture) | `CaptureRequest` (not sent; full capture) | `CapturedPayment` → `Id`, `Status` (CaptureStatus), `Amount` (Money), `SellerReceivableBreakdown.GrossAmount/.PaypalFee/.NetAmount` (Money) | A: `ApiException<CaptureAuthorizedPaymentError>`; `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` | none | map/operations/Payments.md; Models/CapturedPayment.cs; Models/SellerReceivableBreakdown.cs |
| GetAuthorizedPayment | `client.Payments.GetAuthorizedPayment(GetAuthorizedPaymentRequest)` | `AuthorizationId: string, REQUIRED` | — | `PaymentAuthorization` → `Id`, `Status` (AuthorizationStatus), `ExpirationTime`, `Amount` | A: `ApiException<GetAuthorizedPaymentError>`; `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError` | none | map/operations/Payments.md; Models/PaymentAuthorization.cs |
| ReauthorizePayment | `client.Payments.ReauthorizePayment(ReauthorizePaymentRequest)` | `AuthorizationId: string, REQUIRED`; `PayPalRequestId: string?`; `Body: ReauthorizeRequest?` (`Amount (amount): Money?`) | `ReauthorizeRequest`: `Amount (amount): Money?` | `PaymentAuthorization` → `Id`, `Status`, `ExpirationTime`, `Amount` | A: `ApiException<ReauthorizePaymentError>`; `TryGetError` [400,401,403,404,422] · `TryGetNoContent` [500] · `TryGetRawError` | none | map/operations/Payments.md; Models/ReauthorizeRequest.cs |
| VoidPayment | `client.Payments.VoidPayment(VoidPaymentRequest)` | `AuthorizationId: string, REQUIRED`; `PayPalRequestId: string?` | — | `PaymentAuthorization` → `Id`, `Status` (expect VOIDED) | A: `ApiException<VoidPaymentError>`; `TryGetError` [401,403,404,409,422] · `TryGetNoContent` [500] · `TryGetRawError` | none | map/operations/Payments.md |
| RefundCapturedPayment | `client.Payments.RefundCapturedPayment(RefundCapturedPaymentRequest)` | `CaptureId: string, REQUIRED`; `PayPalRequestId: string?` (45d) = caller idempotency key; `Body: RefundRequest?` | `RefundRequest`: `Amount (amount): Money?` (omit → full); `InvoiceId (invoice_id): string?`; `CustomId (custom_id): string?` | `Refund` → `Id`, `Status` (RefundStatus), `Amount` (Money), `SellerPayableBreakdown` | A: `ApiException<RefundCapturedPaymentError>`; `TryGetError` [400,401,403,404,409,422] · `TryGetNoContent` [500] · `TryGetRawError` | none | map/operations/Payments.md; Models/RefundRequest.cs; Models/Refund.cs |
| CreatePaymentToken | `client.Vault.CreatePaymentToken(CreatePaymentTokenRequest)` | `Body: PaymentTokenRequest, REQUIRED`; `PayPalRequestId: string?` (3h) | `PaymentTokenRequest`: `PaymentSource (payment_source): PaymentTokenRequestPaymentSource, REQ` (`.Card: PaymentTokenRequestCard?` = name/number/expiry/security_code/billing_address); `Customer (customer): Customer?` (`.Id: string?` ≤22, `.MerchantCustomerId: string?`) | `PaymentTokenResponse` → `Id`, `Customer.Id`, `PaymentSource.Card` (CardPaymentTokenEntity: `LastDigits`,`Brand`,`Expiry`,`Name`) | A: `ApiException<CreatePaymentTokenError>`; `TryGetError` [400,403,404,422,500] · `TryGetRawError` | none | map/operations/Vault.md; Models/PaymentTokenRequest.cs; Models/PaymentTokenResponse.cs; Models/CardPaymentTokenEntity.cs |
| DeletePaymentToken | `client.Vault.DeletePaymentToken(DeletePaymentTokenRequest)` | `Id: string, REQUIRED` | — | `void` (Task) | A: `ApiException<DeletePaymentTokenError>`; `TryGetError` [400,403,500] · `TryGetRawError` | none | map/operations/Vault.md |
| SearchTransactions | `client.TransactionSearch.SearchTransactions(SearchTransactionsRequest)` | `StartDate: string, REQUIRED` (RFC3339, ≥20 chars); `EndDate: string, REQUIRED` (**range ≤ 31 days**); `Fields: string` (="transaction_info"); `BalanceAffectingRecordsOnly: string` (set "N" to include all); `PageSize: int` (≤500); `Page: int` (1-based) | — | `SearchResponse` → `TransactionDetails[].TransactionInfo` (`TransactionId`,`TransactionStatus`,`TransactionAmount`,`FeeAmount`,`InvoiceId`,`CustomField`,`TransactionEventCode`,`TransactionInitiationDate`), `Page`, `TotalPages`, `TotalItems` | **B: `ApiException<RawError>`** (`StatusCode`,`ReadAsString()`,`ReadAsJson<T>()`) | **page-based**, `page`/`page_size`, totals in `TotalPages`/`TotalItems` | map/operations/TransactionSearch.md; Models/SearchResponse.cs; Models/TransactionInformation.cs |

### Enums (Models/Enums — `OpenStringEnum<T>`, static members, no `new`)
- `CheckoutPaymentIntent.Authorize` (wire `AUTHORIZE`), `.Capture`. (Models/Enums/CheckoutPaymentIntent.cs)
- `OrderStatus`: `Created`,`Saved`,`Approved`,`Voided`,`Completed`,`PayerActionRequired`. (Models/Enums/OrderStatus.cs)
- `AuthorizationStatus`: `Created`,`Captured`,`Denied`,`PartiallyCaptured`,`Voided`,`Pending`. (…/AuthorizationStatus.cs)
- `CaptureStatus`: `Completed`,`Declined`,`PartiallyRefunded`,`Pending`,`Refunded`,`Failed`. (…/CaptureStatus.cs)
- `RefundStatus`: `Cancelled`,`Failed`,`Pending`,`Completed`. (…/RefundStatus.cs)
- Read an open enum via `== Status.X`; unknown value handled by `Match`'s `otherwise`.

### Client construction / auth / server (sdk-map.md §Getting a client, §Servers & auth)
- `new PayPalServerSdkClient(HttpClient, PayPalServerSdkClientOptions)`; DI: `services.AddPayPalServerSdkClient(opts => …)`.
- `options.Oauth2 = new OAuth2ClientCredentials { ClientId=…, ClientSecret=… }` (ns `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials`).
- `options.Environment = ServerEnvironment.Sandbox` (ns `PayPalServerSdk.Servers`). **Only `Sandbox` is declared** — no Live member.
- Base-URL override point: `options.Server.Default.Sandbox.BaseUrl`. Token URL default `https://api-m.sandbox.paypal.com/v1/oauth2/token`.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
|---|---|---|
| A saved-card id used to pay must be one this shopper vaulted | `CreateOrder`/`AuthorizeOrder` (payment_source.card.vault_id) ← `CreatePaymentToken` (returns `Id`), recorded as `SavedCard` | `PayService`: look up `SavedCard` by (paymentMethodId, caller BuyerId) before building payment_source; 404/403 if not owned/missing |
| A capture refunded must be one this order captured | `RefundCapturedPayment` (CaptureId) ← this order's own `CaptureAuthorizedPayment` (returns `CapturedPayment.Id`) | `RefundService`: CaptureId read from the caller's own `OrderPayment`; never caller-supplied |
| An authorization captured/voided/reauthorized must be one this order created | `CaptureAuthorizedPayment`/`VoidPayment`/`ReauthorizePayment` (AuthorizationId) ← this order's own `CreateOrder`/`AuthorizeOrder` | AuthorizationId read from caller's own `OrderPayment`; never caller-supplied |
| Delete only a vault token this shopper owns | `DeletePaymentToken` (Id) ← `SavedCard` recorded for this BuyerId | `PaymentMethodService`: load `SavedCard` by (id, BuyerId) before delete; 404 otherwise |

⚠ The map never states these; derived from the task. The legal set is always "what THIS shopper/order created and we recorded locally", never a provider lookup that only proves existence.

---

## 3. Trap notes (hazard + skill, not resolved here)

- **Client/HttpClient lifetime**: wrong lifetime (rebuild per request vs. singleton) leaks sockets or captures a stale token — decide with `MUST load dotnet-client-initialization`.
- **Credential application failure looks like success**: an unset/misapplied OAuth2 credential is skipped, not thrown, surfacing later as 401 — `MUST load dotnet-authentication`.
- **Open enums & unions aren't C# enums**: `CheckoutPaymentIntent`/`OrderStatus`/etc. have no public ctor and need `Match`/`==`; request `payment_source` is a plain record not a union — `MUST load dotnet-models`.
- **Error ladder completeness**: Case A `TryGetError` vs Case B `RawError` differ per op, and a drifted 2xx/ non-2xx body throws `ResponseDeserializationException` which is NOT `ApiException<TError>` — `MUST load dotnet-error-handling`.
- **`Timeout` is per-attempt; POST is never auto-retried; `LogRequestBody` logs card JSON unredacted; base-URL/token-URL override; page walking** — `MUST load dotnet-configuration-resilience`.
- **Test seam is the `HttpClient` ctor arg** — `MUST load dotnet-testing`.

---

## 4. REQUIRED READING (load BEFORE implementation; sheet does not carry their contents)

- `paypal-sdk:dotnet-client-initialization` — step 1 (client build + DI + HttpClient lifetime).
- `paypal-sdk:dotnet-authentication` — step 1 (OAuth2 client-credentials wiring, fail-fast).
- `paypal-sdk:dotnet-calling-endpoints` — step 3 (first calls; request-record + real idempotency key `PayPalRequestId`).
- `paypal-sdk:dotnet-models` — step 3 (open enums, records, Money string values, extension-data).
- `paypal-sdk:dotnet-error-handling` — error boundary in the gateway. **Always-include hazard**: a drifted **2xx** (missing `required` member) or a **non-2xx** body not matching the op's `{Operation}Error` shape surfaces as `ResponseDeserializationException` — an `ApiException` that keeps the status and names the target type but is **not** `ApiException<TError>`; a ladder catching only `ApiException<TError>` lets it escape, so also catch `ResponseDeserializationException` (or `ApiException`).
- `paypal-sdk:dotnet-configuration-resilience` — retries/timeouts/base-URL/pagination/logging.
- `paypal-sdk:dotnet-testing` — gateway tests.

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
| 1 | Credential fail-fast | `PayPalSettings` bound from `PayPal:` section; a startup validator throws `InvalidOperationException` if `ClientId`, `ClientSecret`, `Environment`, or `Currency` is null/blank — **each part checked separately** (blank ≠ missing). Registered before the app builds. |
| 2 | Secret sourcing & rotation | Secrets come from **.NET user-secrets** (loaded there from env `PAYPAL_*` by me; values never in repo). Options object is built once at DI registration and captured in the singleton client → a rotated secret needs a process restart. Documented; acceptable for this app. |
| 3 | Total timeout budget | SDK `Timeout` is per-attempt. Each gateway call is bounded by a `CancellationToken` with a total deadline (default 100s) created in the gateway, so a hung retryable call cannot exceed it. |
| 4 | Write-retry ownership | Default `HttpMethodsToRetry` = GET/HEAD/PUT/OPTIONS; all our writes are **POST** (CreateOrder/Authorize/Capture/Void/Refund/CreatePaymentToken) or **DELETE** → SDK never auto-resends them. We rely on `PayPal-Request-Id` + local claims for safe manual retry. |
| 5 | Idempotency & ambiguous writes | Every PayPal write carries a **stable** `PayPalRequestId` persisted on our row (authorize/capture/void generate-once-and-store; refund uses the **caller-supplied** idempotency key). Local status/claim guards stop a second app-level attempt before the SDK call. |
| 6 | Observability | Gateway logs op name + our order/payment id + PayPal resource id + status at Information; errors at Warning/Error with the PayPal `debug_id`/`name` parsed from the error body. `LogRequestBody` stays **off**. |
| 7 | Sensitive data | Request models carry PANs/CVV (`CardRequest`, `PaymentTokenRequestCard`). ∴ `options.Logging.LogRequestBody` stays off **and** `options.Logging.LoggerFactory` is set explicitly so `PAYPALSERVERSDKCLIENT_LOG` cannot force body logging on; our own code never logs a card number/CVV — only `last_digits`/brand. Full PAN/CVV never persisted. |
| 8 | Environment selection | SDK declares only `ServerEnvironment.Sandbox`. `PayPal:Environment`="sandbox" → Sandbox. Any non-sandbox value **requires** `PayPal:BaseUrl` to point the client (and token request) at that host, else startup throws — test traffic cannot silently hit live. `PayPal:BaseUrl`, when set, is applied verbatim to `options.Server.Default.Sandbox.BaseUrl` for every call. (Token-URL-follows-base to be confirmed at impl; see §6.) |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. In-memory store is single-host per run; an in-process per-order async lock plus a persisted status transition (claim) rejects the second caller before the SDK call; `PayPal-Request-Id` is the provider-side backstop. |
| 10 | Partial results | Only paged read we walk is `SearchTransactions`; we loop `Page=1..TotalPages` per ≤31-day window so the report is complete. The report DTO carries `Complete`/`Truncated=false` and per-window counts so the caller sees coverage. `ListCustomerPaymentTokens` is NOT used — saved-card listing is served from our own `SavedCard` records (authoritative ownership). |
| 11 | Unknown outcomes | Each write's `catch` on `SdkConnectionException`/`SdkTimeoutException` leaves the row in a pending/unknown state carrying the stable `PayPal-Request-Id`; a retry (same key) re-hits PayPal idempotency and settles it; reconciliation is the backstop. See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
| Authorize (`CreateOrder`/`AuthorizeOrder`) | `OrderPayment` row (our DbContext) | status guard: row must be `AwaitingPayment`/`Failed`; in-process per-order lock serializes | re-read after lock returns the already-`Authorized` row → return existing, no SDK call | `OrderPaymentService.PayAsync`: `_lock.AcquireAsync(OrderGate(orderId))` + status checks, then `_gateway.AuthorizeAsync` |
| Capture (`CaptureAuthorizedPayment`) | `OrderPayment` row | status guard: must be `Authorized`; lock | re-read returns `Captured`/`PartiallyRefunded`/`Refunded` → return existing | `OrderPaymentService.FulfilAsync`: `_lock.AcquireAsync(OrderGate)` + status check → `CaptureAsync` → `_gateway.CaptureAsync` |
| Void (`VoidPayment`) | `OrderPayment` row | status guard: must be `Authorized`; lock | re-read returns `Cancelled` → return existing | `OrderPaymentService.CancelAsync`: `_lock.AcquireAsync(OrderGate)` + status check → `_gateway.VoidAsync` |
| Refund (`RefundCapturedPayment`) | `PaymentRefund` row keyed on (`OrderPaymentId`,`IdempotencyKey`) | per-order lock + `FindRefundByKey` under the lock; a settled claim (has `PayPalRefundId`) returns itself with no SDK call | `RefundAsync` lookup under the lock returns the stored refund | `OrderPaymentService.RefundAsync`: `_lock.AcquireAsync(OrderGate)` + `payment.FindRefundByKey`/`AddRefundClaim` → `_gateway.RefundAsync` (caller key = `PayPal-Request-Id`) |
| Save card (`CreatePaymentToken`) | `SavedCard` row | per-buyer lock + `PayPal-Request-Id` (3h); returned vault `Id` deduped against existing before insert | `SaveCardAsync` dedup check on `PayPalVaultId` returns the existing card | `SavedCardService.SaveCardAsync`: `_lock.AcquireAsync(BuyerGate)` + vault-id dedup → `_gateway.VaultCardAsync` |

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
|---|---|---|---|
| `SearchTransactions` (reconciliation) | `page_size` (500) + `page`; 31-day window max; `MaxPagesPerWindow` safety cap | `ReconciliationReport.Complete` — `true` when every page of every window was walked; `false` if the safety cap truncated it | `PayPalPaymentGateway.SearchTransactionsAsync` sets `complete=false` at the cap → `TransactionSearchResult.Complete`; `ReconciliationService.ReconcileAsync` copies it to `ReconciliationReport.Complete` |

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
|---|---|---|---|---|
| CreateOrder/AuthorizeOrder | reconciliation by `invoice_id` (+ `GetAuthorizedPayment` once an auth id is known) | our stored `InvoiceId` / `PayPalOrderId` | `PayPalPaymentGateway` `TryTranslateInfra` sets `OutcomeUnknown` on `SdkConnection`/`SdkTimeout`; `OrderPaymentService.PayAsync` does **not** mark the row `Failed` on a non-caller error, so the stable `CreateOrderIdempotencyKey`/`AuthorizeIdempotencyKey` replay on retry | `OrderPaymentServiceTests.PayAsync_gateway_connection_failure_leaves_order_awaiting_payment` |
| CaptureAuthorizedPayment | `Payments.GetAuthorizedPayment` then retry capture with same `PayPal-Request-Id` | `AuthorizationId` | `OrderPaymentService.FulfilAsync` keeps status `Authorized` on an unknown outcome; retry replays `CaptureIdempotencyKey` (`PayPalPaymentGateway.GetAuthorizationAsync` is the re-read) | `OrderPaymentServiceTests.FulfilAsync_gateway_connection_failure_keeps_order_authorized` |
| RefundCapturedPayment | retry with same caller `PayPal-Request-Id` (PayPal dedupes) | `IdempotencyKey` as `PayPal-Request-Id` | `OrderPaymentService.RefundAsync` catch on `OutcomeUnknown` → `claim.MarkUnknown()`; a retry with the same key replays and settles | `OrderPaymentServiceTests.RefundAsync_unknown_outcome_marks_claim_unknown_and_settles_on_retry` |
| VoidPayment | `Payments.GetAuthorizedPayment` (status → VOIDED?) | `AuthorizationId` | `OrderPaymentService.CancelAsync` keeps status `Authorized` on an unknown outcome; retry replays `VoidIdempotencyKey` | `OrderPaymentServiceTests.CancelAsync_gateway_connection_failure_keeps_order_authorized` |
| CreatePaymentToken | retry with same `PayPal-Request-Id` | `PayPal-Request-Id` | `SavedCardService.SaveCardAsync` lets the gateway error propagate; retry replays the stable `PayPal-Request-Id` | `SavedCardServiceTests.SaveCardAsync_gateway_failure_propagates` |

---

## 6. Assumptions & Blockers

- **Pay flow (single-step card, YOUR CALL — not in the map):** `CreateOrder(intent=AUTHORIZE, payment_source.card{number|vault_id})` with `Prefer: return=representation`, then branch on the returned `Order`: (a) authorization already present in `purchase_units[].payments.authorizations[]` → use it; (b) `status=APPROVED` and none present → call `AuthorizeOrder(id)` to create it; (c) `status=PAYER_ACTION_REQUIRED` or an approval/`payer-action` link → **STOP & report a challenge** (return 402, do not build a browser round-trip). Exact sandbox behaviour (a vs b) is confirmed at run time by self-verify; both paths are implemented. The `<remarks>` on `AuthorizeOrder` confirm a `payment_source` in the request is a valid alternative to buyer approval.
- **Base-URL ⇒ token-URL (RESOLVED):** confirmed from source (`AuthSchemes.cs`) that the OAuth token URL is `server.Default("/v1/oauth2/token")`, built from the same `Server(options.Environment, options.Server)` used for every API call. ∴ setting `options.Server.Default.Sandbox.BaseUrl = PayPal:BaseUrl` redirects **every** call including the token request — exactly the task requirement. No custom token strategy needed.
- **Saved-card listing:** served from local `SavedCard` records (authoritative per-shopper ownership + safe description), not `ListCustomerPaymentTokens`. Using our own record of what we vaulted is not bypassing PayPal; save & delete still go through the SDK. This is a design decision, not a gap.
- No capability gap found: every task capability maps to an SDK operation above.

### Verified behaviour (self-verify against the sandbox)

- **Pay flow (confirmed):** `CreateOrder(intent=AUTHORIZE, payment_source.card)` with `Prefer: return=representation` returns the authorization directly under `purchase_units[].payments.authorizations[]` (path (a)); path (b) `AuthorizeOrder` remains implemented as a fallback. End-to-end verified: authorize $29.00 hold, capture reporting fee $1.24 / net $27.76, partial refunds, and void (cancel).
- **Vault (fixed at runtime):** direct `CreatePaymentToken` with a raw card works (201) and is used; **sending `customer.merchant_customer_id` makes the sandbox return 500**, so it is omitted — a first card creates the PayPal customer (we store the returned `customer.id`), later cards reuse it via `customer.id`. The setup-token path was trialled and reverted. Verified: save card → reuse to pay a second order → delete → unusable.
- **Void returns 204 No Content:** `VoidPayment` succeeds with an empty body the SDK cannot deserialize into `PaymentAuthorization` (surfaces as `ResponseDeserializationException` with a 2xx status). `PayPalPaymentGateway.VoidAsync` treats a 2xx deserialization failure as success.
