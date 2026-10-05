# Upvest Investment API integration plan — "Invest your change" for eShopOnWeb

## 1. Scope & sequence

Additive "Invest your change" capability on `src/PublicApi` (JWT). Persistence: new EF aggregates in `ApplicationCore`/`Infrastructure` (works with in-memory provider). All Upvest traffic goes through the **Up-v-ApimaticSDK** client whose `HttpClient` carries one `UpvestAuthDelegatingHandler` that attaches OAuth bearer + HTTP message signature + `upvest-client-id` to every outgoing request; no call site attaches credentials.

Build order:
1. **Config & secrets** — bind `Upvest:*` into `UpvestSettings`; fail-fast at startup (every part non-blank); load values from env into user-secrets (never into repo files).
2. **SDK client + DI** — `IHttpClientFactory`-managed named client → `UpvestInvestmentApiClient` (singleton options captured at registration). Pipeline handler = `UpvestAuthDelegatingHandler`.
3. **Auth internals** — `IUpvestTokenProvider` (caches token from `AccessTokens.IssueToken`); `IUpvestRequestSigner` (HTTP message signature from the PEM signing key). Handler uses both.
4. **Domain** — `InvestorEnrolment`, `ChangeLedger` (set-aside balance), `Investment` aggregates + EF configs + DbSets.
5. **Upvest gateway** — `IUpvestInvestingGateway` wrapping the SDK operations below (enrol, poll acceptance, place order, settle), with the error boundary.
6. **Application service** — `IInvestingService`: enrol, record round-up on paid order, invest-when-threshold, reconcile acceptance/settlement.
7. **Endpoints** — the 7 routes under `/api/` (6 investing + `POST /api/orders`), JWT; identity from token `ClaimTypes.Name`.
8. **Order paid hook** — `POST /api/orders` creates an eShop `Order`, marks it paid (design: an order placed through this endpoint is paid-on-placement), triggers round-up + invest attempt in a way that never fails the order.
9. **Tests** — integration tests against PublicApi with a stub Upvest gateway seam; unit tests for round-up + threshold.
10. **Self-verify** against the local mock (`Upvest:BaseUrl`), iterating the signature profile from the mock's responses.

A capability the map lacks is a Blocker (§6), not an invented path.

## 2. CONTRACT SHEET

⚠ **Signatures are generated code, verbatim.** Each operation that takes input takes ONE request record as its first parameter (object initializer, the record's own property names — never flat args). An operation with no inputs takes none.
⚠ **Every SDK type is written fully-qualified with the namespace its source path implies** (taken from the path the map gives for THAT type, never a neighbour's). Models → `UpvestInvestmentApi.Models`; enums → `UpvestInvestmentApi.Models.Enums`; unions → `UpvestInvestmentApi.Models.AnyOf`; request records → `UpvestInvestmentApi.Requests.<Controller>`; errors → `UpvestInvestmentApi.Errors`; client/options → `UpvestInvestmentApi`; `ServerEnvironment` → `UpvestInvestmentApi.Servers`.

All operations: **Case A typed error**, accessors `TryGetNoContent(out RawError)` + `TryGetRawError(out RawError)`; throw-only; no pagination unless a query offset/limit is listed; server group `Default` (base URL = `Upvest:BaseUrl`). Headers `Authorization`/`Signature`/`SignatureInput`/`upvest-client-id`/`Idempotency-Key` are set by the delegating handler — call sites pass `""`/placeholder for the credential params; `UpvestClientId`/`IdempotencyKey` passed as real `Guid` values from config/record.

| Operation | Controller · signature | Request record (members) | Body model (fields, wire) | Response (fields read) | Error | Source |
| --- | --- | --- | --- | --- | --- | --- |
| IssueToken | `client.AccessTokens.IssueToken(IssueTokenRequest)` | req req: `UpvestClientId:Guid`, `Signature:string`, `SignatureInput:string`, `ClientId:Guid`, `ClientSecret:string`, `Scope:string`; `GrantType="client_credentials"`, `UpvestApiVersion=_1` | n/a (form-encoded by SDK) | `AuthAccessToken`: `AccessToken(access_token)`, `ExpiresIn(expires_in)`, `TokenType(token_type)`, `Scope(scope)` | `IssueTokenError` | AccessTokens.md; Requests/AccessTokens/IssueTokenRequest.cs; Models/AuthAccessToken.cs |
| CreateUser | `client.Users.CreateUser(CreateUserRequest)` | req: `UpvestClientId`, `Authorization`, `Signature`, `SignatureInput`, `IdempotencyKey:Guid`; `UpvestApiVersion=_1`; `Body:UserCreateRequest?` | `UserCreateRequest.UserTolCreateRequest(UserTolCreateRequest)`: req `FirstName(first_name)`, `LastName(last_name)`, `Email(email)`, `BirthDate(birth_date):DateTimeOffset`, `Nationalities(nationalities):IReadOnlyList<Nationality>` (min 1), `Address(address):Address`, `Fatca(fatca):Fatca`; opt `PhoneNumber(phone_number)`, `TermsAndConditions`, `DataPrivacyAndSharingAgreement` | `UserCreateRequest1` union → `TryGetUserTol(out UserTol)`: `Id:Guid`, `Status` (deprecated — do NOT use for acceptance) | `CreateUserError` | Users.md; Requests/Users/CreateUserRequest.cs; Models/AnyOf/UserCreateRequest.cs; Models/UserTolCreateRequest.cs; Models/AnyOf/UserCreateRequest1.cs; Models/UserTol.cs |
| CreateAccountGroup | `client.AccountGroups.CreateAccountGroup(CreateAccountGroupRequest)` | req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey`; `Body:AccountGroupCreateRequest?` | `AccountGroupCreateRequest.AccountGroupCreateUserRequest(AccountGroupCreateUserRequest)`: req `UserId(user_id):Guid`, `Type(type):Type13=Personal` | `AccountGroupCreateResponse` union → `TryGetAccountGroup(out AccountGroup)`: `Id:Guid`, `Status:Status18` | `CreateAccountGroupError` | AccountGroups.md; Models/AnyOf/AccountGroupCreateRequest.cs; Models/AccountGroupCreateUserRequest.cs; Models/AnyOf/AccountGroupCreateResponse.cs; Models/AccountGroup.cs |
| RetrieveAccountGroup | `client.AccountGroups.RetrieveAccountGroup(RetrieveAccountGroupRequest)` | req: `AccountGroupId:string`, `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput` | n/a | `AccountGroupRetrieveResponse` union → `TryGetAccountGroup(out AccountGroup)`: `Status:Status18` | `RetrieveAccountGroupError` | AccountGroups.md; Models/AnyOf/AccountGroupRetrieveResponse.cs |
| CreateAccount | `client.AccountsApi.CreateAccount(CreateAccountRequest)` | req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey`; `Body:AccountCreateRequest?` | `AccountCreateRequest.AccountCreateUserRequest(AccountCreateUserRequest)`: req `UserId(user_id)`, `AccountGroupId(account_group_id):Guid`, `Type(type):Type16=Trading` | `AccountCreateResponse` union → `TryGetAccount(out Account)`: `Id:Guid`, `Status:Status21` | `CreateAccountError` | AccountsApi.md; Models/AnyOf/AccountCreateRequest.cs; Models/AccountCreateUserRequest.cs; Models/AnyOf/AccountCreateResponse.cs; Models/Account.cs |
| PlaceOrder | `client.Orders.PlaceOrder(PlaceOrderRequest)` | req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey`; `Body:OrderPlaceRequest?` | `OrderPlaceRequest`: req `AccountId(account_id):Guid`, `Side(side):Side=Buy`, `InstrumentId(instrument_id):string` (ISIN); `InstrumentIdType="ISIN"`; opt `UserId(user_id)`, `CashAmount(cash_amount):string "^[0-9]{1,9}(\.[0-9]{2})?$"`, `Currency(currency):Currency29=Eur`, `OrderType(order_type):OrderType`, `ClientReference(client_reference)` | `Order39`: `Id:Guid`, `Status:Status51` (NEW/PROCESSING/FILLED/CANCELLED), `CashAmount`, `CreatedAt`, `CancellationReason?` | `PlaceOrderError` | Orders.md; Models/OrderPlaceRequest.cs; Models/Order39.cs |
| RetrieveOrder | `client.Orders.RetrieveOrder(RetrieveOrderRequest)` | req: `OrderId:string`, `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput` | n/a | `Order39`: `Status:Status51`, `CancellationReason?` | `RetrieveOrderError` | Orders.md; Models/Order39.cs |
| ListAccountOrders (reconcile) | `client.Orders.ListAccountOrders(ListAccountOrdersRequest)` | req: `AccountId:string`, creds; query opt `Status`, `Limit`, `Offset` | n/a | `OrdersListResponse` (list of Order39; match by `ClientReference`) | `ListAccountOrdersError` | Orders.md; Models/OrdersListResponse.cs |

Enum members used: `Type13.Personal`; `Type16.Trading`; `Side.Buy`; `Currency29.Eur`; `Status18.{PendingApproval,Active,Closing,Closed,Locked}`; `Status21.{PendingApproval,Active,...}`; `Status51.{New,Processing,Filled,Cancelled}`; `OrderType.Market` (verify member at build); `UpvestApiVersion._1`. `Nationality`/`Country` constructed from ISO alpha-2 via `Nationality.TryGetKnownValue(code, out …)` / `Country.TryGetKnownValue(...)` (private ctor, no public factory; `FromValueCore` is internal — `TryGetKnownValue` is the only outside path, and every ISO code is a declared static member).

Client construction: `new UpvestInvestmentApiClient(httpClient, options)`; `options.Server.Default.Production.BaseUrl = Upvest:BaseUrl` (verbatim, overriding the `https://sandbox.upvest.co` default); `options.Environment = ServerEnvironment.Production`. `OauthClientCredentials` left **unset** — the handler supplies the bearer itself, so the SDK's no-op scheme is harmless; bearer comes from `IssueToken` (the spec's documented token op, which the mock implements), not the SDK's Basic-auth strategy.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| `account_id` sent to `PlaceOrder` must be one `CreateAccount` returned for this shopper | `PlaceOrder` ← `CreateAccount` | investing service reads the `UpvestAccountId` stored on the shopper's `InvestorEnrolment`; never a caller value |
| `instrument_id` must be the configured fund | `PlaceOrder` ← config `Upvest:InstrumentId` (already an ISIN; a caller never supplies it) | gateway reads `Upvest:InstrumentId` only |
| enrolment form `nationality`/`address.country`/`taxCountry` must be valid ISO alpha-2 | `CreateUser` ← `Nationality`/`Country` known-value set | enrolment endpoint rejects with 400 when `TryGetKnownValue` fails, before `CreateUser` |

## 3. Trap notes

- Building the SDK `HttpClient` pipeline + lifetime (handler long-lived, client-over-factory) — getting this wrong leaks sockets or rebuilds per request. `MUST load upvest:dotnet-client-initialization`.
- Bearer not set ⇒ silent unauthenticated 401 one round-trip away; options read once at construction so a rotated secret needs restart. `MUST load upvest:dotnet-authentication`.
- Every input on the operation's request record; `required` members must be set; the injected `Idempotency-Key` guid is NOT a real key. `MUST load upvest:dotnet-calling-endpoints`.
- Unions built via static factory / read via `TryGet…`; enums are `OpenStringEnum` (no public ctor, `Match` with `otherwise`); extension-data bag presence varies. `MUST load upvest:dotnet-models`.
- Case A vs Case B; `TryGetRawError` is not a catch-all; a drifted 2xx / mismatched non-2xx body throws `ResponseDeserializationException` (an `ApiException`, not `ApiException<TError>`) — the ladder must also catch it. `MUST load upvest:dotnet-error-handling`.
- `Timeout` is per-attempt not total; `POST`/`PATCH` never auto-retried but `PUT` is; `LogRequestBody` logs JSON unredacted; `UPVESTINVESTMENTAPICLIENT_LOG` can arm body logging unless `LoggerFactory` is set. `MUST load upvest:dotnet-configuration-resilience`.
- The `HttpClient` ctor arg is the test seam; match the project's MSTest style. `MUST load upvest:dotnet-testing`.

## 4. REQUIRED READING (load BEFORE implementation; this sheet deliberately omits their contents)

- `upvest:dotnet-client-initialization` — step 2 (client + DI).
- `upvest:dotnet-authentication` — step 3 (bearer/credentials).
- `upvest:dotnet-calling-endpoints` — step 5 (first SDK calls).
- `upvest:dotnet-models` — step 5 (unions/enums/bodies).
- `upvest:dotnet-error-handling` — step 5 (error boundary). Hazard (always): a drifted/malformed **2xx** (missing `required` member) or a **non-2xx** body not matching the operation's `{Operation}Error` shape surfaces as `ResponseDeserializationException` — an `ApiException` that keeps the status and names the target type but is **not** `ApiException<TError>`; a ladder catching only `ApiException<TError>` lets it escape, so also catch `ResponseDeserializationException` (or `ApiException`).
- `upvest:dotnet-configuration-resilience` — step 2/5 (base URL, retries, timeout, logging).
- `upvest:dotnet-testing` — step 9.

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | `UpvestSettings` bound from `Upvest:` with `[Required]` on every part (ClientId, ClientSecret, SigningKeyId, SigningKeyPath, SigningKeyPassphrase, BaseUrl, InstrumentId, CallbackBaseUrl); `.ValidateDataAnnotations().ValidateOnStart()`; an extra guard confirms the signing-key file exists and the PEM loads with the passphrase at startup. Message names the key, never the value. No fallback/placeholder. |
| 2 | Secret sourcing & rotation | Secrets from .NET user-secrets (loaded from env by me; never written into repo files). Options captured once in the singleton handler → rotation needs process restart (documented). |
| 3 | Total timeout budget | SDK `Timeout` per-attempt; the whole call bounded by a `CancellationToken` deadline the gateway creates (linked to the request token) — default 30s per Upvest call, enforced in the gateway. |
| 4 | Write-retry ownership | Default `HttpMethodsToRetry` = GET/HEAD/PUT/OPTIONS; our writes are all `POST` (CreateUser/CreateAccountGroup/CreateAccount/PlaceOrder/IssueToken) → never auto-resent by the SDK. Reconciliation (§UNKNOWN OUTCOMES) handles ambiguous POSTs. |
| 5 | Idempotency & ambiguous writes | `IdempotencyKey` (required Guid) set to a **stable** guid derived from our own claim record id (enrolment id for user/group/account; investment id for the order) so a retry reuses the key, not a fresh `Guid.NewGuid()`. `PlaceOrder.ClientReference` = investment id for reconciliation. |
| 6 | Observability | Structured logs at Information (enrol requested, order placed, invested, settled) / Warning (Upvest error, reconcile) / Error (unexpected). `LogRequestBody` stays OFF. Upvest error `RawError` body string + status logged (no PII). Never log personal details, token, secret, or signing key. |
| 7 | Sensitive data | `CreateUser` body carries personal data (name, email, birthdate, address, phone). `LogRequestBody` OFF **and** `options.Logging.LoggerFactory` set explicitly so `UPVESTINVESTMENTAPICLIENT_LOG` cannot switch body logging on. Our own logs never echo the enrolment form. |
| 8 | Environment selection | One server group `Default`. Both deployments here point at `Upvest:BaseUrl` (local mock `http://127.0.0.1:…`). Set `options.Server.Default.Production.BaseUrl` = `Upvest:BaseUrl` verbatim; no live Upvest host is ever contacted. |
| 9 | Duplicate prevention under concurrency | Enrolment: one `InvestorEnrolment` per `BuyerId`, unique key on `BuyerId` → second concurrent enrol rejected by the store. Investment: a per-buyer `SemaphoreSlim` + re-check-balance-inside guards the threshold crossing; the pending `Investment` row is written (claim) before `PlaceOrder`. See DUPLICATE CLAIMS. |
| 10 | Partial results | Caller-facing reads (`GET /investments`, `/balance`, `/enrolment`) come from our local store, not Upvest pagination → no cut-short to surface. Internal reconciliation list read is bounded (see PAGED READS). |
| 11 | Unknown outcomes | Each POST's `catch` for a connection failure re-reads by our stable reference (ListAccountOrders by ClientReference for the order; ListUser* for enrol) and settles, rather than reporting failure. See UNKNOWN OUTCOMES. |

**DUPLICATE CLAIMS**

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Enrol (CreateUser+group+account) | `InvestorEnrolment` row, unique index on `BuyerId` (`InvestorEnrolmentConfiguration`) | per-buyer `IInvestingLocks` + existing-row check inside the lock | `InvestingService.EnrolAsync` returns the existing enrolment | `InvestingService.EnrolAsync`: `_locks.AcquireAsync` → `FirstOrDefaultAsync(EnrolmentByBuyerSpecification)` → `_enrolments.AddAsync` |
| Invest (PlaceOrder) | pending `Investment` row written before the SDK call, under a per-buyer lock; balance re-checked in-lock | `_locks.AcquireAsync` + in-lock re-read; `enrolment.TakeBalanceForInvestment` zeroes the balance before the call | `InvestingService` invest path | `InvestingService.SetAsideAndMaybeInvestAsync` (lock + re-read) → `InvestWholeBalanceAsync`: `enrolment.TakeBalanceForInvestment` + `_investments.AddAsync` **before** `_gateway.PlaceInvestmentAsync` |

**PAGED READS**

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| ListAccountOrders (reconcile only) | provider default page (100) + match by ClientReference | internal only; if not found the investment is left `Pending` (not reported settled) and retried, or written off after a grace period | `UpvestInvestingGateway.FindInvestmentByReferenceAsync`; driven by `InvestingService.ReconcilePendingInvestmentsAsync` |

**UNKNOWN OUTCOMES**

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| PlaceOrder | `ListAccountOrders(account_id)` | `ClientReference` = investment id (`InvestmentId.ToString("N")`) | `InvestingService.InvestWholeBalanceAsync` `catch (UpvestGatewayException ex) when (ex.OutcomeUnknown)` → `_gateway.FindInvestmentByReferenceAsync`; swept by `ReconcilePendingInvestmentsAsync` for order-id-less pendings | unit test `InvestingNeverFailsTheOrderWhenUpvestThrows` (gateway throws → order still placed, money returned) |
| CreateUser/AccountGroup/Account | `RetrieveUser` + account-group/account creation are idempotent (stable `IdempotencyKey` per step) | stored Upvest ids on `InvestorEnrolment`; stable keys reuse the provider's cached result | enrolment left `Pending`; `InvestingService.TryAdvanceOnboardingAsync` resumes on the next `GET /enrolment` (user/group/account steps are each re-entrant) | reconciliation exercised live (enrolment pending→active) |

## 6. Assumptions & Blockers

**Assumptions (minor — proceeding):**
- Operating model **TOL** (Upvest holds the licence; the shop onboards shoppers as Upvest users whose assets Upvest holds). TOL is the fit for "Upvest holds what is bought for them."
- "Accepted as an investor" = the shopper's **account group reaches `ACTIVE`** (the deprecated `UserTol.Status` is explicitly not to be used; activation is signalled by role/account-group status). Enrolment status map: `PENDING_APPROVAL`/`CLOSING`→`pending`, `ACTIVE`→`active`, `CLOSED`/`LOCKED`→`rejected`. Reconciled by polling `RetrieveAccountGroup` on `GET /enrolment` and before an invest attempt.
- Settlement by **polling** `RetrieveOrder` (reconciliation on `GET /investments` & `/balance`): `FILLED`→`settled`, `CANCELLED`→`failed`, `NEW`/`PROCESSING`→`pending`. This "reflects what actually happened at Upvest" without depending on webhook reachability. A webhook receiver is a documented production enhancement, not required for the flows.
- `POST /api/orders` order is **paid-on-placement** (eShopOnWeb has no payment step; this is the stated design freedom).
- `taxId`/`taxCountry` from the sign-up form: stored on the enrolment (not a field on the TOL user create body; tax residency is a separate Upvest resource). Captured and persisted; registering a tax residency with Upvest is out of the minimal invest path and noted as an enhancement.
- **Request signing**: the SDK requires `Signature`/`SignatureInput` on every call but provides **no** outbound-signing capability and documents no Upvest signing profile (only inbound webhook HMAC verification exists). The PEM signing key + `SigningKeyId` are provided expressly to sign, and the field docs cite the IETF HTTP Message Signatures draft — so signing is intended implementation work, not a missing capability. I implement the signer per that standard (keyid = `SigningKeyId`, key from the encrypted PEM) behind `IUpvestRequestSigner`, and tune the covered-component profile empirically against the local mock (`Upvest:BaseUrl`) — the authoritative Upvest endpoint for this task — not from external docs.

**Blockers:** none that stop planning. If, after genuine effort, the mock rejects every standards-compliant signature profile and gives no usable signal, the exact Upvest signing profile becomes a narrow reportable gap — recorded then, not assumed now.

## 7. Source labels: every row above cites a map page or a map-named source file; the ISO-code construction, TOL choice, acceptance=account-group-ACTIVE, paid-on-placement, polling-settlement and signing-profile are `YOUR CALL — not in the map` / `UNVERIFIED` as marked in §6.
