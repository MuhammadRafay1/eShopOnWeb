# PayPal .NET SDK Integration Plan — eShopOnWeb

Target: ASP.NET Core 8 / C#, three-layer (ApplicationCore / Infrastructure / PublicApi).
SDK: `PayPalServerSdk` (NuGet id `AsadAli.Checkout.Sdk`). Grounded against the bundled SDK map
(source commit `9653d18`, tag `v1.0.1`) plus the SDK source clone (same tag) for the facts the map
did not carry (namespaces of a few auth/server types, and the exact header wiring behind the
idempotency parameters — see the citations on each row).

## 1. Scope & sequence

1. **Client & DI setup** — register `PayPalServerSdkClient` with `OAuth2ClientCredentials`,
   `ServerEnvironment.Sandbox`, and a caller-overridable base URL. No operations; wiring only.
2. **Checkout (authorize, no capture)** — `Orders.CreateOrder` (intent=`AUTHORIZE`, raw card
   *or* a saved `vault_id`) → `Orders.AuthorizeOrder` (holds funds).
3. **Fulfilment (capture)** — `Payments.CaptureAuthorizedPayment`, reading the fee/net
   breakdown from the response.
4. **Reauthorization / staleness detection** — `Payments.GetAuthorizedPayment` (status check) →
   `Payments.ReauthorizePayment`; detect "can no longer be renewed" from the typed error.
5. **Void** — `Payments.VoidPayment`.
6. **Refund (full/partial, idempotent, capped)** — `Payments.RefundCapturedPayment`, with our
   own refund ledger enforcing the "not more than captured" rule (the SDK/API does not expose a
   remaining-refundable-amount read).
7. **Vault** — `Vault.CreatePaymentToken` (raw card → token, no setup-token/buyer-confirmation
   step needed for a direct card), `Vault.ListCustomerPaymentTokens`, `Vault.DeletePaymentToken`;
   pay a later order by putting the saved token id into `CardRequest.VaultId` on step 2's
   `CreateOrder` call instead of raw PAN fields.
8. **Reconciliation** — `TransactionSearch.SearchTransactions`, paged manually by `page` up to
   `SearchResponse.TotalPages`; correlate via `invoice_id`, with a `custom_field` fallback (see
   CONTRACT SHEET note — the wire name changes between the order side and the transaction side).
9. **Error boundary** — one exception-translation layer wrapping steps 2–8, built per
   `dotnet-error-handling` (see REQUIRED READING) and the per-operation error rows below.

Out of scope / gaps against the ten numbered capabilities: **none are unsupported** — all ten are
directly exposed by the SDK. Two deserve explicit flags:
- Capability 6 (idempotency): the SDK's own auto-generated `Idempotency-Key` header is **not**
  usable for caller-controlled idempotency — see the dedicated note under "Idempotency" below.
- Capability 5 ("prevent refunding beyond what was captured"): there is no SDK/API read that
  reports "amount already refunded" for a capture — this must be enforced by our own ledger plus
  relying on PayPal's own rejection as backstop (detailed below).

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
> live in different ones.

### 2.1 Namespaces used below (cite once, use throughout)

| Contents | Namespace |
|---|---|
| Client, `PayPalServerSdkClientOptions`, `Server`, `ServerOptions` | `PayPalServerSdk` |
| `ServerEnvironment`, `DefaultOptions` (`options.Server.Default...`) | `PayPalServerSdk.Servers` |
| `OAuth2ClientCredentials` | `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials` |
| `RetryOptions` | `PayPalServerSdk.Core.Configuration` |
| Operation controllers (`client.Orders`, `client.Payments`, `client.Vault`, `client.TransactionSearch`) | `PayPalServerSdk.Api` |
| Request/response records (`OrderRequest`, `Order`, `CardRequest`, …) | `PayPalServerSdk.Models` |
| Enums (`CheckoutPaymentIntent`, `CardBrand`, …) | `PayPalServerSdk.Models.Enums` |
| `{Operation}Error` typed error classes (`AuthorizeOrderError`, …) | `PayPalServerSdk.Errors` |
| `ApiError`, `RawError` | `PayPalServerSdk.Core.ErrorResponse` |
| `SdkException<T>` | `PayPalServerSdk.Core.Exceptions` |

Source: `sdk-map.md` (Servers & auth, Models sections) for all rows except `OAuth2ClientCredentials`'s
namespace, which the map states only as a bare type name — confirmed from
`Core/Authentication/OAuth2/ClientCredentials/OAuth2ClientCredentials.cs` (properties:
`ClientId: string` (required), `ClientSecret: string` (required), `Scope: string?`).

### 2.2 Client construction, auth, base-URL override

```csharp
var options = new PayPalServerSdkClientOptions
{
    Environment = ServerEnvironment.Sandbox,                       // PayPalServerSdk.Servers
    Oauth2 = new OAuth2ClientCredentials                           // PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials
    {
        ClientId = "...",
        ClientSecret = "...",
    },
};
// Base-URL override — see below for why this covers OAuth too:
if (overrideBaseUrl is not null)
    options.Server.Default.Sandbox.BaseUrl = overrideBaseUrl;      // default "https://api-m.sandbox.paypal.com"

var client = new PayPalServerSdkClient(httpClient, options);
```

**Base URL applies verbatim to every call, including the OAuth token request — confirmed from
source, not assumed.** `options.Server` is `ServerOptions { Default: DefaultOptions }`;
`DefaultOptions.Sandbox.BaseUrl` (default `"https://api-m.sandbox.paypal.com"`) is the only knob.
Both the OAuth2 token strategy and every operation controller resolve their request URL through
the same `Server.Default(path)` method:
- `AuthSchemes.cs`: `OAuth2ClientCredentialsStrategy.ForBasicAuthRequest(server.Default("/v1/oauth2/token"), rawClient)`.
- `Api/Orders.cs` (and every other controller): e.g. `_rawClient.Execute(_server.Default("/v2/checkout/orders"), …)`.
- `Server.Default(path)` → `_options.Default.Resolve(_environment, path)` → `new UrlTemplate(Sandbox.BaseUrl, path, [])`.

So setting `options.Server.Default.Sandbox.BaseUrl` before constructing the client redirects the
token endpoint and every API call identically — there is no separate, unconfigurable auth base URL
to worry about. `ServerEnvironment` has only one member (`Sandbox`), so this is also the only
environment branch that exists in this SDK build.

DI form (`services.AddPayPalServerSdkClient(o => { ... })`) sets the same properties on `o`; load
`dotnet-client-initialization` before wiring this into DI (HttpClient lifetime is not obvious from
the signature — see Trap notes).

### 2.3 Idempotency — the exact parameter, and the trap that looks like one but isn't

Every mutating operation in scope takes an explicit `string? payPalRequestId` parameter that maps
1:1 to the `PayPal-Request-Id` header PayPal itself keys idempotency on (confirmed in
`Api/Orders.cs`/`Api/Payments.cs`/`Api/Vault.cs`: `new HeaderParam("PayPal-Request-Id", payPalRequestId)`).
**This is the parameter to populate from our own idempotency-key store on every retry.**

**Trap, confirmed from source, not the map:** every one of these same calls *also* unconditionally
sends a second header, `Idempotency-Key: Guid.NewGuid()`, generated fresh inline in the SDK's own
request-builder call on every invocation — it is **not** derived from `payPalRequestId`, has **no**
caller-facing parameter, and is a **different random value on every retry**. Despite the name, it
gives zero idempotency protection. Do not build any idempotency logic around it, and do not assume
"the SDK already sends an idempotency header" — it sends one, but not a reusable one. The only
caller-controlled idempotency lever is `payPalRequestId`.

| Operation | `payPalRequestId` param position | Present? |
|---|---|---|
| `Orders.CreateOrder` | 2nd param | Yes |
| `Orders.AuthorizeOrder` | 3rd param | Yes |
| `Payments.CaptureAuthorizedPayment` | 3rd param | Yes |
| `Payments.ReauthorizePayment` | 2nd param | Yes |
| `Payments.RefundCapturedPayment` | 3rd param | Yes |
| `Payments.VoidPayment` | 4th param | Yes |
| `Vault.CreatePaymentToken` | 1st param | Yes |
| `Vault.CreateSetupToken` | 1st param | Yes |
| `Vault.DeletePaymentToken` | — | **No parameter at all** — DELETE is naturally idempotent by resource id, so this is not a functional gap, just note it when wiring retries. |

(Full signatures for each are in §2.4; positions above count only the operation's own parameters,
left to right, per the signatures given there.)

### 2.4 Operations

Each row: controller · signature (defaults shown; `?` + no default ⇒ must pass explicitly, `null`
to skip) · request model fields · response fields the integration reads · error case + accessors +
payload type · pagination. Source: `map/operations/Orders.md`, `map/operations/Payments.md`,
`map/operations/Vault.md`, `map/operations/TransactionSearch.md`.

#### CreateOrder — `client.Orders` (`operations/Orders.md`)

```
CreateOrder(string? payPalMockResponse, string? payPalRequestId, string? payPalPartnerAttributionId,
            string? payPalClientMetadataId, string? payPalAuthAssertion, OrderRequest body,
            string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)
```
Request `OrderRequest` (`PayPalServerSdk.Models`): `Intent (intent): CheckoutPaymentIntent !req` (use
`CheckoutPaymentIntent.Authorize` → wire `AUTHORIZE`) · `Payer (payer): Payer?` · `PurchaseUnits
(purchase_units): IReadOnlyList<PurchaseUnitRequest> !req` · `PaymentSource (payment_source):
PaymentSource?` · `ApplicationContext (application_context): OrderApplicationContext?`.

`PurchaseUnitRequest`: `Amount (amount): AmountWithBreakdown !req` · `InvoiceId (invoice_id): string?`
· `CustomId (custom_id): string?` (**set both at order-creation time for reconciliation — see §2.5**) ·
`ReferenceId`, `Payee`, `Description`, `SoftDescriptor`, `Items`, `Shipping`, `PaymentInstruction`,
`SupplementaryData` (all optional, not needed for a minimal charge).

`AmountWithBreakdown`: `CurrencyCode (currency_code): string !req` · `Value (value): string !req` ·
`Breakdown (breakdown): AmountBreakdown?`.

`PaymentSource.Card` → `CardRequest`: `Name (name): string?` · `Number (number): string?` (raw PAN,
e.g. sandbox `4111111111111111`) · `Expiry (expiry): string?` · `SecurityCode (security_code):
string?` (CVC) · `BillingAddress (billing_address): Address?` · `VaultId (vault_id): string?` (set
this **instead of** `Number`/`Expiry`/`SecurityCode` to pay with a saved token — see §2.5) ·
`Attributes`, `SingleUseToken`, `StoredCredential`, `NetworkToken`, `ExperienceContext` (optional).

`Address`: `AddressLine1`, `AddressLine2`, `AdminArea2`, `AdminArea1`, `PostalCode` (all
`string?`) · `CountryCode (country_code): string !req`.

Response `Order`: `Id (id): string?` · `Status (status): OrderStatus?` (**check for
`OrderStatus.PayerActionRequired` — see §2.6**) · `PaymentSource`, `Payer`, `PurchaseUnits`,
`Links (links): IReadOnlyList<LinkDescription>?`, `CreateTime`, `UpdateTime`.

Error: `SdkException<CreateOrderError>` (`PayPalServerSdk.Errors`) — Case A. Accessors:
`TryGetError(out Error)` [400, 401, 422] · `TryGetRawError(out RawError)` [fallback]. `Error`
(`PayPalServerSdk.Models`): `Name`, `Message`, `DebugId` (all `string !req`) · `Details (details):
IReadOnlyList<ErrorDetails>?` · `Links`. `ErrorDetails`: `Issue (issue): string !req` (the
structured reason code) · `Field`, `Value`, `Location`, `Description`, `Links`.

Pagination: none.

#### AuthorizeOrder — `client.Orders` (`operations/Orders.md`)

```
AuthorizeOrder(string id, string? payPalMockResponse, string? payPalRequestId, string? payPalClientMetadataId,
               string? payPalAuthAssertion, OrderAuthorizeRequest? body,
               string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)
```
Notes (from the operation's own doc comment): "the buyer must first approve the order **or** a
valid `payment_source` must be provided in the request" — since step 2 already supplied
`payment_source.card` at `CreateOrder`, pass `body: null` first. If PayPal rejects with a 422
whose `Error.Details[].Issue` indicates a missing/unverifiable payment source, retry once with
`body: new OrderAuthorizeRequest { PaymentSource = new OrderAuthorizeRequestPaymentSource { Card =
<same CardRequest> } }` — `OrderAuthorizeRequestPaymentSource`: `Card (card): CardRequest?` ·
`Token`, `Paypal`, `ApplePay`, `GooglePay`, `Venmo` (optional). **UNVERIFIED** which of these two
shapes the live sandbox actually requires for a direct-card AUTHORIZE order — the map/source state
only that either satisfies the check, not which one a fresh order needs; code both paths.

Response `OrderAuthorizeResponse`: same field shape as `Order` (`Id`, `Status`, `PaymentSource`,
`Payer`, `PurchaseUnits`, `Links`, `CreateTime`, `UpdateTime`) but a distinct C# type. Read
`PurchaseUnits[].Payments.Authorizations[].Id` (via `AuthorizationWithAdditionalData.Id`) as the
`authorization_id` used by every `Payments.*` call below.

Error: `SdkException<AuthorizeOrderError>` — Case A. `TryGetError(out Error)` [400, 401, 403, 404,
422, 500] · `TryGetRawError(out RawError)` [fallback]. Same `Error`/`ErrorDetails` shape as above.

Pagination: none.

#### CaptureAuthorizedPayment — `client.Payments` (`operations/Payments.md`)

```
CaptureAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalRequestId,
                          string? payPalAuthAssertion, CaptureRequest? body,
                          string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)
```
Request `CaptureRequest` (all optional — pass `null` for a full capture of the authorized amount,
or set `Amount` for a partial capture): `Amount (amount): Money?` · `InvoiceId (invoice_id):
string?` · `FinalCapture (final_capture): bool? = false` · `PaymentInstruction`, `NoteToPayer`,
`SoftDescriptor` (optional).

Response `CapturedPayment` — **this is where the fee/net data required by capability 2 lives**:
`Id (id): string?` · `Status (status): CaptureStatus?` · `Amount (amount): Money?` (the captured
amount: `CurrencyCode`, `Value`) · `SellerReceivableBreakdown (seller_receivable_breakdown):
SellerReceivableBreakdown?` → `GrossAmount (gross_amount): Money !req` (captured amount) ·
`PaypalFee (paypal_fee): Money?` (**PayPal's fee**) · `NetAmount (net_amount): Money?` (**net
proceeds to merchant**) · `ReceivableAmount`, `ExchangeRate`, `PlatformFees` (optional) ·
`FinalCapture (final_capture): bool?` · `InvoiceId`, `CustomId`, `Links`, `CreateTime`, `UpdateTime`.

**UNVERIFIED**: whether `prefer: "return=minimal"` (the SDK's own default) still populates
`SellerReceivableBreakdown`, or whether PayPal only returns that breakdown under
`return=representation` — this is server-side HTTP `Prefer`-header behavior the map/source cannot
settle (it is negotiated over the wire, not declared in the SDK). Defensive directive: **always
pass `prefer: "return=representation"` explicitly** on this call (and on every other write call
whose response fields below are read: `AuthorizeOrder`, `ReauthorizePayment`,
`RefundCapturedPayment`, `VoidPayment`, `CreateOrder`) — never rely on the SDK's default.

Error: `SdkException<CaptureAuthorizedPaymentError>` — Case A. `TryGetError(out Error)` [400, 401,
403, 404, 409, 422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)`
[fallback]. **Note the 3-tier shape** — every `Payments` operation (unlike `Orders`/`Vault`) has
this extra `TryGetNoContent` accessor for its 500 case; catch-ladder order must check
`TryGetError` → `TryGetNoContent` → `TryGetRawError`.

Pagination: none.

#### GetAuthorizedPayment — `client.Payments` (`operations/Payments.md`)

```
GetAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion,
                      RequestOptions? requestOptions = null, CancellationToken ct = default)
```
Response `PaymentAuthorization`: `Status (status): AuthorizationStatus?` (`Created`, `Captured`,
`Denied`, `PartiallyCaptured`, `Voided`, `Pending`) · `StatusDetails (status_details):
AuthorizationStatusDetails?` → `Reason (reason): AuthorizationIncompleteReason?` (`PendingReview`,
`DeclinedByRiskFraudFilters`) · `ExpirationTime (expiration_time): string?` (honor-period end —
use this to decide whether to call `ReauthorizePayment` proactively) · `Amount`, `InvoiceId`,
`CustomId`, `SellerProtection`, `Links`, `CreateTime`, `UpdateTime`.

Error: `SdkException<GetAuthorizedPaymentError>` — Case A. `TryGetError(out Error)` [401, 403, 404]
· `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)` [fallback].

Pagination: none.

#### ReauthorizePayment — `client.Payments` (`operations/Payments.md`)

```
ReauthorizePayment(string authorizationId, string? payPalRequestId, string? payPalAuthAssertion,
                    ReauthorizeRequest? body, string? prefer = "return=minimal",
                    RequestOptions? requestOptions = null, CancellationToken ct = default)
```
Request `ReauthorizeRequest`: `Amount (amount): Money?` (only field the operation supports, per
its own doc comment — "Supports only the `amount` request parameter").

**Business-rule window, straight from the operation's own doc comment (`operations/Payments.md`)**:
reauthorize only after the initial 3-day honor period, from day 4 to day 29 after the original
authorization; **if 30 days have elapsed since the original authorization, PayPal will not
reauthorize — a brand-new `CreateOrder`/`AuthorizeOrder` is required instead.**

**Operator-actionable "can no longer be renewed" signal**: catch `SdkException<ReauthorizePaymentError>`,
call `TryGetError(out Error)`. If it returns `true`, inspect `Error.Details[].Issue` and surface it
verbatim to the operator (the SDK does not enumerate the specific issue-code strings, so match on
substring/logging rather than a closed enum) alongside the HTTP status (422 is the expected class
per the accessor list below). Also proactively compute "authorization age > 29 days" from
`GetAuthorizedPayment`'s `CreateTime`/`ExpirationTime` and surface that as a pre-emptive
"cannot renew — create a new order" signal before even attempting the call.

Response `PaymentAuthorization` (same shape as `GetAuthorizedPayment`, fresh `ExpirationTime`).

Error: `SdkException<ReauthorizePaymentError>` — Case A. `TryGetError(out Error)` [400, 401, 403,
404, 422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)` [fallback].

Pagination: none.

#### VoidPayment — `client.Payments` (`operations/Payments.md`)

```
VoidPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, string? payPalRequestId,
            string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)
```
No request body — void is by `authorizationId` alone. Response `PaymentAuthorization` with
`Status` expected to become `AuthorizationStatus.Voided`.

Error: `SdkException<VoidPaymentError>` — Case A. `TryGetError(out Error)` [401, 403, 404, 409,
422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)` [fallback]. (409 is
the expected status for "already captured, cannot void" per the operation's own note.)

Pagination: none.

#### RefundCapturedPayment — `client.Payments` (`operations/Payments.md`)

```
RefundCapturedPayment(string captureId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion,
                       RefundRequest? body, string? prefer = "return=minimal",
                       RequestOptions? requestOptions = null, CancellationToken ct = default)
```
Request `RefundRequest`: pass `body: null` (or `Amount: null`) for a **full refund**; set `Amount
(amount): Money?` for a **partial refund**. Also: `CustomId (custom_id): string?`, `InvoiceId
(invoice_id): string?`, `NoteToPayer (note_to_payer): string?`, `PaymentInstruction
(payment_instruction): RefundPaymentInstruction?`.

**Capability 5 — preventing over-refund**: neither `CapturedPayment` (from
`CaptureAuthorizedPayment`/`GetCapturedPayment`) nor any other read in this SDK exposes a
"remaining refundable amount" field — `CapturedPayment` has no running-refunded-total member.
Directive: (a) maintain our own ledger of refunds issued per `captureId` in our database and
reject client-side before calling the SDK if the requested amount would exceed
`capture.Amount.Value` minus our ledger total; (b) treat PayPal's own rejection (expected 422, via
`TryGetError1`... — see below) as the authoritative backstop, since PayPal's server-side total is
the only source of truth for concurrent/duplicate refund attempts our own ledger might race with.

Response `Refund`: `Id (id): string?` · `Status (status): RefundStatus?` (`Cancelled`, `Failed`,
`Pending`, `Completed`) · `Amount (amount): Money?` · `SellerPayableBreakdown
(seller_payable_breakdown): SellerPayableBreakdown?` → `GrossAmount`, `PaypalFee`,
`PaypalFeeInReceivableCurrency`, `NetAmount`, `NetAmountInReceivableCurrency`, `TotalRefundedAmount
(total_refunded_amount): Money?` (**running total refunded on the capture, post-this-refund** —
use this to update our own ledger after each successful refund), `PlatformFees`,
`NetAmountBreakdown` · `InvoiceId`, `CustomId`, `CreateTime`, `UpdateTime`, `Links`.

Error: `SdkException<RefundCapturedPaymentError>` — Case A. `TryGetError(out Error)` [400, 401,
403, 404, 409, 422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)`
[fallback]. Same `Error`/`ErrorDetails` model as `CreateOrder`'s (this controller reuses the plain
`Error`/`ErrorDetails` records, **not** `Error1`/`ErrorDetails1` — that pair is `Vault`-only, see
below).

Pagination: none.

#### GetRefund / GetCapturedPayment — `client.Payments` (`operations/Payments.md`)

Use to re-read a refund/capture's current state (e.g., idempotent status polling).
`GetRefund(string refundId, string? payPalMockResponse, string? payPalAuthAssertion,
RequestOptions? requestOptions = null, CancellationToken ct = default)` → `Refund`.
`GetCapturedPayment(string captureId, string? payPalMockResponse, RequestOptions? requestOptions =
null, CancellationToken ct = default)` → `CapturedPayment`. Both Case A, `TryGetError(out Error)`
[401, 403, 404] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError(out RawError)` [fallback].
No pagination.

#### CreatePaymentToken — `client.Vault` (`operations/Vault.md`)

```
CreatePaymentToken(string? payPalRequestId, PaymentTokenRequest body, RequestOptions? requestOptions = null, CancellationToken ct = default)
```
Request `PaymentTokenRequest`: `Customer (customer): Customer?` (`Id`, `MerchantCustomerId` —
optional, only if we already track a PayPal customer id) · `PaymentSource (payment_source):
PaymentTokenRequestPaymentSource !req` → `Card (card): PaymentTokenRequestCard?`: `Name`, `Number`,
`Expiry`, `SecurityCode`, `Brand (brand): CardBrand?`, `BillingAddress (billing_address): Address?`
(raw card — same shape family as checkout's `CardRequest`, no setup-token/buyer-confirmation round
trip needed for a direct card).

Response `PaymentTokenResponse`: `Id (id): string?` (**this is the vault id to store — see §2.5**)
· `Customer (customer): CustomerResponse?` · `PaymentSource (payment_source):
PaymentTokenResponsePaymentSource?` → `Card (card): CardPaymentTokenEntity?`: `Name`, `LastDigits
(last_digits): string?`, `Brand (brand): CardBrand?`, `Expiry (expiry): string?`,
`BillingAddress`, `VerificationStatus`, `Verification`, `NetworkTransactionReference`,
`AuthenticationResult`, `BinDetails`, `Type` · `Links`.

**Confirmed safe descriptor, grounded in the model — not assumed**: `CardPaymentTokenEntity` (the
response shape) has **no `Number`/PAN field at all** — its only card-identifying fields are
`LastDigits`, `Brand`, `Expiry`, `Name`. Contrast with the *request* shape
(`PaymentTokenRequestCard`), which does carry `Number`. The response type the SDK deserializes
into structurally cannot carry a full PAN back to us, so `Name`/`LastDigits`/`Brand`/`Expiry` is
the safe descriptor to persist and show the shopper.

Error: `SdkException<CreatePaymentTokenError>` — Case A. **Different payload type from
Orders/Payments**: `TryGetError1(out Error1)` [400, 403, 404, 422, 500] · `TryGetRawError(out
RawError)` [fallback]. `Error1` (`PayPalServerSdk.Models`): `Name`, `Message`, `DebugId` (`string
!req`) · `Details (details): IReadOnlyList<ErrorDetails1>?` (`ErrorDetails1.Issue: string !req` —
same shape as `ErrorDetails` but a distinct generated type) · `Links (links):
IReadOnlyList<ErrorLinkDescription>?` (a distinct link-record type from `Error`'s `LinkDescription`).

Pagination: none.

#### ListCustomerPaymentTokens — `client.Vault` (`operations/Vault.md`)

```
ListCustomerPaymentTokens(string customerId, int? pageSize = 5, int? page = 1, bool? totalRequired = false,
                           RequestOptions? requestOptions = null, CancellationToken ct = default)
```
Query wire mapping: `customer_id` ← `customerId`, `page_size` ← `pageSize`, `page` ← `page`,
`total_required` ← `totalRequired`. `customerId` is the PayPal-assigned customer id
(`CustomerResponse.Id` from a prior `CreatePaymentToken` response, or our own if we set
`Customer.Id` on the request) — **not** our own merchant customer id unless we also set
`Customer.MerchantCustomerId` and query by that (the SDK's `customerId` param maps to `customer_id`
which PayPal semantics tie to the PayPal-side id, not `merchant_customer_id`; UNVERIFIED which one
the live sandbox actually accepts in the `customer_id` query param — the map/source give only the
query-param name, not which of `Customer.Id`/`Customer.MerchantCustomerId` populates it
server-side. Defensive directive: capture and persist whichever `CustomerResponse.Id` PayPal
returns on `CreatePaymentToken` and always query with that value first).

Response `CustomerVaultPaymentTokensResponse`: `TotalItems (total_items): int?` · `TotalPages
(total_pages): int?` · `PaymentTokens (payment_tokens): IReadOnlyList<PaymentTokenResponse>?` ·
`Customer (customer): VaultResponseCustomer?` · `Links`. Loop `page` from 1 to `TotalPages`
(pass `totalRequired: true` to guarantee `TotalItems`/`TotalPages` are populated) exactly as for
`SearchTransactions` below — no auto-paginating helper exists on this SDK (map: "Pagination: none
(only `page`, no `perPage`)").

Error: `SdkException<ListCustomerPaymentTokensError>` — Case A. `TryGetError1(out Error1)` [400,
403, 500] · `TryGetRawError(out RawError)` [fallback].

#### DeletePaymentToken — `client.Vault` (`operations/Vault.md`)

```
DeletePaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)
```
Returns `void` (`Task`). No `payPalRequestId` parameter (see §2.3). Error:
`SdkException<DeletePaymentTokenError>` — Case A. `TryGetError1(out Error1)` [400, 403, 500] ·
`TryGetRawError(out RawError)` [fallback].

#### SearchTransactions — `client.TransactionSearch` (`operations/TransactionSearch.md`)

```
SearchTransactions(string startDate, string endDate, string? transactionId, string? transactionType,
                    string? transactionStatus, string? transactionAmount, string? transactionCurrency,
                    string? paymentInstrumentType, string? storeId, string? terminalId,
                    string? fields = "transaction_info", string? balanceAffectingRecordsOnly = "Y",
                    int? pageSize = 100, int? page = 1, RequestOptions? requestOptions = null, CancellationToken ct = default)
```
`startDate`/`endDate` required, ISO 8601 strings. The 8 middle filters are nullable-no-default —
pass `null` for all of them when doing a plain date-range sweep. Query wire mapping: `start_date` ←
`startDate`, `end_date` ← `endDate`, `page_size` ← `pageSize`, `page` ← `page`, (+ the rest 1:1
snake_case). `fields` defaults to `"transaction_info"` only — **if correlation needs more than
`TransactionInfo`, pass a broader `fields` value explicitly** (the map does not enumerate the
allowed `fields` values; treat the default as the only value grounded from source and pass it
explicitly rather than relying on the C# default silently matching).

Response `SearchResponse`: `TransactionDetails (transaction_details):
IReadOnlyList<TransactionDetails>?` · `Page (page): int?` · `TotalItems`, `TotalPages
(total_pages): int?` (**loop `page` = 1..`TotalPages`, incrementing and re-calling with the same
date range, until `page > TotalPages`** — no auto-pager, per map's "Pagination: none (only `page`,
no `perPage`)") · `AccountNumber`, `StartDate`, `EndDate`, `LastRefreshedDatetime`, `Links`.

`TransactionDetails.TransactionInfo (transaction_info): TransactionInformation?` — the correlation
fields: `InvoiceId (invoice_id): string?` and `CustomField (custom_field): string?`.

**Reconciliation correlation — a genuine wire-name mismatch, confirmed from the model pages, not
guessed**: `PurchaseUnitRequest.InvoiceId` writes wire field `invoice_id` at order-creation time,
and `TransactionInformation.InvoiceId` reads back wire field `invoice_id` on the transaction side —
**these match**, so `invoice_id` round-trips cleanly. But `PurchaseUnitRequest.CustomId` writes
wire field `custom_id` at order-creation time, while the transaction-search side's matching field
is `TransactionInformation.CustomField`, wire name `custom_field` — **a different wire name, not
`custom_id`**. Directive: primarily correlate on `invoice_id` (set a unique value per order at
creation); treat `custom_field` as a secondary/best-effort match only, and do not assume it always
carries what we wrote as `custom_id` — **UNVERIFIED** whether PayPal's transaction-search backend
actually copies the order's `custom_id` into `custom_field` verbatim (this is a live-data question
the SDK's generated models cannot answer; confirm empirically against a real sandbox transaction
before relying on it, and fall back to `invoice_id`-only correlation if it does not).

Error: `SdkException<RawError>` (`PayPalServerSdk.Core.ErrorResponse`) — **Case B, the one
operation in scope whose error shape differs from every other**: no typed `{Operation}Error`/`Issue`
accessor exists here at all. Read `ex.Error.StatusCode: HttpStatusCode`, `ex.Error.ReadAsString():
string`, or `ex.Error.ReadAsJson<T>(): T?` (e.g. `ReadAsJson<SearchError>()` — `SearchError`
(`PayPalServerSdk.Models`) mirrors the same `Name`/`Message`/`DebugId`/`Details`/`Links` shape with
`Details: IReadOnlyList<TransactionSearchErrorDetails>?`, `Issue: string !req`, if the body happens
to match it — there is no guarantee it does, since this is the raw case).

Pagination: manual, as described above.

### 2.5 Vault reuse for a later order (capability 7's "no re-entry, no browser approval")

No distinct operation — reuse `CardRequest.VaultId (vault_id): string?` on a **later**
`Orders.CreateOrder` call's `PaymentSource.Card`, leaving `Number`/`Expiry`/`SecurityCode` unset.
Same `AuthorizeOrder`/`CaptureAuthorizedPayment` flow from §2.4 follows — vaulting only changes how
the card is supplied at order-creation time, not any later step.

### 2.6 PAYER_ACTION_REQUIRED / 3DS — explicit flag per the brief's stop condition

**This can happen for a direct card, and the SDK models it — confirmed from the map's enum and
record pages, not from training-data memory of the live API:**
- `OrderStatus` (`PayPalServerSdk.Models.Enums`) includes a `PayerActionRequired (PAYER_ACTION_REQUIRED)`
  member — a real, named terminal-ish state the `Order`/`OrderAuthorizeResponse.Status` field can
  hold.
- `PaymentTokenStatus` (vaulting) likewise includes `PayerActionRequired (PAYER_ACTION_REQUIRED)`.
- `CardVerification.Method (method): OrdersCardVerificationMethod?` defaults to
  `OrdersCardVerificationMethod.ScaWhenRequired` (`SCA_WHEN_REQUIRED`) — i.e., **step-up
  authentication is the SDK's own default posture for a card payment**, not an opt-in; it is not
  suppressed just because no explicit 3DS/redirect flow is being built.
- `CardResponse`/`ApplePayCardResponse`/`CardPaymentTokenEntity` all carry an
  `AuthenticationResult`/`AuthenticationResult` (`AuthenticationResponse` /
  `CardAuthenticationResponse`) field with `LiabilityShift`/`ThreeDSecure` sub-shapes — further
  confirming a 3DS outcome is a first-class, expected part of a card response, not an edge case
  this SDK omits.

**Per the brief: do not build a browser-approval round trip.** Defensive detection to add at every
response boundary in steps 2 and 7 (`CreateOrder`, `AuthorizeOrder`, `CreatePaymentToken`):
after deserializing, check `response.Status == OrderStatus.PayerActionRequired` (or
`PaymentTokenStatus.PayerActionRequired` for vaulting) and, as a secondary signal, scan
`response.Links` for a `LinkDescription.Rel` suggesting payer action (e.g. containing
`"payer-action"` — the exact `rel` string is **UNVERIFIED** from the map/source, which list only
the field shape `Rel (rel): string !req`, not the enumerated `rel` values PayPal sends). On either
signal: **stop, do not proceed to capture, and surface a blocking operator alert** — do not attempt
any redirect/approval flow. Whether PayPal's sandbox actually returns this status for the specific
test card `4111 1111 1111 1111` under a default (`ScaWhenRequired`) verification method is
**UNVERIFIED** — this is exactly a live-sandbox-behavior question the generated SDK cannot answer;
the detection above must be wired defensively regardless of whether it is observed to trigger in
initial testing.

---

## 3. Trap notes

⚠ Step 1 (client registration) — `HttpClient` lifetime and the transient-vs-singleton split between
it and the SDK client wrapper are not obvious from `PayPalServerSdkClient`'s constructor. **MUST
load `dotnet-client-initialization`** before writing the DI registration.

⚠ Step 1 (auth) — where exactly to set `Oauth2`/`Oauth2TokenStrategy` relative to client
construction, and how to source the secret from configuration rather than hardcoding, isn't implied
by the options class shape alone. **MUST load `dotnet-authentication`** before wiring credentials.

⚠ Steps 2–8 (every call) — several operations have 5–9 nullable-no-default parameters
(`payPalMockResponse`, `payPalClientMetadataId`, `payPalAuthAssertion`, the `SearchTransactions`
filter set) that will silently mis-bind if called positionally. **MUST load
`dotnet-calling-endpoints`** before writing the first call — use named arguments throughout.

⚠ Steps 2, 6, 7 (request/response models) — unions (`PaymentSource`'s variant properties),
`StringEnum<T>` construction (`CheckoutPaymentIntent.Authorize` is not a C# `enum` member the
compiler enumerates the usual way), and which JSON fields get silently dropped on an unmodeled
response shape are not visible from the field tables above alone. **MUST load `dotnet-models`**
before constructing `OrderRequest`/`CardRequest`/reading any response.

⚠ Step 9 (error boundary) — the per-operation tables above give the *shape* of each error
(Case A vs B, which `TryGet…`), but not the *sequencing* rules — e.g., whether `TryGetRawError` is
safe to call unconditionally as a catch-all on a Case A exception, or what happens if none of the
typed `TryGet…`s match. **MUST load `dotnet-error-handling`** before writing the catch ladder (see
the two mandatory rows below).

⚠ Step 1 / all steps (resilience) — `RetryOptions.HttpMethodsToRetry` only gates the **status**-code
retry trigger; a transport-level `HttpRequestException` retries on every verb including `POST`
regardless of that list, which interacts directly with the idempotency design in §2.3 (a retried
`CaptureAuthorizedPayment`/`RefundCapturedPayment` on transport failure needs its `payPalRequestId`
held constant across the retry, or the retry becomes a second, unintended operation). **MUST load
`dotnet-configuration-resilience`** before tuning retries/timeouts.

⚠ Step 9 (tests) — which seam to fake for the `Orders`/`Payments`/`Vault`/`TransactionSearch`
controllers so tests don't depend on SDK internals isn't obvious from the constructor shapes. **MUST
load `dotnet-testing`** before writing tests for the integration layer.

---

## 4. REQUIRED READING

Load all of the following **before implementation starts** — this sheet deliberately does not
carry their contents:

| Skill | Governs |
|---|---|
| `dotnet-client-initialization` | Step 1 — `HttpClient`/SDK-client lifetime and DI registration. |
| `dotnet-authentication` | Step 1 — where/how to set `OAuth2ClientCredentials` safely. |
| `dotnet-calling-endpoints` | Steps 2–8 — named-argument calling convention for every operation above. |
| `dotnet-models` | Steps 2, 6, 7 — building `OrderRequest`/`CardRequest`/etc., enum/union construction, wire-name mapping. |
| `dotnet-error-handling` | Step 9 — the full catch-ladder mechanics behind every Case A/B row above. |
| `dotnet-configuration-resilience` | Step 1 / cross-cutting — retry/timeout semantics interacting with §2.3's idempotency design, and the base-URL override in §2.2. |
| `dotnet-testing` | Step 9 — the seam to fake when testing the integration layer. |

Always include, verbatim, **both** of these hazard rows — `System.Text.Json.JsonException` reaches
the boundary from two directions and they need opposite handling:
- a drifted or malformed **2xx** body (a missing `required` member) surfaces as a `JsonException`
  from deserialization, **not** as an `SdkException` — so an SDK-exception-only catch ladder lets
  it escape the integration boundary;
- a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape throws
  `JsonException` *while the error object is being constructed*, so the `JsonException` **replaces**
  the `SdkException` and the HTTP status is destroyed with it — a boundary that maps every
  `JsonException` to a 5xx then reports a deterministic rejection as an outage, and a caller that
  retries 5xx retries something that can never succeed.

**MUST load `dotnet-error-handling`** before writing that boundary.

---

## 5. Assumptions & Blockers

**Assumptions:**
- "Direct, one-off card" (capability 1) means populating `CardRequest.Number`/`Expiry`/`SecurityCode`
  directly rather than via a hosted-fields/tokenize-in-browser flow — consistent with the sandbox
  test-card requirement in the brief.
- The merchant-side idempotency key referenced in capability 6 is our own application-generated
  key (e.g., a GUID tied to our order/operation id), passed through `payPalRequestId` — the SDK does
  not generate or require any particular format for this string beyond it being a header value.
  Same value must be resent unchanged across retries of the *same* logical operation.
  Confirmed the SDK's own `Idempotency-Key` header cannot substitute for this (§2.3).
  **Constraint carried from the map, not assumed**: PayPal's own key-retention window (per
  `AuthorizeOrder`'s doc comment) is 6 hours by default (extendable to 72h by request) — our own
  idempotency-key store's retry window must not outlive that if it relies on PayPal-side
  deduplication rather than just our own pre-call ledger check.
- `customer_id` for `ListCustomerPaymentTokens` is populated from whatever `CustomerResponse.Id`
  PayPal returns on `CreatePaymentToken` (see §2.4's `ListCustomerPaymentTokens` UNVERIFIED note) —
  we assume we persist that id ourselves at vault-creation time rather than trying to derive it
  later.
- No requirement in the brief calls for `Vault.CreateSetupToken`/`Orders.ConfirmOrder` (the
  buyer-confirmation flow for wallet-type vaulting) — out of scope, since capability 7 is
  raw-card vaulting only, which `CreatePaymentToken` handles directly.

**Blockers:** none — all ten numbered capabilities are directly exposed by the SDK; see §1 for the
two flagged nuances (idempotency-header trap, no server-side "remaining refundable" read) rather
than true blockers.
