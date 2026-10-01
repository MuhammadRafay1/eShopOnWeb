# pay-pal-server-sdk-plan.md — PayPal payments + saved cards for eShopOnWeb

Contract sheet + implementation plan. Every SDK fact below was read **this session** from the PayPal
Server SDK map (`sdk-map.md` + `map/operations/*`) and the map-named source files, in a fresh clone of
`https://github.com/Darker98/paypal-csharp-sdk` branch `main` (API spec `2.29`) in the system temp dir. The
application design (entities, endpoints, persistence, concurrency) is decided here against the task and
`PLAN.md`. Root namespace `PayPalServerSdk`.

---

## 1. Scope & sequence

Build order; each step names the PayPal operation(s) it uses (`client.X.Op`).

1. **Project wiring** — add `Darker98.PayPalServerSdk` (version-less) to `src/Infrastructure`. `PayPalOptions`
   + startup validation. Register SDK client via `AddPayPalServerSdkClient` in `src/PublicApi/Program.cs`.
   No PayPal calls.
2. **Domain model** — extend `Order` (status machine), add `OrderPayment`, `OrderRefund`, `PaymentMethod`
   entities + EF config + specifications. No PayPal calls.
3. **Gateway** — `IPayPalGateway` in `ApplicationCore.Interfaces`, implemented `Infrastructure.PayPal`. One
   method per capability; every SDK call lives here. Translates SDK exceptions to app result/exception types.
4. **`POST /api/orders`** — no PayPal call; creates `Order`+`OrderItem`s+`OrderPayment` in `AwaitingPayment`.
5. **`POST /api/orders/{id}/pay`** — `Orders.CreateOrder` (Intent=AUTHORIZE + payment_source = create+authorize in one call).
6. **`POST /api/orders/{id}/fulfil`** — `Payments.GetAuthorizedPayment`, conditional `Payments.ReauthorizePayment`, then `Payments.CaptureAuthorizedPayment`.
7. **`POST /api/orders/{id}/cancel`** — `Payments.VoidPayment`.
8. **`POST /api/orders/{id}/refunds`** — `Payments.RefundCapturedPayment`.
9. **`GET /api/my-orders`** — no PayPal call.
10. **Saved cards** — `POST /api/payment-methods` (`Vault.CreateSetupToken` → `Vault.CreatePaymentToken`),
    `GET /api/payment-methods` (`Vault.ListCustomerPaymentTokens`), `DELETE` (`Vault.DeletePaymentToken`).
11. **`GET /api/reconciliation`** — `TransactionSearch.SearchTransactions`, paged manually.
12. **Tests** + sandbox verification + user guide.

---

## 2. CONTRACT SHEET

⚠ Signatures are generated code, verbatim. Every operation that takes input takes **one** request record as
its first parameter, built with an object initializer using that record's own property names — never flat
arguments. ⚠ Every SDK type is written fully-qualified with the namespace its declaring file's path implies
(take it from that type's own Source cell, never a neighbour's).

Client: `new PayPalServerSdkClient(HttpClient, PayPalServerSdkClientOptions)`. Only ctor.
Auth: `options.Oauth2 = new PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials { ClientId, ClientSecret }`.
Env: `options.Environment = PayPalServerSdk.Servers.ServerEnvironment.Sandbox` (the **only** declared constant).
BaseUrl override: `options.Server.Default.Sandbox.BaseUrl` — one server group `Default`, token call
(`POST {BaseUrl}/v1/oauth2/token`) resolves through the same group, so overriding redirects the token call too.

### 2.1 Operations used

| Controller.Operation | Signature · required members | Body model · fields used (wire) | Response · fields read | Error case · accessors | Source |
| --- | --- | --- | --- | --- | --- |
| `Orders.CreateOrder` | `CreateOrder(CreateOrderRequest req, …)` · `Body` required; also set `PayPalRequestId` | `OrderRequest { Intent (req, `intent`, `CheckoutPaymentIntent`), PurchaseUnits (req, `purchase_units`, `IReadOnlyList<PurchaseUnitRequest>`), PaymentSource (`payment_source`) }`; `PurchaseUnitRequest { Amount (req, `amount`, `AmountWithBreakdown`), InvoiceId (`invoice_id`), CustomId (`custom_id`), Description }`; `AmountWithBreakdown { CurrencyCode (req), Value (req) }`; `PaymentSource { Card (`CardRequest`), Token }`; `CardRequest { Number, Expiry (`YYYY-MM`), SecurityCode, Name, BillingAddress (`Address`), VaultId }` | `Order { Id, Status (`OrderStatus?`), PurchaseUnits[0].Payments (`PaymentCollection?`).Authorizations[0] (`AuthorizationWithAdditionalData`) { Id, Status (`AuthorizationStatus?`), Amount (`Money?`), ExpirationTime } }` | A: `ApiException<CreateOrderError>` → `TryGetError(out Error)` [400,401,422] · `TryGetRawError(out RawError)` [fallback] | `Requests/Orders/CreateOrderRequest.cs`, `Models/OrderRequest.cs`, `Models/PurchaseUnitRequest.cs`, `Models/AmountWithBreakdown.cs`, `Models/PaymentSource.cs`, `Models/CardRequest.cs`, `Models/Order.cs`, `Models/PurchaseUnit.cs`, `Models/PaymentCollection.cs`, `Models/AuthorizationWithAdditionalData.cs`, `Errors/CreateOrderError.cs`, `Models/Error.cs` |
| `Payments.GetAuthorizedPayment` | `GetAuthorizedPayment(GetAuthorizedPaymentRequest req, …)` · `AuthorizationId` req | — | `PaymentAuthorization { Id, Status (`AuthorizationStatus?`), ExpirationTime, Amount }` | A: `TryGetError(out Error)` [401,403,404] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback] | `Requests/Payments/GetAuthorizedPaymentRequest.cs`, `Models/PaymentAuthorization.cs` |
| `Payments.ReauthorizePayment` | `ReauthorizePayment(ReauthorizePaymentRequest req, …)` · `AuthorizationId` req; set `Body`, `PayPalRequestId` | `ReauthorizeRequest { Amount (`amount`, `Money?`) }` (omit to reauth same amount) | `PaymentAuthorization { Id, Status, ExpirationTime }` — take id to capture from **this response's own `Id`** (UNVERIFIED whether it equals the input id) | A: `TryGetError(out Error)` [400,401,403,404,422] · `TryGetNoContent` [500] · `TryGetRawError` | `Requests/Payments/ReauthorizePaymentRequest.cs`, `Models/ReauthorizeRequest.cs`, `Models/PaymentAuthorization.cs` |
| `Payments.CaptureAuthorizedPayment` | `CaptureAuthorizedPayment(CaptureAuthorizedPaymentRequest req, …)` · `AuthorizationId` req; set `Body`, `PayPalRequestId` | `CaptureRequest { Amount (`amount`), FinalCapture (`final_capture`, default false), NoteToPayer, SoftDescriptor }` | `CapturedPayment { Id, Status (`CaptureStatus?`), Amount, SellerReceivableBreakdown { GrossAmount (req), PaypalFee, NetAmount } }` | A: `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent` [500] · `TryGetRawError` | `Requests/Payments/CaptureAuthorizedPaymentRequest.cs`, `Models/CaptureRequest.cs`, `Models/CapturedPayment.cs`, `Models/SellerReceivableBreakdown.cs` |
| `Payments.VoidPayment` | `VoidPayment(VoidPaymentRequest req, …)` · `AuthorizationId` req; set `PayPalRequestId` | — (no Body member) | `PaymentAuthorization { Id, Status }` | A: `TryGetError(out Error)` [401,403,404,409,422] · `TryGetNoContent` [500] · `TryGetRawError` | `Requests/Payments/VoidPaymentRequest.cs`, `Models/PaymentAuthorization.cs` |
| `Payments.RefundCapturedPayment` | `RefundCapturedPayment(RefundCapturedPaymentRequest req, …)` · `CaptureId` req; set `Body`, `PayPalRequestId` | `RefundRequest { Amount (`amount`, omit for full), NoteToPayer, InvoiceId }` | `Refund { Id, Status (`RefundStatus?`), Amount, SellerPayableBreakdown }` | A: `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent` [500] · `TryGetRawError` | `Requests/Payments/RefundCapturedPaymentRequest.cs`, `Models/RefundRequest.cs`, `Models/Refund.cs` |
| `Payments.GetRefund` | `GetRefund(GetRefundRequest req, …)` · `RefundId` req | — | `Refund { Id, Status, Amount }` | A: `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError` | `Requests/Payments/GetRefundRequest.cs`, `Models/Refund.cs` |
| `Vault.CreateSetupToken` | `CreateSetupToken(CreateSetupTokenRequest req, …)` · `Body` req; set `PayPalRequestId` | `SetupTokenRequest { Customer (`customer`, `Customer?`), PaymentSource (req, `payment_source`, `SetupTokenRequestPaymentSource`) }`; `SetupTokenRequestPaymentSource { Card (`SetupTokenRequestCard?`) }`; `SetupTokenRequestCard { Number, Expiry, SecurityCode, Name, BillingAddress, VerificationMethod (`VaultCardVerificationMethod?`) }`; `Customer { Id, MerchantCustomerId }` | `SetupTokenResponse { Id, Status (`PaymentTokenStatus?`), Customer.Id }` | A: `TryGetError(out Error)` [400,403,422,500] · `TryGetRawError` | `Requests/Vault/CreateSetupTokenRequest.cs`, `Models/SetupTokenRequest.cs`, `Models/SetupTokenRequestPaymentSource.cs`, `Models/SetupTokenRequestCard.cs`, `Models/Customer.cs`, `Models/SetupTokenResponse.cs`, `Errors/CreateSetupTokenError.cs` |
| `Vault.CreatePaymentToken` | `CreatePaymentToken(CreatePaymentTokenRequest req, …)` · `Body` req; set `PayPalRequestId` | `PaymentTokenRequest { Customer (`customer`), PaymentSource (req, `payment_source`, `PaymentTokenRequestPaymentSource`) }`; `PaymentTokenRequestPaymentSource { Token (`VaultTokenRequest?`) }`; `VaultTokenRequest { Id (req), Type (req, `VaultTokenRequestType.SetupToken`) }` | `PaymentTokenResponse { Id, Customer (`CustomerResponse?`).Id, PaymentSource (`PaymentTokenResponsePaymentSource?`).Card (`CardPaymentTokenEntity?`) { LastDigits, Brand (`CardBrand?`), Expiry } }` | A: `TryGetError(out Error)` [400,403,404,422,500] · `TryGetRawError` | `Requests/Vault/CreatePaymentTokenRequest.cs`, `Models/PaymentTokenRequest.cs`, `Models/PaymentTokenRequestPaymentSource.cs`, `Models/VaultTokenRequest.cs`, `Models/PaymentTokenResponse.cs`, `Models/PaymentTokenResponsePaymentSource.cs`, `Models/CardPaymentTokenEntity.cs` |
| `Vault.ListCustomerPaymentTokens` | `ListCustomerPaymentTokens(ListCustomerPaymentTokensRequest req, …)` · `CustomerId` req | — | `CustomerVaultPaymentTokensResponse { PaymentTokens[] (`PaymentTokenResponse`), TotalItems, TotalPages }` | A: `TryGetError(out Error)` [400,403,500] · `TryGetRawError` | `Requests/Vault/ListCustomerPaymentTokensRequest.cs`, `Models/CustomerVaultPaymentTokensResponse.cs` |
| `Vault.DeletePaymentToken` | `DeletePaymentToken(DeletePaymentTokenRequest req, …)` · `Id` req | — | `void` (Task) | A: `TryGetError(out Error)` [400,403,500] · `TryGetRawError` | `Requests/Vault/DeletePaymentTokenRequest.cs` |
| `TransactionSearch.SearchTransactions` | `SearchTransactions(SearchTransactionsRequest req, …)` · `StartDate`,`EndDate` req; set `PageSize`(max 500), `Page`, `Fields`, `BalanceAffectingRecordsOnly` | — | `SearchResponse { TransactionDetails[] (`TransactionDetails`).TransactionInfo (`TransactionInformation?`) { TransactionId, TransactionAmount (`Money?`), TransactionStatus, InvoiceId, TransactionInitiationDate, PaypalReferenceId }, Page, TotalItems, TotalPages }` | **B (raw)**: `ApiException<RawError>` — `StatusCode`, `ReadAsString()`, `ReadAsJson<T>()`; no typed accessors | `Requests/TransactionSearch/SearchTransactionsRequest.cs`, `Models/SearchResponse.cs`, `Models/TransactionDetails.cs`, `Models/TransactionInformation.cs` |

StartDate/EndDate wire names: `start_date`/`end_date`; query mapping confirmed on the TransactionSearch page.
`ListCustomerPaymentTokensRequest` query wire: `customer_id`,`page_size`,`page`,`total_required`.

### 2.2 Enum values used (all `OpenStringEnum<T>`, `Models/Enums/`, namespace `PayPalServerSdk.Models.Enums`; use static members, branch with `Match`/`==`)

| Enum | Values | Wire |
| --- | --- | --- |
| `CheckoutPaymentIntent` | `Authorize` | `AUTHORIZE` (never `Capture` — always hold first) |
| `OrderStatus` | `Completed` (expected on direct-card create), `PayerActionRequired` (**STOP** condition) | `COMPLETED`, `PAYER_ACTION_REQUIRED` |
| `AuthorizationStatus` | `Created`, `Captured`, `PartiallyCaptured`, `Denied`, `Voided`, `Pending` | — |
| `CaptureStatus` | `Completed`, `Declined`, `Pending`, `PartiallyRefunded`, `Refunded`, `Failed` | — |
| `RefundStatus` | `Completed`, `Pending`, `Failed`, `Cancelled` | — |
| `PaymentTokenStatus` | `Created`, `Approved`, `Vaulted`, `Tokenized` (happy); `PayerActionRequired` (**STOP**) | — |
| `VaultCardVerificationMethod` | `ScaWhenRequired` (set explicitly to minimise — not eliminate — payer-action) | `SCA_WHEN_REQUIRED` |
| `VaultTokenRequestType` | `SetupToken` (only declared value) | `SETUP_TOKEN` |

### 2.3 CROSS-OPERATION INVARIANTS (the map never states these; derived from the task)

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| The `AuthorizationId` captured/voided/reauthorized must be one *this order's* `CreateOrder`/`ReauthorizePayment` returned | `CaptureAuthorizedPayment`/`VoidPayment`/`ReauthorizePayment` ← `CreateOrder`/`ReauthorizePayment` | read `OrderPayment.PayPalAuthorizationId` for the row — never accept an auth id from the HTTP caller |
| The `CaptureId` refunded must be one *this order's* `CaptureAuthorizedPayment` returned | `RefundCapturedPayment` ← `CaptureAuthorizedPayment` | read `OrderPayment.PayPalCaptureId` — never from the caller |
| A refund's amount must never exceed `CapturedAmount − Σ(prior non-failed refunds)` | `RefundCapturedPayment` ← `CaptureAuthorizedPayment`, prior refunds | app check in `RefundOrderEndpoint`, computed from `OrderPayment.CapturedAmount` and `OrderRefund` rows, **before** calling PayPal |
| The saved card paid with (`card.vault_id`) must be one `ListCustomerPaymentTokens`/`CreatePaymentToken` returned for *this shopper* | `CreateOrder` (saved-card path) ← `CreatePaymentToken`/`ListCustomerPaymentTokens` | `PayOrderEndpoint` resolves caller's int `paymentMethodId` to a `PaymentMethod` row filtered by `BuyerId==caller`, reads `PayPalVaultId` — never a raw vault id from the caller |
| The `Customer.Id` on CreateSetupToken/CreatePaymentToken/ListCustomerPaymentTokens for a shopper must be the **same** PayPal id across calls | `CreateSetupToken`/`CreatePaymentToken` → `ListCustomerPaymentTokens` | `SavePaymentMethodEndpoint` reuses the stored `PaymentMethod.PayPalCustomerId` for `BuyerId`; if none, sends `Customer.MerchantCustomerId = hash(BuyerId)` and persists the PayPal-returned `customer.id`. `ListCustomerPaymentTokensRequest.CustomerId` pattern (`^[0-9a-zA-Z_-]+$`, 7–36) rules out the shopper's email — use the stored PayPal id only |

---

## 3. Trap notes (one hazard per line + `MUST load`; not resolved here)

- **`CreateOrder` alone creates+authorizes when a `payment_source` is supplied** — reading the authorization
  out of the response means walking a 3-level nested envelope (`Order.PurchaseUnits[0].Payments.Authorizations[0]`).
  `MUST load dotnet-calling-endpoints` for reading a nested response envelope.
- **Reauthorize may not keep the same authorization id (UNVERIFIED).** Overwrite
  `OrderPayment.PayPalAuthorizationId` with the response's own `Id` before capturing. `MUST load dotnet-models`
  for branching on the returned enum via `Match`/`==` rather than string compare.
- **`PayerActionRequired` (on `OrderStatus` and `PaymentTokenStatus`) is a declared, checkable value** — the
  task's STOP condition, not an `otherwise` branch. `MUST load dotnet-models` for how to branch on the enum.
- **Typed-error accessor is `TryGetError` on every Case-A op, but the declared type differs per operation**
  (`CreateOrderError`, `CaptureAuthorizedPaymentError`, …); `SearchTransactions` is the one Case-B op.
  `MUST load dotnet-error-handling` before any catch — incl. the mandatory `ResponseDeserializationException`
  arm (a drifted 2xx or an error body not matching `{Operation}Error` surfaces there, not in a typed catch).
- **`PayPal-Request-Id` (request record member `PayPalRequestId`) is the real idempotency key; the generator-
  injected `Idempotency-Key: Guid.NewGuid()` header is not.** `MUST load dotnet-configuration-resilience` for
  the idempotency-key reasoning and why a header's mere presence proves nothing.
- **`ListCustomerPaymentTokens.CustomerId` cannot be the shopper's email** (pattern rules it out) — use the
  persisted PayPal `customer.id`. `MUST load dotnet-calling-endpoints` for required-vs-optional params.
- **`SearchTransactions` pagination is manual** (plain `page`/`page_size` + `SearchResponse.TotalPages`, no
  auto-`Pageable`); the loop needs a hand-written stop condition and page cap. `MUST load
  dotnet-configuration-resilience` for bounding a page loop.
- **A write whose connection fails after PayPal may have acted must be settled, not reported as failure.**
  `MUST load dotnet-configuration-resilience` for the unknown-outcome recovery pattern.
- **Card PAN/CVV (`CardRequest.Number`/`SecurityCode`, `SetupTokenRequestCard.Number`/`SecurityCode`) must
  never be logged or persisted.** `MUST load dotnet-configuration-resilience` for the `LogRequestBody`/
  `LoggerFactory` posture.
- **DI/HttpClient lifetime for the SDK client.** `MUST load dotnet-client-initialization`.
- **Binding credentials to `options.Oauth2` + startup fail-fast.** `MUST load dotnet-authentication`.
- **Testing the gateway via the `HttpClient` seam.** `MUST load dotnet-testing`.

---

## 4. REQUIRED READING (load every one BEFORE implementation; this sheet does not carry their content)

| Skill (plugin: `paypal-sdk`) | Governs |
| --- | --- |
| `paypal-sdk:dotnet-client-initialization` | Registering `PayPalServerSdkClient` via DI — `HttpClient` lifetime, named client |
| `paypal-sdk:dotnet-authentication` | Binding `PayPal:ClientId`/`ClientSecret` to `options.Oauth2`; startup fail-fast |
| `paypal-sdk:dotnet-calling-endpoints` | Every §2.1 call — one request record per call, nested response envelopes |
| `paypal-sdk:dotnet-models` | Branching on enums via `Match`/`==`; `Money.Value` formatting; union `TryGet` |
| `paypal-sdk:dotnet-error-handling` | Every catch — Case-A ladder per op, mandatory `ResponseDeserializationException`, Case-B for `SearchTransactions` |
| `paypal-sdk:dotnet-configuration-resilience` | Retry/timeout, idempotency-key reasoning, manual pagination, unknown-outcome recovery, logging/sensitive data |
| `paypal-sdk:dotnet-testing` | Gateway unit tests — stub `HttpMessageHandler`, assert outgoing `PayPal-Request-Id`/body, assert error cases |

⚠ Every APIMatic .NET plugin ships these same skill names; load the `paypal-sdk:` copies.
⚠ Mandatory hazard row: a drifted/malformed **2xx** (missing `required` member) or a **non-2xx** body not
matching its `{Operation}Error` shape surfaces as `ResponseDeserializationException` — an `ApiException`
keeping the HTTP status and target type but **not** an `ApiException<TError>`; a ladder handling only
`ApiException<TError>` lets it escape, so also catch `ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | **Credential fail-fast** | `PayPalOptions { ClientId, ClientSecret, Environment, Currency, BaseUrl }` bound from `PayPal` section via `AddOptions<PayPalOptions>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`. `ValidateOnStart` delegate also rejects whitespace-only `ClientId`/`ClientSecret`/`Environment`/`Currency` (not `BaseUrl` — optional) via `string.IsNullOrWhiteSpace` (`[Required]` alone passes whitespace). Host refuses to start if any is blank. |
| 2 | **Secret sourcing & rotation** | Values come from env vars `PAYPAL_CLIENT_ID`/`_SECRET`/`_ENVIRONMENT`/`_CURRENCY`, loaded into **.NET user-secrets** (never into repo files). `Program.cs` adds env vars + user-secrets to config; the options callback captures the bound options **once at registration** in the singleton client — a rotated secret needs a process restart (acceptable; no live-rotation requirement). |
| 3 | **Total timeout budget** | `options.Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) }` (per-attempt) + named `HttpClient.Timeout = 100s` backstop. The real whole-call budget is a `CancellationTokenSource` linked to `HttpContext.RequestAborted` with `CancelAfter(30s)`, created once in a shared gateway helper and passed to every SDK call. |
| 4 | **Write-retry ownership** | All writes here are `POST`/`DELETE`; SDK default `HttpMethodsToRetry` = `GET,HEAD,PUT,OPTIONS` already excludes them — not widened. Reads (`GetAuthorizedPayment`, `GetRefund`, `ListCustomerPaymentTokens`, `SearchTransactions`) are `GET`, safe under default retry. |
| 5 | **Idempotency & ambiguous writes** | Each write sets `PayPalRequestId` deterministically in local state, keyed on the order's **globally-unique `InvoiceReference`** (a per-order GUID), **not** the order id — see §10: the in-memory store resets order ids each run and PayPal retains keys for hours/days, so an id-based key would return a prior run's stale result. `CreateOrder`→`pay-{invoiceRef}-{version}`; `Capture`→`fulfil-{invoiceRef}`; `Void`→`cancel-{invoiceRef}`; `Reauthorize`→`reauth-{invoiceRef}-{version}`; `Refund`→`refund-{invoiceRef}-{callerKey}` (caller key from request body). `CreateSetupToken`/`CreatePaymentToken`→fresh GUID (task needs no save idempotency; worst case a duplicate saved card the shopper deletes). The injected `Idempotency-Key` header is **not** cited as a key. |
| 6 | **Observability** | `options.Logging.LoggerFactory` always assigned from DI (via `AddPayPalServerSdkClient`) so `PAYPALSERVERSDKCLIENT_LOG` can't silently enable body logging; `LogRequestBody` stays `false` always. On a PayPal error, log (Warning/Error): operation name, local order/refund id, HTTP status, and (Case A) `Error.Name`/`Error.DebugId`. `DebugId` is surfaced to the operator on the stale-auth-can't-renew response. |
| 7 | **Sensitive data** | `CardRequest`/`SetupTokenRequestCard` carry `Number` (PAN) + `SecurityCode` (CVV): read from the HTTP request, used to build the SDK request, discarded; never persisted, never logged. `LogRequestBody=false` always (single shared client). Stored/returned card data is PayPal-masked (`CardPaymentTokenEntity.LastDigits`/`Brand`/`Expiry`) — safe to persist/return. |
| 8 | **Environment selection** | SDK declares one `ServerEnvironment` constant (`Sandbox`); no live endpoint reachable through the selector. Startup still checks `PayPal:Environment` case-insensitively == `sandbox` and fails fast otherwise (named-key message). `PayPal:BaseUrl`, when non-empty, applied verbatim to `options.Server.Default.Sandbox.BaseUrl` (also redirects the OAuth2 token call). |
| 9 | **Duplicate prevention** | See DUPLICATE CLAIMS. |
| 10 | **Partial results** | See PAGED READS. |
| 11 | **Unknown outcomes** | See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `POST /api/orders/{id}/pay` | `Order.Status` + concurrency token `OrderPayment.Version` (`int`, `.IsConcurrencyToken()`); `Order.BeginAuthorizing()` asserts `AwaitingPayment`, sets `Authorizing`, bumps Version, `UpdateAsync` before PayPal | EF Core optimistic-concurrency check on `Version` (InMemory enforces concurrency tokens → `DbUpdateConcurrencyException`) | `catch (DbUpdateConcurrencyException)` in `PaymentService.PayAsync` → re-read, return current state | `PaymentService.PayAsync`: `order.BeginAuthorizing()` then `_orders.UpdateAsync(order, ct)` |
| `POST /api/orders/{id}/fulfil` | same, `Authorized→Capturing` | same | `PaymentService.FulfilAsync` | `PaymentService.FulfilAsync`: `order.BeginCapturing()` then `_orders.UpdateAsync(order, ct)` |
| `POST /api/orders/{id}/cancel` | same, `{AwaitingPayment,Authorized}→Cancelling` | same | `PaymentService.CancelAsync` | `PaymentService.CancelAsync`: `order.BeginCancelling()` then `_orders.UpdateAsync(order, ct)` |
| `POST /api/orders/{id}/refunds` | new `OrderRefund` row inserted **before** PayPal, unique index on `(OrderPaymentId, IdempotencyKey)`; a prior-row lookup by key short-circuits the common double-click (InMemory does not enforce unique indexes, so PayPal's own `PayPal-Request-Id` is the ultimate money-safety guard) | lookup-by-key + unique index (`DbUpdateException` where enforced) + PayPal idempotency | `catch (DbUpdateException)` in `PaymentService.RefundAsync` → load existing row for key, return its stored result | `PaymentService.RefundAsync`: `order.AttachRefund(refund)` then `_orders.UpdateAsync(order, ct)`, preceding `_gateway.RefundAsync(...)` |
| `POST /api/payment-methods` | none (task needs none; a double-click = a duplicate saved card the shopper can delete) | n/a | n/a | n/a |

### PAGED READS

| Read | What caps it | How the caller learns it was cut short | Where in the code |
| --- | --- | --- | --- |
| `GET /api/reconciliation` (`SearchTransactions`, looped by `page`, `page_size=500`) | hard `MaxPages` (200) **and** natural stop `page >= TotalPages` | `truncated: bool` field on the `ReconciliationView` response body, set true only if `MaxPages` hit before `TotalPages` | `PayPalGateway.SearchTransactionsAsync` page loop → `PayPalTransactionReport.Truncated` → `ReconciliationView.Truncated` |
| ~~`GET /api/payment-methods`~~ | n/a — see §10: this endpoint reads the application's own `PaymentMethod` rows, not a paged PayPal list | n/a | n/a |

### UNKNOWN OUTCOMES

| Write | Re-read with | Reference searched by | Where in the code | Test that fails the connection |
| --- | --- | --- | --- | --- |
| `CreateOrder` (pay) | `CreateOrder` resent | same `PayPal-Request-Id` (`pay-{orderId}-{version}`, 6h key retention) → returns original outcome | `PayPalGateway.ExecuteAsync` transient arm resends `call(ct)` once; `PaymentService.PayAsync` `catch (PaymentOutcomeUnknownException)` → `order.MarkUnknown()` | `PayPalGatewayTests.Authorize_connection_failure_retries_same_request_id` |
| `CaptureAuthorizedPayment` (fulfil) | `CaptureAuthorizedPayment` resent | same `PayPal-Request-Id` (`fulfil-{orderId}`, 45d) | `PayPalGateway.ExecuteAsync` transient arm; `PaymentService.FulfilAsync` `catch` → `MarkUnknown()` | `PayPalGatewayTests.Capture_connection_failure_retries_same_request_id` |
| `VoidPayment` (cancel) | `VoidPayment` resent (same key `cancel-{orderId}`) — **changed from the planned `GetAuthorizedPayment` recovery**: `VoidPaymentRequest` carries a real `PayPalRequestId` (45d), so a same-key resend is both simpler and idempotent | `PayPalGateway.ExecuteAsync` transient arm; `PaymentService.CancelAsync` `catch` → `MarkUnknown()` | `PayPalGatewayTests.Void_connection_failure_retries_same_request_id` |
| `RefundCapturedPayment` (refund) | `RefundCapturedPayment` resent | same `PayPal-Request-Id` (`refund-{orderId}-{callerKey}`, 45d) | `PayPalGateway.ExecuteAsync` transient arm; `PaymentService.RefundAsync` `catch` leaves the refund `PENDING` (still counts) + `MarkUnknown()` | `PayPalGatewayTests.Refund_connection_failure_retries_same_request_id` |

On `SdkConnectionException`/`SdkTimeoutException` from a write, the gateway re-runs the write **once** with
the same key; if still unsettled it throws `PaymentOutcomeUnknownException`, and the service marks the order
`Status = Unknown` for a human/sweep — never silently failed, never silently succeeded.

---

## 10. Implementation notes — verified deviations (recorded after live sandbox runs)

These are `YOUR CALL` application decisions revised at implementation time against observed sandbox behaviour;
every SDK contract fact above is unchanged.

- **Capture sends an explicit amount, not an implicit full capture.** `CaptureAuthorizedPayment` is called
  with `CaptureRequest.Amount` = the order total (= authorized amount) and `FinalCapture = true`. Observed:
  the PayPal sandbox intermittently captured a wrong (smaller) amount when the amount was omitted; sending it
  explicitly makes the captured amount equal the order total to the cent, every time. (`PayPalGateway.CaptureAsync`)
- **Card vaulting uses a minimal setup-token.** `CreateSetupToken` sends only `card.{number,expiry,name,billing_address}`
  (plus `customer.id` when the shopper already has one). Sending `security_code`, `verification_method`
  (`ScaWhenRequired`), or `customer.merchant_customer_id` produced a persistent PayPal `500 INTERNAL_SERVER_ERROR`
  on this sandbox; the minimal shape vaults cleanly with no payer-action. The customer-id continuity invariant
  (§2.3) is preserved by sending the stored `customer.id` on a shopper's second and later cards. (`PayPalGateway.SaveCardAsync`)
- **`GET /api/payment-methods` reads the application's own `PaymentMethod` rows**, not
  `Vault.ListCustomerPaymentTokens`. The vaulted-card data this app shows is already persisted (masked) at save
  time and is the authoritative "caller's saved cards"; a delete removes the local row and the PayPal vault
  token, so the list stays correct. `Vault.ListCustomerPaymentTokens` is therefore not on the live path, and the
  only paged read is reconciliation (PAGED READS now has one row).
- **`PAYER_ACTION_REQUIRED` / 3DS was not encountered** with the sandbox test card on either the pay or the
  vault path; the checkable stop-condition code path (`PayPalPayerActionRequiredException`) remains in place.
- **`TRANSACTION_REFUSED` (422) is an intermittent sandbox decline, not a gap.** A refused authorization reverts
  the order to `AwaitingPayment` so the shopper can retry; the integration is otherwise correct.
- **Idempotency keys are keyed on `OrderPayment.InvoiceReference` (a per-order GUID), not the order id.**
  Observed: with the in-memory store resetting order ids to small integers each run, an id-based key
  (`fulfil-4`) collided with a prior run and PayPal returned that run's *stale* capture/authorization (wrong
  amount) under its 45-day/6-hour key retention. Keying on the globally-unique invoice reference removes the
  collision while preserving within-run idempotency (the reference is stable for an order's lifetime).

---

## 6. Assumptions & Blockers

**Assumptions** (decided, not blocking):
- PublicApi JWT carries only `ClaimTypes.Name` (username/email) + role claims (`IdentityTokenClaimService`);
  no numeric user-id claim. New endpoints use `User.Identity!.Name!` as buyer id (matches existing
  `Order.BuyerId`/`CustomerOrdersSpecification`). Not touching the auth pipeline.
- `Order` ctor requires a free-text `ShipToAddress` (`Address(street,city,state,country,zipcode)`). `POST
  /api/orders` DTO carries that shape. PayPal card `BillingAddress` needs a 2-letter ISO `CountryCode`
  (`Models/Address.cs`, `required`) — a different shape — so `POST /api/orders/{id}/pay`'s one-off-card DTO
  carries its own billing-address fields incl. a 2-letter country code. Deliberate, task-consistent.
- New endpoints follow the `MinimalApi.Endpoint` `IEndpoint<IResult, TRequest[, TService]>` convention (the
  dominant one; `CreateCatalogItemEndpoint` exemplar); role auth applied inline on the mapped lambda with
  `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
- Amounts formatted `decimal.ToString("F2", InvariantCulture)` (matches `Money.Value` regex + "to the cent").
  `CurrencyCode` always from `PayPal:Currency`, never hardcoded.
- Reconciliation lineup key: the capture id we store (`OrderPayment.PayPalCaptureId`) vs report
  `TransactionInformation.TransactionId`/`PaypalReferenceId`, plus `InvoiceId` (we set `invoice_id` on the
  purchase unit to a stored per-order reference). PayPal sandbox reporting lag may return an empty but
  well-formed range — not a gap.

**Blockers:** none. Every capability maps to a grounded operation (§2.1). The one task STOP condition
(payer-action/3DS) is a declared, checkable enum value (§2.2), not unrepresentable.
