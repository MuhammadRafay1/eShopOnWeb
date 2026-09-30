# PayPal .NET SDK — implementation plan & contract sheet

> Grounded fresh this session in the bundled SDK map (`paypal-getting-started`, release tag
> `v1.0.1`, source commit `9653d18`). A handful of facts required opening SDK source directly
> (each marked *source-confirmed* below, source never referenced by path). This file is the
> **sole** PayPal reference for implementation — do not use general/training knowledge of this
> API. If a fact is missing here, ask the `paypal-sdk` agent; do not invent it.

SDK: NuGet `AsadAli.Checkout.Sdk` (install version-less) · root namespace `PayPalServerSdk` ·
client `PayPalServerSdkClient` · target `netstandard2.0`, `LangVersion 14`, `Nullable enable`.

---

## 1. Scope & sequence

1. **Client & DI registration** (Infrastructure) — construct `PayPalServerSdkClient` via DI,
   OAuth2 client-credentials, sandbox environment, optional `PayPal:BaseUrl` override.
2. **Authorize** an order total for a one-off raw card OR a vaulted card —
   `Orders.CreateOrder` (intent=`Authorize`) → `Orders.AuthorizeOrder` (payment_source = card).
   Fail closed on any PAYER_ACTION_REQUIRED / challenge signal.
3. **Capture** a held authorization — `Payments.CaptureAuthorizedPayment`. Read back captured
   amount, PayPal fee, net amount from `SellerReceivableBreakdown`.
4. **Reauthorize** a stale authorization before capture, when capture reports it's no longer
   fundable — `Payments.ReauthorizePayment`; on refusal, surface an operator-actionable reason.
5. **Void** an authorization before fulfilment — `Payments.VoidPayment`.
6. **Refund** a capture, full or partial, with caller idempotency — `Payments.RefundCapturedPayment`.
7. **Save a card to the vault** — `Vault.CreatePaymentToken`; reuse via `CardRequest.VaultId` on
   step 2's `AuthorizeOrder` call.
8. **Delete a saved card** — `Vault.DeletePaymentToken`.
9. **Transaction/reconciliation search** — `TransactionSearch.SearchTransactions`, paged over the
   full date range, joined back to our own record via `invoice_id`.
10. **Error boundary** — one exception-translation layer wrapping every call above (Case A/B, plus
    the two `JsonException` traps — see REQUIRED READING).

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

### 2.0 Namespace legend

| Type(s) | Namespace |
|---|---|
| `PayPalServerSdkClient`, `PayPalServerSdkClientOptions`, `ServerOptions`, `Server` | `PayPalServerSdk` (root — file at repo root) |
| `ServerEnvironment` | `PayPalServerSdk.Servers` |
| `DefaultOptions` (+ nested `DefaultOptions.SandboxOptions`) | `PayPalServerSdk.Servers` *(source-confirmed: `Servers/DefaultOptions.cs`)* |
| Controllers `Orders`/`Payments`/`Vault`/`TransactionSearch` (`client.X`) | `PayPalServerSdk.Api` |
| Record models (`OrderRequest`, `Money`, `CardRequest`, `CapturedPayment`, `SellerReceivableBreakdown`, …) | `PayPalServerSdk.Models` |
| Enums (`CheckoutPaymentIntent`, `OrderStatus`, `AuthorizationStatus`, `CardBrand`, …) | `PayPalServerSdk.Models.Enums` |
| Typed error classes (`CreateOrderError`, `AuthorizeOrderError`, `CaptureAuthorizedPaymentError`, `CreatePaymentTokenError`, …) | `PayPalServerSdk.Errors` |
| `SdkException<TError>` | `PayPalServerSdk.Core.Exceptions` |
| `RawError`, `ApiError` | `PayPalServerSdk.Core.ErrorResponse` |
| `RequestOptions` | `PayPalServerSdk.Core` *(source-confirmed: `Core/RequestOptions.cs`)* |

**§2.0a — per-type additions (answering the implementer's follow-up).** The generic "Record
models … → `PayPalServerSdk.Models`" row above is correct but was too coarse to write `using`
directives from directly. **Every one of the following payload/model types the sheet names is
`PayPalServerSdk.Models`** (all confirmed on `map/models/records-{1,2,3}.md`, each row's `Source`
column literally reading `Models/{TypeName}.cs`) — **none of them are in `PayPalServerSdk.Errors`**,
which is reserved for the 39 generated `{Operation}Error : ApiError` wrapper classes only
(`CreateOrderError`, `AuthorizeOrderError`, `CaptureAuthorizedPaymentError`,
`CreatePaymentTokenError`, …) — the **payload** you get from those wrappers' `TryGet…(out …)`
accessors is a plain `Models` record:

| Type | Namespace | Map source row |
|---|---|---|
| `Error` (payload of `TryGetError(out Error)` on Orders/Payments ops) | `PayPalServerSdk.Models` | `Models/Error.cs` |
| `ErrorDetails` (`Error.Details[]` element) | `PayPalServerSdk.Models` | `Models/ErrorDetails.cs` |
| `Error1` (payload of `TryGetError1(out Error1)` on Vault ops) | `PayPalServerSdk.Models` | `Models/Error1.cs` |
| `ErrorDetails1` (`Error1.Details[]` element) | `PayPalServerSdk.Models` | `Models/ErrorDetails1.cs` |
| `DefaultError` (payload of `TryGetDefaultError` on `SearchBalances`) | `PayPalServerSdk.Models` | `Models/DefaultError.cs` |
| `LinkDescription` (`Order.Links`/`OrderAuthorizeResponse.Links` elements; `Href`/`Rel` both `!req`) | `PayPalServerSdk.Models` | `Models/LinkDescription.cs` |
| `ErrorLinkDescription` (`Error1.Links` elements; `Rel` is `string?`, nullable — differs from `LinkDescription.Rel`) | `PayPalServerSdk.Models` | `Models/ErrorLinkDescription.cs` |
| `PurchaseUnit` (response side, `OrderAuthorizeResponse.PurchaseUnits`/`Order.PurchaseUnits`) | `PayPalServerSdk.Models` | `Models/PurchaseUnit.cs` |
| `PaymentCollection` (`PurchaseUnit.Payments`) | `PayPalServerSdk.Models` | `Models/PaymentCollection.cs` |
| `AuthorizationWithAdditionalData` (`PaymentCollection.Authorizations[]` elements) | `PayPalServerSdk.Models` | `Models/AuthorizationWithAdditionalData.cs` |
| `OrderAuthorizeRequestPaymentSource` | `PayPalServerSdk.Models` | `Models/OrderAuthorizeRequestPaymentSource.cs` |
| `CardRequest` | `PayPalServerSdk.Models` | `Models/CardRequest.cs` |
| `PaymentTokenRequestPaymentSource` | `PayPalServerSdk.Models` | `Models/PaymentTokenRequestPaymentSource.cs` |
| `PaymentTokenRequestCard` | `PayPalServerSdk.Models` | `Models/PaymentTokenRequestCard.cs` |
| `Customer` | `PayPalServerSdk.Models` | `Models/Customer.cs` |
| `PaymentTokenResponsePaymentSource` | `PayPalServerSdk.Models` | `Models/PaymentTokenResponsePaymentSource.cs` |
| `CardPaymentTokenEntity` (safe descriptor: `Brand`, `LastDigits`, `Expiry` — no `Number`) | `PayPalServerSdk.Models` | `Models/CardPaymentTokenEntity.cs` |
| `Address` (PayPal's request-side billing address — **name collides with
  `Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate.Address`**; alias it, e.g.
  `using PayPalAddress = PayPalServerSdk.Models.Address;`, in any file that references both) | `PayPalServerSdk.Models` | `Models/Address.cs` |

**Enums — confirmed, all six live in `PayPalServerSdk.Models.Enums`** (`RefundStatus`,
`CaptureStatus`, `AuthorizationStatus`, `OrderStatus`, `CheckoutPaymentIntent`, `CardBrand`;
`enums.md` header: "namespace `PayPalServerSdk.Models.Enums`", each is `StringEnum` backing).
**`.ToString()` and `.Value` are interchangeable for reading the wire value back**
*(source-confirmed, `Core/Enum/TypedEnum.cs`: `public TValue Value { get; init; }` and
`public override string ToString() => Value.ToString() ?? string.Empty;` — for these
`TypedEnum<string, TEnum>` types `Value.ToString()` is a no-op, so both return the identical wire
string, e.g. `CardBrand.Visa.ToString() == CardBrand.Visa.Value == "VISA"`)*. This SDK's enums are
**not** the `FromValue`-less kind `dotnet-models` warns about — even `ServerEnvironment` (the
server/environment selector) is built on the same `StringEnum<ServerEnvironment>` base
*(source-confirmed, `Servers/ServerEnvironment.cs`: `public record ServerEnvironment :
StringEnum<ServerEnvironment>`)*, so `.Value`/`.ToString()` work uniformly across model enums and
`ServerEnvironment` alike; `ServerEnvironment` just exposes only one public static instance
(`.Sandbox`) rather than a public `FromValue` factory, since it isn't meant to be constructed
from arbitrary wire strings the way model enums are.

**Controller-property pattern — confirmed.** `client.Orders`, `client.Payments`, `client.Vault`,
`client.TransactionSearch` (and `client.Subscriptions`, out of scope here) are ordinary **instance
properties** on `PayPalServerSdkClient`, assigned once in its constructor
*(source-confirmed, `PayPalServerSdkClient.cs`: `public Orders Orders { get; }` etc., each `new
Orders(rawClient, server, auth)` inside the constructor body)* — not static members, and no
separate construction step is needed beyond building the one `PayPalServerSdkClient`.
| `RetryOptions`, `RetryAttempt` | `PayPalServerSdk.Core.Configuration` |
| `OAuth2ClientCredentials`, `IOAuth2TokenStrategy<T>` | `PayPalServerSdk.Core.Authentication.OAuth2` / `…OAuth2.ClientCredentials` |

### 2.1 Universal facts (read once, applies to every operation below)

- **Throw-based only** — no no-throw `…Result` variant exists anywhere in this SDK (40/40 ops).
- **`SdkException<TError>` exposes ONLY `.Error`** — there is **no `.StatusCode` on the exception
  itself** *(source-confirmed, `Core/Exceptions/SdkException.cs`: `public sealed class
  SdkException<TError> : Exception { public required TError Error { get; init; } }`)*. To get a
  numeric HTTP status: Case B directly via `ex.Error.StatusCode`; Case A via the inherited
  `TryGetRawError(out RawError)` fallback on the typed error. The typed `Error`/`Error1`/
  `DefaultError` payloads carry only `Name`, `Message`, `DebugId`, `Details[]`, `Links[]` — **no
  numeric status field** on the typed shape itself; the status bucket is implied by which
  `TryGet…` returned true (each op's row below lists the HTTP status each accessor maps to).
- **`RequestOptions` carries only one member: `LogLevel?`** *(source-confirmed,
  `Core/RequestOptions.cs`)* — there is **no general per-request custom-header hook**. The only
  idempotency mechanism is the dedicated `string? payPalRequestId` **parameter** on the ops that
  have one (§2.6), which the SDK sends as header `PayPal-Request-Id` *(source-confirmed,
  `Api/Orders.cs`/`Api/Payments.cs`: `new HeaderParam("PayPal-Request-Id", payPalRequestId)`).
  PayPal retains that key server-side (6–72h) — resending the *same* key returns the original
  result instead of re-executing, which is exactly the caller-supplied idempotency key mechanism
  requirement #5/#6 need.
- ⚠️ **`prefer` defaults to `"return=minimal"` on every op that has it, and a minimal response
  omits everything except `id`, `status`, and HATEOAS links** *(source-confirmed doc-comment,
  identical on every occurrence in `Api/Orders.cs` and `Api/Payments.cs`: "return=minimal. …
  includes the id, status and HATEOAS links. return=representation. … complete resource
  representation, including the current state of the resource.")*. Ops with a `prefer` param:
  `CreateOrder`, `AuthorizeOrder`, `CaptureOrder`, `ConfirmOrder` (Orders); `CaptureAuthorizedPayment`,
  `ReauthorizePayment`, `RefundCapturedPayment`, `VoidPayment` (Payments). **Every one of these
  calls in this integration MUST pass `prefer: "return=representation"` explicitly** — otherwise
  `PurchaseUnits[].Payments.Authorizations[]`, `SellerReceivableBreakdown`, `ExpirationTime`,
  `SellerPayableBreakdown`, etc. come back **null**, and every "read back the amount/fee/expiry"
  requirement in this task (capabilities #1–#5) silently breaks. **This is the single highest-risk
  trap in this SDK for this task and was NOT flagged in the prior draft — verify it lands in the
  boundary code.**
- **Call every op with named arguments.** Most ops have a run of nullable-no-default header
  params that must be passed explicitly (pass `null` to skip); a positional call mis-binds them.

### 2.2 Orders controller — `client.Orders` (`map/operations/Orders.md`)

| Op | Signature (must-pass-explicitly in bold) | Request model | Response | Error |
|---|---|---|---|---|
| `CreateOrder` | `CreateOrder(`**`string? payPalMockResponse, string? payPalRequestId, string? payPalPartnerAttributionId, string? payPalClientMetadataId, string? payPalAuthAssertion,`**` OrderRequest body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `OrderRequest`: `Intent (intent): CheckoutPaymentIntent !req`, `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnitRequest> !req`, `Payer?`, `PaymentSource (payment_source): PaymentSource?` (omit for the two-step raw-card flow), `ApplicationContext?` | `Order`: `Id?`, `Status (status): OrderStatus?`, `Links?`, `PurchaseUnits?`, `PaymentSource?` | `SdkException<CreateOrderError>` Case A · `TryGetError(out Error)` [400,401,422] · `TryGetRawError` [fallback] |
| `AuthorizeOrder` | `AuthorizeOrder(string id,`**` string? payPalMockResponse, string? payPalRequestId, string? payPalClientMetadataId, string? payPalAuthAssertion,`**` OrderAuthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `OrderAuthorizeRequest`: `PaymentSource (payment_source): OrderAuthorizeRequestPaymentSource?` → `.Card (card): CardRequest?` | `OrderAuthorizeResponse`: `Id?`, `Status (status): OrderStatus?`, `Links?`, `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnit>?` — **authorization is nested, see §2.4** | `SdkException<AuthorizeOrderError>` Case A · `TryGetError(out Error)` [400,401,403,404,422,500] · `TryGetRawError` [fallback] |
| `GetOrder` | `GetOrder(string id, `**`string? fields, string? payPalMockResponse, string? payPalAuthAssertion,`**` RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `Order` | `SdkException<GetOrderError>` Case A · `TryGetError` [401,404] · `TryGetRawError` |

**`PurchaseUnitRequest`** (`records-2-Pa-Ve.md`): `Amount (amount): AmountWithBreakdown !req`,
`ReferenceId (reference_id)?`, `CustomId (custom_id)?`, `InvoiceId (invoice_id)?`, `Items?`,
`Payee?`, `Description?`, `SoftDescriptor?`.
**`AmountWithBreakdown`**: `CurrencyCode (currency_code): string !req`, `Value (value): string !req`,
`Breakdown?`.
**`CardRequest`** (raw card): `Name?`, `Number (number): string?`, `Expiry (expiry): string?`,
`SecurityCode (security_code): string?`, `BillingAddress (billing_address): Address?`,
`Attributes (attributes): CardAttributes?`, **`VaultId (vault_id): string?`** (set this — and
leave `Number`/`SecurityCode` null — to pay with a saved card), `SingleUseToken?`,
`StoredCredential?`, `NetworkToken?`, `ExperienceContext (experience_context): CardExperienceContext?`.
**`Address`**: `AddressLine1?`, `AddressLine2?`, `AdminArea1 (admin_area_1)?` (state),
`AdminArea2 (admin_area_2)?` (city), `PostalCode (postal_code)?`, `CountryCode (country_code): string !req`.

**Sandbox test card 4111 1111 1111 1111 = `CardBrand.Visa`.**

### 2.3 Challenge / PAYER_ACTION_REQUIRED detection (fail closed, do not build an approval round-trip)

Primary signal: `OrderAuthorizeResponse.Status == OrderStatus.PayerActionRequired` (wire
`PAYER_ACTION_REQUIRED`) — one of the 6 `OrderStatus` members (`Created`, `Saved`, `Approved`,
`Voided`, `Completed`, `PayerActionRequired`; `enums.md`). Treat this, and the absence of any
populated `Authorizations[]` element when status isn't `Approved`/`Completed`, as "approval
required — STOP and report", never as a completed authorization.
Secondary belt-and-braces signal: scan `OrderAuthorizeResponse.Links` (`LinkDescription { Href
!req, Rel !req, Method? }`, `records-1-Ac-Pa.md`) for any `Rel` containing `payer-action` /
`approve` / `3ds` (case-insensitive). **UNVERIFIED**: the exact `Rel` token PayPal sends for a
card-challenge link is not encoded in any SDK type (`Rel` is a free string) — only the
`OrderStatus.PayerActionRequired` enum check is a hard contract guarantee; treat the `Rel` scan
as best-effort defense-in-depth, not the primary signal.

### 2.4 Reading the authorization out of `AuthorizeOrder`'s response

`OrderAuthorizeResponse.PurchaseUnits` (`IReadOnlyList<PurchaseUnit>`) → `PurchaseUnit.Payments`
(`PaymentCollection?`, `records-2-Pa-Ve.md`) → `PaymentCollection.Authorizations
(authorizations): IReadOnlyList<AuthorizationWithAdditionalData>?`. Each
**`AuthorizationWithAdditionalData`**: `Id (id): string?`, `Status (status): AuthorizationStatus?`,
`ExpirationTime (expiration_time): string?`, `Amount (amount): Money?`, `ProcessorResponse?`.
Persist `resp.PurchaseUnits[0].Payments.Authorizations[0].Id` (this is the `authorizationId`
every Payments-controller op below takes) plus `.ExpirationTime`. **Null-guard the whole chain**
— an empty/absent element pairs with the §2.3 challenge-detection rule. Remember §2.1's `prefer`
trap: this chain is populated only when `AuthorizeOrder` was called with `prefer:
"return=representation"`.

### 2.5 Payments controller — `client.Payments` (`map/operations/Payments.md`)

All 4 write ops here additionally expose `TryGetNoContent(out RawError)` for **[500]** — an
accessor **Orders-controller ops do not have**; and Vault ops use a differently-named accessor
again (`TryGetError1`, §2.7) — three different accessor-name conventions across three
controllers for materially the same "typed 4xx / typed 500 / raw fallback" shape (capability #12).

| Op | Signature (must-pass-explicitly bold) | Request | Response | Error |
|---|---|---|---|---|
| `CaptureAuthorizedPayment` | `CaptureAuthorizedPayment(string authorizationId,`**` string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion,`**` CaptureRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `CaptureRequest`: `Amount (amount): Money?` (null = full capture), `InvoiceId?`, `FinalCapture (final_capture): bool? = false`, `NoteToPayer?`, `SoftDescriptor?` | `CapturedPayment`: `Id?`, `Status (status): CaptureStatus?`, `Amount (amount): Money?`, `SellerReceivableBreakdown (seller_receivable_breakdown): SellerReceivableBreakdown?`, `Links?` | `SdkException<CaptureAuthorizedPaymentError>` Case A · `TryGetError(out Error)` [400,401,403,404,409,422] · `TryGetNoContent(out RawError)` [500] · `TryGetRawError` [fallback] |
| `ReauthorizePayment` | `ReauthorizePayment(string authorizationId,`**` string? payPalRequestId, string? payPalAuthAssertion,`**` ReauthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `ReauthorizeRequest`: **`Amount (amount): Money?` only** | `PaymentAuthorization`: `Id?`, `Status (status): AuthorizationStatus?`, `Amount?`, `ExpirationTime (expiration_time): string?`, `Links?` | `SdkException<ReauthorizePaymentError>` Case A · `TryGetError` [400,401,403,404,422] · `TryGetNoContent` [500] · `TryGetRawError` |
| `VoidPayment` | `VoidPayment(string authorizationId,`**` string? payPalMockResponse, string? payPalAuthAssertion, string? payPalRequestId,`**` string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` — note the param **order differs** from the other 3 ops (`payPalMockResponse, payPalAuthAssertion, payPalRequestId`) | — (no body) | `PaymentAuthorization` (status → `Voided`) | `SdkException<VoidPaymentError>` Case A · `TryGetError` [401,403,404,409,422] · `TryGetNoContent` [500] · `TryGetRawError` |
| `RefundCapturedPayment` | `RefundCapturedPayment(string captureId,`**` string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion,`**` RefundRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` | `RefundRequest`: `Amount (amount): Money?` (null = full refund; set for partial), `CustomId?`, `InvoiceId?`, `NoteToPayer?`, `PaymentInstruction?` | `Refund`: `Id?`, `Status (status): RefundStatus?`, `Amount (amount): Money?`, `SellerPayableBreakdown (seller_payable_breakdown): SellerPayableBreakdown?`, `Links?` | `SdkException<RefundCapturedPaymentError>` Case A · `TryGetError` [400,401,403,404,409,422] · `TryGetNoContent` [500] · `TryGetRawError` |
| `GetAuthorizedPayment` | `GetAuthorizedPayment(string authorizationId,`**` string? payPalMockResponse, string? payPalAuthAssertion,`**` RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `PaymentAuthorization` | Case A `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError` |
| `GetCapturedPayment` | `GetCapturedPayment(string captureId,`**` string? payPalMockResponse,`**` RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `CapturedPayment` | Case A `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError` |
| `GetRefund` | `GetRefund(string refundId,`**` string? payPalMockResponse, string? payPalAuthAssertion,`**` RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `Refund` | Case A `TryGetError` [401,403,404] · `TryGetNoContent` [500] · `TryGetRawError` |

**Idempotent partial refunds (capability #5):** each of the two partial `RefundCapturedPayment`
calls sets `RefundRequest.Amount` and a **different** `payPalRequestId`; repeating the same
`payPalRequestId` a second time is PayPal's own dedup — build the "block a repeated key" behaviour
on top of that (e.g. persist `payPalRequestId → Refund.Id` locally and reject/return-cached before
even calling out, since a resent key returns the *original* refund's result, not an error).

**`Money`**: `CurrencyCode (currency_code): string !req`, `Value (value): string !req` — both
`string`, both required. Identical shape reused for `CaptureRequest.Amount`, `RefundRequest.Amount`,
`ReauthorizeRequest.Amount`, `AmountWithBreakdown` (minus breakdown).

**`SellerReceivableBreakdown`** (capture fee/net — capability #2): `GrossAmount (gross_amount):
Money !req`, `PaypalFee (paypal_fee): Money?`, `NetAmount (net_amount): Money?`,
`ReceivableAmount?`, `ExchangeRate?`, `PlatformFees?`. Read `capturedPayment
.SellerReceivableBreakdown.GrossAmount.Value` / `.PaypalFee?.Value` / `.NetAmount?.Value`
(`PaypalFee`/`NetAmount` are nullable — guard).

**`SellerPayableBreakdown`** (refund breakdown): `GrossAmount?`, `PaypalFee (paypal_fee): Money?`,
`NetAmount (net_amount): Money?`, `TotalRefundedAmount (total_refunded_amount): Money?`, `PlatformFees?`.

### 2.6 Enums needed (full lists, `map/models/enums.md`)

- **`CheckoutPaymentIntent`**: `Capture (CAPTURE)`, `Authorize (AUTHORIZE)` — use `.Authorize`.
- **`OrderStatus`**: `Created`, `Saved`, `Approved`, `Voided`, `Completed`, `PayerActionRequired`.
- **`AuthorizationStatus`**: `Created (CREATED)`, `Captured (CAPTURED)`, `Denied (DENIED)`,
  `PartiallyCaptured (PARTIALLY_CAPTURED)`, `Voided (VOIDED)`, `Pending (PENDING)`. **No `Expired`
  member exists in this SDK.**
- **`AuthorizationIncompleteReason`** (the `Reason` inside `AuthorizationStatusDetails` when
  `Status == Pending`): `PendingReview (PENDING_REVIEW)`, `DeclinedByRiskFraudFilters
  (DECLINED_BY_RISK_FRAUD_FILTERS)`. **No `Expired`/staleness reason either.**
- **`CaptureStatus`**: `Completed`, `Declined`, `PartiallyRefunded`, `Pending`, `Refunded`, `Failed`.
- **`RefundStatus`**: `Cancelled (CANCELLED)`, `Failed (FAILED)`, `Pending (PENDING)`, `Completed (COMPLETED)`.
- **`CardBrand`**: 29 members incl. `Visa (VISA)`, `Mastercard`, `Discover`, `Amex`, … `Unknown (UNKNOWN)`.

**Stale/expired-authorization detection (capability #3) — no enum signals it.** Neither
`AuthorizationStatus` nor `AuthorizationIncompleteReason` has an `Expired` member — confirmed by
the full member lists above; this SDK genuinely does not model authorization expiry as a status
value. Detect staleness two ways, both required:
1. **Proactive**: compare `Authorization.ExpirationTime` (a plain ISO-8601 `string?`, unparsed by
   the SDK) to `DateTimeOffset.UtcNow` before attempting capture; if past, go straight to
   `ReauthorizePayment` instead of `CaptureAuthorizedPayment`.
2. **Reactive**: if `CaptureAuthorizedPayment` throws and `TryGetError(out var err)` succeeds,
   `err.Name`/`err.Message`/`err.Details[]` are **free-text strings, not an enum** — PayPal's
   specific business error identifier (e.g. an "authorization expired" style name) is **not**
   modeled anywhere in this C# SDK (`Error.Name` is `string !req` with no enumerated values) and
   is genuinely outside what map or source can settle. **UNVERIFIED — label it as such**: extract
   `err.Name`/`err.Details[0]?.Issue`/`.Description` best-effort into the operator-facing message,
   do not branch program logic on a hard-coded string match, and treat **any** 4xx from
   `CaptureAuthorizedPayment` as "try `ReauthorizePayment` next, then report if that also fails" —
   never assume the specific string.
`ReauthorizePayment` itself either succeeds (new `PaymentAuthorization` with a fresh
`ExpirationTime`) or throws (4xx, typically outside PayPal's 4–29-day reauthorization window) —
on that second failure, surface `err.Message`/`err.Details[0]?.Description` verbatim as the
operator-actionable reason; do not retry further.

### 2.7 Vault controller — `client.Vault` (`map/operations/Vault.md`)

Vault ops use a **third, differently-named** error accessor: `TryGetError1(out Error1)` — neither
`TryGetError` (Orders/Payments) nor `TryGetNoContent` exists here.

| Op | Signature | Request | Response | Error |
|---|---|---|---|---|
| `CreatePaymentToken` | `CreatePaymentToken(`**`string? payPalRequestId,`**` PaymentTokenRequest body, RequestOptions? requestOptions = null, CancellationToken ct = default)` | `PaymentTokenRequest`: `Customer (customer): Customer?`, `PaymentSource (payment_source): PaymentTokenRequestPaymentSource !req` → `.Card (card): PaymentTokenRequestCard?` | `PaymentTokenResponse`: `Id (id): string?` = the vault token id to persist, `Customer?`, `PaymentSource (payment_source): PaymentTokenResponsePaymentSource?`, `Links?` | `SdkException<CreatePaymentTokenError>` Case A · `TryGetError1(out Error1)` [400,403,404,422,500] · `TryGetRawError` [fallback] |
| `GetPaymentToken` | `GetPaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` | — | `PaymentTokenResponse` | Case A `TryGetError1` [403,404,422,500] · `TryGetRawError` |
| `DeletePaymentToken` | `DeletePaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` — **no `payPalRequestId` param at all** (only op in scope with none) | — | `void` (Task; success = no throw) | Case A `TryGetError1` [400,403,500] · `TryGetRawError` |

**`PaymentTokenRequestCard`** (raw card to vault): `Name?`, `Number (number): string?`, `Expiry
(expiry): string?`, `SecurityCode (security_code): string?`, `Brand (brand): CardBrand?`,
`BillingAddress (billing_address): Address?` — **no `ExperienceContext`/verification field on this
type**, confirming direct card vaulting via `CreatePaymentToken` has no mechanism to request a
buyer redirect (unlike the separate `CreateSetupToken`/`SetupTokenRequestCard` flow, which does
carry `VerificationMethod`/`ExperienceContext` and is **not** used by this integration —
out of scope; only needed for wallet/consent-based vaulting).

**Safe descriptor fields (capability #7 — never the full PAN):** `PaymentTokenResponse
.PaymentSource.Card` is **`CardPaymentTokenEntity`**: `Name?`, `LastDigits (last_digits):
string?`, `Brand (brand): CardBrand?`, `Expiry (expiry): string?`, `BillingAddress
(billing_address): CardResponseAddress?`, `VerificationStatus?`, `Verification?`,
`NetworkTransactionReference?`, `AuthenticationResult?`, `BinDetails?`, `Type (type): CardType?`.
**Confirmed: this record has no `Number` field at all** (checked its full field list above) — the
vault response literally cannot carry a full PAN; only `LastDigits` (and non-PAN metadata) come
back, on every vault read path (`CreatePaymentToken`/`GetPaymentToken` share this same response
shape).

**`Error1`** (Vault's typed error payload): `Name !req`, `Message !req`, `DebugId !req`, `Details
(details): IReadOnlyList<ErrorDetails1>?`, `Links (links): IReadOnlyList<ErrorLinkDescription>?`
— structurally close to Orders/Payments' `Error`/`ErrorDetails` but **not the same C# type**, and
`ErrorLinkDescription.Rel` is **nullable** (`string?`) where `LinkDescription.Rel` (used
elsewhere) is `!req` — confirmed difference (`records-1-Ac-Pa.md`: "Identical to
`link_description` except that `rel` is optional: the live API omits `rel` on the documentation
link it returns with `RESOURCE_NOT_FOUND` errors").

**Pay with a vaulted card (2-step reuse, capability #7):** on the later order's `AuthorizeOrder`
call, set `OrderAuthorizeRequest.PaymentSource.Card = new CardRequest { VaultId = "<the saved
PaymentTokenResponse.Id>" }` and leave `Number`/`SecurityCode`/`Expiry` null — do **not** resend
raw PAN data when `VaultId` is set. No browser step is required: `CreatePaymentToken` has no
approval-flow scaffolding (no `ExperienceContext` on `PaymentTokenRequestCard`, confirmed above),
so a direct card vault-create is server-side end-to-end, same as a direct card authorize.

**Deleting an already-deleted token:** since `DeletePaymentToken` has no `payPalRequestId`
param, its own idempotency comes only from HTTP DELETE semantics — a repeat call on an
already-deleted id returns 404/`TryGetError1` rather than succeeding silently; treat a 404 here
as "already satisfied," not as a failure to surface.

### 2.8 TransactionSearch controller — `client.TransactionSearch` (`map/operations/TransactionSearch.md`)

| Op | Signature | Response | Error |
|---|---|---|---|
| `SearchTransactions` | `SearchTransactions(string startDate, string endDate,`**` string? transactionId, string? transactionType, string? transactionStatus, string? transactionAmount, string? transactionCurrency, string? paymentInstrumentType, string? storeId, string? terminalId,`**` string? fields = "transaction_info", string? balanceAffectingRecordsOnly = "Y", int? pageSize = 100, int? page = 1, RequestOptions? requestOptions = null, CancellationToken ct = default)` — `startDate`/`endDate` are **required non-null** strings | `SearchResponse`: `TransactionDetails (transaction_details): IReadOnlyList<TransactionDetails>?`, `Page (page): int?`, `TotalItems (total_items): int?`, `TotalPages (total_pages): int?`, `Links?` | **The one Case-B op in the whole SDK**: `SdkException<RawError>` → `ex.Error.StatusCode`, `ex.Error.ReadAsString()`, `ex.Error.ReadAsJson<T>()`, `ex.Error.ReadAsBytes()` — **no typed accessor exists here at all**, unlike every other operation in this task |

Wire mapping: `start_date`←`startDate`, `end_date`←`endDate`, `page_size`←`pageSize` (default
100), `page`←`page` (default 1), `fields`←`fields` (default `"transaction_info"`) — pass
`startDate`/`endDate` as ISO-8601 date-time strings (this SDK does no date formatting/parsing for
you, matching capability #11's amount-formatting finding — everything date- and amount-shaped is
a plain unvalidated `string`).

**Pagination (cover the whole range — capability #9):** there is **no auto-pager** — call with
`page=1`, read `resp.TotalPages`, then loop `page=2..TotalPages` with identical `startDate`/
`endDate`/`pageSize`, accumulating `TransactionDetails`. Stopping at page 1 silently truncates the
range whenever `TotalPages > 1`.

**Per-transaction fields**: `TransactionDetails.TransactionInfo (transaction_info):
TransactionInformation?` → `TransactionId (transaction_id): string?`, `TransactionStatus
(transaction_status): string?`, `TransactionAmount (transaction_amount): Money?`, `FeeAmount
(fee_amount): Money?`, `InvoiceId (invoice_id): string?`, `CustomField (custom_field): string?`.

**Join key = `invoice_id` — confirmed by wire-name match on both sides, and this is the ONLY
field that matches.** `PurchaseUnitRequest.InvoiceId` (wire `invoice_id`) is what you stamp on
`CreateOrder`; `TransactionInformation.InvoiceId` (wire `invoice_id`) is what
`SearchTransactions` echoes back — **same wire name, safe to join on.**
`PurchaseUnitRequest.CustomId` (wire `custom_id`) is **not** safe for this: the transaction-search
side calls its analogous field `custom_field` (wire `custom_field`), a **different wire name**, so
a `custom_id` you stamp on create is not guaranteed to reappear as `custom_field` on search — do
not use `custom_id`/`CustomId` as the join key. **Stamp your own order/payment reference into
`PurchaseUnitRequest.InvoiceId` and join `SearchTransactions` results back on
`TransactionInformation.InvoiceId`.**

**Reporting lag** (from the op's own doc summary): executed transactions take up to ~3 hours to
appear in `SearchTransactions`, and it only covers the previous 3 years — a just-executed
transaction legitimately returning empty is expected, not a bug to chase.

### 2.9 Client construction, auth, environment, base URL (`sdk-map.md` §"Servers & auth", + source)

- **Construction**: `new PayPalServerSdkClient(HttpClient httpClient, PayPalServerSdkClientOptions
  options)`, or DI: `services.AddPayPalServerSdkClient(o => { … })`.
  `PayPalServerSdkClientOptions` members: `Environment (ServerEnvironment)`, `Retry
  (RetryOptions)`, `Logging (LoggingOptions)`, `Server (ServerOptions)`, `Oauth2
  (OAuth2ClientCredentials?)`, `Oauth2TokenStrategy (IOAuth2TokenStrategy<OAuth2ClientCredentials>?)`.
- **Auth = OAuth2 client-credentials, token handling automatic.** Set
  `options.Oauth2 = new OAuth2ClientCredentials { ClientId = <cfg>, ClientSecret = <cfg> }`
  (`ClientId`/`ClientSecret` are `required`, `Scope` optional). When set, the SDK fetches and
  attaches the bearer token itself on every call — never call the token endpoint from
  integration code.
- **Environment**: `ServerEnvironment` (`Servers/ServerEnvironment.cs`) has **exactly one
  member: `.Sandbox`** — there is no live/production member in this SDK at all. This matches the
  "SANDBOX only" requirement directly; reaching a non-sandbox host (should that ever be wanted)
  is only possible via the `BaseUrl` override below, never via `Environment`.
- **`PayPal:BaseUrl` override — confirmed (source) to cover BOTH the OAuth token request AND every
  API call, with no separate override needed for auth:**
  ```csharp
  options.Server = new ServerOptions {
      Default = new DefaultOptions {
          Sandbox = new DefaultOptions.SandboxOptions { BaseUrl = cfg["PayPal:BaseUrl"] } } };
  ```
  *(source-confirmed chain: `PayPalServerSdkClient` builds one `Server` instance from
  `options.Environment`/`options.Server` and passes that **same instance** into `AuthSchemes`,
  which builds the OAuth2 client-credentials token-fetch strategy via
  `server.Default("/v1/oauth2/token")`; every operation controller (`Orders`, `Payments`, …) calls
  `_server.Default("<op path>")` for its own request — both paths resolve through the identical
  `DefaultOptions.Resolve(environment, path) → Sandbox.BaseUrl` property. One `BaseUrl` value
  therefore really does redirect every call this integration makes, auth included.)*
  Default host when unset: `https://api-m.sandbox.paypal.com`. Leave `PayPal:BaseUrl` unset/empty
  to use the default sandbox host; only set it when a literal override is genuinely needed.

### 2.10 Amount formatting (capability #11)

`Money.Value` / `AmountWithBreakdown.Value` are plain `string !req` fields — **the SDK does no
formatting, parsing, or currency-minor-unit validation on them whatsoever** (confirmed: no
amount/currency converter exists under `Core/Converters/` — only date/time converters do
(`DateOnlyDateTimeOffsetConverter`, `Iso8601…`, `Rfc1123…`, `UnixDateTimeOffsetConverter`)). The
integration must itself: format with `InvariantCulture`, the correct number of decimal digits for
`PAYPAL_CURRENCY` (e.g. 2 for USD, 0 for a zero-decimal currency like JPY, were one ever
configured — this SDK gives no minor-unit table, so the integration must own that mapping or
restrict itself to currencies it has verified), and never rely on the SDK to reject a
malformed/mismatched-precision amount string — a bad string reaches PayPal as-is and comes back
as a 4xx `Error`/`ErrorDetails`, not a client-side validation exception.

---

## 3. Trap notes (hazard + consequence + pointer — not resolved here)

⚠ Step 1 (client & DI) — the `HttpClient` passed into `PayPalServerSdkClient` must be long-lived
and obtained via `IHttpClientFactory`, not rebuilt per request; whether the SDK client wrapper
itself should be transient/scoped/singleton in ASP.NET Core DI is not obvious from the
constructor shape alone. **MUST load `dotnet-client-initialization`** before writing the
registration.

⚠ Step 1 (auth) — where in the construction sequence credentials must be set, and whether
secrets belong in the DI callback vs. built up-front, isn't visible from the options shape.
**MUST load `dotnet-authentication`** before wiring `Oauth2`.

⚠ Steps 2–8 (every call) — many operations here have a run of nullable-no-default params with no
C# default; a positional call silently mis-binds them, and the signature alone doesn't flag
which params are cancellation-safe or reused across retries. **MUST load
`dotnet-calling-endpoints`** before the first call.

⚠ Steps 2–8 (models) — enums here are `StringEnum<T>`, not C# `enum`, and built via static
members/`Type.FromValue`, not `(CardBrand)0`; unmodeled response JSON is silently dropped on
deserialize rather than erroring. **MUST load `dotnet-models`** before constructing any request
body (`OrderRequest`, `CardRequest`, `RefundRequest`, `PaymentTokenRequest`, …).

⚠ Step 10 (error boundary) — three different typed-error accessor names across three
controllers (`TryGetError` / `TryGetError1` / `TryGetDefaultError`, §2.5/§2.7 plus
`SearchBalances`'s `TryGetDefaultError`), one Case-B op with no typed accessor at all
(`SearchTransactions`), and `TryGetNoContent` existing only on Payments ops — a catch ladder
written for one controller's shape does not transfer to the next without checking each op's row
above. **MUST load `dotnet-error-handling`** before writing the boundary (see the two mandatory
`JsonException` hazard rows below — non-negotiable, load before, not after).

⚠ Step 9 (reconciliation) — no auto-pager exists on `SearchTransactions`; a naive "call once,
read the list" implementation silently truncates any range spanning more than `TotalPages == 1`.
Also: `HttpMethodsToRetry`/transport-failure retry semantics interact with the `payPalRequestId`
idempotency story from §2.1 — a retried write without that key, or a retry policy that doesn't
know `SearchTransactions`' 3-hour reporting lag makes an empty result look like a bug.
**MUST load `dotnet-configuration-resilience`** before tuning retries/pagination/timeouts for any
of these calls.

⚠ Testing — the `HttpClient` constructor argument is the seam to fake for all of the above; which
project test framework/assertion style to match isn't a contract fact. **MUST load
`dotnet-testing`** before writing tests for the integration layer.

---

## 4. REQUIRED READING (load before implementation starts — this sheet deliberately omits their contents)

| Skill | Governs |
|---|---|
| `dotnet-client-initialization` | Client construction, `HttpClient` lifetime/`IHttpClientFactory`, DI registration (step 1) |
| `dotnet-authentication` | Setting `Oauth2` credentials, secret sourcing, credential rotation (step 1) |
| `dotnet-calling-endpoints` | Named-argument calls, required-vs-optional params, async/cancellation (steps 2–8) |
| `dotnet-models` | Building request bodies, `StringEnum<T>`, required members, wire names vs C# names (steps 2–8) |
| `dotnet-error-handling` | The exception boundary — mandatory, every integration writes one (step 10) |
| `dotnet-configuration-resilience` | Retries/backoff/timeouts, base-URL selection, manual `SearchTransactions` pagination (steps 1, 9) |
| `dotnet-testing` | The `HttpClient` test seam for stubbing the SDK (all steps) |

**Both of the following are mandatory and must shape the FIRST error-boundary implementation, not
a later revision** — `System.Text.Json.JsonException` reaches the boundary from two directions
that need opposite handling:

- A drifted or malformed **2xx** body (a missing `required` member) surfaces as a `JsonException`
  from deserialization, **not** as an `SdkException` — so an SDK-exception-only catch ladder lets
  it escape the integration boundary entirely.
- A **non-2xx** body that does not match its operation's generated `{Operation}Error`/`Error1`/
  `DefaultError` shape throws `JsonException` *while the error object is being constructed*, so
  the `JsonException` **replaces** the `SdkException` and the real HTTP status is destroyed with
  it — a boundary that maps every `JsonException` to a 5xx then reports a deterministic rejection
  as an outage, and a caller that retries 5xx retries something that can never succeed.

**MUST load `dotnet-error-handling`** before writing that boundary.

---

## 5. Assumptions & Blockers

**Assumptions:**
- Authorize flow is the two-call `CreateOrder` (intent=`Authorize`, no `payment_source`) →
  `AuthorizeOrder` (payment_source = card, direct or vaulted) rather than folding the card into
  `CreateOrder` itself — both are valid per the SDK (`OrderRequest.PaymentSource` and
  `OrderAuthorizeRequest.PaymentSource` are both optional/settable), but the two-call shape keeps
  order-creation and payment-source concerns separate, matching this task's step-by-step
  capability list (#1 is phrased as its own step). If the implementer prefers the single-call
  form, §2.2/§2.3/§2.4 still apply unchanged to whichever call actually carries `payment_source`.
- `PAYPAL_CURRENCY` is assumed to be a standard 2-decimal-digit currency (e.g. USD); the SDK gives
  no minor-unit-digit table (§2.10), so zero-decimal or 3-decimal currencies need the
  integration's own explicit handling if ever configured — flagged, not resolved, since it's a
  design decision outside SDK-contract scope.
- Vault reuse is via `CardRequest.VaultId`, not the `Token`/`TokenType` union on
  `OrderAuthorizeRequestPaymentSource.Token` — `TokenType` only has one member
  (`BillingAgreement`), which is PayPal's legacy billing-agreement token type, not this task's
  payment-token vault flow; `CardRequest.VaultId` is the field actually meant for reusing a
  `Vault.CreatePaymentToken` result.
- `CreateSetupToken`/`GetSetupToken`/`ListCustomerPaymentTokens` (3 of Vault's 6 operations) are
  out of scope: they belong to the setup-token/consent-based vaulting flow (wallet/3DS-eligible
  sources), not the direct-card `CreatePaymentToken` flow this task's capabilities #7/#8 describe.

**Blockers:** none. Every capability in scope (#1–#12) is exposed by this SDK; every genuinely
open contract question was either resolved from source (marked *source-confirmed* above) or
converted into a labeled `UNVERIFIED` defensive-coding directive (§2.3 challenge-link `Rel` token;
§2.6 staleness error-string extraction) rather than left open for "whoever implements."

---

## 6. Disagreement with the prior draft (`PLAN.md` Part 2, §2.1–2.10)

Independently re-deriving every fact above (including re-confirming both of its two
source-confirmed claims — `SdkException<TError>` having only `.Error` with no `.StatusCode`, and
`RequestOptions` carrying only `LogLevel?` — directly from source this session) produced **no
material factual disagreement** with the prior draft: every signature, wire name, enum list,
error-accessor name, and the BaseUrl→OAuth-endpoint coverage claim matched on independent
re-derivation.

**One addition the prior draft did not carry, promoted here to a first-class contract fact and
the top trap note (§2.1):** the `prefer` parameter on every Orders/Payments write op defaults to
`"return=minimal"` (source doc-comment, confirmed verbatim identical across every occurrence in
`Api/Orders.cs`/`Api/Payments.cs`), and a minimal response omits exactly the fields this task's
capabilities #1–#5 need to read back (`Authorizations[]`, `SellerReceivableBreakdown`,
`ExpirationTime`, `SellerPayableBreakdown`). The prior draft's per-operation rows list `prefer`
in each signature but never state that it must be overridden to `"return=representation"` for
this integration's read-back requirements to work at all — every write call in §2.2/§2.5 above
now carries that requirement explicitly.
