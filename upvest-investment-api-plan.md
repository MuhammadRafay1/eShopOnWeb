# Upvest Investment API integration plan — "Invest your change" for eShopOnWeb

## 1. Scope & sequence

Additive capability on `src/PublicApi` (JWT; identity from token). New domain in `ApplicationCore`
(`InvestingAggregate`), Upvest adapter + EF config + auth handler in `Infrastructure`, endpoints in `PublicApi`.

Steps, in build order:

1. **Config & secrets** — `UpvestSettings` bound from `Upvest:` section (keys below). Fail-fast at startup
   (every credential part checked). Secret *values* loaded into .NET user-secrets by me (never into repo files).
2. **Domain** — `Investor` (aggregate root, per shopper) + `Investment` child; money in integer cents;
   `InvestorStatus {Pending,Active,Rejected}`, `InvestmentStatus {Pending,Settled,Failed}`. EF config in `CatalogContext`.
3. **Upvest auth** — one `UpvestAuthHandler : DelegatingHandler` that signs every request (HTTP Message
   Signatures) + sets `upvest-client-id` + `content-digest` + acquires/caches OAuth bearer and sets `Authorization`.
   `UpvestRequestSigner` builds the signature from the signing key. No call site attaches credentials.
4. **Upvest adapter** — `IUpvestClient` (ApplicationCore) → `UpvestClient` (Infrastructure) wrapping the SDK:
   enrol (`CreateUser` TOL + `CreateAccountGroup` PERSONAL), acceptance (`RetrieveAccountGroup`/`ListRoles`),
   account lookup (`ListUserAccounts`), invest (`PlaceOrder`), settle (`RetrieveOrder`). One error boundary.
5. **Orchestration** — `IInvestingService` (ApplicationCore): enrol, get-enrolment (refresh acceptance),
   balance, investments (refresh settlement), `HandlePaidOrderAsync` (round-up + €10 invest, never throws).
6. **Endpoints** — `POST/GET /api/investing/enrolment`, `GET /api/investing/investments`,
   `GET /api/investing/balance`, `POST /api/orders`, and `POST /api/investing/callbacks` (Upvest-only, no token).
7. **Tests** — unit (round-up math, threshold investing, status mapping, enrolment gating) + signer tests.

A capability the map lacks is a Blocker (§6). See §6 for the one genuine reference limitation (request-signing scheme).

## 2. CONTRACT SHEET

> ⚠ Signatures are generated code, verbatim. Each operation that takes input takes ONE request record as its
> first parameter, built with an object initializer using the record's own property names — never flat args.
> ⚠ Every SDK type is written fully-qualified with the namespace its source path implies (that type's own path).

Client: `UpvestInvestmentApiClient(HttpClient, UpvestInvestmentApiClientOptions)` (root ns `UpvestInvestmentApi`).
Env: `ServerEnvironment.Production` = `https://sandbox.upvest.co` (default), `Environment2` = `https://api.upvest.co`
(`UpvestInvestmentApi.Servers`). Base URL overridden verbatim to `Upvest:BaseUrl` via
`options.Server.Default.Production.BaseUrl` (sandbox env selected). Auth: see §Auth below.

Every non-token op requires header members `UpvestClientId` (Guid), `Authorization` (string), `Signature`
(string), `SignatureInput` (string); writes also `IdempotencyKey` (Guid). **These four auth members are passed
as EMPTY strings / the configured client-id at the call site and the real values are injected by
`UpvestAuthHandler`** (task mandate: no call site attaches credentials). `UpvestApiVersion` defaults to `_1`.

| Op | Controller · signature | Request record + members | Body model + fields | Response envelope → fields read | Error case + accessors | Source |
| --- | --- | --- | --- | --- | --- | --- |
| IssueToken | `client.AccessTokens.IssueToken(IssueTokenRequest)` | `UpvestClientId`,`Signature`,`SignatureInput`,`ClientId`(Guid,req),`ClientSecret`(string,req),`Scope`(string,req),`GrantType`="client_credentials" | n/a (form body) | `AuthAccessToken` → `access_token`,`expires_in`,`token_type` | A: `TryGetNoContent(RawError)`[400,401,403,406,429,5xx]·`TryGetRawError` | map/operations/AccessTokens.md; Requests/AccessTokens/IssueTokenRequest.cs; Models/AuthAccessToken.cs |
| CreateUser | `client.Users.CreateUser(CreateUserRequest)` | `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey`(Guid,req),`Body`:`UserCreateRequest?` | `UserCreateRequest` AnyOf → `UserCreateRequest.UserTolCreateRequest(UserTolCreateRequest)`; TOL fields: `FirstName`,`LastName`,`Email`,`BirthDate`(DateTimeOffset),`Nationalities`(IReadOnlyList<Nationality>, min1),`Address`(Address),`Fatca`(Fatca) all required; opt `PhoneNumber`,`TermsAndConditions`,`DataPrivacyAndSharingAgreement` | `UserCreateRequest1` AnyOf → `TryGetUserTol(out UserTol)` → `UserTol.Id`(Guid),`UserTol.Status` | A: `TryGetNoContent`·`TryGetRawError` | map/operations/Users.md; Requests/Users/CreateUserRequest.cs; Models/UserTolCreateRequest.cs; Models/UserTol.cs |
| CreateAccountGroup | `client.AccountGroups.CreateAccountGroup(CreateAccountGroupRequest)` | auth+`IdempotencyKey`,`Body`:`AccountGroupCreateRequest?` | `AccountGroupCreateRequest.AccountGroupCreateUserRequest(AccountGroupCreateUserRequest{UserId=Guid,Type=Type13.Personal})` | `AccountGroupCreateResponse` AnyOf → group id (read at code time) | A: `TryGetNoContent`·`TryGetRawError` | map/operations/AccountGroups.md; Models/AccountGroupCreateUserRequest.cs; Models/Enums/Type13.cs; Models/AnyOf/AccountGroupCreateResponse.cs |
| RetrieveAccountGroup | `client.AccountGroups.RetrieveAccountGroup(RetrieveAccountGroupRequest{AccountGroupId=string,...auth})` | `AccountGroupId`(string,req)+auth | — | `AccountGroupRetrieveResponse` AnyOf → group status/owner-role status (read at code time) | A | map/operations/AccountGroups.md; Models/AnyOf/AccountGroupRetrieveResponse.cs |
| ListRoles | `client.Roles.ListRoles(ListRolesRequest{UserIdTemplate=string,...auth})` | `UserIdTemplate`(req)+`UserId`,`EntityId` query+auth | — | `RolesListResponse` → roles w/ status (read at code time) | A | map/operations/Roles.md; Models/RolesListResponse.cs |
| ListUserAccounts | `client.AccountsApi.ListUserAccounts(ListUserAccountsRequest{UserId=string,...auth})` | `UserId`(string,req)+auth | — | `AccountsListResponse` → account ids (read at code time) | A | map/operations/AccountsApi.md; Models/AccountsListResponse.cs |
| PlaceOrder | `client.Orders.PlaceOrder(PlaceOrderRequest)` | auth+`IdempotencyKey`,`Body`:`OrderPlaceRequest?` | `OrderPlaceRequest{AccountId=Guid(req),Side=Side.Buy(req),InstrumentId=ISIN(req),CashAmount=string "d.dd",Currency=Currency29?}` (`InstrumentIdType`="ISIN" fixed) | `Order39` → `Id`(Guid),`Status`(Status51),`CashAmount`,`Executions` | A: `TryGetNoContent`[...,422,...]·`TryGetRawError` | map/operations/Orders.md; Models/OrderPlaceRequest.cs; Models/Order39.cs; Models/Enums/Side.cs |
| RetrieveOrder | `client.Orders.RetrieveOrder(RetrieveOrderRequest{OrderId=string,...auth})` | `OrderId`(string,req)+auth | — | `Order39` → `Status`(Status51: New/Processing/Filled/Cancelled) | A: `TryGetNoContent`·`TryGetRawError` | map/operations/Orders.md; Models/Order39.cs; Models/Enums/Status51.cs |

Enums needed: `Side.Buy`="BUY" (Models/Enums/Side.cs). `Status51` New/Processing/Filled/Cancelled
(Models/Enums/Status51.cs) → Filled⇒Settled, Cancelled⇒Failed, else Pending. `Type13.Personal`="PERSONAL".
`Nationality`/`Country` are `OpenStringEnum` with 2-letter members, NO public factory → build from a code via
`Nationality.TryGetKnownValue(code, out var n)` / `Country.TryGetKnownValue(...)` (Models/Enums/Nationality.cs, Country.cs).

Client construction/auth/server: single long-lived client over a NAMED HttpClient; `UpvestAuthHandler` attached
to that named client; `options.Server.Default.Production.BaseUrl = Upvest:BaseUrl`; `options.Environment =
ServerEnvironment.Production`; `OauthClientCredentials` left UNSET (handler owns auth, incl. the signed token call).

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| Instrument the BUY order names must be one Upvest offers | `PlaceOrder` ← `Upvest:InstrumentId` (config, operator-supplied ISIN) | `UpvestClient.PlaceInvestmentAsync` passes only the configured `Upvest:InstrumentId`; no caller-supplied instrument exists |
| `account_id` on an order must be one of the user's accounts | `PlaceOrder` ← `ListUserAccounts` | `UpvestClient` resolves `account_id` only from `ListUserAccounts` for the enrolled user; never caller-supplied |
| `user_id` for the account group / order must be a user this app created | `CreateAccountGroup`,`PlaceOrder` ← `CreateUser` | ids persisted on `Investor` at enrolment; never caller-supplied |

⚠ No value from the shopper's request is ever used as an Upvest identifier/instrument. Catalog item ids on
`POST /api/orders` are validated against the shop's own catalog repo (existing `CatalogItemsSpecification`), not Upvest.

## 3. Trap notes

- Request HTTP Message Signatures are NOT computed by the SDK (it sends `Signature`/`SignatureInput` as opaque
  strings). I compute them in `UpvestRequestSigner` — see §6 (reference limitation), not a companion-skill topic.
- Logging a request body would print PII (names, DOB, tax id). Hazard: generator logs JSON bodies unredacted. **MUST load dotnet-configuration-resilience** (§Logging / env var) before configuring logging.
- `PlaceOrder`/`CreateUser` are POST writes the SDK never resends, but a transport failure ≠ "did not happen". **MUST load dotnet-configuration-resilience** (§A write whose outcome is unknown).
- Enum interpolated into a string gives debug form, not wire value. **MUST load dotnet-models** (§Enums `.Value`).
- A drifted 2xx/ error body throws `ResponseDeserializationException`, not `ApiException<TError>`. **MUST load dotnet-error-handling**.
- AnyOf response matching no variant throws `JsonException` mid-deserialize. **MUST load dotnet-models** (§unions).

## 4. REQUIRED READING (load before implementation — contents deliberately not copied here)

- `upvest:dotnet-client-initialization` — client/DI/named-HttpClient + DelegatingHandler attachment. *(loaded)*
- `upvest:dotnet-authentication` — OAuth surface + fail-fast on missing credential. *(loaded)*
- `upvest:dotnet-calling-endpoints` — request-record object-initializer + response unwrap. *(loaded)*
- `upvest:dotnet-models` — AnyOf factories/`TryGet…`, enums (`TryGetKnownValue`,`.Value`,`Match`), `DateTimeOffset`. *(loaded)*
- `upvest:dotnet-error-handling` — Case-A ladder, `ResponseDeserializationException`, `Sdk*Exception` family, boundary mapping. *(loaded)*
- `upvest:dotnet-configuration-resilience` — base-URL/env, timeouts (CancellationToken = only total bound), retries, logging/PII, DelegatingHandler. *(loaded)*
- `upvest:dotnet-testing` — fake the `HttpClient` seam. *(load before writing tests)*

Mandatory hazard row: a drifted **2xx** (missing `required` member) or a **non-2xx** body not matching the
operation's `{Operation}Error` shape surfaces as `ResponseDeserializationException` (an `ApiException`, NOT
`ApiException<TError>`); the catch ladder must also catch `ResponseDeserializationException`/`ApiException`.

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | `UpvestSettings` bound from `Upvest:`; `[Required]` on ClientId, ClientSecret, SigningKeyId, SigningKeyPath, SigningKeyPassphrase, BaseUrl, InstrumentId, CallbackBaseUrl; `ValidateDataAnnotations().ValidateOnStart()` in PublicApi `Program`. Signing key file existence + loadability also checked at startup. Each part checked (blank ≠ missing). |
| 2 | Secret sourcing & rotation | Values from env → loaded into user-secrets by me; bound once at registration into the singleton client + handler. Rotation needs process restart (documented). |
| 3 | Total timeout budget | SDK `Retry.Timeout` (per-attempt) set to 15s; every call bounded by a `CancellationToken` (`CancelAfter(30s)`, linked to `HttpContext.RequestAborted`) in `UpvestClient` — the only whole-call bound. |
| 4 | Write-retry ownership | Keep SDK default `HttpMethodsToRetry` (GET/HEAD/PUT/OPTIONS). `POST` (CreateUser/CreateAccountGroup/PlaceOrder) never auto-resent. No PUT used. |
| 5 | Idempotency & ambiguous writes | SDK injects a fresh `Idempotency-Key` header (not a real key). The request records expose a real `IdempotencyKey` member → I pass a **deterministic** Guid per logical write (stored on `Investment`/`Investor`) so a caller-level retry reuses it. See DUPLICATE CLAIMS. |
| 6 | Observability | Serilog/ILogger at Information for lifecycle (enrol/invest/settle) logging only ids + amounts, never PII. `LogRequestBody` stays OFF; `LoggerFactory` set explicitly so the env var cannot enable body logging. Upvest error status + any correlation carried into our `UpvestException`. |
| 7 | Sensitive data | Enrolment body carries PII (name, DOB, address, phone, tax id). `LogRequestBody=false` + explicit `LoggerFactory`. Our own logs never echo the form. |
| 8 | Environment selection | One server group `Default`. Sandbox (`Production` env) selected; `BaseUrl` overridden verbatim to `Upvest:BaseUrl`. Live (`Environment2`) never used. Test traffic stays on sandbox by config. |
| 9 | Duplicate prevention under concurrency | Investing is triggered from `HandlePaidOrderAsync`; the `Investor` row is the claim store (PK = ShopperId). An in-flight-investment guard uses a deterministic idempotency key persisted before the SDK call. See DUPLICATE CLAIMS. |
| 10 | Partial results | `ListUserAccounts`/`ListRoles` read with limit; I read the first matching account/owner-role and do not page unboundedly (single account group per shopper by construction). No caller-facing truncation. |
| 11 | Unknown outcomes | `PlaceOrder` transport failure → `Investment` kept `Pending` with its idempotency key; settlement sweep (`GET /investments` refresh + webhook) re-reads via `RetrieveOrder` to settle. See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| CreateUser (enrol) | `Investor` row, unique index on `ShopperId` | a second `Investor` with the same `ShopperId` (pre-read returns the existing one) | `InvestingService.EnrolAsync` | `EnrolAsync` (`FirstOrDefaultAsync` → `AddAsync`); unique index in `InvestorConfiguration` |
| PlaceOrder (invest) | `Investment` row w/ `IdempotencyKey`, saved (UpdateAsync) before the SDK call; pending reset to 0 in the same save | the order carries that key (`PlaceOrder` sends `request.IdempotencyKey`); pending=0 means no second cross | `InvestingService.TryInvestBalanceAsync` | `investor.BeginInvestment()` + `UpdateAsync`, then `UpvestClient.PlaceInvestmentAsync` |

### PAGED READS

| Read | What caps it | How the caller learns it was cut short | Where in the code |
| --- | --- | --- | --- |
| ListUserAccounts | one account group/user by construction; take first active TRADING account | n/a — not a user-facing list | `UpvestClient.TryResolveInvestmentAccountAsync` (reads `data[0]`, does not page) |
| ListUserAccountGroups | one group per shopper by construction; take first | n/a — not a user-facing list | `UpvestClient.EnsureAccountGroupAsync` (reads `data[0]`, does not page) |

### UNKNOWN OUTCOMES

| Write | Re-read op | Reference searched by | Where in the code | Test that fails the connection |
| --- | --- | --- | --- | --- |
| PlaceOrder | RetrieveOrder | stored `Investment.UpvestOrderId`; on an unknown outcome the tranche stays `Pending` and is reconciled later via `GetInvestmentOutcomeAsync` (GET /investments refresh + the webhook) | `TryInvestBalanceAsync` non-client-error catch leaves the tranche `Pending`; `SettlePendingInvestmentsAsync` re-reads it | `HandlePaidOrderAsyncTests.InvestingFailure_DoesNotPreventSettingAside` (5xx → tranche stays Pending) |

## 6. Assumptions & Blockers

**Assumption** — operating model is **TOL** (Upvest holds the licence and holds assets on the shopper's behalf),
which matches "hold what is bought for them". `CreateUser` sends `UserTolCreateRequest`.

**Decision (implemented)** — "accepted as an investor" = the Upvest user has reached status `ACTIVE`, which the
provider does only after a KYC check **and** tax residency are on file. Enrolment therefore performs three
calls — `CreateUser` → `CreateUserCheck` (KYC) → `SetTaxResidencies` — and acceptance is read from
`RetrieveUser.status` (`ACTIVE`⇒`active`, `OFFBOARDING`/`OFFBOARDED`⇒`rejected`, else `pending`). The account
group and a TRADING account are provisioned lazily at first investment (`TryResolveInvestmentAccountAsync`),
and the set-aside cash is moved to Upvest via `CreateTopup` before the buy order (an unfunded buy is cancelled).

**Assumption** — `taxId`/`taxCountry` from the sign-up form are captured on enrolment; the TOL `CreateUser` body
has no tax field (tax residency is a separate `TaxResidencies` controller), so they are stored and not required
for the core invest flow. Not a blocker.

**Request signing (hard implementation decision, resolved)** — the plugin exposes every operation needed but
treats the request `Signature`/`SignatureInput` as opaque strings ("some example string" everywhere), citing
only the IETF *HTTP Message Signatures* draft the request records reference. The SDK delegates signing to the
integrator, so this is a hard decision, not a capability gap: `UpvestRequestSigner` implements the scheme and
`UpvestAuthHandler` is the single `DelegatingHandler` that applies it to every call plus the OAuth bearer.
Verified end-to-end against the environment's Upvest endpoint, the accepted scheme is: ECDSA over **SHA-512**,
**ASN.1 DER** signature encoding; covered components `@method @path upvest-client-id` plus `authorization`
(off the token endpoint), `@query` (when present), `content-length content-type digest` with a **SHA-256**
`digest` header (when there is a body), and `idempotency-key` (when present); signature parameters `created`,
`keyid`, `alg`. The signing key is EC P-521.

**End-to-end verification (this environment)** — all five flows were driven through PublicApi against the
Upvest endpoint: a shopper enrolled (`pending`→`active` after KYC+tax), orders placed until the set-aside
balance crossed €10, the whole balance invested in the configured ETF, and the investment settled
(`pending`→`settled`, reflecting the order filling at Upvest). Non-accepted shoppers set nothing aside; the
order endpoint always succeeds regardless of investing.

**Blockers** — none.
