# PayPal .NET SDK — Integration Contract Sheet & Plan

Scope: `src/PublicApi` (ASP.NET Core 8) in eShopOnWeb. Sandbox. NuGet `AsadAli.Checkout.Sdk`
(install version-less), root namespace `PayPalServerSdk`. SDK map release `v1.0.1` (commit `9653d18`).
Config keys: `PayPal:ClientId`, `PayPal:ClientSecret`, `PayPal:Environment`, `PayPal:Currency`,
optional `PayPal:BaseUrl`.

This SDK exposes only 5 controllers: `Orders`, `Payments`, `Vault`, `TransactionSearch`,
`Subscriptions`. Only the first four are in scope here.

---

## 1. Scope & sequence

| # | Feature | Operation(s) | Controller |
|---|---|---|---|
| 1 | Client construction + DI + BaseUrl override reaching OAuth | `AddPayPalServerSdkClient` / `new PayPalServerSdkClient` | (root) |
| 2 | Direct card AUTHORIZE (single-step, raw card) | `CreateOrder` (`Intent=Authorize`, `payment_source.card`) | `Orders` |
| 3 | Vault a card from raw fields | `CreatePaymentToken` | `Vault` |
| 4 | Delete vaulted card | `DeletePaymentToken` | `Vault` |
| 5 | Pay with vaulted card | `CreateOrder` (`payment_source.card.vault_id`) | `Orders` |
| 6 | Capture authorization at fulfilment | `CaptureAuthorizedPayment` | `Payments` |
| 7 | Reauthorize a stale authorization | `ReauthorizePayment` | `Payments` |
| 8 | Void an authorization | `VoidPayment` | `Payments` |
| 9 | Refund a capture (full/partial) | `RefundCapturedPayment` | `Payments` |
| 10 | Fetch status by id | `GetOrder` / `GetAuthorizedPayment` / `GetCapturedPayment` / `GetRefund` / `GetPaymentToken` | `Orders`/`Payments`/`Vault` |
| 11 | Transaction search / reconciliation | `SearchTransactions` | `TransactionSearch` |
| 12 | Error boundary | (all) | — |
| 13 | Idempotency (`PayPal-Request-Id`) | write ops with a `payPalRequestId` param | — |
| 14 | Amount/currency formatting | `Money` | — |

Recommended build order: 1 (client/DI) → 12/13/14 (boundary + shared helpers) → 2 → 6 → 8/7 → 9 → 3/5/4 → 10 → 11.

---

## 2. CONTRACT SHEET

> **Signatures are generated code, verbatim — every parameter name is the literal
> C# identifier. The cancellation-token parameter really is named `ct`: in named
> arguments write `ct:`, never `cancellationToken:`.**
>
> **Every SDK type is written fully-qualified with the namespace the map gives it** — take
> each one from that type's own map row, never from where a neighbouring type sits. A members
> table names the namespace outright; otherwise the row's source path implies it
> (`Core/Configuration/…` ⇒ `…Core.Configuration`; a file at the repo root ⇒ the root
> namespace). Enums, unions, auth, server and client-config types are spread across different
> child namespaces, and two types configured side by side in the same options object routinely
> live in different ones. Dropping a type to the root or to `.Models` makes the implementer
> guess the wrong `using`, and the build breaks.

### 2a. Namespaces (add a `using` per type-kind)

| Type / kind | Namespace | Cite |
|---|---|---|
| `PayPalServerSdkClient`, `PayPalServerSdkClientOptions`, `ServerOptions`, `AddPayPalServerSdkClient` | `PayPalServerSdk` | sdk-map · source `PayPalServerSdkClientOptions.cs`, `ServiceCollectionExtensions.cs` |
| Controllers (`Orders`, `Payments`, `Vault`, `TransactionSearch`) | `PayPalServerSdk.Api` | sdk-map namespaces |
| All request/response records (`OrderRequest`, `Order`, `CardRequest`, `CapturedPayment`, `Refund`, `PaymentTokenRequest`, `PaymentTokenResponse`, `Money`, `Error`, `Error1`, `DefaultError`, …) | `PayPalServerSdk.Models` | records-1/records-2 header |
| Enums (`CheckoutPaymentIntent`, `OrderStatus`, `AuthorizationStatus`, `CaptureStatus`, `RefundStatus`, `CardBrand`, …) | `PayPalServerSdk.Models.Enums` | enums.md |
| Per-operation error classes (`CreateOrderError`, `AuthorizeOrderError`, `CaptureAuthorizedPaymentError`, `RefundCapturedPaymentError`, `CreatePaymentTokenError`, …) | `PayPalServerSdk.Errors` | sdk-map namespaces |
| `SdkException<TError>` | `PayPalServerSdk.Core.Exceptions` | source `Core/Exceptions/SdkException.cs` |
| `RawError` | `PayPalServerSdk.Core.ErrorResponse` | sdk-map error-core |
| `OAuth2ClientCredentials` | `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials` | source `OAuth2ClientCredentials.cs` |
| `IOAuth2TokenStrategy<>` | `PayPalServerSdk.Core.Authentication.OAuth2` | source `PayPalServerSdkClientOptions.cs` |
| `RetryOptions`, `LoggingOptions` | `PayPalServerSdk.Core.Configuration` | sdk-map client-options |
| `ServerEnvironment`, `DefaultOptions` (+ nested `DefaultOptions.SandboxOptions`) | `PayPalServerSdk.Servers` | source `Servers/ServerEnvironment.cs`, `Servers/DefaultOptions.cs` |

`requestOptions` is optional on every operation (default `null`) — pass `requestOptions: null`; no extra `using` needed for it.

### 2b. Operations

Legend for request-model fields: `CSharpName (wire_name): Type` — `!req` = C# `required`, `?` = nullable/optional.

| Op (controller) | Signature (params in order, verbatim) | Request model + fields used | Response envelope → inner fields read | Error case + accessors + payload | Page |
|---|---|---|---|---|---|
| **CreateOrder** (`client.Orders`) — feat 2 & 5 | `CreateOrder(string? payPalMockResponse, string? payPalRequestId, string? payPalPartnerAttributionId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderRequest body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — first 5 nullable params have **no default → pass `null`** | `OrderRequest`: `Intent (intent): CheckoutPaymentIntent !req`, `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnitRequest> !req`, `PaymentSource (payment_source): PaymentSource?`. → `PurchaseUnitRequest`: `Amount (amount): AmountWithBreakdown !req` (+ `InvoiceId (invoice_id): string?`, `CustomId (custom_id): string?` for reconciliation). → `AmountWithBreakdown`: `CurrencyCode (currency_code): string !req`, `Value (value): string !req`. → `PaymentSource`: `Card (card): CardRequest?`. → `CardRequest`: `Number (number): string?`, `Expiry (expiry): string?`, `SecurityCode (security_code): string?`, `Name (name): string?`, `BillingAddress (billing_address): Address?`, **`VaultId (vault_id): string?`** (feat 5: set instead of `Number`), `StoredCredential (stored_credential): CardStoredCredential?` (card-on-file). | Returns **`Order`**. Read: `Status (status): OrderStatus?` (STOP signal, see below); `Id (id): string?`; `PurchaseUnits[].Payments (payments): PaymentCollection?` → `.Authorizations: IReadOnlyList<AuthorizationWithAdditionalData>` → `.Id` (the authorization id to capture later); `Links (links)`. | Case **A**: `SdkException<CreateOrderError>`. `TryGetError(out Error)` [400,401,422] · `TryGetRawError(out RawError)` [fallback]. Payload `Error` (records-1). | operations/Orders.md; records-1/2 |
| **AuthorizeOrder** (`client.Orders`) — alt for buyer-approved orders | `AuthorizeOrder(string id, string? payPalMockResponse, string? payPalRequestId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderAuthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — 5 nullable params + `body` **pass `null`** to skip | `OrderAuthorizeRequest`: `PaymentSource (payment_source): OrderAuthorizeRequestPaymentSource?` (only needed if not already set on the order). | Returns **`OrderAuthorizeResponse`**: `Status (status): OrderStatus?`, `PurchaseUnits[].Payments.Authorizations[].Id`, `Id`, `Links`. | Case **A**: `SdkException<AuthorizeOrderError>`. `TryGetError(out Error)` [400,401,403,404,422,500] · `TryGetRawError` [fallback]. | operations/Orders.md |
| **CaptureAuthorizedPayment** (`client.Payments`) — feat 6 | `CaptureAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, CaptureRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — 4 nullable params + `body` **pass `null`** to skip (partial capture only) | `CaptureRequest`: `Amount (amount): Money?` (omit/`null` body ⇒ full capture), `InvoiceId (invoice_id): string?`, `FinalCapture (final_capture): bool? = false`, `NoteToPayer`, `SoftDescriptor`. | Returns **`CapturedPayment`**. Read: `Id`; `Status (status): CaptureStatus?`; `Amount (amount): Money?` (captured amount); `SellerReceivableBreakdown (seller_receivable_breakdown): SellerReceivableBreakdown?` → `GrossAmount (gross_amount): Money !req`, `PaypalFee (paypal_fee): Money?` (fee), `NetAmount (net_amount): Money?` (net to merchant). Breakdown/fee may be **absent for PENDING** captures — null-check. | Case **A**: `SdkException<CaptureAuthorizedPaymentError>`. `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback]. | operations/Payments.md; records-1/2 |
| **ReauthorizePayment** (`client.Payments`) — feat 7 | `ReauthorizePayment(string authorizationId, string? payPalRequestId, string? payPalAuthAssertion, ReauthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — `payPalRequestId`, `payPalAuthAssertion`, `body` **pass `null`** to skip | `ReauthorizeRequest`: `Amount (amount): Money?` (only `amount` is supported). | Returns **`PaymentAuthorization`**: `Status (status): AuthorizationStatus?`, `Id`, `Amount (amount): Money?`, `ExpirationTime (expiration_time): string?` (new 3-day honor-period end). | Case **A**: `SdkException<ReauthorizePaymentError>`. `TryGetError(out Error)` [400,401,403,404,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback]. | operations/Payments.md |
| **VoidPayment** (`client.Payments`) — feat 8 | `VoidPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, string? payPalRequestId, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — **note param order: `payPalAuthAssertion` BEFORE `payPalRequestId`** (differs from other write ops); all three nullable **pass `null`** to skip | (no request body) | Returns **`PaymentAuthorization`**: `Status (status): AuthorizationStatus?` → expect `Voided (VOIDED)`. | Case **A**: `SdkException<VoidPaymentError>`. `TryGetError(out Error)` [401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback]. Fully-captured auth ⇒ error (409/422). | operations/Payments.md |
| **RefundCapturedPayment** (`client.Payments`) — feat 9 | `RefundCapturedPayment(string captureId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, RefundRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — 4 nullable params + `body` **pass `null`** to skip (full refund) | `RefundRequest`: `Amount (amount): Money?` (omit/`null` body ⇒ **full** refund; set `Amount` ⇒ **partial**), `CustomId`, `InvoiceId`, `NoteToPayer`. | Returns **`Refund`**. Read: `Id`; `Status (status): RefundStatus?`; `Amount (amount): Money?`; `SellerPayableBreakdown (seller_payable_breakdown): SellerPayableBreakdown?` → **`TotalRefundedAmount (total_refunded_amount): Money?`** (cumulative-refunded — over-refund guard), `GrossAmount`, `PaypalFee`, `NetAmount`. | Case **A**: `SdkException<RefundCapturedPaymentError>`. `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback]. | operations/Payments.md; records-2 |
| **CreateSetupToken** (`client.Vault`) — feat 3, step 1 of two-step vault | `CreateSetupToken(string? payPalRequestId, SetupTokenRequest body, RequestOptions? requestOptions = null, CancellationToken ct = default)` — `payPalRequestId` **pass `null`** to skip | `SetupTokenRequest`: `PaymentSource (payment_source): SetupTokenRequestPaymentSource !req`, `Customer (customer): Customer?`. → `SetupTokenRequestPaymentSource`: `Card (card): SetupTokenRequestCard?` (+ `Paypal`, `Venmo`, `ApplePay`, `Token`, `Bank` — all optional). → **`SetupTokenRequestCard`**: `Name (name): string?`, `Number (number): string?`, `Expiry (expiry): string?`, `SecurityCode (security_code): string?`, `Brand (brand): CardBrand?`, `BillingAddress (billing_address): Address?`, **`VerificationMethod (verification_method): VaultCardVerificationMethod?`**, **`ExperienceContext (experience_context): VaultCardExperienceContext?`** — none `!req`. | Returns **`SetupTokenResponse`**. Read: **`Id (id): string?`** (the setup-token id → feed into `CreatePaymentToken`), `Status (status): PaymentTokenStatus? = Created`, `Links`. | Case **A**: `SdkException<CreateSetupTokenError>`. **`TryGetError1(out Error1)`** [400,403,422,500] · `TryGetRawError` [fallback]. | operations/Vault.md; records-2 |
| **CreatePaymentToken** (`client.Vault`) — feat 3 | `CreatePaymentToken(string? payPalRequestId, PaymentTokenRequest body, RequestOptions? requestOptions = null, CancellationToken ct = default)` — `payPalRequestId` **pass `null`** to skip | `PaymentTokenRequest`: `PaymentSource (payment_source): PaymentTokenRequestPaymentSource !req`, `Customer (customer): Customer?`. → **`PaymentTokenRequestPaymentSource` has TWO alternatives (neither individually `!req`)**: (a) **`Card (card): PaymentTokenRequestCard?`** — direct raw card; (b) **`Token (token): VaultTokenRequest?`** — reference a setup-token from step 1. → `PaymentTokenRequestCard`: `Name (name): string?`, `Number (number): string?`, `Expiry (expiry): string?`, `SecurityCode (security_code): string?`, `Brand (brand): CardBrand?`, `BillingAddress (billing_address): Address?` — **all optional, none `!req`; and it has NO `attributes`/`verification`/`experience_context` member at all**. → `VaultTokenRequest`: **`Id (id): string !req`** (= the `SetupTokenResponse.Id`), **`Type (type): VaultTokenRequestType !req`** (only member `VaultTokenRequestType.SetupToken` / wire `SETUP_TOKEN`). | Returns **`PaymentTokenResponse`**. Read: `Id (id): string?` (**the vault id** for feat 5/4); `PaymentSource (payment_source): PaymentTokenResponsePaymentSource?` → `Card (card): CardPaymentTokenEntity?` → **safe to display: `Brand (brand): CardBrand?`, `LastDigits (last_digits): string?`, `Expiry (expiry): string?`** — never a PAN (SDK returns no full number on response). | Case **A**: `SdkException<CreatePaymentTokenError>`. **`TryGetError1(out Error1)`** [400,403,404,422,500] · `TryGetRawError` [fallback]. Payload is **`Error1`** (not `Error`). | operations/Vault.md; records-1/2 |
| **GetSetupToken** (`client.Vault`) — feat 3 status | `GetSetupToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | Returns **`SetupTokenResponse`**: `Id`, `Status (status): PaymentTokenStatus?`. | Case **A**: `SdkException<GetSetupTokenError>`. **`TryGetError1(out Error1)`** [403,404,422,500] · `TryGetRawError` [fallback]. | operations/Vault.md |
| **DeletePaymentToken** (`client.Vault`) — feat 4 | `DeletePaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` | (path id only) | Returns **`void` (Task)** — no body. | Case **A**: `SdkException<DeletePaymentTokenError>`. **`TryGetError1(out Error1)`** [400,403,500] · `TryGetRawError` [fallback]. | operations/Vault.md |
| **GetPaymentToken** (`client.Vault`) — feat 10 | `GetPaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | Returns **`PaymentTokenResponse`** (same envelope as CreatePaymentToken). | Case **A**: `SdkException<GetPaymentTokenError>`. **`TryGetError1(out Error1)`** [403,404,422,500] · `TryGetRawError` [fallback]. | operations/Vault.md |
| **GetOrder** (`client.Orders`) — feat 10 | `GetOrder(string id, string? fields, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` — `fields`, `payPalMockResponse`, `payPalAuthAssertion` **pass `null`** to skip | — | Returns **`Order`**: `Status (status): OrderStatus?`. | Case **A**: `SdkException<GetOrderError>`. `TryGetError(out Error)` [401,404] · `TryGetRawError` [fallback]. | operations/Orders.md |
| **GetAuthorizedPayment** (`client.Payments`) — feat 10 | `GetAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` — 2 nullable params **pass `null`** | — | Returns **`PaymentAuthorization`**: `Status (status): AuthorizationStatus?`, `ExpirationTime`. | Case **A**: `SdkException<GetAuthorizedPaymentError>`. `TryGetError(out Error)` [401,403,404] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback]. | operations/Payments.md |
| **GetCapturedPayment** (`client.Payments`) — feat 10 | `GetCapturedPayment(string captureId, string? payPalMockResponse, RequestOptions? requestOptions = null, CancellationToken ct = default)` — `payPalMockResponse` **pass `null`** | — | Returns **`CapturedPayment`**: `Status (status): CaptureStatus?`, `SellerReceivableBreakdown`. | Case **A**: `SdkException<GetCapturedPaymentError>`. `TryGetError(out Error)` [401,403,404] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback]. | operations/Payments.md |
| **GetRefund** (`client.Payments`) — feat 10 | `GetRefund(string refundId, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` — 2 nullable params **pass `null`** | — | Returns **`Refund`**: `Status (status): RefundStatus?`, `SellerPayableBreakdown.TotalRefundedAmount`. | Case **A**: `SdkException<GetRefundError>`. `TryGetError(out Error)` [401,403,404] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback]. | operations/Payments.md |
| **SearchTransactions** (`client.TransactionSearch`) — feat 11 | `SearchTransactions(string startDate, string endDate, string? transactionId, string? transactionType, string? transactionStatus, string? transactionAmount, string? transactionCurrency, string? paymentInstrumentType, string? storeId, string? terminalId, string? fields = "transaction_info", string? balanceAffectingRecordsOnly = "Y", int? pageSize = 100, int? page = 1, RequestOptions? requestOptions = null, CancellationToken ct = default)` — 8 nullable params (`transactionId`…`terminalId`) **pass `null`**; **call with named args** | Query strings: `start_date`←`startDate`, `end_date`←`endDate`, `page`←`page`, `page_size`←`pageSize`. `startDate`/`endDate` are ISO-8601 strings (see assumptions). | Returns **`SearchResponse`**: `TransactionDetails (transaction_details): IReadOnlyList<TransactionDetails>?` → `TransactionInfo (transaction_info): TransactionInformation?` → **`InvoiceId (invoice_id): string?`**, **`CustomField (custom_field): string?`** (reconciliation correlation), `TransactionId`, `TransactionAmount (transaction_amount): Money?`, `FeeAmount (fee_amount): Money?`. Pagination: `Page (page): int?`, `TotalPages (total_pages): int?`, `TotalItems (total_items): int?`. | Case **B** (unique in scope): `SdkException<RawError>`. **No typed accessors** — use `ex.Error.StatusCode`, `ex.Error.ReadAsString()`, `ex.Error.ReadAsJson<DefaultError>()`. | operations/TransactionSearch.md; records-2 |

**Pagination model (feat 11):** no auto-pager anywhere in this SDK (every op's "Pagination: none"). `SearchTransactions` exposes only `page`/`pageSize` (no `perPage` cursor). Loop `page` from 1 to `SearchResponse.TotalPages`, `pageSize` default 100. No `IAsyncEnumerable`/pager helper exists.

**3DS / payer-action STOP signal (feat 2):** after `CreateOrder` with `Intent=Authorize` and a raw card, read `Order.Status`. `OrderStatus.PayerActionRequired` (wire `PAYER_ACTION_REQUIRED`) means the card needs a 3DS challenge / redirect — **STOP** the no-redirect flow here and surface the `rel:payer-action` HATEOAS link from `Order.Links`; do not attempt capture. A clean non-3DS authorize typically returns `Completed`/`Approved` with the authorization under `PurchaseUnits[].Payments.Authorizations[]`.

### 2c. Enum value tables (C# member → wire) — only those in scope

`CheckoutPaymentIntent` (enums.md): `Capture (CAPTURE)`, `Authorize (AUTHORIZE)`.

`OrderStatus`: `Created (CREATED)`, `Saved (SAVED)`, `Approved (APPROVED)`, `Voided (VOIDED)`, `Completed (COMPLETED)`, `PayerActionRequired (PAYER_ACTION_REQUIRED)`.

`AuthorizationStatus`: `Created (CREATED)`, `Captured (CAPTURED)`, `Denied (DENIED)`, `PartiallyCaptured (PARTIALLY_CAPTURED)`, `Voided (VOIDED)`, `Pending (PENDING)`. **There is no `Expired` member** (see feat 7 below).

`AuthorizationIncompleteReason`: `PendingReview (PENDING_REVIEW)`, `DeclinedByRiskFraudFilters (DECLINED_BY_RISK_FRAUD_FILTERS)`.

`CaptureStatus`: `Completed (COMPLETED)`, `Declined (DECLINED)`, `PartiallyRefunded (PARTIALLY_REFUNDED)`, `Pending (PENDING)`, `Refunded (REFUNDED)`, `Failed (FAILED)`.

`RefundStatus`: `Cancelled (CANCELLED)`, `Failed (FAILED)`, `Pending (PENDING)`, `Completed (COMPLETED)`.

`CardType`: `Credit (CREDIT)`, `Debit (DEBIT)`, `Prepaid (PREPAID)`, `Store (STORE)`, `Unknown (UNKNOWN)`.

`CardVerificationStatus`: `Verified (VERIFIED)`, `Failed (FAILED)`.

`VaultStatus`: `Vaulted (VAULTED)`, `Created (CREATED)`, `Approved (APPROVED)`.

`PaymentTokenStatus`: `Created (CREATED)`, `PayerActionRequired (PAYER_ACTION_REQUIRED)`, `Approved (APPROVED)`, `Vaulted (VAULTED)`, `Tokenized (TOKENIZED)`.

`CardBrand` (30 members — display only): `Visa (VISA)`, `Mastercard (MASTERCARD)`, `Amex (AMEX)`, `Discover (DISCOVER)`, `Diners (DINERS)`, `Jcb (JCB)`, `Maestro (MAESTRO)`, `Elo (ELO)`, `Rupay (RUPAY)`, `ChinaUnionPay (CHINA_UNION_PAY)`, … `Unknown (UNKNOWN)`. Full list: enums.md `CardBrand`.

Card-on-file (only if using `CardStoredCredential` for repeat vaulted-card charges): `PaymentInitiator`: `Customer (CUSTOMER)`, `Merchant (MERCHANT)`. `StoredPaymentSourcePaymentType`: `OneTime (ONE_TIME)`, `Recurring (RECURRING)`, `Unscheduled (UNSCHEDULED)`. `StoredPaymentSourceUsageType`: `First (FIRST)`, `Subsequent (SUBSEQUENT)`, `Derived (DERIVED)`.

Enums are `StringEnum<T>` (NOT C# enums): build via the static member (`CheckoutPaymentIntent.Authorize`) or `CheckoutPaymentIntent.FromValue("AUTHORIZE")`; never `CheckoutPaymentIntent.AUTHORIZE`. (See `dotnet-models` — REQUIRED READING.)

### 2d. Client construction, auth, server-node facts (feat 1)

Constructor: `new PayPalServerSdkClient(HttpClient httpClient, PayPalServerSdkClientOptions options)`
(source `PayPalServerSdkClient.cs`).

DI: `services.AddPayPalServerSdkClient(o => { … })` registers the client as a **singleton** and
internally calls `services.AddHttpClient()` + `IHttpClientFactory.CreateClient()` once at
construction; options (incl. credentials/BaseUrl) are read once at registration
(source `ServiceCollectionExtensions.cs`).

`PayPalServerSdkClientOptions` members set here (source `PayPalServerSdkClientOptions.cs`):
- `Environment: ServerEnvironment` — default `ServerEnvironment.Default()` == `ServerEnvironment.Sandbox`. **`Sandbox` is the ONLY member** (source `Servers/ServerEnvironment.cs`); there is no `Production`/`Live`. Map `PayPal:Environment` accordingly (see Assumptions).
- `Oauth2: OAuth2ClientCredentials?` — set to `new OAuth2ClientCredentials { ClientId = cfg["PayPal:ClientId"], ClientSecret = cfg["PayPal:ClientSecret"] }`. **`ClientId` and `ClientSecret` are both `required`**; `Scope` is optional (source `OAuth2ClientCredentials.cs`).
- `Server: ServerOptions` — base-URL override point (see below).

**BaseUrl override reaching the OAuth token endpoint (source-verified):** the override lives at
`options.Server.Default.Sandbox.BaseUrl` (`ServerOptions.Default` : `DefaultOptions`;
`DefaultOptions.Sandbox` : nested `SandboxOptions`; `SandboxOptions.BaseUrl : string`, default
`"https://api-m.sandbox.paypal.com"`). The OAuth client-credentials token request is built with
`server.Default("/v1/oauth2/token")` (source `AuthSchemes.cs`), which resolves through the **same**
`DefaultOptions.Sandbox.BaseUrl` as every API call (source `Servers/DefaultOptions.cs`, `Server.cs`).
**Therefore setting `o.Server.Default.Sandbox.BaseUrl = cfg["PayPal:BaseUrl"]` (verbatim, when the
key is present) automatically applies to the OAuth token endpoint AND all operations — no separate
auth-host wiring exists or is needed.** When `PayPal:BaseUrl` is absent, leave the default. Because
`Sandbox` is the only environment, pointing this BaseUrl at a live host is also the only way to
target non-sandbox (the "environment" name stays `Sandbox`).

Amount helper (feat 14): `Money` (records-1) = `CurrencyCode (currency_code): string !req`,
`Value (value): string !req`. The SDK does **no** numeric formatting — `Value` is passed as the
literal string. Format server-side to the currency's minor units using invariant culture; never
`double.ToString()` with a locale. `CurrencyCode` comes from `PayPal:Currency`. (Per-currency
decimal-place rules are API-side — see Assumptions, UNVERIFIED.)

Idempotency (feat 13): the `PayPal-Request-Id` header is the `string? payPalRequestId` parameter,
present on write ops: `CreateOrder`, `AuthorizeOrder`, `CaptureOrder`, `CaptureAuthorizedPayment`,
`ReauthorizePayment`, `RefundCapturedPayment`, `VoidPayment`, `CreatePaymentToken`,
`CreateSetupToken`. It is **nullable with no default → you must pass it explicitly** (`null` to
skip). The SDK does not force it. Semantics — **same key = server treats as a retry / returns the
original result; a new key = a distinct operation** (e.g. a second, separate partial refund).
`Get*` reads and `DeletePaymentToken` have no `payPalRequestId` parameter. Generate and persist one
id per intended write so a transport retry re-sends the same key. (Whether the live server honours
a replayed key for a given op is API-side — treat the "same key = idempotent replay" contract as
best-effort and reconcile via `Get*`/`SearchTransactions` if in doubt — `UNVERIFIED`.)

### 2e. Error boundary contract (feat 12) — source-verified

- Operations are **throw-based**; there are **no `…Result` no-throw variants** anywhere.
- The only SDK exception type that reaches a catch is **`SdkException<TError>`** (`Core/Exceptions/SdkException.cs`). Source-verified: it declares **only `public required TError Error { get; init; }` — it carries NO `StatusCode`, no HTTP status of its own.**
- **Do typed errors carry the HTTP status? No.** For Case-A ops the payload records (`Error`, `Error1`, `DefaultError`) have fields `Name`, `Message`, `DebugId`, `Details[]`, `Links[]` — **no status field** (records-1). The HTTP status is only available via **`RawError.StatusCode`** (`Core/ErrorResponse/RawError.cs`): reachable directly in Case B (`SdkException<RawError>`), or on a Case-A error through the inherited **`TryGetRawError(out RawError)`** fallback accessor (which yields a `RawError` carrying `StatusCode`). The per-accessor `[400,401,…]` status lists in the sheet tell you which HTTP status maps to which typed shape — they are documentation, not a value on the object.
- Case-A catch shape (typed): `catch (SdkException<CreateOrderError> ex) { if (ex.Error.TryGetError(out var e)) {…} else if (ex.Error.TryGetRawError(out var raw)) { var status = raw.StatusCode; } }`. **Accessor name differs by controller:** Orders/Payments = `TryGetError(out Error)`; Vault = `TryGetError1(out Error1)`; `SearchBalances` = `TryGetDefaultError(out DefaultError)`. Payments ops also have `TryGetNoContent(out RawError)` for 500.
- Case-B catch shape (only `SearchTransactions` in scope): `catch (SdkException<RawError> ex) { var status = ex.Error.StatusCode; var body = ex.Error.ReadAsString(); }` — no typed accessors at all.
- `TryGetRawError` is a fallback, **not** a catch-all that also returns the typed shape — call the typed `TryGet…` first, then `TryGetRawError`.

---

## 3. Trap notes (load the named skill before writing that step)

⚠ **Step 1 (client & DI)** — the `HttpClient`/handler pipeline lifetime and whether the
singleton-registered client is safe to share, plus how `IHttpClientFactory` should own the handler,
are not shown by the constructor. **MUST load `dotnet-client-initialization`** before writing
`AddPayPalServerSdkClient` / `new PayPalServerSdkClient`.

⚠ **Step 1/2 (auth)** — when credentials must be set relative to client construction, how the
client-credentials token is fetched/cached/refreshed, and how a 401 should be handled, are not
visible in the options shape. **MUST load `dotnet-authentication`** before wiring `Oauth2`.

⚠ **Step 1 (base URL / resilience)** — the SDK `Retry.Timeout` does **not** bound a whole call and
is **not** the timeout on the `HttpClient` you register; and whether a failed **write** (POST
capture/refund/authorize) can be re-sent by the retry pipeline is not decided by the option names.
This matters directly to idempotency (feat 13). **MUST load `dotnet-configuration-resilience`**
before tuning `RetryOptions`/timeouts or relying on `PayPal-Request-Id` for safe retries.

⚠ **Steps 2–11 (calls)** — many params are nullable-without-a-default and mis-bind positionally
(`SearchTransactions` especially, and note `VoidPayment`'s reordered `payPalAuthAssertion` /
`payPalRequestId`). **MUST load `dotnet-calling-endpoints`** before the first call; prefer named
arguments.

⚠ **Steps 2–11 (models)** — enums are `StringEnum<T>` not C# enums, `required` members must be set
in the initializer, and unmodeled JSON is dropped on deserialize. **MUST load `dotnet-models`**
before building any request payload or mapping a response.

⚠ **Step 12 (error boundary)** — see REQUIRED READING; the `JsonException` directions below are the
part a signature cannot show. **MUST load `dotnet-error-handling`** before writing the catch ladder.

---

## 4. REQUIRED READING (load BEFORE implementation starts)

This sheet deliberately does **not** carry these skills' contents (defaults, worked examples, the
parts a one-line note can't hold). Load each before its step:

| Skill | Governs |
|---|---|
| `dotnet-client-initialization` | Step 1 — client construction, HttpClient lifetime, DI registration |
| `dotnet-authentication` | Step 1/2 — supplying `Oauth2` credentials, token fetch/refresh, 401 handling |
| `dotnet-configuration-resilience` | Step 1 — retries/timeouts, what `Timeout` bounds, which verbs re-send (idempotency) |
| `dotnet-calling-endpoints` | Steps 2–11 — named-argument calls, required vs optional params, async/cancellation |
| `dotnet-models` | Steps 2–11 — `StringEnum<T>`, `required` members, wire-name mapping, dropped fields |
| `dotnet-error-handling` | Step 12 — which exceptions reach catch, reading status/body safely, the catch-ladder traps |
| `dotnet-testing` | Tests — the `HttpClient` constructor arg is the test seam; match project framework |

**Two mandatory `JsonException` hazard rows for the error boundary** (`System.Text.Json.JsonException`
reaches the boundary from two directions and they need opposite handling):

- a drifted or malformed **2xx** body (a missing `required` member) surfaces as a
  `JsonException` from deserialization, **not** as an `SdkException` — so an
  SDK-exception-only catch ladder lets it escape the integration boundary;
- a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape
  throws `JsonException` *while the error object is being constructed*, so the `JsonException`
  **replaces** the `SdkException` and the HTTP status is destroyed with it — a boundary that
  maps every `JsonException` to a 5xx then reports a deterministic rejection as an outage,
  and a caller that retries 5xx retries something that can never succeed.

**MUST load `dotnet-error-handling`** before writing that boundary.

---

## 5. Assumptions & Blockers

- **`PayPal:Environment`** — the SDK's `ServerEnvironment` has only `Sandbox` (source-verified). Assumption: the app maps any `PayPal:Environment` value to `ServerEnvironment.Sandbox`; a value like `Production`/`Live` has **no SDK member** and must instead be expressed by pointing `PayPal:BaseUrl` (→ `Server.Default.Sandbox.BaseUrl`) at the live host. Confirm whether the app wants that behavior or should reject a non-`Sandbox` environment value. **Gap / Not-Supported:** no built-in Production environment node.
- **`PayPal:BaseUrl`** — assumed to be a full origin (scheme + host, no trailing slash), e.g. `https://api-m.sandbox.paypal.com`, since it is concatenated with paths beginning `/`. Source-verified it reaches both OAuth and API calls via the single `Sandbox.BaseUrl`.
- **Reading nested ids/breakdowns from write responses** — `prefer` defaults to `"return=minimal"` on `CreateOrder`/`CaptureAuthorizedPayment`/`RefundCapturedPayment` etc. Whether `"return=minimal"` omits `purchase_units[].payments` / `seller_receivable_breakdown` / `total_refunded_amount` is an **API representation behavior** the map/source cannot settle (`UNVERIFIED`). Directive: pass `prefer: "return=representation"` when the integration needs those nested fields; still null-check them (breakdown is absent for PENDING captures per the model doc), and fall back to a `Get*` fetch if a needed field is null.
- **Feat 7 staleness / "no `Expired` enum"** — confirmed there is no `AuthorizationStatus.Expired`. Staleness is **not** surfaced as a status enum. Signals available from the contract: `PaymentAuthorization.ExpirationTime` (honor-period end you can compare against `now` pre-emptively), and — once too old to reauthorize (the notes describe a 30-day cap) — `ReauthorizePayment` **throws** `SdkException<ReauthorizePaymentError>` (typically 422). The exact `Error.Details[].Issue` string the live API returns for "cannot reauthorize / expired" is **not** in the map or source (`UNVERIFIED`). Directive: when `ReauthorizePayment` fails, read `ex.Error.TryGetError(out var e)` best-effort (`e.Message` / `e.Details[].Issue`), and on a 4xx treat the authorization as un-reauthorizable → **create a fresh authorization** via a new `CreateOrder` (`Intent=Authorize`) rather than retrying reauthorize.
- **Feat 11 max date range** — the SDK takes `startDate`/`endDate` as plain strings and enforces no window; the maximum per-call range is an **API-side** rule not present in the map or source (`UNVERIFIED` — PayPal documents a cap commonly cited as 31 days, but that cannot be confirmed here). Directive: window requests defensively and, on a 4xx from `SearchTransactions` (Case B — read `ex.Error.StatusCode` / `ex.Error.ReadAsString()`), treat a range-validation failure as "narrow the window" rather than an outage. Date-time string format is likewise API-side (ISO-8601 with offset, e.g. `2024-01-01T00:00:00-0000`) — `UNVERIFIED` against the SDK, which only sees a string.
- **Feat 3 vaulting path (raw card vs setup-token two-step)** — the map/source model **both** shapes as valid requests: `PaymentTokenRequestPaymentSource.Card` (direct raw card) **and** `PaymentTokenRequestPaymentSource.Token` = `VaultTokenRequest { Id = setupTokenId, Type = SetupToken }` (reference a token from `CreateSetupToken`). The contract does **not** declare direct raw-card vaulting unsupported, so the map alone **cannot settle** whether the live `/v3/vault/payment-tokens` accepts an inline PAN or requires a prior setup token — that is API/live-wire behavior (`UNVERIFIED`). **Map-visible evidence favouring the two-step path:** the generated `SetupTokenRequestCard` carries `VerificationMethod` and `ExperienceContext` members, while `PaymentTokenRequestCard` has **no** verification/experience_context/attributes member at all — the raw-PAN-with-verification flow is modeled on the setup-token side. Directive: switch feat 3 to the two-step flow — `CreateSetupToken` (raw card in `SetupTokenRequestPaymentSource.Card`) → read `SetupTokenResponse.Id` → `CreatePaymentToken` with `PaymentSource = new PaymentTokenRequestPaymentSource { Token = new VaultTokenRequest { Id = <setupTokenId>, Type = VaultTokenRequestType.SetupToken } }`. This is the safest modeled path for a raw PAN; the observed opaque 500 on the direct-card call is consistent with (but does not prove) the direct inline-PAN path being unsupported server-side.
- **Feat 11 reconciliation correlation** — `SearchTransactions` has no `invoice_id`/`custom_field` *filter* query param; correlate by reading `TransactionInformation.InvoiceId` / `.CustomField` from results and matching client-side (or filter by `transactionId` when you have it).
- **Feat 14 per-currency decimals** — the exact minor-unit/decimal-place requirement per currency is API-side and not in the SDK (`Money.Value` is just a string) — `UNVERIFIED` against map/source; format defensively and surface any 422 currency/format rejection.
- No other blockers: all in-scope operation signatures, request/response models, envelopes, error cases, and the client/auth/server wiring are resolved from the map (and, for the three noted facts, the SDK source).
