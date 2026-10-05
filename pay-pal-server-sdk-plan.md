# PayPal Server SDK (.NET) — integration plan for eShopOnWeb

SDK source (plugin-relative): `sdk/dotnet/` of the `paypal` plugin. Map: `sdk/dotnet/sdk-map.md`, `sdk/dotnet/map/operations/*.md`.
Every path in the **source** column below is relative to that SDK root.

## 1. Scope & sequence

| # | Step | SDK operations |
| --- | --- | --- |
| 0 | Repo prerequisites: `global.json` roll-forward, reference `sdk/dotnet/PayPalServerSdk.csproj` from `src/Infrastructure`, user-secrets for `PayPal:*` | — |
| 1 | `PayPalOptions` bound from `PayPal:` (`ClientId`, `ClientSecret`, `Environment`, `Currency`, `BaseUrl`), validated on start; client registered once | client construction |
| 2 | Domain: `Order` gains status + `OrderPayment` (+ refunds); `SavedPaymentMethod` aggregate; `PaymentOperationClaim` (string PK) | — |
| 3 | `POST /api/orders`, `GET /api/my-orders` (no PayPal) | — |
| 4 | Saved cards: `POST/GET/DELETE /api/payment-methods` | `Vault.CreatePaymentToken`, `Vault.DeletePaymentToken` |
| 5 | Pay (authorize): `POST /api/orders/{id}/pay` | `Orders.CreateOrder`, `Orders.AuthorizeOrder`, `Orders.GetOrder` (settle) |
| 6 | Fulfil (capture): `POST /api/orders/{id}/fulfil` | `Payments.GetAuthorizedPayment`, `Payments.ReauthorizePayment`, `Payments.CaptureAuthorizedPayment`, `Payments.GetCapturedPayment`, `Orders.GetOrder` (settle) |
| 7 | Cancel (void): `POST /api/orders/{id}/cancel` | `Payments.VoidPayment`, `Payments.GetAuthorizedPayment` (settle) |
| 8 | Refund: `POST /api/orders/{id}/refunds` | `Payments.RefundCapturedPayment`, `Orders.GetOrder` (settle) |
| 9 | Reconciliation: `GET /api/reconciliation?from&to` | `TransactionSearch.SearchTransactions` (manual page walk, ≤31-day windows) |
| 10 | Offline tests (fake `HttpMessageHandler` behind the SDK's `HttpClient`), live sandbox verification | all of the above |

The `…1` duplicates on the Payments/Vault/TransactionSearch pages (e.g. `CaptureAuthorizedPayment1`) hit the identical route (`Api/Payments.cs` lines 47/80); the un-suffixed variant is used everywhere.

## 2. CONTRACT SHEET

> ⚠ Signatures are generated code, verbatim. Each operation that takes input takes ONE request record as its first parameter, built with an object initializer using the record's own property names — never flat arguments. Every call is `(request, RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)`.
> ⚠ Every SDK type is written with the namespace its own source path implies: `Models/X.cs` → `PayPalServerSdk.Models`, `Models/Enums/X.cs` → `PayPalServerSdk.Models.Enums`, `Errors/X.cs` → `PayPalServerSdk.Errors`, `Requests/<Ctl>/X.cs` → `PayPalServerSdk.Requests.<Ctl>`, `Core/Exceptions` → `PayPalServerSdk.Core.Exceptions`, `Core/ErrorResponse` → `PayPalServerSdk.Core.ErrorResponse`, `Core/Configuration` → `PayPalServerSdk.Core.Configuration`, `Servers/` → `PayPalServerSdk.Servers`, OAuth creds → `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials`.

| Op (`client.X.Y`) | Request record (members used) | Body model (fields used, wire) | Returns → fields read | Error case | Source |
| --- | --- | --- | --- | --- | --- |
| `Orders.CreateOrder` | `CreateOrderRequest { Body: OrderRequest (req), PayPalRequestId: string? (1..108; docs: mandatory for single-step create with card/vault payment source; kept 6 h), Prefer: string = "return=minimal" }` | `OrderRequest { Intent (intent): CheckoutPaymentIntent req; PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnitRequest> req; PaymentSource (payment_source): PaymentSource? }` · `PurchaseUnitRequest { Amount (amount): AmountWithBreakdown req; ReferenceId (reference_id); CustomId (custom_id); Description (description) }` · `AmountWithBreakdown { CurrencyCode (currency_code) req; Value (value): string req }` · `PaymentSource { Card (card): CardRequest? }` · `CardRequest { Name (name), Number (number), Expiry (expiry), SecurityCode (security_code), BillingAddress (billing_address): Address?, VaultId (vault_id) }` · `Address { AddressLine1 (address_line_1), AdminArea2 (admin_area_2), AdminArea1 (admin_area_1), PostalCode (postal_code), CountryCode (country_code) req }` | `Order` → `Id`, `Status: OrderStatus?`, `PurchaseUnits[0].Payments.Authorizations`, `PaymentSource.Card (CardResponse: LastDigits, Brand)` | A: `ApiException<CreateOrderError>`; `TryGetError(out Error)` [400,401,422], `TryGetRawError` | `map/operations/Orders.md#CreateOrder`; `Requests/Orders/CreateOrderRequest.cs`; `Models/OrderRequest.cs`, `PurchaseUnitRequest.cs`, `AmountWithBreakdown.cs`, `PaymentSource.cs`, `CardRequest.cs`, `Address.cs`, `Order.cs` |
| `Orders.AuthorizeOrder` | `AuthorizeOrderRequest { Id: string req (^[A-Z0-9]+$, ≤36), PayPalRequestId: string? (kept 6 h), Prefer = "return=minimal", Body: OrderAuthorizeRequest? }` | `OrderAuthorizeRequest { PaymentSource (payment_source): OrderAuthorizeRequestPaymentSource? }` — left null (source was given on create; remarks: "buyer must first approve the order or a valid payment_source must be provided") | `OrderAuthorizeResponse` → `Id`, `Status: OrderStatus?`, `PurchaseUnits[0].Payments.Authorizations[0]` (`AuthorizationWithAdditionalData`: `Id`, `Status: AuthorizationStatus?`, `StatusDetails.Reason`, `Amount: Money?`, `ExpirationTime`, `CreateTime`), `PaymentSource.Card (CardResponse)` | A: `ApiException<AuthorizeOrderError>`; `TryGetError(out Error)` [400,401,403,404,422,500] | `map/operations/Orders.md#AuthorizeOrder`; `Requests/Orders/AuthorizeOrderRequest.cs`; `Models/OrderAuthorizeResponse.cs`, `OrderAuthorizeResponsePaymentSource.cs`, `AuthorizationWithAdditionalData.cs`, `PurchaseUnit.cs`, `PaymentCollection.cs`, `CardResponse.cs` |
| `Orders.GetOrder` | `GetOrderRequest { Id: string req }` | — | `Order` → `Status`, `PurchaseUnits[0].Payments.{Authorizations, Captures (OrdersCapture), Refunds (Refund)}` | A: `ApiException<GetOrderError>`; `TryGetError(out Error)` [401,404] | `map/operations/Orders.md#GetOrder`; `Requests/Orders/GetOrderRequest.cs`; `Models/PaymentCollection.cs`, `OrdersCapture.cs` |
| `Payments.GetAuthorizedPayment` | `GetAuthorizedPaymentRequest { AuthorizationId: string req }` | — | `PaymentAuthorization` → `Id`, `Status`, `Amount`, `ExpirationTime`, `CreateTime` | A: `ApiException<GetAuthorizedPaymentError>`; `TryGetError(out Error)` [401,403,404], `TryGetNoContent(out RawError)` [500] | `map/operations/Payments.md#GetAuthorizedPayment`; `Requests/Payments/GetAuthorizedPaymentRequest.cs`; `Models/PaymentAuthorization.cs` |
| `Payments.ReauthorizePayment` | `ReauthorizePaymentRequest { AuthorizationId req, PayPalRequestId: string? (kept 45 d), Prefer, Body: ReauthorizeRequest? }` | `ReauthorizeRequest { Amount (amount): Money? }` (remarks: only `amount` supported; allowed 4–29 days after authorization; ≥30 days → new authorization required) | `PaymentAuthorization` → new `Id`, `Status`, `ExpirationTime`, `CreateTime` | A: `ApiException<ReauthorizePaymentError>`; `TryGetError(out Error)` [400,401,403,404,422], `TryGetNoContent` [500] | `map/operations/Payments.md#ReauthorizePayment`; `Requests/Payments/ReauthorizePaymentRequest.cs`; `Models/ReauthorizeRequest.cs`; remarks `Api/Payments.cs` ReauthorizePayment |
| `Payments.CaptureAuthorizedPayment` | `CaptureAuthorizedPaymentRequest { AuthorizationId req, PayPalRequestId: string? (kept 45 d), Prefer = "return=minimal" → set "return=representation", Body: CaptureRequest? }` | `CaptureRequest { Amount (amount): Money?, FinalCapture (final_capture): bool? = false }` · `Money { CurrencyCode (currency_code) req, Value (value) req }` | `CapturedPayment` → `Id`, `Status: CaptureStatus?`, `Amount`, `SellerReceivableBreakdown { GrossAmount (req), PaypalFee, NetAmount }`, `CreateTime` | A: `ApiException<CaptureAuthorizedPaymentError>`; `TryGetError(out Error)` [400,401,403,404,409,422], `TryGetNoContent(out RawError)` [500] | `map/operations/Payments.md#CaptureAuthorizedPayment`; `Requests/Payments/CaptureAuthorizedPaymentRequest.cs`; `Models/CaptureRequest.cs`, `CapturedPayment.cs`, `SellerReceivableBreakdown.cs`, `Money.cs` |
| `Payments.GetCapturedPayment` | `GetCapturedPaymentRequest { CaptureId req }` | — | `CapturedPayment` (as above) | A: `ApiException<GetCapturedPaymentError>`; `TryGetError` [401,403,404], `TryGetNoContent` [500] | `map/operations/Payments.md#GetCapturedPayment`; `Requests/Payments/GetCapturedPaymentRequest.cs` |
| `Payments.VoidPayment` | `VoidPaymentRequest { AuthorizationId req, PayPalRequestId: string? (kept 45 d), Prefer }` — no body | — | `PaymentAuthorization` → `Status` (expect `VOIDED`) | A: `ApiException<VoidPaymentError>`; `TryGetError(out Error)` [401,403,404,409,422], `TryGetNoContent` [500] | `map/operations/Payments.md#VoidPayment`; `Requests/Payments/VoidPaymentRequest.cs` |
| `Payments.RefundCapturedPayment` | `RefundCapturedPaymentRequest { CaptureId req, PayPalRequestId: string? (kept 45 d), Prefer → "return=representation", Body: RefundRequest? }` | `RefundRequest { Amount (amount): Money? (null = full refund, per remarks), CustomId (custom_id), NoteToPayer (note_to_payer) }` | `Refund` → `Id`, `Status: RefundStatus?`, `Amount`, `CustomId`, `CreateTime`, `SellerPayableBreakdown.TotalRefundedAmount` | A: `ApiException<RefundCapturedPaymentError>`; `TryGetError(out Error)` [400,401,403,404,409,422], `TryGetNoContent` [500] | `map/operations/Payments.md#RefundCapturedPayment`; `Requests/Payments/RefundCapturedPaymentRequest.cs`; `Models/RefundRequest.cs`, `Refund.cs`, `SellerPayableBreakdown.cs` |
| `Vault.CreatePaymentToken` | `CreatePaymentTokenRequest { Body: PaymentTokenRequest req, PayPalRequestId: string? (1..108, kept 3 h) }` | `PaymentTokenRequest { Customer (customer): Customer?, PaymentSource (payment_source): PaymentTokenRequestPaymentSource req }` · `PaymentTokenRequestPaymentSource { Card (card): PaymentTokenRequestCard? }` · `PaymentTokenRequestCard { Name, Number, Expiry, SecurityCode (security_code), BillingAddress: Address? }` · `Customer { Id (id) }` | `PaymentTokenResponse` → `Id`, `Customer.Id (CustomerResponse)`, `PaymentSource.Card (CardPaymentTokenEntity: LastDigits, Brand, Expiry)` | A: `ApiException<CreatePaymentTokenError>`; `TryGetError(out Error)` [400,403,404,422,500] | `map/operations/Vault.md#CreatePaymentToken`; `Requests/Vault/CreatePaymentTokenRequest.cs`; `Models/PaymentTokenRequest.cs`, `PaymentTokenRequestPaymentSource.cs`, `PaymentTokenRequestCard.cs`, `Customer.cs`, `PaymentTokenResponse.cs`, `PaymentTokenResponsePaymentSource.cs`, `CardPaymentTokenEntity.cs` |
| `Vault.DeletePaymentToken` | `DeletePaymentTokenRequest { Id: string req (^[0-9a-zA-Z_-]+$, ≤36) }` | — | `void` | A: `ApiException<DeletePaymentTokenError>`; `TryGetError(out Error)` [400,403,500] | `map/operations/Vault.md#DeletePaymentToken`; `Requests/Vault/DeletePaymentTokenRequest.cs` |
| `TransactionSearch.SearchTransactions` | `SearchTransactionsRequest { StartDate: string req (RFC3339, seconds required), EndDate: string req (max range 31 days), TransactionCurrency?, Fields = "transaction_info", BalanceAffectingRecordsOnly = "Y", PageSize: int = 100 (1..500), Page: int = 1 }` | — | `SearchResponse` → `TransactionDetails[].TransactionInfo` (`TransactionInformation`: `TransactionId`, `PaypalReferenceId`, `TransactionEventCode`, `TransactionInitiationDate`, `TransactionAmount`, `FeeAmount`, `TransactionStatus`, `CustomField`, `InvoiceId`), `Page`, `TotalPages`, `TotalItems` | **B**: `ApiException<RawError>` | `map/operations/TransactionSearch.md#SearchTransactions`; `Requests/TransactionSearch/SearchTransactionsRequest.cs`; `Models/SearchResponse.cs`, `TransactionDetails.cs`, `TransactionInformation.cs`; remarks `Api/TransactionSearch.cs` (≤3 h lag) |

Shared error payload `Error` (`Models/Error.cs`): `Name (name) req`, `Message (message) req`, `DebugId (debug_id) req`, `Details (details): IReadOnlyList<ErrorDetails>?` → `ErrorDetails { Issue (issue) req, Description, Field }` (`Models/ErrorDetails.cs`).

Pagination: no operation in scope carries a **Pagination** bullet → no `Pageable`; `SearchTransactions` is walked manually by `Page` until `SearchResponse.TotalPages` (map defaults table, `sdk-map.md`).

### Enum values used

| Enum (`PayPalServerSdk.Models.Enums`) | Members used | Source |
| --- | --- | --- |
| `CheckoutPaymentIntent` | `Authorize` ("AUTHORIZE") | `Models/Enums/CheckoutPaymentIntent.cs` |
| `OrderStatus` | `Created`, `Saved`, `Approved`, `Voided`, `Completed`, `PayerActionRequired` | `Models/Enums/OrderStatus.cs` |
| `AuthorizationStatus` | `Created`, `Captured`, `Denied`, `PartiallyCaptured`, `Voided`, `Pending` | `Models/Enums/AuthorizationStatus.cs` |
| `CaptureStatus` | `Completed`, `Declined`, `PartiallyRefunded`, `Pending`, `Refunded`, `Failed` | `Models/Enums/CaptureStatus.cs` |
| `RefundStatus` | `Cancelled`, `Failed`, `Pending`, `Completed` | `Models/Enums/RefundStatus.cs` |
| `CardBrand` | read only (`.Value` string) | `Models/Enums/CardBrand.cs` |

Enums are `OpenStringEnum<T>` records (compare with `==` against static members; unknown wire values arrive intact).

### Client construction / auth / servers

| Fact | Value | Source |
| --- | --- | --- |
| Constructor | `new PayPalServerSdk.PayPalServerSdkClient(HttpClient httpClient, PayPalServerSdkClientOptions options)` — only constructor | `sdk-map.md` Getting a client |
| Credentials | `options.Oauth2 = new OAuth2ClientCredentials { ClientId (req), ClientSecret (req) }` | `Core/Authentication/OAuth2/ClientCredentials/OAuth2ClientCredentials.cs` |
| Environments | only `ServerEnvironment.Sandbox` (default) → `https://api-m.sandbox.paypal.com` | `Servers/ServerEnvironment.cs` |
| Base URL override | `options.Server.Default.Sandbox.BaseUrl` (string); the token URL is resolved through the same `server.Default("/v1/oauth2/token")`, so the override covers the token request too | `Servers/DefaultOptions.cs`, `AuthSchemes.cs` |
| Options | `Retry: RetryOptions` (all members required; start from `RetryOptions.Default()`), `Logging: LoggingOptions`, `Hooks` | `PayPalServerSdkClientOptions.cs`, `Core/Configuration/RetryOptions.cs` |
| Real idempotency key | `PayPalRequestId` member (→ `PayPal-Request-Id` header) on CreateOrder / AuthorizeOrder / ReauthorizePayment / CaptureAuthorizedPayment / VoidPayment / RefundCapturedPayment / CreatePaymentToken request records. The injected `Idempotency-Key: Guid.NewGuid()` header is NOT a key. | the request records above; `Api/Payments.cs`, `Api/Orders.cs` |

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| `paymentMethodId` in a pay request must be a saved card the caller saved and has not deleted; the `vault_id` sent must be the token id `CreatePaymentToken` returned for it | `CreateOrder` (card.vault_id) ← `CreatePaymentToken` (stored as `SavedPaymentMethod.VaultTokenId`) | `OrderPaymentService.PayAsync` loads `SavedPaymentMethod` by id **and** buyer id, not removed, before `CreateOrder` |
| authorization id captured/voided/reauthorized must be the one `AuthorizeOrder` (or `ReauthorizePayment`) returned for this order | `CaptureAuthorizedPayment` / `VoidPayment` / `ReauthorizePayment` ← `AuthorizeOrder` | `OrderPaymentService.FulfilAsync` / `CancelAsync` read `OrderPayment.AuthorizationId`; the caller never supplies an id |
| capture id refunded must be the one `CaptureAuthorizedPayment` returned for this order; refund amount ≤ captured − already refunded/pending | `RefundCapturedPayment` ← `CaptureAuthorizedPayment` | `OrderPaymentService.RefundAsync` reads `OrderPayment.CaptureId` and reserves the amount on the order (concurrency token) before the call |
| `paymentMethodId` in DELETE must be one the caller saved | `DeletePaymentToken` ← `CreatePaymentToken` | `SavedPaymentMethodService.DeleteAsync` loads by id **and** buyer id |
| reconciliation PayPal ids are matched only against ids stored from `AuthorizeOrder`/`CaptureAuthorizedPayment`/`RefundCapturedPayment` | `SearchTransactions` ← those writes | `ReconciliationService` builds the eShop side from `OrderPayment` rows |

## 3. Trap notes

| Step | Hazard → consequence | Skill |
| --- | --- | --- |
| 1 | HttpClient/handler lifetime and how the SDK client is registered in DI — a wrong lifetime exhausts sockets or pins stale DNS | MUST load `paypal:dotnet-client-initialization` |
| 1 | Where credentials go and what an unset credential does at call time — a blank secret surfaces as a 401 in production, not at startup | MUST load `paypal:dotnet-authentication` |
| 1,9 | What `RetryOptions.Timeout` actually bounds and which verbs retry — the 30 s caller budget can be blown by retried attempts | MUST load `paypal:dotnet-configuration-resilience` |
| 1 | `LogRequestBody`, the `PAYPALSERVERSDKCLIENT_LOG` env var and unset `LoggerFactory` — card numbers in request bodies could reach logs | MUST load `paypal:dotnet-configuration-resilience` |
| 4–8 | Building request records/bodies, `Prefer` default changing what the response carries | MUST load `paypal:dotnet-calling-endpoints` |
| 4–9 | Open enums, optional/required members, unknown fields on responses | MUST load `paypal:dotnet-models` |
| 4–9 | Which exception types reach a catch (typed vs raw vs transport vs deserialization) — a ladder that misses one leaks a 500 or mislabels an unknown outcome | MUST load `paypal:dotnet-error-handling` |
| 10 | Which seam to fake so tests run offline without depending on SDK internals | MUST load `paypal:dotnet-testing` |

## 4. REQUIRED READING (load all before implementation starts; this sheet deliberately does not carry their contents)

- `paypal:dotnet-client-initialization` — step 1 (client + DI)
- `paypal:dotnet-authentication` — step 1 (OAuth2 credentials, fail-fast)
- `paypal:dotnet-calling-endpoints` — steps 4–9
- `paypal:dotnet-models` — steps 4–9
- `paypal:dotnet-error-handling` — steps 4–9 (error boundary)
- `paypal:dotnet-configuration-resilience` — steps 1, 5–9 (timeouts, retries, logging, base URL)
- `paypal:dotnet-testing` — step 10

Hazard (verbatim): a body that does not match its declared type — a drifted or malformed **2xx** response (a missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch ladder that handles only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | `PayPalOptions` bound from `PayPal:`; `ValidateOnStart` rejects blank `ClientId`, blank `ClientSecret`, a `Currency` that is not 3 letters, an `Environment` other than `sandbox` unless `BaseUrl` is set (the SDK declares only Sandbox), and a `BaseUrl` that is not an absolute http(s) URI. Host refuses to start. |
| 2 | Secret sourcing & rotation | Dev: .NET user-secrets (`PayPal:ClientId`, `PayPal:ClientSecret`, …) loaded from the `PAYPAL_*` env vars; any host also accepts the `PAYPAL_*` env vars directly (mapped to `PayPal:*` when the key is not otherwise set). Options are read once at registration into the singleton SDK client → a rotated secret takes effect on process restart (documented; no hot rotation). |
| 3 | Total timeout budget | Each eShop HTTP request gets ONE PayPal budget: a `CancellationToken` deadline of 25 s (`PayPalOptions.RequestBudgetSeconds`, default 25, max 28) shared by every SDK call in that request; per-attempt `Timeout` 10 s. Exceeding it → 504 `"PayPal did not respond within …"`. Total stays < 30 s. Enforced in `PayPalPaymentGateway` via `PayPalCallBudget`. |
| 4 | Write-retry ownership | `HttpMethodsToRetry` = GET only (PUT removed — none in scope anyway). All POST/DELETE writes are never resent by the SDK; the app re-issues a write only under the same stored `PayPal-Request-Id`. |
| 5 | Idempotency & ambiguous writes | CreateOrder/AuthorizeOrder/Capture/Reauthorize/Void/Refund/CreatePaymentToken each send `PayPalRequestId` = a GUID generated once at claim time and stored on the claim row (suffixed per step), so a re-issue after an unknown outcome is deduplicated by PayPal (6 h orders, 45 d payments, 3 h vault). Refunds: caller-supplied `idempotencyKey` → claim key `refund:{orderId}:{key}`. `DeletePaymentToken` has no key: it is naturally idempotent (row soft-deleted first; re-DELETE retries). |
| 6 | Observability | App logs (Information) one line per PayPal operation: op name, eShop order id, PayPal ids, status, elapsed ms; Warning on provider errors with PayPal `debug_id` (from `Error.DebugId`) and `Error.Name`/issue; Error on unknown outcomes. SDK `LoggerFactory` = the app's, `LogRequestBody`/`LogResponseBody` off. |
| 7 | Sensitive data | Request models carry card `number`, `security_code`, `expiry` (`CardRequest`, `PaymentTokenRequestCard`). SDK `LogRequestBody` stays false, `LoggerFactory` explicitly assigned, our DTO `ToString` never prints card fields, exceptions/messages never echo request bodies. DB stores only vault token id, brand, last 4, expiry. |
| 8 | Environment selection | One server group (`Default`). `PayPal:Environment=sandbox` → `ServerEnvironment.Sandbox` (`https://api-m.sandbox.paypal.com`). Any other environment requires `PayPal:BaseUrl`, which is assigned verbatim to `options.Server.Default.Sandbox.BaseUrl` (also governs the token URL). Tests point `BaseUrl` at a fake handler — no test traffic leaves the process. |
| 9 | Duplicate prevention | See DUPLICATE CLAIMS. Claim table `PaymentOperationClaims`, string primary key. |
| 10 | Partial results | Reconciliation walks every page of every ≤31-day window; a hard cap (`MaxPagesPerWindow` = 50 × 500 rows) sets `ReconciliationReport.IsComplete=false` + `TruncatedWindows`. |
| 11 | Unknown outcomes | See UNKNOWN OUTCOMES: claim kept in state `Unknown`, settled by a re-read in the catch (budget permitting) and again by the next request for the same claim before any re-issue (which reuses the stored `PayPal-Request-Id`). |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Authorize (CreateOrder + AuthorizeOrder) | `PaymentOperationClaims` row, PK `authorize:{orderId}` (CatalogContext) | primary-key violation on insert | `EfPaymentClaimStore.TryClaimAsync` `catch (DbUpdateException)` | TBD |
| Capture (+ Reauthorize) | PK `capture:{orderId}` | primary-key violation | same | TBD |
| Void | PK `void:{orderId}` | primary-key violation | same | TBD |
| Refund | PK `refund:{orderId}:{idempotencyKey}` + amount reservation on `Order` (concurrency token) | PK violation (same key); `DbUpdateConcurrencyException` (distinct concurrent refunds) | `TryClaimAsync`; `OrderPaymentService.RefundAsync` | TBD |
| Save card (CreatePaymentToken) | PK `vault:{buyerId}:{idempotencyKey}` when the caller supplies a key | primary-key violation | `TryClaimAsync` | TBD |

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `SearchTransactions` per ≤31-day window | `MaxPagesPerWindow` (50) × `PageSize` 500 | `ReconciliationReport.IsComplete` = false and `TruncatedWindows` lists the windows | TBD |

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| CreateOrder | re-issue `CreateOrder` with the same stored `PayPalRequestId` (PayPal returns the original order for 6 h) | stored `PayPalRequestId` | TBD | TBD |
| AuthorizeOrder | `Orders.GetOrder` | stored PayPal order id → `payments.authorizations` | TBD | TBD |
| CaptureAuthorizedPayment | `Orders.GetOrder` | PayPal order id → `payments.captures` (non-declined) | TBD | TBD |
| VoidPayment | `Payments.GetAuthorizedPayment` | authorization id → status `VOIDED` | TBD | TBD |
| RefundCapturedPayment | `Orders.GetOrder` | PayPal order id → `payments.refunds[].custom_id` == claim key hash | TBD | TBD |
| CreatePaymentToken | re-issue with same `PayPalRequestId` (kept 3 h) when the caller retries with the same key; without a key the outcome is reported unknown (a stray vault token holds no money) | stored `PayPalRequestId` | TBD | TBD |
| ReauthorizePayment | `Payments.GetAuthorizedPayment` on the original id fails to reveal the new id → re-issue with the same `PayPalRequestId` (kept 45 d) | stored `PayPalRequestId` | TBD | TBD |

## 6. Assumptions & Blockers

- UNVERIFIED: `CreatePaymentToken` with `payment_source.card` (raw card) is accepted by the sandbox account without a setup token/3-DS step. Defensive: if PayPal answers with a payer-action status or link, the save fails with an explicit "requires shopper approval in a browser" error (task: STOP and report).
- UNVERIFIED: CreateOrder with a card `payment_source` and intent AUTHORIZE may return `APPROVED` (→ call AuthorizeOrder) or already `COMPLETED` with an authorization; both are handled. `PAYER_ACTION_REQUIRED` → 3-DS challenge → reported, not round-tripped.
- UNVERIFIED: `Orders.GetOrder` lists `payments.refunds` with `custom_id`; if absent the settle falls back to re-issuing the refund under the same `PayPalRequestId`.
- Assumption: eShop `BuyerId` = JWT `ClaimTypes.Name` (existing convention, `IdentityTokenClaimService`).
- Assumption: refunds are allowed for the owning shopper and for administrators (Flow 1 says an operator refunds; the rules list refunds as shopper-scoped).
- Assumption: staleness = honor period (3 days after the authorization's `create_time`, from `ReauthorizePayment` remarks) → reauthorize before capture; past `expiration_time`/29 days, or reauthorization refused → 409 with an operator-actionable message; the order returns to `AwaitingPayment`.
- Blockers: none.
