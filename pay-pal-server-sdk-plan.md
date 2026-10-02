# PayPal integration plan — eShopOnWeb PublicApi

## 1. Scope & sequence

1. **Domain model** — extend `Order` (additive) with payment/fulfilment state; add `OrderPayment` (PayPal
   hold/capture/refund state), `Refund` records, and persist `Buyer`/`PaymentMethod` (currently dead code,
   not in any `DbSet`). New `OrderStatus` (app-level, distinct from PayPal's `OrderStatus`): `AwaitingPayment`,
   `Authorized`, `Fulfilled`, `Cancelled`, `Refunded`, `PartiallyRefunded`.
2. **PayPal client registration** — `PayPalServerSdkClient` via DI, credentials from user-secrets-backed
   configuration (`PayPal:ClientId/ClientSecret/Environment/Currency/BaseUrl`), fail-fast on missing secret.
   Uses `Orders`, `Payments`, `Vault`, `TransactionSearch` controllers.
3. **Flow 2 (saved cards) first** — `POST/GET/DELETE /api/payment-methods`, since Flow 1's "pay with saved
   card" path depends on it. Vault: `CreatePaymentToken`, `ListCustomerPaymentTokens`, `DeletePaymentToken`.
4. **Flow 1 (pay for an order)**:
   - `POST /api/orders` — pure eShop write, no PayPal call. Reuses `Order`/`OrderItem`.
   - `POST /api/orders/{id}/pay` — `Orders.CreateOrder` (intent=AUTHORIZE, `payment_source.card` direct or
     `vault_id`), store PayPal order id + authorization id/status.
   - `POST /api/orders/{id}/fulfil` — `Payments.CaptureAuthorizedPayment`; on a stale/expired authorization,
     `Payments.ReauthorizePayment` then retry capture once.
   - `POST /api/orders/{id}/cancel` — `Payments.VoidPayment`.
   - `POST /api/orders/{id}/refunds` — `Payments.RefundCapturedPayment`, full or partial.
   - `GET /api/my-orders` — caller's orders + payment state, no PayPal call (reads our own stored state).
5. **Reconciliation** — `GET /api/reconciliation` — `TransactionSearch.SearchTransactions`, looped across
   pages for the whole `[from,to]` range, matched against stored `OrderPayment.CaptureId`.

## 2. CONTRACT SHEET

⚠ Signatures below are generated code, verbatim — each operation that takes input takes ONE request record
as its first parameter, built via object initializer with the record's own property names, never flat
arguments. ⚠ Every SDK type is written fully-qualified with the namespace its source path implies (taken
from the path the map/source gives for *that* type).

Client: `PayPalServerSdk.PayPalServerSdkClient` (`PayPalServerSdk` namespace). Controllers used:
`client.Orders` (`Api/Orders.cs`), `client.Payments` (`Api/Payments.cs`), `client.Vault` (`Api/Vault.cs`),
`client.TransactionSearch` (`Api/TransactionSearch.cs`).

| Operation | Request record + members | Body model + fields | Response envelope (fields read) | Error case + accessors | Pagination | Source |
|---|---|---|---|---|---|---|
| `Orders.CreateOrder` | `PayPalServerSdk.Requests.Orders.CreateOrderRequest { Body: required OrderRequest, PayPalRequestId?: string }` | `PayPalServerSdk.Models.OrderRequest { Intent: required CheckoutPaymentIntent, PurchaseUnits: required IReadOnlyList<PurchaseUnitRequest>, PaymentSource?: PaymentSource }` | `PayPalServerSdk.Models.Order { Id, Status: OrderStatus, PurchaseUnits: IReadOnlyList<PurchaseUnit> (→ `[0].Payments.Authorizations[0].{Id,Status}`), PaymentSource: PaymentSourceResponse }` | `ApiException<CreateOrderError>` (A) — `TryGetError(out Error)`[400,401,422]·`TryGetRawError(out RawError)`[fallback] | none | `map/operations/Orders.md`, `Requests/Orders/CreateOrderRequest.cs`, `Models/OrderRequest.cs`, `Models/Order.cs` |
| `Orders.AuthorizeOrder` | `Requests.Orders.AuthorizeOrderRequest { Id: required string, Body?: OrderAuthorizeRequest, PayPalRequestId?: string }` | n/a (not used — see §6 Assumptions: direct-card CreateOrder with intent=AUTHORIZE authorizes synchronously) | `OrderAuthorizeResponse` | `ApiException<AuthorizeOrderError>` (A) — `TryGetError(out Error)`[400,401,403,404,422,500]·`TryGetRawError`[fallback] | none | `map/operations/Orders.md` — **not called in this plan**, kept for reference only |
| `Payments.GetAuthorizedPayment` | `Requests.Payments.GetAuthorizedPaymentRequest { AuthorizationId: required string }` | — | `PayPalServerSdk.Models.PaymentAuthorization { Status: AuthorizationStatus, Id, Amount: Money }` | `ApiException<GetAuthorizedPaymentError>` (A) — `TryGetError(out Error)`[401,403,404]·`TryGetNoContent(out RawError)`[500]·`TryGetRawError`[fallback] | none | `map/operations/Payments.md` |
| `Payments.CaptureAuthorizedPayment` | `Requests.Payments.CaptureAuthorizedPaymentRequest { AuthorizationId: required string, Body?: CaptureRequest, PayPalRequestId?: string }` | `PayPalServerSdk.Models.CaptureRequest { Amount?: Money, FinalCapture?: bool=false }` | `PayPalServerSdk.Models.CapturedPayment { Id, Status: CaptureStatus, Amount: Money, SellerReceivableBreakdown?: SellerReceivableBreakdown { GrossAmount: required Money, PaypalFee?: Money, NetAmount?: Money } }` | `ApiException<CaptureAuthorizedPaymentError>` (A) — `TryGetError(out Error)`[400,401,403,404,409,422]·`TryGetNoContent(out RawError)`[500]·`TryGetRawError`[fallback] | none | `map/operations/Payments.md`, `Requests/Payments/CaptureAuthorizedPaymentRequest.cs`, `Models/CaptureRequest.cs`, `Models/CapturedPayment.cs`, `Models/SellerReceivableBreakdown.cs` |
| `Payments.ReauthorizePayment` | `Requests.Payments.ReauthorizePaymentRequest { AuthorizationId: required string, Body?: ReauthorizeRequest, PayPalRequestId?: string }` | `PayPalServerSdk.Models.ReauthorizeRequest { Amount?: Money }` | `PaymentAuthorization { Id, Status }` | `ApiException<ReauthorizePaymentError>` (A) — `TryGetError(out Error)`[400,401,403,404,422]·`TryGetNoContent(out RawError)`[500]·`TryGetRawError`[fallback] | none | `map/operations/Payments.md`, `Requests/Payments/ReauthorizePaymentRequest.cs` |
| `Payments.VoidPayment` | `Requests.Payments.VoidPaymentRequest { AuthorizationId: required string }` | — | `PaymentAuthorization` (`void` semantically — Task<PaymentAuthorization>) | `ApiException<VoidPaymentError>` (A) — `TryGetError(out Error)`[401,403,404,409,422]·`TryGetNoContent(out RawError)`[500]·`TryGetRawError`[fallback] | none | `map/operations/Payments.md` |
| `Payments.RefundCapturedPayment` | `Requests.Payments.RefundCapturedPaymentRequest { CaptureId: required string, Body?: RefundRequest, PayPalRequestId?: string }` | `PayPalServerSdk.Models.RefundRequest { Amount?: Money, NoteToPayer?: string }` | `PayPalServerSdk.Models.Refund { Id, Status: RefundStatus, Amount: Money }` | `ApiException<RefundCapturedPaymentError>` (A) — `TryGetError(out Error)`[400,401,403,404,409,422]·`TryGetNoContent(out RawError)`[500]·`TryGetRawError`[fallback] | none | `map/operations/Payments.md`, `Requests/Payments/RefundCapturedPaymentRequest.cs`, `Models/RefundRequest.cs`, `Models/Refund.cs` |
| `Vault.CreatePaymentToken` | `Requests.Vault.CreatePaymentTokenRequest { Body: required PaymentTokenRequest, PayPalRequestId?: string }` | `PaymentTokenRequest { Customer?: Customer { MerchantCustomerId?: string }, PaymentSource: required PaymentTokenRequestPaymentSource { Card?: PaymentTokenRequestCard { Name?,Number?,Expiry?,SecurityCode?,BillingAddress? } } }` | `PaymentTokenResponse { Id, PaymentSource?: PaymentTokenResponsePaymentSource { Card?: CardPaymentTokenEntity { Name?,LastDigits?,Brand?:CardBrand,Expiry? } } }` | `ApiException<CreatePaymentTokenError>` (A) — `TryGetError(out Error)`[400,403,404,422,500]·`TryGetRawError`[fallback] | none | `map/operations/Vault.md`, `Requests/Vault/CreatePaymentTokenRequest.cs`, `Models/PaymentTokenRequest.cs`, `Models/PaymentTokenRequestPaymentSource.cs`, `Models/PaymentTokenRequestCard.cs`, `Models/PaymentTokenResponse.cs`, `Models/CardPaymentTokenEntity.cs` |
| `Vault.ListCustomerPaymentTokens` | `Requests.Vault.ListCustomerPaymentTokensRequest { CustomerId: required string, PageSize=5, Page=1, TotalRequired=false }` | — (query only) | `CustomerVaultPaymentTokensResponse { TotalItems?, TotalPages?, PaymentTokens?: IReadOnlyList<PaymentTokenResponse> }` | `ApiException<ListCustomerPaymentTokensError>` (A) — `TryGetError(out Error)`[400,403,500]·`TryGetRawError`[fallback] | page-number (manual; silent on a Pagination bullet ⇒ default "no `Pageable`" applies — read `TotalPages`/loop `Page` yourself) | `map/operations/Vault.md`, `Requests/Vault/ListCustomerPaymentTokensRequest.cs`, `Models/CustomerVaultPaymentTokensResponse.cs` |
| `Vault.DeletePaymentToken` | `Requests.Vault.DeletePaymentTokenRequest { Id: required string }` | — | `void` (Task) | `ApiException<DeletePaymentTokenError>` (A) — `TryGetError(out Error)`[400,403,500]·`TryGetRawError`[fallback] | none | `map/operations/Vault.md` |
| `TransactionSearch.SearchTransactions` | `Requests.TransactionSearch.SearchTransactionsRequest { StartDate: required string, EndDate: required string, PageSize=100, Page=1, Fields="transaction_info", BalanceAffectingRecordsOnly="Y" }` | — (query only) | `SearchResponse { TransactionDetails?: IReadOnlyList<TransactionDetails> (→ `.TransactionInfo.{TransactionId,TransactionAmount,TransactionInitiationDate}`), TotalPages?, Page? }` | `ApiException<RawError>` (B) — `StatusCode`·`ReadAsBytes()`·`ReadAsString()`·`ReadAsJson<T>()` | **no `Pageable`** (map silent) — `page`/`page_size` query params exist on the request record; loop `Page` from 1 while `Page <= (TotalPages ?? 1)`, accumulating `TransactionDetails` | `map/operations/TransactionSearch.md`, `Requests/TransactionSearch/SearchTransactionsRequest.cs`, `Models/SearchResponse.cs`, `Models/TransactionDetails.cs`, `Models/TransactionInformation.cs` |

Supporting model shapes used above (all `PayPalServerSdk.Models` unless noted):
- `Money { CurrencyCode: required string, Value: required string }` (`Models/Money.cs`) — amounts are
  **strings**, not decimal; format with `"0.00"` / invariant culture before assigning.
- `AmountWithBreakdown { CurrencyCode: required string, Value: required string }` (`Models/AmountWithBreakdown.cs`) — used for `PurchaseUnitRequest.Amount`.
- `PurchaseUnitRequest { Amount: required AmountWithBreakdown, ReferenceId? }` (`Models/PurchaseUnitRequest.cs`).
- `PaymentSource { Card?: CardRequest }` (`Models/PaymentSource.cs`) — `CardRequest { Name?, Number?, Expiry?(YYYY-MM), SecurityCode?, BillingAddress?: Address, VaultId? }` (`Models/CardRequest.cs`) — **either** `Number`/`Expiry`/`SecurityCode` (one-off card) **or** `VaultId` (saved card), never both.
- `Address { AddressLine1?, AdminArea2?(city), AdminArea1?(state), PostalCode?, CountryCode: required string }` (`Models/Address.cs`).
- `PaymentSourceResponse.Card: CardResponse? { LastDigits?, Brand?: CardBrand, Expiry? }` (`Models/CardResponse.cs`) — read back after `CreateOrder` for authorization-time card echo (not used for persistence — vault response is canonical for saved cards).
- Enums (`Models/Enums/`, `OpenStringEnum<T>`, static constants, `.Value` for wire string):
  - `CheckoutPaymentIntent`: `Capture`("CAPTURE"), `Authorize`("AUTHORIZE") — use `Authorize`.
  - `OrderStatus` (PayPal's, NOT our app's): `Created`,`Saved`,`Approved`,`Voided`,`Completed`,`PayerActionRequired`. **`PayerActionRequired` on `CreateOrder`'s response ⇒ STOP (see §6 Blocker) — this is the browser-approval challenge the task says to report, not build around.**
  - `AuthorizationStatus`: `Created`,`Captured`,`Denied`,`PartiallyCaptured`,`Voided`,`Pending`. No `Expired` member exists (confirmed from source) — staleness surfaces only as a capture-time error, not a status (§6 UNVERIFIED).
  - `CaptureStatus`: `Completed`,`Declined`,`PartiallyRefunded`,`Pending`,`Refunded`,`Failed`.
  - `RefundStatus`: `Cancelled`,`Failed`,`Pending`,`Completed`.
  - `CardBrand`: `Visa`("VISA"), `Mastercard`, `Discover`, `Amex`, `Solo`, others — open enum, use `.Value` for display.
- `Error { Name: required string, Message: required string, DebugId: required string, Details?: IReadOnlyList<ErrorDetails> }` (`Models/Error.cs`) — `Name`/`Details` are free-form provider strings, not enumerated in source (§6 UNVERIFIED for exact literal match).

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
|---|---|---|
| A `vault_id` passed to `CreateOrder`'s `CardRequest.VaultId` must be one this buyer actually owns | `Orders.CreateOrder` ← our own `PaymentMethod` table (populated only by `Vault.CreatePaymentToken` responses we stored) | `PayEndpoint`, before building the request: look up `PaymentMethod` by `(BuyerId, PaymentMethodId)`; 404 if absent/not owned — never pass a caller-supplied vault id straight through |
| An `AuthorizationId` acted on by `CaptureAuthorizedPayment`/`VoidPayment`/`ReauthorizePayment` must be the one this app recorded for that order | all three ← `Orders.CreateOrder` (stored on `OrderPayment.AuthorizationId`) | `FulfilEndpoint`/`CancelEndpoint`: load `OrderPayment` by our own `OrderId`, never accept a PayPal id from the caller |
| A `CaptureId` acted on by `RefundCapturedPayment` must be the one this app recorded for that order's capture | `RefundCapturedPayment` ← `CaptureAuthorizedPayment` (stored on `OrderPayment.CaptureId`) | `RefundEndpoint`: load `OrderPayment` by our own `OrderId` |
| A `CustomerId` passed to `ListCustomerPaymentTokens`/`Customer.MerchantCustomerId` on `CreatePaymentToken` must be the caller's own buyer id | both ← JWT `ClaimTypes.Name` (our `BuyerId`, matching existing `Order.BuyerId`/`Basket.BuyerId` convention) | `PaymentMethodsEndpoints`: always set from the authenticated principal, never from request body |

## 3. Trap notes

- Fulfil step (`CaptureAuthorizedPayment`): amounts/fees are returned as `Money` with a **string** `Value` — do not parse as culture-sensitive decimal naively. `MUST load dotnet-models` (string vs decimal fields; already loaded — applied: use `decimal.Parse(value, CultureInfo.InvariantCulture)` / format with `.ToString("F2", CultureInfo.InvariantCulture)`).
- Every write operation in scope (`CreateOrder`, `CaptureAuthorizedPayment`, `RefundCapturedPayment`, `ReauthorizePayment`, `CreatePaymentToken`) carries a **real** `PayPalRequestId` member distinct from the generator-injected `Idempotency-Key` header. `MUST load dotnet-configuration-resilience` (§ Making a write safe under retries — already loaded; applied: use `PayPalRequestId` as the idempotency key, derived deterministically from our own entity, never `Guid.NewGuid()` per call).
- `CreateOrder`/`CaptureAuthorizedPayment`/etc. are Case A with multiple `TryGet*` + `TryGetNoContent` + `TryGetRawError` arms — a ladder that checks only `TryGetError` silently drops the 500 `TryGetNoContent` and the fallback. `MUST load dotnet-error-handling` (already loaded; applied below in §PRODUCTION READINESS row 6/7 and in the catch ladders).
- `SearchTransactions` is Case B (`ApiException<RawError>`) while every other operation in scope is Case A — a shared catch helper typed for Case A will not compile against it. `MUST load dotnet-error-handling`.
- `OpenStringEnum<T>.ToString()` is NOT the wire value (record `ToString()` shadows the base override) — any enum written into a log line or comparison must use `.Value`. `MUST load dotnet-models`.

## 4. REQUIRED READING

Loaded before implementation starts (this session, in full):
- `dotnet-client-initialization` — client/DI construction, HttpClient lifetime.
- `dotnet-authentication` — `Oauth2` credentials wiring, fail-fast validation.
- `dotnet-calling-endpoints` — request-record construction, `RequestOptions`/`CancellationToken` placement.
- `dotnet-error-handling` — Case A/B catch ladders, `ResponseDeserializationException`.
- `dotnet-models` — `Money` string fields, enum `.Value`/`Match`, `AdditionalProperties`.
- `dotnet-configuration-resilience` — retries/idempotency/timeouts/logging, pagination (manual, since this SDK version returns no `Pageable` for `SearchTransactions`/`ListCustomerPaymentTokens`).
- `dotnet-testing` — `HttpMessageHandler` stub seam for unit tests of the payment service.

Mandatory hazard (verbatim): a body that does not match its declared type — a drifted or malformed **2xx**
response (a missing `required` member) or a **non-2xx** body that does not match its operation's generated
`{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the
HTTP status and names the target type but is **not** an `ApiException<TError>`; every catch ladder in this
plan also catches `ResponseDeserializationException` (or the non-generic `ApiException`) alongside the
typed/raw arms.

## 5. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
| 1 | Credential fail-fast | Bind `PayPal:ClientId/ClientSecret/Environment/Currency` into a `PayPalOptions` class via `AddOptions<PayPalOptions>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`; `[Required]` on all four. Host refuses to start if any is missing/blank (each checked independently — a blank string fails `[Required]` same as absent). `PayPal:BaseUrl` is optional, not validated. |
| 2 | Secret sourcing & rotation | Secrets loaded from environment variables (`PAYPAL_CLIENT_ID` etc., task-mandated) into **.NET user-secrets** at setup time (`dotnet user-secrets set`, run once by us, never written to any repo file); `AddPayPalServerSdkClient` options are captured once at DI registration (per `dotnet-client-initialization`) — rotating the secret requires a process restart; this is acceptable for a reference app and stated here rather than silently assumed. |
| 3 | Total timeout budget | `options.Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) }`; all PayPal calls in this plan are POST/DELETE/GET on non-idempotent-sensitive paths — per-attempt bound is 15s. A per-request `CancellationToken` budget of 30s is applied at the one service boundary (`PayPalPaymentService`) that fronts every SDK call, linked to `HttpContext.RequestAborted`. |
| 4 | Write-retry ownership | Default `HttpMethodsToRetry = GET,HEAD,PUT,OPTIONS` is left unchanged — every write in scope (`CreateOrder`, `CaptureAuthorizedPayment`, `RefundCapturedPayment`, `ReauthorizePayment`, `VoidPayment`, `CreatePaymentToken`, `DeletePaymentToken`) is POST/DELETE, so the SDK itself never resends them. `GetAuthorizedPayment`/`ListCustomerPaymentTokens`/`SearchTransactions` are GET and safely retfollowed are safely retried (read-only). |
| 5 | Idempotency & ambiguous writes | `PayPalRequestId` set on every write that offers it (see Trap notes), derived from a per-`OrderPayment` **guid salt** (`IdempotencySalt`, generated once and persisted on the claim row), not from our own small sequential `OrderId` — **corrected during self-verification**: PayPal's idempotency cache is scoped to the whole merchant account, shared across every deployment/test session pointed at this sandbox business account, and a key derived from a small integer (`order-1`, `capture-2`) collided with unrelated history from earlier sessions against the same shared sandbox account (observed: a `capture-2` key returned a cached capture from over a month earlier, for a different order entirely). Concretely: `CreateOrder` ← `{salt}-authorize`; `CaptureAuthorizedPayment` ← `{salt}-capture`; `ReauthorizePayment` ← `{salt}-reauthorize`; `RefundCapturedPayment` ← `{salt}-refund-{caller's Idempotency-Key header}` (task-mandated caller key, folded into the salted key so it stays globally unique too); `CreatePaymentToken` ← a fresh `Guid` per call (vaulting twice is harmless, see DUPLICATE CLAIMS). The salt re-rolls on a fresh payment attempt (`OrderPayment.ResetForNewAttempt`, called when retrying after `AuthorizationFailed`) so a new attempt never replays a failed attempt's cached outcome. `VoidPayment`/`DeletePaymentToken`/`GetAuthorizedPayment` carry no `PayPalRequestId` member (confirmed from source) — naturally idempotent in effect (voiding an already-voided/denied authorization, or deleting an already-deleted token, is treated as success-equivalent, see row 9). |
| 6 | Observability | `options.Logging.LoggerFactory` set explicitly from the host's `ILoggerFactory` (never left null); `LogRequestBody = false` always (row 7). Request/response line at `Information`, failures at `Error`/`Warning` per SDK default. `Error.DebugId` from any caught typed error is logged alongside our own `OrderId`/`PaymentMethodId` for correlation. |
| 7 | Sensitive data | `CardRequest`/`PaymentTokenRequestCard` carry raw PAN (`Number`), CVV (`SecurityCode`) — full card fields. `LogRequestBody` stays `false` unconditionally (not configurable), and `LoggerFactory` is always assigned explicitly (never left null) so `PAYPALSERVERSDKCLIENT_LOG` cannot switch body logging on externally. Our own request DTOs/logs never echo `CardNumber`/`Cvv`; only `Last4`/`Brand`/`PaymentMethodId` are logged or returned. |
| 8 | Environment selection | Single server group `Default`; `options.Environment = ServerEnvironment.Sandbox` always (task mandates sandbox only; `PayPal:Environment` config value is recorded/validated but this SDK version exposes only `Sandbox` as a declared environment — see §6 Assumption). `PayPal:BaseUrl`, when set, is applied to `options.Server.Default.Sandbox.BaseUrl` verbatim (task-mandated override point), which this SDK's per-environment `ServerOptions` shape supports regardless of which `ServerEnvironment` is selected. |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS below. |
| 10 | Partial results | `SearchTransactions` loop reads `TotalPages`; if the loop is stopped early by a page cap (100 pages, ~10k transactions for a date range — generous for sandbox testing), the reconciliation response carries `Truncated: bool` and `PagesRead`/`TotalPages` so the caller sees it was cut short, never silently. `ListCustomerPaymentTokens` is read to completion (bounded: `TotalPages` max 10 per the model's own `[Maximum(10)]`, so no cap needed, but the same loop-bound pattern is applied defensively with a 10-page cap). |
| 11 | Unknown outcomes | `CaptureAuthorizedPayment`/`RefundCapturedPayment`/`CreateOrder` on `SdkConnectionException`/`SdkTimeoutException`: re-read via `GetAuthorizedPayment`(for capture)/`GetRefund`(for refund, not in original op list — added)/`GetCapturedPayment` keyed by our stored `OrderId`-derived `PayPalRequestId`... see UNKNOWN OUTCOMES table — PayPal's idempotency on `PayPalRequestId` means a retried identical request after a connection failure is itself the reconciliation path (safe to resend with the same key), so the "re-read" for these three is "resend the same call with the same `PayPalRequestId`" rather than a separate lookup op. |

**DUPLICATE CLAIMS**

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
| `POST /api/orders/{id}/pay` → `CreateOrder` | `OrderPayment` row, created with a unique constraint on `OrderId` before the SDK call | EF Core unique index on `OrderPayment.OrderId` (single row per order) + an app-level status guard (`Order.Status != AwaitingPayment` ⇒ reject) checked **and the claim written** inside one DB transaction before the PayPal call | `DbUpdateException` (unique violation) around the claim insert, mapped to 409 Conflict | `PayEndpoint.HandleAsync`: insert `OrderPayment{OrderId, Status=Authorizing}` in a transaction → on success, call `CreateOrder` → on success, update the same row; on failure, mark `Status=AuthorizationFailed` |
| `POST /api/orders/{id}/fulfil` → `CaptureAuthorizedPayment` | `OrderPayment.Status` transition `Authorized → Capturing` via `EfRepository` optimistic concurrency (EF Core `RowVersion`/concurrency token) | EF Core `DbUpdateConcurrencyException` on the status-transition save, performed BEFORE the SDK call | caught around the pre-call status update; second caller gets 409 | `FulfilEndpoint.HandleAsync`: load `OrderPayment`, guard `Status == Authorized`, save `Status=Capturing` → catch concurrency exception → call `CaptureAuthorizedPayment` → save final status |
| `POST /api/orders/{id}/cancel` → `VoidPayment` | same optimistic-concurrency transition, `Authorized → Voiding` | `DbUpdateConcurrencyException` | same pattern | `CancelEndpoint.HandleAsync` |
| `POST /api/orders/{id}/refunds` → `RefundCapturedPayment` | a `Refund` row keyed by caller's idempotency key (`UNIQUE(OrderId, IdempotencyKey)`), inserted before the SDK call | EF Core unique index violation on `(OrderId, IdempotencyKey)` | `DbUpdateException` around the claim insert; second caller with the SAME key gets the first result back (looked up, not reinserted); a DIFFERENT key for a new partial refund proceeds normally | `RefundEndpoint.HandleAsync`: `TryInsertRefundClaim` → on conflict, `LoadExistingRefundByKey` and return it; on success, call `RefundCapturedPayment`, update the row with PayPal's refund id/status |
| `POST /api/payment-methods` → `CreatePaymentToken` | none needed — vaulting a card twice creates two harmless tokens, not a double charge; no money moves. Still: `PaymentMethod.Id` pre-allocated as a `Guid` before the call, used as the deterministic `PayPalRequestId`, so a client retry after a timeout reuses PayPal's own dedup on that key | PayPal's own `PayPalRequestId` dedup (3-hour window per Vault's doc) | n/a (no local conflict expected; PayPal returns the same token if resent within the window) | `CreatePaymentMethodEndpoint.HandleAsync` |

**PAGED READS**

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
|---|---|---|---|
| `GET /api/reconciliation` → `SearchTransactions` loop | page cap = 100 (own constant) | `ReconciliationResponse.Truncated: bool` + `PagesRead`/`TotalPages` fields | `ReconciliationEndpoint.HandleAsync` / `PayPalPaymentService.SearchAllTransactionsAsync` |
| `GET /api/payment-methods` → `ListCustomerPaymentTokens` (only if we choose to re-query PayPal rather than serve from our own `PaymentMethod` table) | N/A — **not used**: `GET /api/payment-methods` serves from our own `PaymentMethod` table (already scoped to the caller), never calls PayPal | n/a | n/a |

**UNKNOWN OUTCOMES**

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
|---|---|---|---|---|
| `CreateOrder` (pay) | resend `CreateOrder` with the identical `PayPalRequestId` (`order-{OrderId}`) | the `PayPalRequestId` itself (PayPal dedups and returns the original order) | `PayPalPaymentService.AuthorizeAsync` catch block for `SdkConnectionException`/`SdkTimeoutException` | `PayPalPaymentServiceTests.AuthorizeAsync_OnConnectionFailure_ResendIsSafeViaIdempotencyKey` (stub throws `HttpRequestException` once, then returns 201; assert exactly 2 requests sent, both carrying the same `PayPal-Request-Id` header value) |
| `CaptureAuthorizedPayment` (fulfil) | resend `CaptureAuthorizedPayment` with identical `PayPalRequestId` (`capture-{OrderId}`) | same | `PayPalPaymentService.CaptureAsync` catch block | `PayPalPaymentServiceTests.CaptureAsync_OnConnectionFailure_ResendIsSafeViaIdempotencyKey` |
| `RefundCapturedPayment` (refund) | resend `RefundCapturedPayment` with identical `PayPalRequestId` (`refund-{idempotencyKey}`) | same | `PayPalPaymentService.RefundAsync` catch block | `PayPalPaymentServiceTests.RefundAsync_OnConnectionFailure_ResendIsSafeViaIdempotencyKey` |

## 6. Assumptions & Blockers

**Assumptions (not blockers):**
- Direct-card `payment_source.card` (full PAN, or `vault_id`) with `Intent=Authorize` processes **synchronously**
  inside `CreateOrder` — i.e. no separate `AuthorizeOrder` call is needed, and the resulting `Order.Status`
  is `Completed` with `PurchaseUnits[0].Payments.Authorizations[0]` populated — because there is no payer
  redirect involved (card entered directly / vaulted card reused, matching the task's "no browser step").
  This matches `CreateOrderRequest.PayPalRequestId`'s own doc comment ("mandatory for all single-step create
  order calls — e.g. Create Order Request with payment source information like Card") describing exactly
  this as a single-step flow. If PayPal instead returns `Status = PayerActionRequired`, that is the
  browser-challenge case the task says to **stop and report** (see Blocker below) — not a bug to route
  around.
- Authorization staleness is not representable as an `AuthorizationStatus` (confirmed: no `Expired` member
  in the enum's source). The task requires "renew rather than fail outright" — implemented generically:
  on ANY `CaptureAuthorizedPaymentError` (via `TryGetError`/`TryGetNoContent`), attempt
  `ReauthorizePayment` once, then retry the capture once; if reauthorization itself throws, surface a
  502-with-operator-guidance response ("authorization can no longer be renewed; a new payment is required")
  rather than retrying indefinitely. The exact PayPal error `Name` string for "expired" is `UNVERIFIED`
  (not in generated source — provider prose only) so we do not branch on it; we branch generically on
  capture failure, which is safe because a reauthorize-then-retry on a non-staleness failure (e.g. a
  genuinely declined card) will also fail and surface the same operator-actionable error.
- Vault `CustomerId`/`Customer.MerchantCustomerId` scoping: our own `BuyerId` (JWT `ClaimTypes.Name`) is
  used as the Vault customer identifier (`merchant_customer_id`), consistent with the existing app's
  `Order.BuyerId`/`Basket.BuyerId` convention — `YOUR CALL — not in the map`.
- Reconciliation matching: **corrected twice during self-verification** — `TransactionInformation.TransactionId`
  is NOT the capture id (confirmed against live sandbox data). Matching instead uses
  `PurchaseUnitRequest.InvoiceId`, which `TransactionInformation.InvoiceId`'s own doc comment confirms is
  echoed back ("the invoice ID that is sent by the merchant with the transaction... the invoice ID of the
  authorizing transaction is reported") — a grounded correlation key, not a guess. First attempt derived it
  as `eshop-order-{orderId}`, which collided with `invoice_id` uniqueness ("required to be unique within
  each merchant account by default" per the same field's docs) across this conversation's own earlier test
  runs against the same shared sandbox account. Final design: the invoice id IS the order's own
  `OrderPayment.AuthorizeIdempotencyKey` (already a guid-salted, globally-unique string — see row 5), so
  one value serves both as PayPal's dedup key and the reconciliation correlation key, with no separate
  uniqueness concern. `YOUR CALL — not in the map` is the choice to use `InvoiceId` for this purpose; the
  field's echo-back behavior is documented in `Models/PurchaseUnitRequest.cs` / `Models/TransactionInformation.cs`.
  Unmatched entries on either side are reported, never hidden.
- `GetRefund`/`GetCapturedPayment` operations exist in the map (`Payments.md`) but are not required by any
  task-mandated endpoint; not implemented, kept as map references only.

**Blocker (reported per task instructions, not solved):**
- If a live sandbox call against `Orders.CreateOrder` with the test Visa card returns
  `Order.Status == OrderStatus.PayerActionRequired` (a 3DS/challenge requirement), this is the exact
  "challenge that requires a shopper to approve in a browser" scenario the task says to **stop and report**
  rather than build an approval round-trip for. The implementation below detects this status and returns a
  clear 502 identifying it as such; whether the sandbox business account actually triggers this for the
  given test card can only be confirmed by running the flow (done in self-verification, §below task
  execution, not at planning time).
