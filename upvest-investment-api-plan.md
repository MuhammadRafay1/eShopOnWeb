# Upvest Investment API integration plan — "Invest your change" for eShopOnWeb

## 1. Scope & sequence

New capability in **src/PublicApi** (JWT auth; caller identity from token), additive to eShopOnWeb. All Upvest traffic goes through the SDK `UpvestInvestmentApiClient`, whose `HttpClient` has **one** `DelegatingHandler` (`UpvestAuthenticationHandler`) that (a) attaches the OAuth2 bearer token, (b) adds Upvest v15 HTTP-message-signature headers, (c) captures the raw response body/status for defensive parsing. No call site attaches credentials.

Build order:
1. Config binding (`Upvest:*`) + fail-fast validation + user-secrets loading (from env vars) — §5 row 1/2.
2. `UpvestRequestSigner` (ECDSA P-521 / SHA-512 / DER, v15 scheme) + `UpvestAuthenticationHandler` + `UpvestTokenProvider` (POST `/auth/token`, cached) — uses `AccessTokens.IssueToken`? No — token fetched by provider through the same handler (see §2 note). DI wiring for the SDK client.
3. `UpvestGateway` — thin wrapper over SDK ops used by the integration; each op is called, then success/error decided by **captured HTTP status**, and fields read from the **captured raw JSON** (because many 2xx bodies fail SDK strict-`required` deserialization — see §4). Ops used: `AccessTokens.IssueToken` (via provider), `Users.CreateUser`, `UserChecks.CreateUserCheck`, `TaxResidencies.SetTaxResidencies`, `Users.RetrieveUser`, `AccountGroups.CreateAccountGroup`, `AccountsApi.CreateAccount`, `AccountsApi.RetrieveAccount`, `TopUps.CreateTopup`, `Orders.PlaceOrder`, `Orders.RetrieveOrder`, `Orders.ListAccountOrders` (reconcile-by-reference).
4. Domain (ApplicationCore): `Investor` (enrolment: buyerId, upvestUserId, accountGroupId, accountId, status, idempotency keys), `RoundUpLedger`/pending balance, `Investment` (amount, upvestOrderId/clientReference, status). Persisted via EF Core (`Investor`/`Investment` aggregates), in-memory provider.
5. Application services: `EnrolmentService`, `RoundUpService`, `InvestingService`, reconciliation.
6. Order placement reuse: `POST /api/orders` builds the existing `Order` aggregate from catalog items (reusing `IOrderService`-style logic), marks it paid, then (never failing the request) sets aside round-up and, when the pending balance ≥ €10 and investor active, enqueues an investment.
7. Background `IHostedService` investment processor (fund → wait → buy → reconcile), own DI scope.
8. PublicApi endpoints (MinimalApi.Endpoint `IEndpoint` convention) under `/api/investing/*` and `/api/orders`.
9. Tests (xUnit, matching repo) for round-up math, ledger/threshold, signer base-string, response parsing, authz isolation.
10. Self-verify end-to-end against the live mock; write a verification guide.

A capability the SDK lacks is a Blocker (§6), not an invented path. None found — every needed call is an SDK operation.

## 2. CONTRACT SHEET

⚠ **Signatures are generated code, verbatim.** Each operation that takes input takes **one request record** as its first parameter, built with an object initializer whose property names are the record's own (never flat arguments). An operation with no inputs takes none.
⚠ **Every SDK type is written fully-qualified with the namespace its source path implies** (`Models/`→`UpvestInvestmentApi.Models`, `Models/AnyOf/`→`...Models.AnyOf`, `Models/Enums/`→`...Models.Enums`, `Requests/<Ctrl>/`→`...Requests.<Ctrl>`, `Errors/`→`...Errors`), taken from THAT type's own path.

Every request record below also carries header members `UpvestClientId: Guid`, `Authorization: string` (regex `^Bearer ...`), `Signature: string`, `SignatureInput: string` (and `IdempotencyKey: Guid` on POSTs), plus `UpvestApiVersion` (default `_1`). **Call sites pass placeholders** (`Authorization="Bearer placeholder"`, `Signature=""`, `SignatureInput=""`, `UpvestClientId`=configured client id, `IdempotencyKey`=fresh/stored Guid); the handler removes & re-adds the real `authorization`/`signature`/`signature-input`/`upvest-client-id`/`content-digest`/`upvest-signature-version` headers. This is the only way to honor the single-handler mandate given the generated required header members.

| Op (accessor.Method) | Request record + body | Response (read from RAW body) | Error case | Source |
| --- | --- | --- | --- | --- |
| `AccessTokens.IssueToken` | `IssueTokenRequest` (req: UpvestClientId, Signature, SignatureInput, ClientId:Guid, ClientSecret:string, Scope:string; GrantType default `client_credentials`). **Not used directly** — token acquired by `UpvestTokenProvider` posting form to `/auth/token` so the single handler signs it. | `AuthAccessToken` → `access_token`, `expires_in` | A `IssueTokenError`: `TryGetNoContent`[400,401,403,406,429,500,503,504] | map/operations/AccessTokens.md |
| `Users.CreateUser` | `CreateUserRequest`{ Body = `Models.AnyOf.UserCreateRequest` = `Models.UserTolCreateRequest`{FirstName,LastName,Email(req),BirthDate:DateTimeOffset(req),Nationalities:IReadOnlyList<Enums.Nationality>(req),Address:Models.Address(req),Fatca:Models.Fatca(req),PhoneNumber?} } | resp deser OK (`UserCreateRequest1`) but read RAW: `id`, `status` | A `CreateUserError` | Users.md; UserTolCreateRequest.cs |
| `UserChecks.CreateUserCheck` | `CreateUserCheckRequest`{ UserId:Guid, Body=`Models.AnyOf.UserCheckCreateRequest`=`Models.UserCheckKnowYourCustomerCreateRequest`{Type="KYC",CheckConfirmedAt:DateTimeOffset(req),DataDownloadLink(req),DocumentType:Enums.DocumentType3(req),Provider(req),Method:Enums.Method(req)} } | 202; raw: `id`,`status` | A `CreateUserCheckError` | UserChecks.md |
| `TaxResidencies.SetTaxResidencies` | `SetTaxResidenciesRequest`{ UserId, Body=`Models.TaxResidenciesSetRequest`{TaxResidencies:IReadOnlyList<`Models.AnyOf.TaxResidencyForCreateRequest`=`Models.WithTaxIdentifierNumber`{Country:Enums.Country(req),TaxIdentifierNumber(req)}>} } | **deser FAILS**; status 200 = success (no fields needed) | A `SetTaxResidenciesError` | TaxResidencies.md |
| `Users.RetrieveUser` | `RetrieveUserRequest`{ UserId } | deser OK (`UserGetResponse`); read RAW `status` (INACTIVE/ACTIVE) | A `RetrieveUserError`: NoContent[401,403,404,...] | Users.md |
| `AccountGroups.CreateAccountGroup` | `CreateAccountGroupRequest`{ Body=`Models.AnyOf.AccountGroupCreateRequest`=`Models.AccountGroupCreateUserRequest`{UserId:Guid(req),Type:Enums.Type13(req)=`Personal`} } | **deser FAILS**; status 2xx = success; read RAW `id`,`status` | A `CreateAccountGroupError` | AccountGroups.md |
| `AccountsApi.CreateAccount` | `CreateAccountRequest`{ Body=`Models.AnyOf.AccountCreateRequest`=`Models.AccountCreateUserRequest`{UserId(req),AccountGroupId:Guid(req),Type:Enums.Type16(req)=`Trading`} } | **deser FAILS**; raw `id`,`status` | A `CreateAccountError` | AccountsApi.md |
| `AccountsApi.RetrieveAccount` | `RetrieveAccountRequest`{ AccountId } | **deser FAILS**; raw `status` | A `RetrieveAccountError` | AccountsApi.md |
| `TopUps.CreateTopup` | `CreateTopupRequest`{ Body=`Models.PaymentsTopUpCreateRequest`{AccountGroupId:Guid(req),CashAmount:string(req) regex `^[0-9]{1,9}(\.[0-9]{2})?$`,Currency:Enums.Currency(req)=`Eur`} } | **deser FAILS**; raw `id`,`status` | A `CreateTopupError` | TopUps.md |
| `Orders.PlaceOrder` | `PlaceOrderRequest`{ Body=`Models.OrderPlaceRequest`{AccountId:Guid(req),Side:Enums.Side(req)=`Buy`,InstrumentId:string(req) ISIN regex,InstrumentIdType="ISIN"(fixed),CashAmount:string?,Currency:Enums.Currency29?=`Eur`,OrderType:Enums.OrderType?=`Market`,UserId:Guid?,UserInstrumentFitAcknowledgement:bool?,ClientReference:string?} } | 202; **deser FAILS** (Order39 missing fee/initiation_flow/executions); raw `id`,`status`,`client_reference` | A `PlaceOrderError`: NoContent[400,401,403,406,422,...] | Orders.md; OrderPlaceRequest.cs |
| `Orders.RetrieveOrder` | `RetrieveOrderRequest`{ OrderId } | **deser FAILS**; raw `status` (NEW/PROCESSING/FILLED/CANCELLED), `quantity`, `cash_amount` | A `RetrieveOrderError` | Orders.md |
| `Orders.ListAccountOrders` | `ListAccountOrdersRequest`{ AccountId } (query: user_id, status, limit…) | **deser FAILS**; raw `data[]` → match by `client_reference` | A `ListAccountOrdersError` | Orders.md |

Enums (static members → wire): `Side.Buy`→BUY; `OrderType.Market`→MARKET; `Currency.Eur`/`Currency29.Eur`→EUR; `Type13.Personal`→PERSONAL; `Type16.Trading`→TRADING; `Nationality.De`→"DE", `Country.De`→"DE"; `DocumentType3.Passport`→PASSPORT; `Method.VideoId`→VIDEO_ID; `UpvestApiVersion._1`→"1". (OpenStringEnum: static members, no `new`; wire via source.)

Client construction: `new UpvestInvestmentApiClient(httpClient, new UpvestInvestmentApiClientOptions{ Environment = ServerEnvironment.Production, Server = new ServerOptions{ Default = new(){ Production = { BaseUrl = cfg.BaseUrl } } }, OauthClientCredentials = null, Logging = <explicit, LogRequestBody off> })`. `OauthClientCredentials` left null → SDK adds no auth; our handler does. Namespaces: `UpvestInvestmentApi`, `UpvestInvestmentApi.Servers`.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| Account group/account creation requires the **user ACTIVE** (else 409) | `CreateAccountGroup`/`CreateAccount` ← `RetrieveUser` status | `EnrolmentReconciler` reconciles `RetrieveUser`→ACTIVE before creating group/account |
| An order's `account_id` must be an account this app created & made ACTIVE | `PlaceOrder` ← `CreateAccount`+`RetrieveAccount` | `InvestmentProcessor` checks stored `accountId` is ACTIVE (reconcile) before `PlaceOrder` |
| An order fills only if account group funded ≥ amount (else CANCELLED) | `PlaceOrder` ← `CreateTopup` | `InvestmentProcessor` tops up the exact invested amount, waits for settle, then places order |
| `instrument_id` must be the configured fund ISIN | `PlaceOrder` ← `Upvest:InstrumentId` | `InvestmentProcessor` uses `cfg.InstrumentId` verbatim |

## 3. Trap notes

- Single auth handler must sign the **final** outgoing request (SDK-injected `idempotency-key` + body present) and cover exactly the mock's required component set; signature-format & retry eligibility of `POST` differ from `GET`. — `MUST load dotnet-configuration-resilience`, `MUST load dotnet-client-initialization`.
- Credentials attached only in the handler; SDK OAuth left unset means "skipped not thrown" — a silent 401 if mis-wired. — `MUST load dotnet-authentication`.
- Building AnyOf request bodies (`UserCreateRequest`, `AccountGroupCreateRequest`, …) via factory/implicit, and OpenStringEnum members (not C# enums). — `MUST load dotnet-models`.
- Every SDK call needs a catch ladder; many 2xx bodies throw `ResponseDeserializationException`, distinct from `ApiException<TError>`; decide success by captured status. — `MUST load dotnet-error-handling`.
- Calling op = request-record-first; idempotency-key reuse semantics for retries. — `MUST load dotnet-calling-endpoints`.
- Faking the HttpClient seam for tests. — `MUST load dotnet-testing`.

## 4. REQUIRED READING (load before implementation)

- `upvest:dotnet-client-initialization` — HttpClient/handler lifetime + DI for the SDK client.
- `upvest:dotnet-authentication` — credential wiring (we bypass SDK OAuth via the handler).
- `upvest:dotnet-calling-endpoints` — request-record-first call shape, idempotency-key.
- `upvest:dotnet-models` — AnyOf union construction, OpenStringEnum usage.
- `upvest:dotnet-error-handling` — Case A `ApiException<TError>` vs `ResponseDeserializationException` vs `SdkConnectionException/SdkTimeoutException`. **Hazard (verbatim):** a 2xx with a missing `required` member, or a non-2xx body not matching `{Operation}Error`, surfaces as `ResponseDeserializationException` — an `ApiException` keeping HTTP status/target type but **not** `ApiException<TError>`; a ladder catching only `ApiException<TError>` lets it escape, so also catch `ResponseDeserializationException` (or `ApiException`). **In this integration most 2xx bodies hit exactly this**, so success is decided by the handler-captured status, not by whether deserialization succeeded.
- `upvest:dotnet-configuration-resilience` — retries/timeouts/base-URL/logging(secrets); `Timeout` per-attempt.
- `upvest:dotnet-testing` — the HttpClient test seam.

These are to be loaded before implementation; their contents are deliberately not copied here.

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | `UpvestOptions` bound from `Upvest:` section; a `IValidateOptions`/startup check refuses to start if any of ClientId, ClientSecret, SigningKeyId, SigningKeyPath, SigningKeyPassphrase, BaseUrl, InstrumentId is missing/blank, and if the signing key file can't be loaded/decrypted. Multi-part: every part checked (blank ≠ missing). |
| 2 | Secret sourcing & rotation | Values come from **.NET user-secrets** (loaded from env vars by a setup step, never written to repo files). Signer loads the EC key **once** (singleton) at startup; token acquired lazily & cached. Rotation requires process restart (documented); no hot-reload required for this task. |
| 3 | Total timeout budget | Per-call `CancellationToken` with a bounded deadline from endpoints; SDK `Retry` left at defaults (GET retried, POST not). Background investment processor uses its own linked timeout. `Timeout` is per-attempt — enforce whole-call bound via the token. |
| 4 | Write-retry ownership | Default `HttpMethodsToRetry` = GET/HEAD/PUT/OPTIONS → our POSTs (`/users`, `/orders`, `/payments/topups`, …) are **not** auto-resent by the SDK. Safe: topup/order carry stored idempotency keys so a manual retry is exactly-once. |
| 5 | Idempotency & ambiguous writes | Every POST sends `IdempotencyKey` (required member). For topup & place_order we **store** the key (and a unique `client_reference` on orders) on the `Investment`, so any retry reuses the same key → mock replays, no double fund/buy. Onboarding POSTs use fresh keys (create-once, guarded by stored upvestUserId). |
| 6 | Observability | Structured logs at Info for lifecycle (enrolled, activated, invested, settled) and Warning/Error for Upvest failures, each with the Upvest `request_id`/`id` from the captured body and our correlation id. `LogRequestBody` stays **off**. Personal data never logged (§7). |
| 7 | Sensitive data | Enrolment carries PII (name, email, birth date, address, tax id). `LogRequestBody` off **and** `options.Logging.LoggerFactory` set explicitly so `UPVESTINVESTMENTAPICLIENT_LOG` can't switch body logging on. Our own logs never echo request bodies/PII. Secrets (client secret, key passphrase) never logged/returned/written to repo. |
| 8 | Environment selection | One server group `Default`. `Environment=Production` with `Server.Default.Production.BaseUrl = Upvest:BaseUrl` (the local mock) — used verbatim; never the hard-coded sandbox default. Live (`Environment2`) unused. |
| 9 | Duplicate prevention under concurrency | The investment trigger (balance ≥ €10) is guarded: within a per-investor lock/transaction, create `Investment`(pending)+debit ledger atomically **before** any Upvest call; a second concurrent trigger sees balance < €10. See DUPLICATE CLAIMS. |
| 10 | Partial results | Only paged read is `ListAccountOrders` (reconcile-by-reference fallback); we request a large `limit` and match by `client_reference`; if not found we treat as still-unknown (don't mark settled). See PAGED READS. |
| 11 | Unknown outcomes | `PlaceOrder`/`CreateTopup` connection failures after the mock may have acted → reconcile by `ListAccountOrders` filtered by stored `client_reference`; topup reconciled by cash-balance/`Investment` state. Investment stays `pending` (never falsely `failed`) until an outcome is read. See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Investment (topup+order) when balance≥€10 | app EF store (`Investment` row + ledger debit, under a per-investor `SemaphoreSlim`) | inside the lock the balance is re-read and debited to 0 (`Investor.TakePendingForInvestment`); the 2nd caller sees `PendingAmountCents < 1000` so `CanInvest` is false | the lock re-reads fresh state before `CanInvest`; 2nd caller creates no `Investment` | `InvestingService.HandleOrderPaidAsync` (lock → re-read → `AddSetAside` → `CanInvest` → `TakePendingForInvestment` → `_investments.AddAsync` → `_queue.Enqueue`) |
| Enrolment (create_user) | app EF store (`Investor` row, unique index on `BuyerId`) | the per-investor lock serialises; the 2nd caller finds the existing `Investor` and returns it instead of creating another | the existing-row check inside the lock | `InvestingService.EnrolAsync` (lock → `FirstOrDefaultAsync` existing → else `_upvest.EnrolAsync` + `_investors.AddAsync`) |

### PAGED READS

| Read | What caps it | How the caller learns it was cut short | Where in the code |
| --- | --- | --- | --- |
| `ListAccountOrders` (reconcile-by-reference fallback) | `limit` query param (default 100) | match is by `client_reference`; if absent in the page, the method returns `null` and the `Investment` is left `pending` (never settled from a truncated page) | `UpvestGateway.FindOrderByReferenceAsync` (returns `null` when no match); consumed by `InvestingService.ReconcileInvestmentsAsync` |

### UNKNOWN OUTCOMES

| Write | Re-read with | Reference searched by | Where in the code | Test that fails the connection |
| --- | --- | --- | --- | --- |
| `Orders.PlaceOrder` | `Orders.ListAccountOrders` (raw) | stored `Investment.ClientReference` | `UpvestGateway.PlaceInvestmentOrderAsync` throws `UpvestUnavailableException` on transport failure; `InvestmentProcessor.ProcessAsync` leaves the `Investment` `pending` (no `UpvestOrderId`); `InvestingService.ReconcileInvestmentsAsync` then calls `FindOrderByReferenceAsync(accountId, ClientReference)` on the next read and links/settles it | Settlement verified live (order FILLED → `settled`); `ReconcileInvestmentsAsync` null-`UpvestOrderId` branch is the settling code. No dedicated fault-injection test added. |
| `TopUps.CreateTopup` | re-send with the stored `Investment.TopupIdempotencyKey` (mock replays) | `Investment.TopupIdempotencyKey` (+ `AccountGroupId`) | `UpvestGateway.FundAsync` sends the stored key; `InvestmentProcessor` advances to `PlaceOrder` only once `Investment.Funded` is set, so a retry re-sends the same key (no double-fund) | Idempotency-key reuse verified by design (key stored on `Investment`). No dedicated fault test. |

## 6. Assumptions & Blockers

- **Assumption (YOUR CALL):** `UPVEST_BASE_URL` is a local mock implementing Upvest v15 HTTP signatures (verified live). Signing scheme calibrated against it; all flows run end-to-end.
- **Assumption (YOUR CALL):** Identifiers step is skipped — the onboarding reference says CONCAT nationalities (AT,DE,FR,HU,IE,LU) need none, and the runtime only requires a check + tax residency to activate; the test investor uses nationality the shopper supplies (defaulting handling for non-CONCAT documented). No `UserIdentifiers` write is needed.
- **Assumption (YOUR CALL):** Order settlement is reconciled by reading `RetrieveOrder` (pull), authoritative and deterministic; webhooks (the push alternative) are not required for correctness and are omitted to avoid callback-reachability/TLS coupling. `UPVEST_CALLBACK_BASE_URL` is bound (for a future webhook receiver) but not depended on.
- **No Blockers.** Every needed capability is an SDK operation; response-shape strictness is handled per the documented `ResponseDeserializationException` path, not worked around.
