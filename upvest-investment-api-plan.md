# Upvest Investment API integration plan — "Invest your change" (eShopOnWeb PublicApi)

Grounded from the SDK map (`sdk-map.md` + `map/operations/*`) and the map-named source files, plus the
`upvest-workflows/` packages. The live Upvest endpoint for this run is a **local mock** bound at
`Upvest:BaseUrl` (`http://127.0.0.1:36618`); every call uses that base URL verbatim.

## 1. Scope & sequence

All capabilities are additive HTTP endpoints on `src/PublicApi` (JWT; caller identity = `ClaimTypes.Name`).
The Upvest connection is a single reusable `DelegatingHandler` that acquires the OAuth token and HTTP-signs
every outgoing request. No call site attaches credentials. SDK client registered via `IHttpClientFactory`.

1. **Auth plumbing** — `UpvestRequestSigner` (RFC-9421-style HTTP Message Signatures, ECDSA P-521/SHA-512,
   ASN.1 DER signature, v6 digest `digest: SHA-256=…`) + `UpvestTokenProvider` (signed form-body POST
   `/auth/token`, cached) + `UpvestAuthenticationHandler : DelegatingHandler` + response-body capture.
2. **Enrolment** (`POST/GET /api/investing/enrolment`): `Users.CreateUser` → `UserChecks.CreateUserCheck`
   → `TaxResidencies.SetTaxResidencies`; a background worker polls `Users.RetrieveUser` until `ACTIVE`, then
   `AccountGroups.CreateAccountGroup` + `AccountsApi.CreateAccount`, polls `AccountsApi.RetrieveAccount`
   until `ACTIVE` → enrolment `active`. Hard 4xx → `rejected`.
3. **Orders** (`POST /api/orders`): build the app's existing `Order`/`OrderItem` aggregate from catalog-item
   ids+quantities; treat as paid on creation; set aside `ceil(total)-total` to the shopper ledger iff the
   shopper is an accepted investor. Investing never fails the order (isolated try/catch).
4. **Investing**: when ledger `pending >= €10`, create an `Investment(amount=pending)`, reset pending to 0;
   background worker runs it: `TopUps.CreateTopup` (fund the account group) → `CashBalances.RetrieveCashBalance`
   (poll until the funded cash is available; the mock exposes no top-up GET) → `Orders.PlaceOrder`
   (nominal BUY `cash_amount` on `Upvest:InstrumentId`). (An unfunded buy is cancelled, so funding is required
   and is exposed by the SDK — not a gap.)
5. **Settlement** (Flow 4): worker polls `Orders.RetrieveOrder`; `FILLED`→`settled`, `CANCELLED`/terminal→`failed`.
6. **Balance** (`GET /api/investing/balance`): `pendingAmount`, `investedAmount` (= Σ settled).
7. **Investments** (`GET /api/investing/investments`): newest first; `investmentId`, `amount`, `status`.
8. **Webhooks** (production push, non-critical): subscribe via `WebhookSubscriptions.CreateWebhook` to
   `{Upvest:CallbackBaseUrl}/api/investing/upvest/webhooks`; receiver verifies the delivery signature against
   `WebhookSubscriptions.GetJwks` and triggers an immediate authoritative reconcile. The polling worker is the
   source of truth, so webhook availability/verification is never required for correctness.

## 2. CONTRACT SHEET

⚠ Signatures are generated code, verbatim. Each operation that takes input takes ONE request record as its
first parameter, built with an object initializer using the record's own property names (never flat args).
⚠ Every SDK type is written fully-qualified with the namespace its source path implies.

Auth members present on (nearly) every request record — `UpvestClientId: Guid` (req), `Authorization: string`
(req), `Signature: string` (req), `SignatureInput: string` (req), sometimes `IdempotencyKey: Guid` (req),
`UpvestApiVersion: UpvestApiVersion` (opt, default `_1`). **Decision:** pass `Authorization/Signature/
SignatureInput = null!` (the Api method emits them as header params; `ParameterFlattener.Flatten(null)` yields
no header — confirmed in `Core/ParameterFlattener.cs` + `Core/Extensions/HttpRequestExtensions.cs`), pass
`UpvestClientId = default` (handler overwrites the `upvest-client-id` header), and pass `IdempotencyKey` = a
**stable per-logical-operation** Guid (not a credential; handler signs it when present). The handler sets the
real `authorization`, `upvest-client-id`, `signature`, `signature-input`, `digest` headers. Request records are
NOT runtime-validated (the Api method decomposes them; no `Validator` call), so `null!` is safe.

| Operation (controller.method) | Request record + members used | Body model + fields (wire) | Response — fields READ (from captured JSON) | Error | Source |
| --- | --- | --- | --- | --- | --- |
| `AccessTokens` — (token done by handler, not SDK) | n/a — signed form POST `/auth/token` `grant_type=client_credentials&client_id&client_secret&scope` | — | `access_token`, `expires_in` | 401 body | `map/operations/AccessTokens.md`; mock `/auth/token` |
| `Users.CreateUser` | `CreateUserRequest{ Body, IdempotencyKey, auth… }` | `UserCreateRequest` ← `UserTolCreateRequest`: `FirstName first_name`(req), `LastName last_name`(req), `Email email`(req), `BirthDate birth_date`(req DateTimeOffset), `Nationalities nationalities`(req `IReadOnlyList<Nationality>`), `Address address`(req `Address`), `Fatca fatca`(req `Fatca{Status bool, ConfirmedAt}`), opt `PhoneNumber phone_number` | `id` (Guid), `status` | Case A `CreateUserError`.`TryGetNoContent`[400,401,403,406,429,5xx] | `Requests/Users/CreateUserRequest.cs`, `Models/UserTolCreateRequest.cs`, `Models/Address.cs`, `Models/Fatca.cs` |
| `Users.RetrieveUser` | `RetrieveUserRequest{ UserId, auth… }` | — | `status` (`INACTIVE`/`ACTIVE`) | Case A `RetrieveUserError`[401,403,404,…] | `Requests/Users/RetrieveUserRequest.cs` |
| `UserChecks.CreateUserCheck` | `CreateUserCheckRequest{ UserId, Body, auth… }` (no IdempotencyKey member) | `UserCheckCreateRequest` ← `UserCheckKnowYourCustomerCreateRequest`: `Type type`(="KYC"), `CheckConfirmedAt check_confirmed_at`(req), `DataDownloadLink data_download_link`(req), `DocumentType document_type`(req `DocumentType3.Passport`), `Provider provider`(req), `Method method`(req `Method.VideoId`) | (ignored) | Case A `CreateUserCheckError` | `Requests/UserChecks/CreateUserCheckRequest.cs`, `Models/UserCheckKnowYourCustomerCreateRequest.cs` |
| `TaxResidencies.SetTaxResidencies` | `SetTaxResidenciesRequest{ UserId, Body, IdempotencyKey, auth… }` | `TaxResidenciesSetRequest{ TaxResidencies tax_residencies: IReadOnlyList<TaxResidencyForCreateRequest> }` ← `WithTaxIdentifierNumber{ Country country(req Country), TaxIdentifierNumber tax_identifier_number(req) }` | (ignored) | Case A | `Requests/TaxResidencies/SetTaxResidenciesRequest.cs`, `Models/TaxResidenciesSetRequest.cs`, `Models/WithTaxIdentifierNumber.cs` |
| `AccountGroups.CreateAccountGroup` | `CreateAccountGroupRequest{ Body, IdempotencyKey, auth… }` | `AccountGroupCreateRequest` ← `AccountGroupCreateUserRequest{ UserId user_id(req), Type type(req Type13.Personal) }` | `id` (Guid) | Case A | `Requests/AccountGroups/CreateAccountGroupRequest.cs`, `Models/AccountGroupCreateUserRequest.cs` |
| `AccountsApi.CreateAccount` | `CreateAccountRequest{ Body, IdempotencyKey, auth… }` | `AccountCreateRequest` ← `AccountCreateUserRequest{ UserId user_id(req), AccountGroupId account_group_id(req), Type type(req Type16.Trading), Name name(opt) }` | `id` (Guid), `status` | Case A | `Requests/AccountsApi/CreateAccountRequest.cs`, `Models/AccountCreateUserRequest.cs` |
| `AccountsApi.RetrieveAccount` | `RetrieveAccountRequest{ AccountId, auth… }` | — | `status` (`PENDING_APPROVAL`/`ACTIVE`) | Case A | `Requests/AccountsApi/RetrieveAccountRequest.cs` |
| `TopUps.CreateTopup` | `CreateTopupRequest{ Body, IdempotencyKey, auth… }` | `PaymentsTopUpCreateRequest{ AccountGroupId account_group_id(req Guid), CashAmount cash_amount(req string "10.00"), Currency currency(req Currency.Eur) }` | `id` | Case A | `map/operations/TopUps.md`, `Models/PaymentsTopUpCreateRequest.cs` |
| `CashBalances.RetrieveCashBalance` | `RetrieveCashBalanceRequest{ AccountGroupId, auth… }` | — | `available` (euros string → cents) | Case A | `map/operations/CashBalances.md` (confirms funding landed; the mock has no GET top-up) |
| `Orders.PlaceOrder` | `PlaceOrderRequest{ Body, IdempotencyKey, auth… }` | `OrderPlaceRequest{ AccountId account_id(req), Side side(req Side.Buy), InstrumentId instrument_id(req ISIN string), InstrumentIdType="ISIN"(get-only), CashAmount cash_amount(opt string), Currency currency(opt Currency29.Eur), OrderType order_type(opt OrderType.Market), ClientReference client_reference(opt) }` | `id` (Guid), `status` | Case A `PlaceOrderError`[400,401,403,406,422,429,5xx] | `Requests/Orders/PlaceOrderRequest.cs`, `Models/OrderPlaceRequest.cs` |
| `Orders.RetrieveOrder` | `RetrieveOrderRequest{ OrderId, auth… }` | — | `status` (`NEW`/`PROCESSING`/`FILLED`/`CANCELLED`), `cash_amount` | Case A | `Requests/Orders/RetrieveOrderRequest.cs`; return type `Order39` |
| `WebhookSubscriptions.CreateWebhook` | `CreateWebhookRequest{ Body, auth… }` (no IdempotencyKey member) | `WebhookCreateRequest{ Title title(req), Url url(req), Type type(opt IReadOnlyList<TypeEnum>) }` | `id` (Guid) | Case A | `Requests/WebhookSubscriptions/CreateWebhookRequest.cs`, `Models/WebhookCreateRequest.cs` |
| `WebhookSubscriptions.GetJwks` | `GetJwksRequest{ auth… }` | — | `keys[].{kid,kty,crv,x,y}` | Case A | `map/operations/WebhookSubscriptions.md` |

⚠ Response fields are read from the **captured raw JSON body** (see Trap note R), not the typed SDK return,
because several 2xx response models (e.g. `Order39` requires `fee`/`initiation_flow`/`executions`) mark fields
the mock omits — SDK deserialization would throw `ResponseDeserializationException`. The request is still made
through the SDK; only response-field extraction is defensive. Wire field names confirmed against the mock.

Enum construction: fixed values use static members (`Type13.Personal`, `Type16.Trading`, `Side.Buy`,
`Currency.Eur`, `Currency29.Eur`, `OrderType.Market`, `DocumentType3.Passport`, `Method.VideoId`). Dynamic
ISO codes (nationality, address country, tax country) → `JsonSerializer.Deserialize<T>("\"XX\"")` through the
SDK's `StringEnumConverter` (open enum, no public ctor). `Nationality`/`Country` members are PascalCase alpha-2
(`.De`→"DE").

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| `account_id` used by `PlaceOrder`/`RetrieveAccount` must be one this app created via `CreateAccount` for that shopper | `PlaceOrder`/`RetrieveAccount` ← `CreateAccount` | Investment carries the shopper's stored `UpvestAccountId`; placing an order reads it from the enrolment row, never from the caller |
| `account_group_id` used by `TopUps.CreateTopup` must be one created via `CreateAccountGroup` for that shopper | `CreateTopup` ← `CreateAccountGroup` | stored `UpvestAccountGroupId` on the enrolment row |
| `instrument_id` for `PlaceOrder` must be a tradable ISIN Upvest offers | `PlaceOrder` ← `Upvest:InstrumentId` config (mock seeds it) | fixed from config; the order endpoint 400s if unknown (surfaced as investment `failed`) |
| `UserId` for check/tax/group/account must be one returned by `CreateUser` for that shopper | all ← `CreateUser` | stored `UpvestUserId`; one enrolment row per shopper (PK = shopper id) |

## 3. Trap notes

- **T1 (client init / DI):** the SDK `HttpClient`+handler pipeline must be long-lived via `IHttpClientFactory`,
  not rebuilt per request; the SDK client wrapper may be transient. `MUST load upvest:dotnet-client-initialization`.
- **T2 (auth):** credentials go on options at registration; the SDK's built-in OAuth is unusable here (it sends
  client_id/secret via Basic header and never signs, which the mock rejects), so the handler owns token + signing.
  `MUST load upvest:dotnet-authentication`.
- **T3 (calling endpoints):** every input is on the operation's request record; the injected per-call
  `Idempotency-Key` guid is NOT a real key — real idempotency uses the record's `IdempotencyKey` member.
  `MUST load upvest:dotnet-calling-endpoints`.
- **T4 (models):** open enums are not C# enums (no public ctor; `StringEnumConverter`); AnyOf bodies are built via
  static factory / implicit conversion. `MUST load upvest:dotnet-models`.
- **T5 (error handling):** a drifted/short 2xx body or a non-2xx body not matching `{Operation}Error` surfaces as
  `ResponseDeserializationException` — an `ApiException` that is NOT `ApiException<TError>`; the catch ladder must
  also catch it (see Trap R). `MUST load upvest:dotnet-error-handling`.
- **T6 (config/resilience):** `Timeout` is per-attempt not total; default `HttpMethodsToRetry` never resends POST;
  `LogRequestBody` logs JSON unredacted. `MUST load upvest:dotnet-configuration-resilience`.
- **T7 (testing):** the `HttpClient` constructor arg is the test seam. `MUST load upvest:dotnet-testing`.
- **R (response capture):** 2xx responses here routinely fail the SDK's `required`-member deserialization; the
  handler buffers + captures each raw response body and the integration reads needed fields from it, catching
  `ResponseDeserializationException` as success-with-unmappable-body. `MUST load upvest:dotnet-error-handling`.

## 4. REQUIRED READING (load before implementation; this sheet deliberately omits their contents)

- `upvest:dotnet-client-initialization` — SDK client construction + DI (step 1).
- `upvest:dotnet-authentication` — credential options surface + fail-fast (step 1, used by handler design).
- `upvest:dotnet-calling-endpoints` — request-record call shape + idempotency (steps 2–5).
- `upvest:dotnet-models` — open enums, AnyOf bodies, wire names (steps 2–5).
- `upvest:dotnet-error-handling` — Case A/B, `TryGet…`, and the `ResponseDeserializationException` hazard: a body
  not matching its declared type (short 2xx or non-`{Operation}Error` non-2xx) is an `ApiException` that is NOT
  `ApiException<TError>`; a ladder catching only `ApiException<TError>` lets it escape — catch it (or `ApiException`).
- `upvest:dotnet-configuration-resilience` — retries/timeouts/base-URL/logging (step 1).
- `upvest:dotnet-testing` — HttpClient seam (tests).

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | `UpvestOptions` bound from `Upvest:`; a startup `IValidateOptions`/guard refuses to start if any of the 8 keys (ClientId, ClientSecret, SigningKeyId, SigningKeyPath, SigningKeyPassphrase, BaseUrl, InstrumentId, CallbackBaseUrl) is missing/blank, and the signing key file is loadable with the passphrase. Each multi-part credential checked individually. |
| 2 | Secret sourcing & rotation | All 8 values loaded from env into **.NET user-secrets** under `Upvest:*` (never in repo). Options bound once at registration; signer/handler are singletons capturing them, so a rotated secret takes effect on process restart (documented; restart-to-rotate is acceptable for this integration). |
| 3 | Total timeout budget | A `CancellationToken` deadline (per enrolment/invest step, e.g. 30 s) bounds the whole call; SDK `RetryOptions.Timeout` is per-attempt only. Token acquisition uses its own bounded `HttpClient` timeout. |
| 4 | Write-retry ownership | SDK default retries GET/PUT/HEAD/OPTIONS only — our POSTs (CreateUser, checks, tax, group, account, topup, order) are never auto-resent by the SDK. Retries of those are driven by the idempotent worker using stored idempotency keys. |
| 5 | Idempotency & ambiguous writes | Each idempotent POST (users, checks, tax, account_groups, accounts, topups, orders) carries a **stored, stable** `IdempotencyKey` (persisted on the owning row), so a worker retry replays rather than duplicates. See DUPLICATE CLAIMS. |
| 6 | Observability | Structured logs at Info (state transitions) / Warning (retryable) / Error (terminal); the Upvest `upvest-request-id` response header is captured and logged for correlation. `LogRequestBody` stays **off**; `LoggerFactory` set explicitly (Sensitive-data, #7). No personal data logged. |
| 7 | Sensitive data | CreateUser/checks/tax bodies carry personal data (name, DOB, email, address, TIN). `LogRequestBody` OFF and `options.Logging.LoggerFactory` set explicitly so the `UPVESTINVESTMENTAPICLIENT_LOG` env var cannot force body logging on; our own logs never echo request bodies or personal fields. |
| 8 | Environment selection | One server group `Default`. `options.Server.Default.Production.BaseUrl` overridden to `Upvest:BaseUrl` verbatim (the run's mock); `Environment` left `Production`. No real-vs-sandbox ambiguity — the single configured base URL is the only target. |
| 9 | Duplicate prevention under concurrency | Enrolment row PK = shopper id → a second concurrent enrolment insert is refused by the store. Investment creation is single-threaded per shopper inside the ledger update transaction. See DUPLICATE CLAIMS. |
| 10 | Partial results | The only list reads (positions) are not used in the response surface; enrolment/settlement use single-resource reads. `none` material. |
| 11 | Unknown outcomes | A topup/order POST whose connection fails after the provider may have acted is settled by the worker re-reading by the stored idempotency key / stored order id before reporting failure. See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Enrolment (CreateUser chain) | `Enrolments` table, PK = shopper id | PK insert conflict on second enrolment | `InvestingService.EnrolAsync` catches the `AddAsync` conflict and returns the existing row | `InvestingService.EnrolAsync` (`AddAsync` claim precedes the `CreateInvestorAsync` SDK call) |
| Investment (topup+order) | `Investments` row created inside `RecordPaidOrderAsync` when the ledger crosses €10 (pending reset to 0 same call); stable random `TopupIdempotencyKey`/`OrderIdempotencyKey` on the row | one Investment row per threshold-crossing; the stored idempotency keys make a resent top-up/order replay at Upvest | `InvestingReconciler` acts on the row's stage; replay returns the original | `InvestingService.RecordPaidOrderAsync` (creates the row) → `InvestingReconciler.AdvanceInvestmentAsync` (`CreateTopupAsync` then `PlaceBuyOrderAsync`, each with the stored key) |

### PAGED READS

| Read | What caps it | How the caller learns it was cut short | Where in the code |
| --- | --- | --- | --- |
| none (no paged list is part of the response surface) | — | — | — |

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `TopUps.CreateTopup` | `CashBalances.RetrieveCashBalance` (available cash confirms the top-up) | account group id on the Investment's enrolment | `InvestingReconciler.AdvanceInvestmentAsync` AwaitingFunds stage; a re-driven top-up replays via the stored `TopupIdempotencyKey` | worker reconcile loop every 2 s |
| `Orders.PlaceOrder` | `Orders.RetrieveOrder` | stored `UpvestOrderId` (read from place response); a re-place replays via the stored `OrderIdempotencyKey` | `InvestingReconciler.AdvanceInvestmentAsync` AwaitingFill stage | worker reconcile loop every 2 s |
| enrolment POSTs | `Users.RetrieveUser` / `AccountsApi.RetrieveAccount` | stored `UpvestUserId`/`UpvestAccountId` | `InvestingReconciler.AdvanceEnrolmentAsync` | worker reconcile loop every 2 s |

## 6. Assumptions & Blockers

- **Assumption:** "paid" = order creation via `POST /api/orders` (no separate payment step exists). Round-up is set
  aside immediately for accepted investors. (Design decision, per task.)
- **Assumption:** funding the Upvest account group (TopUps) models the shop transferring the collected spare change
  before the ETF purchase; required because the mock cancels an unfunded buy. Funding IS exposed by the SDK
  (`TopUps`), so it is not a gap.
- **Assumption:** a test investor uses a CONCAT nationality (DE) so no separate user-identifier submission is needed
  (Upvest generates the CONCAT identifier; activation depends only on check + tax). Non-CONCAT nationalities would
  need `UserIdentifiers.CreateIdentifier`; handled where applicable, non-fatal to activation.
- **No Blockers.** Every capability the flows need is exposed by the SDK + workflow packages.
