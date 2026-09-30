# PayPal .NET SDK integration plan — eShopOnWeb PublicApi (sandbox: direct card + card vaulting)

SDK: `AsadAli.Checkout.Sdk` (NuGet, install version-less) · root namespace `PayPalServerSdk` ·
client `PayPalServerSdkClient` · map release `v1.0.1` / commit `9653d18`.
Target port: `IPaymentGatewayService` implemented in the Infrastructure layer; the SDK is the sole
reference for talking to PayPal. Config bound from a `PayPal:` section:
`PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENVIRONMENT`, `PAYPAL_CURRENCY`, `PayPal:BaseUrl` (optional override).

Every row below cites the map page (or, where the map could not answer, the SDK source file) it came from.

---

## 1. Scope & sequence

1. **Client + DI + auth registration** (`AddPayPalServerSdkClient`) — OAuth2 client-credentials, environment, optional BaseUrl override. Uses no operation.
2. **Create order + authorize** — `client.Orders.CreateOrder` (intent AUTHORIZE) then `client.Orders.AuthorizeOrder`; card OR vault-id payment source; detect payer-action/3DS and STOP.
3. **Read authorization** — `client.Payments.GetAuthorizedPayment`.
4. **Reauthorize** — `client.Payments.ReauthorizePayment`.
5. **Capture (full)** — `client.Payments.CaptureAuthorizedPayment`.
6. **Void** — `client.Payments.VoidPayment`.
7. **Refund (full/partial)** — `client.Payments.RefundCapturedPayment`.
8. **Vault a card / delete token** — `client.Vault.CreatePaymentToken`, `client.Vault.DeletePaymentToken`.
9. **Transaction search (paginated)** — `client.TransactionSearch.SearchTransactions`.
10. **Idempotency** — `payPalRequestId` param on each write.
11. **Error boundary** — one catch ladder over all calls.

---

## CONTRACT SHEET

> **Signatures are generated code, verbatim — every parameter name is the literal C# identifier.
> The cancellation-token parameter really is named `ct`: in named arguments write `ct:`, never
> `cancellationToken:`.**
>
> **Every SDK type is written fully-qualified with the namespace the map gives it** — take each one
> from that type's own map row, never from where a neighbouring type sits. Enums, unions, auth, server
> and client-config types live in different child namespaces, and two types configured side by side in
> the same options object routinely live in different ones. Dropping a type to the root or to `.Models`
> makes the implementer guess the wrong `using`, and the build breaks.

### Namespaces (`using`)

| Kind | Namespace | Source |
|---|---|---|
| Client, `PayPalServerSdkClientOptions`, `ServerOptions`, `AddPayPalServerSdkClient` | `PayPalServerSdk` | sdk-map.md; `ServiceCollectionExtensions.cs` |
| Controllers (`client.Orders` etc.) | `PayPalServerSdk.Api` | sdk-map.md |
| Records (all request/response/error-payload models incl. `Error`, `Error1`, `DefaultError`, `Money`, `CardRequest`, …) | `PayPalServerSdk.Models` | sdk-map.md |
| Enums (`CheckoutPaymentIntent`, `AuthorizationStatus`, `CaptureStatus`, `RefundStatus`, `OrderStatus`, `CardBrand`) | `PayPalServerSdk.Models.Enums` | sdk-map.md |
| `ServerEnvironment` | `PayPalServerSdk.Servers` | `Servers/ServerEnvironment.cs` |
| `OAuth2ClientCredentials` | `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials` | `Core/Authentication/OAuth2/ClientCredentials/OAuth2ClientCredentials.cs` |
| `SdkException<T>` | `PayPalServerSdk.Core.Exceptions` | `Core/Exceptions/SdkException.cs` |
| `RawError` | `PayPalServerSdk.Core.ErrorResponse` | `Core/ErrorResponse/RawError.cs` |
| `{Operation}Error` typed error classes (`CreateOrderError`, `CaptureAuthorizedPaymentError`, `CreatePaymentTokenError`, …) | `PayPalServerSdk.Errors` | sdk-map.md |

### Step 1 — Client construction, auth, environment, BaseUrl override

**Construction (source `PayPalServerSdkClient.cs`, `ServiceCollectionExtensions.cs`):**

- DI: `services.AddPayPalServerSdkClient(o => { … })` — extension on `IServiceCollection` in namespace `PayPalServerSdk`. It calls `services.AddHttpClient()` and registers `PayPalServerSdkClient` as a **singleton** built from `IHttpClientFactory.CreateClient()`. (`ServiceCollectionExtensions.cs`, verified in source.)
- Manual: `new PayPalServerSdkClient(HttpClient httpClient, PayPalServerSdkClientOptions options)` (sdk-map.md).

**Auth — OAuth2 client credentials (map *Servers & auth*; source `OAuth2ClientCredentials.cs`, `AuthSchemes.cs`):**

```
o.Oauth2 = new PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials
{
    ClientId     = <PAYPAL_CLIENT_ID>,   // required (init-only)
    ClientSecret = <PAYPAL_CLIENT_SECRET> // required (init-only)
    // Scope = null (optional)
};
```
The SDK fetches its own bearer token by POSTing `client_credentials` to `/v1/oauth2/token` with HTTP Basic (`clientId:clientSecret` base64) — you supply only id+secret. (`OAuth2ClientCredentialsStrategy.cs`, verified.)
**Leave `o.Oauth2TokenStrategy` unset (null).** The default token strategy is what routes the token request through the configured base URL (see below); supplying your own strategy bypasses that. (`AuthSchemes.cs` line 16–17: `options.Oauth2TokenStrategy ?? OAuth2ClientCredentialsStrategy.ForBasicAuthRequest(server.Default("/v1/oauth2/token"), …)`.)

**Environment (source `Servers/ServerEnvironment.cs`):**

- `o.Environment = PayPalServerSdk.Servers.ServerEnvironment.Sandbox;`
- **`Sandbox` is the ONLY `ServerEnvironment` member — there is no Production/Live member in this SDK.** Selecting live PayPal is impossible via `Environment` alone and REQUIRES the BaseUrl override below. Default base URL for Sandbox is `https://api-m.sandbox.paypal.com` (`Servers/DefaultOptions.cs`).

**BaseUrl override — resolves the CRITICAL question, and it is NOT a gap.** Set:
```
o.Server.Default.Sandbox.BaseUrl = <PayPal:BaseUrl>;   // when the override is configured; else leave default
```
- `PayPalServerSdkClientOptions.Server` is `ServerOptions` (`PayPalServerSdk`) → `.Default` is `DefaultOptions` (`PayPalServerSdk.Servers`) → `.Sandbox.BaseUrl` is a settable `string`. (`ServerOptions.cs`, `Servers/DefaultOptions.cs`, verified.)
- **This override reaches EVERY call INCLUDING the OAuth2 token/credential request.** Verified end-to-end in source: `PayPalServerSdkClient.cs` builds `new Server(options.Environment, options.Server)` and hands that *same* `server` to `AuthSchemes`, which builds the token URL as `server.Default("/v1/oauth2/token")` → `options.Server.Default.Sandbox.BaseUrl` + path. There is no separate token host. **No GAP.**
- Precondition for the token endpoint to honour it: `Oauth2TokenStrategy` must stay null (see Auth above). If a caller ever injects a custom `Oauth2TokenStrategy`, the token request no longer uses `BaseUrl` — call that out as the one way this guarantee breaks.

### Step 2 — Create order + authorize

| Op | Controller.method (params in order) | Request model + key fields | Response envelope → fields read | Error case + accessors | Page |
|---|---|---|---|---|---|
| Create order | `client.Orders.CreateOrder(string? payPalMockResponse, string? payPalRequestId, string? payPalPartnerAttributionId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderRequest body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — first **5** are nullable/no-default → **pass explicitly** (pass `null` to skip); pass `payPalRequestId` for idempotency | `OrderRequest`: `Intent (intent): CheckoutPaymentIntent !req` → set `CheckoutPaymentIntent.Authorize`; `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnitRequest> !req`; `PaymentSource (payment_source): PaymentSource?`; `Payer?`, `ApplicationContext?` | `Order`: `Id (id): string?` (order id), `Status (status): OrderStatus?` | Case A `SdkException<CreateOrderError>` · `TryGetError(out Error)` [400,401,422] · `TryGetRawError(out RawError)` | operations/Orders.md; records-1 |
| Authorize order | `client.Orders.AuthorizeOrder(string id, string? payPalMockResponse, string? payPalRequestId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderAuthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — 5 nullable/no-default (`payPalMockResponse`…`body`) → **pass explicitly** | `OrderAuthorizeRequest`: `PaymentSource (payment_source): OrderAuthorizeRequestPaymentSource?` (only needed if the card/source was not already supplied at create) | `OrderAuthorizeResponse`: `Id (id)` (order id), `Status (status): OrderStatus?`, `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnit>?`, `Links (links): IReadOnlyList<LinkDescription>?` | Case A `SdkException<AuthorizeOrderError>` · `TryGetError(out Error)` [400,401,403,404,422,500] · `TryGetRawError(out RawError)` | operations/Orders.md; records-1 |

**Where to put the card / vault id — `PaymentSource` (records-2) or `OrderAuthorizeRequestPaymentSource` (records-1), both expose `Card (card): CardRequest?`.** `CardRequest` (records-1) fields:
`Name (name): string?`, `Number (number): string?`, `Expiry (expiry): string?`, `SecurityCode (security_code): string?`, `BillingAddress (billing_address): Address?`, `VaultId (vault_id): string?`, `SingleUseToken?`, `StoredCredential?`, `ExperienceContext?`.
- **(a) Raw card** (sandbox Visa `4111111111111111`): set `Number`, `Expiry` (`"YYYY-MM"`), `SecurityCode`, `Name`, `BillingAddress` (see `Address` below). Leave `VaultId` null.
- **(b) Vaulted card**: set **`VaultId` = the stored vault id ONLY**; leave `Number`/`Expiry`/`SecurityCode` null.
- `Address` (records-2): `AddressLine1?`, `AddressLine2?`, `AdminArea2?` (city), `AdminArea1?` (state), `PostalCode?`, `CountryCode (country_code): string !req`.

**`PurchaseUnitRequest` (records-2):** `Amount (amount): AmountWithBreakdown !req`, `ReferenceId?`, `Payee?`, `Description?`, `CustomId?`, `InvoiceId?`, …
**`AmountWithBreakdown` (records-1):** `CurrencyCode (currency_code): string !req`, `Value (value): string !req`, `Breakdown?`. (`Value` and all money amounts are **strings**, e.g. `"10.00"`.)

**Detect "payer action required / 3DS challenge" and STOP (do not build an approval round-trip):**
- Authoritative signal from the map: `OrderAuthorizeResponse.Status == OrderStatus.PayerActionRequired` (enum member `PayerActionRequired` = wire `PAYER_ACTION_REQUIRED`, enums.md). `Status` is `OrderStatus?` — compare with `==` (StringEnum record equality). If it equals `PayerActionRequired`, report and STOP.
- Also inspect `OrderAuthorizeResponse.Links` for a HATEOAS entry whose `rel` indicates payer action (`LinkDescription`). The exact `rel` string (`"payer-action"`) is a live-wire value the map/source cannot pin — **UNVERIFIED**: code defensively — treat `Status == PayerActionRequired` as the primary trigger, and if present surface the link href best-effort; never assume a fixed `rel` literal.

**Reading the authorization out of the authorize response (envelope drill-down — reads go several levels down):**
`OrderAuthorizeResponse.PurchaseUnits[].Payments.Authorizations[]` →
- `PurchaseUnit.Payments (payments): PaymentCollection?` (records-2)
- `PaymentCollection.Authorizations (authorizations): IReadOnlyList<AuthorizationWithAdditionalData>?` (records-2)
- `AuthorizationWithAdditionalData` (records-1): `Id (id): string?` (**authorization id**), `Status (status): AuthorizationStatus?` (**authorization status**), `ExpirationTime (expiration_time): string?` (**authorization expiry**).
- Order id = `OrderAuthorizeResponse.Id`.

### Step 3 — Read authorization status + expiry

| Op | Signature | Response → fields | Error | Page |
|---|---|---|---|---|
| Get authorization | `client.Payments.GetAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` — 2 nullable/no-default → **pass explicitly** | `PaymentAuthorization` (records-2): `Id (id)`, `Status (status): AuthorizationStatus?`, `ExpirationTime (expiration_time): string?`, `Amount (amount): Money?` | Case A `SdkException<GetAuthorizedPaymentError>` · `TryGetError(out Error)` [401,403,404] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)` | operations/Payments.md; records-2 |

### Step 4 — Reauthorize

| Op | Signature | Request → response | Error | Page |
|---|---|---|---|---|
| Reauthorize | `client.Payments.ReauthorizePayment(string authorizationId, string? payPalRequestId, string? payPalAuthAssertion, ReauthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — `payPalRequestId`, `payPalAuthAssertion`, `body` nullable/no-default → **pass explicitly** | `ReauthorizeRequest` (records-2): `Amount (amount): Money?` (the SDK notes: "supports only the `amount` parameter"). Response `PaymentAuthorization` (records-2): `Id`, `Status`, `ExpirationTime` | Case A `SdkException<ReauthorizePaymentError>` · `TryGetError(out Error)` [400,401,403,404,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)` | operations/Payments.md; records-2 |

- **New authorization id?** Reauthorize returns a fresh `PaymentAuthorization` carrying its own `Id`. Per PayPal semantics a reauthorization is a new authorized payment, so the returned `Id` should be treated as a NEW authorization id — whether it literally differs from the input is only confirmable on the live wire (**UNVERIFIED**). Directive: **always read `Id` from the reauthorize response and use it for the subsequent capture/void; never reuse the original authorization id.**
- **Renewable vs not:** the SDK does not encode this in a type — `AuthorizationStatus` has members `Created, Captured, Denied, PartiallyCaptured, Voided, Pending` (enums.md; **note: no `EXPIRED` member exists in this SDK enum**). Which status is renewable is enforced server-side (op notes: reauthorize applies from days 4–29 after the 3-day honor period; a `Voided`/`Captured`/`Denied` authorization is not renewable and the API returns a 4xx/422 typed error). **UNVERIFIED at the type level** — do not gate on a hardcoded status whitelist; attempt the reauthorize and treat a 422 `SdkException<ReauthorizePaymentError>` with `Error.Details[].Issue` as "not renewable", surfacing that issue.

### Step 5 — Capture (full amount)

| Op | Signature | Request → response | Error | Page |
|---|---|---|---|---|
| Capture authorization | `client.Payments.CaptureAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, CaptureRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — 4 nullable/no-default (`payPalMockResponse`…`body`) → **pass explicitly**; pass `payPalRequestId` for idempotency. **Full capture: pass `body: null`** (or a `CaptureRequest` with `FinalCapture = true`). | `CaptureRequest` (records-1): `Amount?`, `FinalCapture (final_capture): bool? = false`, `InvoiceId?`, `NoteToPayer?`, `SoftDescriptor?`. Response `CapturedPayment` (records-1) | Case A `SdkException<CaptureAuthorizedPaymentError>` · `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)` | operations/Payments.md; records-1 |

`CapturedPayment` fields to read:
- `Id (id): string?` — **capture id**
- `Status (status): CaptureStatus?` — capture status (enum members `Completed, Declined, PartiallyRefunded, Pending, Refunded, Failed`, enums.md)
- `Amount (amount): Money?` — captured amount
- `SellerReceivableBreakdown (seller_receivable_breakdown): SellerReceivableBreakdown?` (records-2):
  - `GrossAmount (gross_amount): Money !req` — captured/gross amount
  - `PaypalFee (paypal_fee): Money?` — **PayPal fee**
  - `NetAmount (net_amount): Money?` — **net proceeds to merchant**
  - (`Money` = `CurrencyCode (currency_code): string !req`, `Value (value): string !req`.)

### Step 6 — Void

| Op | Signature | Response | Error | Page |
|---|---|---|---|---|
| Void authorization | `client.Payments.VoidPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, string? payPalRequestId, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — `payPalMockResponse`, `payPalAuthAssertion`, `payPalRequestId` nullable/no-default → **pass explicitly** (note param order: `payPalRequestId` is 4th here) | `PaymentAuthorization` (records-2): `Status` should read `Voided` | Case A `SdkException<VoidPaymentError>` · `TryGetError(out Error)` [401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)` | operations/Payments.md; records-2 |

### Step 7 — Refund (full / partial)

| Op | Signature | Request → response | Error | Page |
|---|---|---|---|---|
| Refund capture | `client.Payments.RefundCapturedPayment(string captureId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, RefundRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — 4 nullable/no-default (`payPalMockResponse`…`body`) → **pass explicitly**; pass `payPalRequestId`. | `RefundRequest` (records-2): `Amount (amount): Money?`, `CustomId?`, `InvoiceId?`, `NoteToPayer?`. Response `Refund` (records-2) | Case A `SdkException<RefundCapturedPaymentError>` · `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)` | operations/Payments.md; records-2 |

- **Full refund: pass `body: null`** (or a `RefundRequest` with `Amount = null`). Per the op notes ("For a full refund, include an empty request body") and `RefundRequest.Amount` being optional, **omitting `Amount` refunds the remaining captured amount — confirmed at the contract level** (op notes + optional field). The exact remaining-amount arithmetic is computed server-side (live behaviour).
- **Partial refund:** set `Amount = new Money { CurrencyCode = …, Value = "x.xx" }`.
- `Refund` fields: `Id (id): string?` (**refund id**), `Status (status): RefundStatus?` (members `Cancelled, Failed, Pending, Completed`, enums.md), `Amount (amount): Money?`.

### Step 8 — Vault a card / delete token

| Op | Signature | Request → response | Error | Page |
|---|---|---|---|---|
| Create payment token | `client.Vault.CreatePaymentToken(string? payPalRequestId, PaymentTokenRequest body, RequestOptions? requestOptions = null, CancellationToken ct = default)` — `payPalRequestId` nullable/no-default → **pass explicitly** | `PaymentTokenRequest` (records-2): `Customer (customer): Customer?`, `PaymentSource (payment_source): PaymentTokenRequestPaymentSource !req`. Response `PaymentTokenResponse` | Case A `SdkException<CreatePaymentTokenError>` · **`TryGetError1(out Error1)`** [400,403,404,422,500] · `TryGetRawError(out RawError)` | operations/Vault.md; records-2 |
| Delete payment token | `client.Vault.DeletePaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` | none; returns `void` (Task). No `payPalRequestId` param exists (delete is idempotent by id). | Case A `SdkException<DeletePaymentTokenError>` · **`TryGetError1(out Error1)`** [400,403,500] · `TryGetRawError(out RawError)` | operations/Vault.md |

**Vault request build (raw card → token):**
- `PaymentTokenRequestPaymentSource` (records-2): `Card (card): PaymentTokenRequestCard?`, `Token (token): VaultTokenRequest?` → set `Card`.
- `PaymentTokenRequestCard` (records-2): `Name?`, `Number?`, `Expiry?`, `SecurityCode?`, `Brand (brand): CardBrand?`, `BillingAddress (billing_address): Address?`. Set `Number`/`Expiry`/`SecurityCode`/`Name`/`BillingAddress`.

**Vault response → vault id + display-safe summary:**
- `PaymentTokenResponse` (records-2): `Id (id): string?` — **vault id**; `Customer (customer): CustomerResponse?`; `PaymentSource (payment_source): PaymentTokenResponsePaymentSource?`.
- `PaymentTokenResponsePaymentSource.Card (card): CardPaymentTokenEntity?` (records-2 → records-1).
- `CardPaymentTokenEntity` (records-1): `Brand (brand): CardBrand?` (**brand**), `LastDigits (last_digits): string?` (**last 4**), `Expiry (expiry): string?` (**expiry**), `Name?`. These three are display-safe (no PAN/CVV) — map onto the domain summary.

### Step 9 — Transaction search (paginated over [from, to])

| Op | Signature | Response → fields | Error | Page |
|---|---|---|---|---|
| Search transactions | `client.TransactionSearch.SearchTransactions(string startDate, string endDate, string? transactionId, string? transactionType, string? transactionStatus, string? transactionAmount, string? transactionCurrency, string? paymentInstrumentType, string? storeId, string? terminalId, string? fields = "transaction_info", string? balanceAffectingRecordsOnly = "Y", int? pageSize = 100, int? page = 1, RequestOptions? requestOptions = null, CancellationToken ct = default)` — 8 nullable/no-default (`transactionId`…`terminalId`) → **pass explicitly (`null` to skip)**; use **named arguments** (`page:`, `pageSize:`, `ct:`) | `SearchResponse` (records-2): `TransactionDetails (transaction_details): IReadOnlyList<TransactionDetails>?`, `Page (page): int?`, `TotalItems (total_items): int?`, `TotalPages (total_pages): int?` | **Case B** `SdkException<RawError>` (the ONLY Case B op) · read `StatusCode`, `ReadAsString()`, `ReadAsJson<T>()` | operations/TransactionSearch.md; records-2 |

- **Pagination mechanism = page / total_pages (NOT a cursor).** `SearchResponse.Page` and `SearchResponse.TotalPages` are ints. To cover the whole range: start `page: 1`, and loop incrementing `page` while `page <= response.TotalPages`, accumulating `TransactionDetails`. Set `pageSize:` explicitly (SDK default 100).
- **Per-transaction fields** via `SearchResponse.TransactionDetails[].TransactionInfo` (`TransactionDetails.TransactionInfo (transaction_info): TransactionInformation?`, records-2). `TransactionInformation` (records-2): `TransactionId (transaction_id): string?` (**transaction id**), `TransactionAmount (transaction_amount): Money?` (**amount + currency** via `Money.Value` / `Money.CurrencyCode`), `TransactionStatus (transaction_status): string?` (**status — a plain string, not an enum**), `TransactionInitiationDate (transaction_initiation_date): string?` (**initiated time**).
- `startDate`/`endDate` are **strings** — the SDK passes them through verbatim as `start_date`/`end_date` query params; format as ISO-8601 with offset (e.g. `2024-01-01T00:00:00-0000`). PayPal enforces a maximum per-request window (commonly 31 days) and ~3-year lookback; these are live-API constraints the SDK does not encode — **UNVERIFIED**. Directive: **chunk a wide reconciliation `[from,to]` range into bounded sub-windows and page each sub-window fully**, rather than assuming one request spans the whole range.

### Step 10 — Idempotency (PayPal-Request-Id)

Set the `PayPal-Request-Id` header by passing the **`payPalRequestId` string parameter** on each write op — there is no separate header API. Present on: `CreateOrder`, `AuthorizeOrder`, `CaptureAuthorizedPayment`, `ReauthorizePayment`, `RefundCapturedPayment`, `VoidPayment` (4th param), `CreatePaymentToken`. Generate one stable unique id per logical write and persist it so a retry re-sends the same value (PayPal replays the original result). `DeletePaymentToken` has no such param. (operations/Orders.md, operations/Payments.md, operations/Vault.md.)

### Step 11 — Error handling (contract facts)

- **Exception type:** all operations throw `SdkException<TError>` (`PayPalServerSdk.Core.Exceptions`) — throw-based; **no `…Result` no-throw variant exists on any op** (sdk-map.md).
- **Case A (39 ops here):** `TError` is a typed `{Operation}Error` (`PayPalServerSdk.Errors`).
  - Orders + most Payments reads/writes: `TryGetError(out Error)`.
  - **Vault ops: `TryGetError1(out Error1)`** (the differing accessor the brief asked about — `CreatePaymentToken`, `DeletePaymentToken`, and the other Vault ops use `Error1`, not `Error`).
  - Payments ops additionally expose `TryGetNoContent(out RawError)` for [500].
  - Fallback on every typed error: `TryGetRawError(out RawError)`.
- **Case B (1 op): `SearchTransactions`** throws `SdkException<RawError>` — no `TryGet…`; read `ex.Error.StatusCode` / `.ReadAsString()` / `.ReadAsJson<T>()`.
- **Reading Name / Message / DebugId:** the typed payloads `Error`, `Error1`, and `DefaultError` (SearchBalances) all carry `Name (name): string !req`, `Message (message): string !req`, `DebugId (debug_id): string !req` (records-1). Field-level decline reasons live in `Details[]` → `ErrorDetails.Issue (issue): string !req` (+ `Field`, `Description`). Read `DebugId` for PayPal support correlation.
  - Note `Error1` uses `ErrorLinkDescription` for its `Links` (its `rel` is optional — do not require `rel` when reading a Vault error link).
- **Reading HTTP status:** on Case A, the status is implied by which `TryGet…` returns true (accessor→status map in each row); on Case B, `ex.Error.StatusCode`. (For a uniform status read, see `dotnet-error-handling`.)
- **Decline/rejection vs transport error:** a decline/rejection is an `SdkException<…>` with a 4xx/422 status and a typed body — inspect `Error.Name` / `Details[].Issue` (e.g. instrument declined). A transport error is NOT an `SdkException` — it surfaces as `HttpRequestException` / `TaskCanceledException` (timeout). Distinguish by exception type first, then by status. See the mandatory `JsonException` rows in REQUIRED READING — a drifted body can turn either into a `JsonException`.

### Enum value tables (only those used)

| Enum (`PayPalServerSdk.Models.Enums`) | Members (`CSharp (WIRE)`) | Source |
|---|---|---|
| `CheckoutPaymentIntent` | `Capture (CAPTURE)`, `Authorize (AUTHORIZE)` | enums.md |
| `OrderStatus` | `Created (CREATED)`, `Saved (SAVED)`, `Approved (APPROVED)`, `Voided (VOIDED)`, `Completed (COMPLETED)`, `PayerActionRequired (PAYER_ACTION_REQUIRED)` | enums.md |
| `AuthorizationStatus` | `Created (CREATED)`, `Captured (CAPTURED)`, `Denied (DENIED)`, `PartiallyCaptured (PARTIALLY_CAPTURED)`, `Voided (VOIDED)`, `Pending (PENDING)` — **no `EXPIRED` member** | enums.md |
| `CaptureStatus` | `Completed (COMPLETED)`, `Declined (DECLINED)`, `PartiallyRefunded (PARTIALLY_REFUNDED)`, `Pending (PENDING)`, `Refunded (REFUNDED)`, `Failed (FAILED)` | enums.md |
| `RefundStatus` | `Cancelled (CANCELLED)`, `Failed (FAILED)`, `Pending (PENDING)`, `Completed (COMPLETED)` | enums.md |

Enums are `StringEnum<T>`, **not** C# enums: build with `CheckoutPaymentIntent.Authorize` (static member) or `.FromValue("AUTHORIZE")`; compare with `==`. `transaction_status` on `TransactionInformation` is a plain `string`, not an enum.

---

## Trap notes (load the named skill before coding that step)

- ⚠ **Step 1 (client + DI)** — the SDK client's `HttpClient`/handler pipeline must be long-lived and reused; how the DI extension owns it and what lifetime the client wrapper should have is not visible in the signature. **MUST load `dotnet-client-initialization`**.
- ⚠ **Step 1 (auth)** — where credentials must be set relative to client construction, and how to load them from configuration rather than hardcoding, is not shown by the property type. **MUST load `dotnet-authentication`**.
- ⚠ **Step 1 (BaseUrl / resilience)** — the SDK's retry/timeout options do **not** bound a whole call and are **not** the `HttpClient` timeout; `HttpMethodsToRetry` gates only the *status* trigger while a transport failure is retried on every verb (POST included), so a non-idempotent write can execute more than once — which is exactly why Step 10's `payPalRequestId` matters. **MUST load `dotnet-configuration-resilience`** before wiring retries/timeouts/base URL/pagination.
- ⚠ **Steps 2–9 (calls)** — list/search ops must be called with named arguments; several optional params have no C# default and mis-bind positionally. **MUST load `dotnet-calling-endpoints`**.
- ⚠ **Steps 2–9 (models)** — enums are `StringEnum<T>` (factory/static members, not C# enums), money `Value` fields are strings, and unmodeled JSON is dropped on deserialize; building nested request payloads has traps a field list can't show. **MUST load `dotnet-models`**.
- ⚠ **Step 11 (error boundary)** — which exception types actually reach the catch, and why an SDK-exception-only ladder is silently wrong, is not inferable from the signatures. **MUST load `dotnet-error-handling`** (see the two mandatory `JsonException` rows below).
- ⚠ **Tests** — the `HttpClient` constructor argument is the test seam; match the project's existing framework/assertion style. **MUST load `dotnet-testing`**.

---

## REQUIRED READING — load BEFORE implementation starts

These `dotnet-*` companion skills are the usage layer this sheet deliberately does NOT restate (defaults, worked examples, and the parts you must still wire yourself live in them). Load each before writing the step it governs:

| Skill | Governs |
|---|---|
| `dotnet-client-initialization` | Step 1 — client construction, HttpClient ownership/lifetime, DI registration |
| `dotnet-authentication` | Step 1 — supplying OAuth2 client-credentials, where/when to set them |
| `dotnet-configuration-resilience` | Step 1 & 9 — retries/timeouts semantics, base-URL selection, pagination |
| `dotnet-calling-endpoints` | Steps 2–9 — named-argument calling, request/response envelope shapes, async/cancellation |
| `dotnet-models` | Steps 2–9 — building request models, `StringEnum<T>`, required/nullable, wire names |
| `dotnet-error-handling` | Step 11 — which exceptions reach the catch, reading status/body safely, the catch-ladder traps |
| `dotnet-testing` | Tests — faking the `HttpClient` seam, covering error/edge paths |

**Two mandatory `JsonException` hazard rows — `System.Text.Json.JsonException` reaches the boundary from two directions and they need opposite handling:**
- A drifted or malformed **2xx** body (a missing `required` member — e.g. `Money.Value`, `Error.Name`) surfaces as a `JsonException` from deserialization, **not** as an `SdkException` — so an SDK-exception-only catch ladder lets it escape the integration boundary.
- A **non-2xx** body that does not match its operation's generated `{Operation}Error` shape throws `JsonException` *while the error object is being constructed*, so the `JsonException` **replaces** the `SdkException` and the HTTP status is destroyed with it — a boundary that maps every `JsonException` to a 5xx then reports a deterministic rejection as an outage, and a caller that retries 5xx retries something that can never succeed.

**MUST load `dotnet-error-handling`** before writing that boundary.

---

## Assumptions & Blockers

- **Assumption:** "sandbox" means `ServerEnvironment.Sandbox` with the default host `https://api-m.sandbox.paypal.com`, overridden by `PayPal:BaseUrl` only when that config key is set. Since this SDK exposes **only** a `Sandbox` environment member, any non-sandbox host (including live) can be reached ONLY via the `PayPal:BaseUrl` override — flagged so the team confirms intended targets.
- **Assumption:** raw-card processing (PAN/CVV) implies the merchant carries PCI SAQ-D obligations (the `CardRequest` doc-comment states this). Out of SDK scope, but called out for the integration owner.
- **No GAP on the CRITICAL BaseUrl-over-token requirement:** verified in SDK source that `options.Server.Default.Sandbox.BaseUrl` drives both API calls and the `/v1/oauth2/token` request — provided `Oauth2TokenStrategy` is left null.
- **UNVERIFIED (live-wire only), handled by defensive directives above, not open lookups:** the exact payer-action link `rel`; whether reauthorize's returned `Id` literally differs from the input; server-side renewability rules per authorization status; that omitting refund `Amount` yields exactly the remaining amount; PayPal's transaction-search per-request date-window/lookback limits.
- No planning blockers.
