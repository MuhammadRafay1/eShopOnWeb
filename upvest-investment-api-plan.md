# Upvest Investment API — integration plan ("Invest your change")

SDK: Upvest Investment API .NET SDK (`UpvestInvestmentApi`), NuGet `Up-v-ApimaticSDK`, spec `1.150.0`, netstandard2.0.
All contract facts below are grounded from the SDK map / source this session. `YOUR CALL` rows are application decisions.

## 0. Environment facts (grounded from env + eShop repo)

- `Upvest:BaseUrl` = a **local mock** (`http://127.0.0.1:…`), NOT sandbox.upvest.co. Override `options.Server.Default.Production.BaseUrl` with it verbatim; keep `Environment = Production`.
- Signing key: EC **P-521** (`secp521r1`), PKCS#8 **encrypted** PEM → RFC 9421 alg `ecdsa-p521-sha512`.
- `Upvest:CallbackBaseUrl` = PublicApi's externally reachable https base (host for webhooks).
- `Upvest:InstrumentId` = 12-char **ISIN** (matches `OrderPlaceRequest.InstrumentId` regex `^[A-Z]{2}[A-Z0-9]{9}[0-9]$`).
- PublicApi serves `https://localhost:36823` (= callback) & `http://localhost:36824`; JWT identity → `ClaimTypes.Name` (username/email).
- eShop `Order`(BuyerId, ShipToAddress, List<OrderItem>), `OrderItem`(CatalogItemOrdered, UnitPrice, Units), persisted via `IRepository<Order>` on `CatalogContext` (in-memory). Money = `decimal`, euros.

## 1. Scope & sequence

1. **Client+DI+auth handler** — register `UpvestInvestmentApiClient` over a named `HttpClient` whose pipeline includes ONE `UpvestAuthenticationHandler` (HTTP message signing + bearer attach). Base URL override. Fail-fast settings binding.
2. **Enrolment (opt-in)** `POST /api/investing/enrolment`: `Users.CreateUser` (TOL) → `UserChecks.CreateUserCheck` (KYC, then INSTRUMENT_FIT) → `TaxResidencies.SetTaxResidencies` → (conditional, non-CONCAT nationality) `UserIdentifiers.CreateIdentifier` → `AccountGroups.CreateAccountGroup` (PERSONAL) → `AccountsApi.CreateAccount` (TRADING) → `WebhookSubscriptions.CreateWebhook` (callback URL). Persist `Investor` (status `pending`).
3. **Enrolment status** `GET /api/investing/enrolment`: refresh from `Users.RetrieveUser` + `AccountsApi.RetrieveAccount`; map to pending/active/rejected.
4. **Order round-up** `POST /api/orders`: create eShop `Order`; if investor active, set aside `ceil(total)-total`; add to ledger. Investing side-effects wrapped so order never fails.
5. **Invest** when set-aside ≥ €10: `VirtualCashBalances.CreateVirtualCashIncrease` (fund account group) → `Orders.PlaceOrder` (nominal BUY, cash_amount=balance, MARKET). Record `Investment` (pending), reset balance.
6. **Settlement** Flow 4: `Orders.RetrieveOrder` (poll on reads) + webhook endpoint `POST /api/investing/upvest/webhook` (no JWT). Map order status → settled/failed.
7. **Investments** `GET /api/investing/investments`; **Balance** `GET /api/investing/balance`.

## 2. CONTRACT SHEET

⚠ Signatures are generated code, verbatim: each operation takes ONE request record as its first parameter, built with an object initializer using the record's own property names (never flat args). Every SDK type is written fully-qualified with the namespace its source path implies (records→`UpvestInvestmentApi.Models`, unions→`.Models.AnyOf`, enums→`.Models.Enums`, requests→`.Requests.<Controller>`, errors→`.Errors`).

Every operation requires header params `UpvestClientId` (Guid), `Authorization` (string), `Signature` (string), `SignatureInput` (string); create-style ops also `IdempotencyKey` (Guid). These map to headers `upvest-client-id`, `authorization`, `signature`, `signature-input`, `idempotency-key`. **The call site passes placeholders; the DelegatingHandler attaches the real signature + bearer.** `UpvestApiVersion` defaults `_1`→"1".

| Op (client.X.Op) | request record + members | body model + fields (wire) | response (fields read) | error | source |
| --- | --- | --- | --- | --- | --- |
| `AccessTokens.IssueToken` | `IssueTokenRequest`{ UpvestClientId:Guid, Signature, SignatureInput, ClientId:Guid, ClientSecret, Scope, GrantType="client_credentials", UpvestApiVersion } | form body: client_id, client_secret, grant_type, scope (headers: upvest-client-id, signature, signature-input, upvest-api-version, Idempotency-Key) | `AuthAccessToken` (access_token, expires_in, token_type) | A `IssueTokenError` (`TryGetNoContent`) | AccessTokens.md; Requests/AccessTokens/IssueTokenRequest.cs; Models/AuthAccessToken.cs |
| `Users.CreateUser` | `CreateUserRequest`{ +IdempotencyKey, Body:`UserCreateRequest?` } | `UserCreateRequest` union → `UserTolCreateRequest`: first_name*, last_name*, email*, birth_date*(DateTimeOffset), nationalities*(≥1 `Nationality`), address*(`Address`), fatca*(`Fatca`), phone_number?, +opt | `UserCreateRequest1` union → `TryGetUserTol`→`UserTol`{ id:Guid, status:`Status` } | A `CreateUserError` (`TryGetNoContent`[400,401,403,406,429,500,503,504]) | Users.md; Requests/Users/CreateUserRequest.cs; Models/AnyOf/UserCreateRequest.cs; Models/UserTolCreateRequest.cs; Models/AnyOf/UserCreateRequest1.cs; Models/UserTol.cs |
| `Users.RetrieveUser` | `RetrieveUserRequest`{ UserId:Guid, +auth } | — | `UserGetResponse` union → `TryGetUserTol`→`UserTol.Status` (`Status`: Active/Inactive/Offboarding/Offboarded) | A `RetrieveUserError` (`TryGetNoContent`[401,403,404,406,429,500,503,504]) | Users.md; Models/AnyOf/UserGetResponse.cs |
| `UserChecks.CreateUserCheck` | `CreateUserCheckRequest`{ UserId:Guid, +auth, Body:`UserCheckCreateRequest?` } | `UserCheckCreateRequest` union → KYC:`UserCheckKnowYourCustomerCreateRequest`{ check_confirmed_at*, data_download_link*, document_type*(`DocumentType3`), provider*, method*(`Method`) }; FIT:`UserCheckInstrumentFitCreateRequest`{ check_confirmed_at*, instrument_suitability*(`InstrumentSuitability`{suitability:bool}) } | `UserCheckCreateResponse`{ id:Guid } | A `CreateUserCheckError` | UserChecks.md; Models/AnyOf/UserCheckCreateRequest.cs; Models/UserCheckKnowYourCustomerCreateRequest.cs; Models/UserCheckInstrumentFitCreateRequest.cs |
| `TaxResidencies.SetTaxResidencies` | `SetTaxResidenciesRequest`{ UserId:Guid, +auth, +IdempotencyKey, Body:`TaxResidenciesSetRequest?` } | `TaxResidenciesSetRequest`{ tax_residencies*(≥1 `TaxResidencyForCreateRequest` union → `WithTaxIdentifierNumber`{ country*(`Country`), tax_identifier_number* } ) } | `TaxResidencyRecord`{ status:`Status63` } | A `SetTaxResidenciesError` | TaxResidencies.md; Models/TaxResidenciesSetRequest.cs; Models/AnyOf/TaxResidencyForCreateRequest.cs; Models/WithTaxIdentifierNumber.cs |
| `UserIdentifiers.CreateIdentifier` | `CreateIdentifierRequest`{ UserId:Guid, +auth, Body:`IdentifierCreateRequest?` } | `IdentifierCreateRequest`{ type?(`Type7`=NATIONAL_ID), issuing_country*(`IssuingCountry`), identifier?, identifier_standard? } | `Identifier` | A `CreateIdentifierError` | UserIdentifiers.md; Models/IdentifierCreateRequest.cs |
| `AccountGroups.CreateAccountGroup` | `CreateAccountGroupRequest`{ +auth, +IdempotencyKey, Body:`AccountGroupCreateRequest?` } | union → `AccountGroupCreateUserRequest`{ user_id*(Guid), type*(`Type13`=Personal) } | `AccountGroupCreateResponse` union → `TryGetAccountGroup`→`AccountGroup`{ id:Guid, status:`Status18` } | A `CreateAccountGroupError` | AccountGroups.md; Models/AnyOf/AccountGroupCreateRequest.cs; Models/AccountGroupCreateUserRequest.cs; Models/AnyOf/AccountGroupCreateResponse.cs; Models/AccountGroup.cs |
| `AccountsApi.CreateAccount` | `CreateAccountRequest`{ +auth, +IdempotencyKey, Body:`AccountCreateRequest?` } | union → `AccountCreateUserRequest`{ user_id*, account_group_id*(Guid), type*(`Type16`=Trading), name? } | `AccountCreateResponse` union → `TryGetAccount`→`Account`{ id:Guid, status:`Status21` } | A `CreateAccountError` | AccountsApi.md; Models/AnyOf/AccountCreateRequest.cs; Models/AccountCreateUserRequest.cs; Models/AnyOf/AccountCreateResponse.cs; Models/Account.cs |
| `AccountsApi.RetrieveAccount` | `RetrieveAccountRequest`{ AccountId:Guid, +auth } | — | `AccountRetrieveResponse` union → `TryGetAccount`→`Account.Status`(`Status21`: PendingApproval/Active/Closing/Closed/Locked) | A `RetrieveAccountError` | AccountsApi.md; Models/AnyOf/AccountRetrieveResponse.cs |
| `WebhookSubscriptions.CreateWebhook` | `CreateWebhookRequest`{ +auth, Body:`WebhookCreateRequest?` } | `WebhookCreateRequest`{ title*(regex `^[a-zA-Z0-9 ()\[\]{}.-]{1,32}$`), url*(≤1000), type?(list `TypeEnum`) } | `Webhook`{ id:Guid } | A `CreateWebhookError` | WebhookSubscriptions.md; Models/WebhookCreateRequest.cs; Models/Webhook.cs |
| `VirtualCashBalances.CreateVirtualCashIncrease` | `CreateVirtualCashIncreaseRequest`{ +auth, +IdempotencyKey, Body:`VirtualCashBalanceVirtualCashIncreaseCreateRequest?` } | { account_group_id*(Guid), amount*(string `^[0-9]{1,9}(\.[0-9]{2})?$`), currency*(`Currency1`=Eur) } | `VirtualCashBalanceVirtualCashIncrease` | A `CreateVirtualCashIncreaseError` | VirtualCashBalances.md; Requests/VirtualCashBalances/CreateVirtualCashIncreaseRequest.cs; Models/VirtualCashBalanceVirtualCashIncreaseCreateRequest.cs; Models/Enums/Currency1.cs |
| `Orders.PlaceOrder` | `PlaceOrderRequest`{ +auth, +IdempotencyKey, Body:`OrderPlaceRequest?` } | `OrderPlaceRequest`{ account_id*(Guid), side*(`Side`=Buy), instrument_id*(ISIN), cash_amount?(string, XOR quantity), currency?(`Currency29`=Eur), order_type?(`OrderType`=Market), user_id?, user_instrument_fit_acknowledgement?=true; instrument_id_type is read-only const "ISIN" (set by SDK) } | `Order39`{ id:Guid, status:`Status51`, cash_amount, executions } | A `PlaceOrderError` (`TryGetNoContent`[400,401,403,406,422,429,500,503,504]) | Orders.md; Models/OrderPlaceRequest.cs; Models/Order39.cs |
| `Orders.RetrieveOrder` | `RetrieveOrderRequest`{ OrderId:Guid, +auth } | — | `Order39.Status`(`Status51`: New/Processing/Filled/Cancelled), `Order39.CancellationReason?` | A `RetrieveOrderError` | Orders.md; Models/Order39.cs; Models/Enums/Status51.cs |

Enums (C# member → wire): `Currency29/Currency1`.Eur→"EUR"; `Side`.Buy→"BUY"; `OrderType`.Market→"MARKET"; `Type13`.Personal→"PERSONAL"; `Type16`.Trading→"TRADING"; `Status`(user).Active→"ACTIVE"/.Inactive→"INACTIVE"; `Status18`/`Status21`.Active→"ACTIVE"/.PendingApproval→"PENDING_APPROVAL"; `Status51`.New/Processing/Filled/Cancelled; `Type7`.NationalId→"NATIONAL_ID"; `Nationality`/`Country`/`IssuingCountry`.De→"DE" (open string enums, any ISO code). `UpvestApiVersion._1`→"1".

Client: `new UpvestInvestmentApiClient(httpClient, options)`; options: `Environment=ServerEnvironment.Production`, `Server.Default.Production.BaseUrl=<Upvest:BaseUrl>`, leave `OauthClientCredentials` **unset** (handler owns auth). Source: UpvestInvestmentApiClient.cs, ServerOptions.cs, Servers/DefaultOptions.cs.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| `account_group_id` funded/ordered-against must be one this app created | `VirtualCashBalances.CreateVirtualCashIncrease`/`Orders.PlaceOrder` ← `AccountGroups.CreateAccountGroup` | stored on `Investor.UpvestAccountGroupId` at enrolment; read from the investor row before fund/order |
| `account_id` ordered-against must be one this app created | `Orders.PlaceOrder` ← `AccountsApi.CreateAccount` | stored on `Investor.UpvestAccountId`; read before order |
| `user_id` must be the one this app created | all user-scoped ops ← `Users.CreateUser` | stored on `Investor.UpvestUserId`; read before each call |
| `instrument_id` (ISIN) | `Orders.PlaceOrder` ← `Upvest:InstrumentId` config | from config; the only instrument this app offers |
| `OrderId` re-read must be one returned by PlaceOrder | `Orders.RetrieveOrder` ← `Orders.PlaceOrder` | stored on `Investment.UpvestOrderId` |

## 3. Trap notes

- Client/DI lifetime & attaching the DelegatingHandler to a **named** HttpClient; singleton client + `PooledConnectionLifetime`. — hazard: wrong lifetime = empty token cache per call / stale DNS. **MUST load dotnet-client-initialization**
- Auth: I bypass the SDK's built-in OAuth and attach bearer+signature in the handler; fail-fast on missing credentials. — hazard: an unset credential is a silent no-op → 401 one layer away. **MUST load dotnet-authentication**
- Building union/enum request bodies (`UserCreateRequest`, `AccountCreateRequest`, open-string enums, `required` members). — hazard: enums are not C# enums; unions via factory not `new`. **MUST load dotnet-models**
- Calling ops / idempotency-key semantics / per-op required members. — hazard: injected `Idempotency-Key` guid is not a real key; which member is the real one. **MUST load dotnet-calling-endpoints**
- Error boundary across all Upvest calls incl. drift. — hazard: `ResponseDeserializationException` escapes a `catch (ApiException<TError>)`-only ladder. **MUST load dotnet-error-handling**
- Base-URL override, per-attempt vs total timeout, retry eligibility of POST, logging/`LogRequestBody` redaction. — hazard: `Timeout` is per attempt; JSON bodies logged unredacted; PII in body. **MUST load dotnet-configuration-resilience**
- Faking the SDK seam in tests (HttpClient). — hazard: test the behaviour, not SDK internals. **MUST load dotnet-testing**

## 4. REQUIRED READING (load BEFORE implementation; sheet deliberately omits their contents)

- `upvest:dotnet-client-initialization` — step 1 client/DI.
- `upvest:dotnet-authentication` — step 1 credentials + fail-fast.
- `upvest:dotnet-calling-endpoints` — steps 2–7 every call.
- `upvest:dotnet-models` — building TOL/union/enum payloads.
- `upvest:dotnet-error-handling` — the error boundary (always). Hazard row (verbatim): a drifted/malformed **2xx** (missing `required`) or a **non-2xx** body not matching the op's `{Operation}Error` shape surfaces as `ResponseDeserializationException` — an `ApiException` keeping the HTTP status/target type but **not** `ApiException<TError>`; a ladder handling only `ApiException<TError>` lets it escape, so also catch `ResponseDeserializationException` (or `ApiException`).
- `upvest:dotnet-configuration-resilience` — base URL, timeout budget, retry, logging.
- `upvest:dotnet-testing` — integration tests.

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | Bind `Upvest:` → `UpvestSettings` with `[Required]` on ClientId, ClientSecret, SigningKeyId, SigningKeyPath, SigningKeyPassphrase, BaseUrl, InstrumentId, CallbackBaseUrl; `.ValidateDataAnnotations().ValidateOnStart()`. Also verify signing key file loads (decrypt with passphrase) at startup via a hosted startup check → refuse to boot otherwise. Every part checked (a blank part ≠ missing). |
| 2 | Secret sourcing & rotation | Secrets from .NET user-secrets (loaded from env by me, never written to repo). DI builds options once at registration (singleton) → rotation needs process restart. Acceptable for this app; documented. |
| 3 | Total timeout budget | Named HttpClient `Timeout`=30s (bounds one attempt). Each Upvest call also gets a `CancellationToken` with an overall deadline (≤60s) from the handler/caller; order round-up invest path uses a short budget so POST /api/orders returns promptly. |
| 4 | Write-retry ownership | Default `HttpMethodsToRetry` = GET/HEAD/PUT/OPTIONS, so POST (CreateUser, PlaceOrder, VirtualCashIncrease, token) is **never** auto-resent — safe (they carry idempotency keys anyway). I keep SDK retry at default; GET RetrieveOrder/RetrieveUser are retryable (idempotent). |
| 5 | Idempotency & ambiguous writes | Real caller key: `IdempotencyKey` member on create ops (CreateUser, SetTaxResidencies, CreateAccountGroup, CreateAccount, PlaceOrder, VirtualCashIncrease). I generate a **stable** key derived from the business action (see DUPLICATE CLAIMS) so a retry replays rather than duplicates. Token/CreateWebhook/CreateUserCheck take no real key → reconcile by re-reading (RetrieveUser/ListWebhooks) before re-issuing. |
| 6 | Observability | Structured logs at Info for lifecycle (enrolment created, invested, settled), Warning for swallowed investing failures, Error for startup/auth. `LogRequestBody` stays **off**. Correlation: log Upvest error status + raw body string (non-PII) from the error boundary. |
| 7 | Sensitive data | Enrolment carries PII (name, email, birth date, address, taxId). `LogRequestBody` off **and** `options.Logging.LoggerFactory` set explicitly (prevents `UPVESTINVESTMENTAPICLIENT_LOG` env from switching body logging on). PII never logged by my code; PII not persisted beyond what Upvest needs (I store only Upvest ids + status, not the raw form). |
| 8 | Environment selection | One server group `Default`; `Environment=Production`, `BaseUrl` overridden to `Upvest:BaseUrl` (the mock). No live host is ever contacted. |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. Claim stored as a row in `CatalogContext` keyed by a unique column the store enforces (primary key / alternate key), written before the SDK call. |
| 10 | Partial results | No paged read is relied on (I store the single order id and re-read it directly). `none`. |
| 11 | Unknown outcomes | See UNKNOWN OUTCOMES — PlaceOrder/VirtualCashIncrease connection failure after provider may have acted is reconciled by re-reading by stored id / listing. |

**DUPLICATE CLAIMS**

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Enrolment (CreateUser + chain) | `Investor` row (unique index on `ShopperId`, `InvestorConfiguration`) | pre-insert existence check + unique index (SQL Server) | existence check returns the existing investor; failed registration deletes the claim | `InvestingService.EnrolAsync` — `_investors.FirstOrDefaultAsync` check, then `_investors.AddAsync(investor)` **before** `_gateway.RegisterInvestorAsync` |
| Invest (PlaceOrder) | `Investment` row persisted with a stable `IdempotencyKey` | the pending `Investment` is created and saved before the provider call; the balance is zeroed so a second crossing cannot re-trigger | the invest side-effect runs once per crossing; distinct per-operation keys make Upvest replay | `InvestingService.TryInvestAsync` — `investor.BeginInvestment()` + `_investors.UpdateAsync` **before** `_gateway.PlaceInvestmentAsync` |

Note: the environment forces the EF **in-memory** provider, which does not enforce unique indexes; the unique index on `ShopperId` is enforced under SQL Server. Within a single run, per-shopper writes are sequential, so the pre-insert check holds.

**PAGED READS**: `none` (single-id reads only).

**UNKNOWN OUTCOMES**

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `Orders.PlaceOrder` | `GetInvestmentStatusAsync` (RetrieveOrder) or `FindInvestmentByReferenceAsync` (ListAccountOrders) | stored `Investment.UpvestOrderId`, else `Investment.ClientReference` | `InvestingService.SettleFailedPlacementAsync` leaves the investment **pending** when `OutcomeUnknown`; `ReconcilePendingInvestmentsAsync` settles it on the next read by order id, or by client_reference when the id was never captured | `InvestingServiceTests.ApplyPaidOrder_NeverThrows_WhenInvestingFails` (gateway throws; order still succeeds, investment not lost) |
| `VirtualCashBalances.CreateVirtualCashIncrease` | idempotent re-issue | stable derived key `DeriveKey(IdempotencyKey, "fund")` | `UpvestGateway.PlaceInvestmentAsync` funds with a stable key before placing the order, so a retry replays rather than double-funds | covered by the same test (funding failure surfaces as an unknown-outcome gateway exception) |

## 6. Assumptions & Blockers

- **Assumption (YOUR CALL):** "paid" = order creation via `POST /api/orders` (no separate payment step exists in eShop). Round-up is set aside at that moment.
- **Assumption:** invest the **whole** current set-aside balance when it reaches €10 (balance may exceed €10 by one round-up); reset to 0 after.
- **Assumption:** map Upvest order `Status51` Filled → investment `settled`; Cancelled → `failed`; New/Processing → `pending`. Enrolment: user+account `ACTIVE` → `active`; user Offboarded/Offboarding → `rejected`; else `pending`.
- **Assumption:** test investors use made-up DE (CONCAT) details → `UserIdentifiers.CreateIdentifier` is skipped for CONCAT nationalities (AT,DE,FR,HU,IE,LU); for non-CONCAT the endpoint still enrolls (identifier best-effort from taxId). FATCA defaulted to `status=false, confirmed_at=now` (form has no FATCA field).
- **UNVERIFIED (live-traffic only):** exact HTTP Message Signature components/label the mock verifier requires. Defensive directive: implement RFC 9421 `ecdsa-p521-sha512` covering `@method @authority @path @query` + `content-digest` (sha-512) for bodies, label `sig1`, params `created`/`keyid`/`alg`; log a clear signing/401 diagnostic; adjust covered set if the mock rejects during self-verification.
- No Blockers: every required capability (onboarding, checks, tax, account group/account, webhook, funding, order, order status) is exposed by the SDK map.
