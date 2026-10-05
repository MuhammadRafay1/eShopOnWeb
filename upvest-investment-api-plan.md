# Upvest Investment API integration plan — "Invest your change" for eShopOnWeb

SDK: `UpvestInvestmentApi` (.NET, APIMatic, spec `1.150.0`). All contract facts below are grounded from the
SDK map (`sdk-map.md` + `map/operations/`) and the one declaring source file each row cites; usage traps
are deferred to the companion `dotnet-*` skills named in REQUIRED READING.

## 1. Scope & sequence

Additive "Investing" capability on `src/PublicApi` (JWT). The shopper identity key = JWT `ClaimTypes.Name`
(the username/email), which is also the eShop `Order.BuyerId`. New aggregates live in `CatalogContext`
(so the existing `EfRepository<T>`/`IRepository<T>` serve them); in-memory per the environment.

Auth to Upvest: **one `DelegatingHandler`** (`UpvestAuthenticationHandler`) on the SDK's `HttpClient`
signs *every* outgoing request (HTTP Message Signatures, RFC-9421-style, ECDSA P-521/SHA-512) and attaches
the OAuth bearer; **no call site sets `UpvestClientId`/`Authorization`/`Signature`/`SignatureInput`** — they
pass placeholders and the handler overwrites the real header values. The bearer is obtained via the SDK's
own `AccessTokens.IssueToken` (through the same handler, which signs it; the token endpoint needs no
bearer), cached by a token provider.

Steps:
1. **Config + fail-fast** — bind `Upvest:*`; refuse to boot on any missing/blank credential. Load secrets into user-secrets (never into repo files).
2. **Signer** — load the encrypted PEM once; produce `signature`/`signature-input`/`digest` for a request.
3. **Token provider** — `AccessTokens.IssueToken` → cache `AuthAccessToken` to ~30s before `expires_in`.
4. **DelegatingHandler** — sign every request; attach bearer on all but `/auth/token`.
5. **Client registration** — named `HttpClient` + handler; `new UpvestInvestmentApiClient(httpClient, options)` singleton, `options.Server.Default.Production.BaseUrl = Upvest:BaseUrl`, OAuth credentials left unset (handler owns auth), `Retry`/`Timeout` tuned.
6. **Enrolment** (`POST/GET /api/investing/enrolment`): `Users.CreateUser` → `UserChecks.CreateUserCheck` + `TaxResidencies.SetTaxResidencies`; background-poll `Users.RetrieveUser` until status `ACTIVE`, then provision `AccountGroups.CreateAccountGroup` (PERSONAL) + `AccountsApi.CreateAccount` (TRADING). Enrolment status pending→active→rejected.
7. **Orders** (`POST /api/orders`): reuse eShop `Order`/`OrderItem` via `IRepository<Order>`; on paid order set aside `ceil(total)−total` into the shopper ledger (only for ACTIVE investors). Investing never fails the order.
8. **Investing**: when ledger ≥ €10, `TopUps.CreateTopup` the account group with the balance, then `Orders.PlaceOrder` (BUY, `Upvest:InstrumentId`, `cash_amount`); reset ledger to 0; record an Investment (pending).
9. **Settlement** (`GET /api/investing/investments`): background reconciler polls `Orders.RetrieveOrder`; map `FILLED`→settled, `CANCELLED`→failed, else pending.
10. **Balance** (`GET /api/investing/balance`): `pendingAmount` (ledger not yet invested) + `investedAmount` (sum of settled/placed investments).

A capability the map lacks is a Blocker (§6), not an invented path.

## 2. CONTRACT SHEET

⚠ Signatures are generated code, verbatim. Every operation that takes input takes **one request record** as
its first parameter, built with an object initializer using the record's own property names — never flat
arguments.
⚠ Every SDK type is written **fully-qualified** with the namespace its source path implies (records →
`UpvestInvestmentApi.Models`; unions → `UpvestInvestmentApi.Models.AnyOf`; enums →
`UpvestInvestmentApi.Models.Enums`; request records → `UpvestInvestmentApi.Requests.<Controller>`; errors →
`UpvestInvestmentApi.Errors`; client/options → `UpvestInvestmentApi`; servers →
`UpvestInvestmentApi.Servers`) — taken from THAT type's own source path, not a neighbour's.

Every operation below is **Case A** (typed `{Operation}Error`) with accessors
`TryGetNoContent(out RawError)` (its status list) then `TryGetRawError(out RawError)` fallback, **throw-only**,
**no pagination used**, server group **Default**. Auth bullet is `options.OauthClientCredentials` on all
except `IssueToken` — but in THIS integration the handler supplies auth and that credential is left unset
(a no-op scheme, request still sent; signature+bearer come from the handler).

| Operation (`client.X.Op`) | Signature (required members) | Body model + fields used | Response envelope → fields read | Error accessors | source |
| --- | --- | --- | --- | --- | --- |
| `AccessTokens.IssueToken` | `IssueToken(IssueTokenRequest)` — req: `UpvestClientId:Guid`, `Signature:string`, `SignatureInput:string`, `ClientId:Guid`, `ClientSecret:string`, `Scope:string` (form fields; `GrantType` defaults `client_credentials`) | *(form body, no `Body`)* | `AuthAccessToken` → `AccessToken(access_token):string`, `ExpiresIn(expires_in):int`, `Scope:string` | `TryGetNoContent`[400,401,403,406,429,500,503,504]·`TryGetRawError` | map/operations/AccessTokens.md; Requests/AccessTokens/IssueTokenRequest.cs; Models/AuthAccessToken.cs |
| `Users.CreateUser` | `CreateUser(CreateUserRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey:Guid`; `Body:UserCreateRequest?` | `UserCreateRequest` union → build `.UserByolCreateRequest(UserByolCreateRequest{ FirstName, LastName, BirthDate:DateTimeOffset, Nationalities:IReadOnlyList<Nationality>(req,≥1), Address:Address(req), Email?, PhoneNumber?(^[0-9]{8,15}$) })` | `UserCreateRequest1` union → `TryGetUserByol(out UserByol)` → `UserByol.Id:Guid`, `UserByol.Status:Status` (`.Value`) | `TryGetNoContent`[400,401,403,406,429,500,503,504]·`TryGetRawError` | map/operations/Users.md; Models/UserByolCreateRequest.cs; Models/Address.cs; Models/AnyOf/UserCreateRequest1.cs; Models/UserByol.cs |
| `Users.RetrieveUser` | `RetrieveUser(RetrieveUserRequest)` — req: `UserId:Guid`,`UpvestClientId`,`Authorization`,`Signature`,`SignatureInput` | — | `UserGetResponse` union → `TryGetUserByol(out UserByol)` → `.Status` (`.Value`: `INACTIVE`/`ACTIVE`) | `TryGetNoContent`[401,403,404,406,429,500,503,504]·`TryGetRawError` | map/operations/Users.md; Models/AnyOf/UserGetResponse.cs; Models/UserByol.cs |
| `UserChecks.CreateUserCheck` | `CreateUserCheck(CreateUserCheckRequest)` — req: `UserId:Guid`,`UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`; `Body:UserCheckCreateRequest?` *(no IdempotencyKey member → generator injects `Idempotency-Key: Guid.NewGuid()`)* | `UserCheckCreateRequest` union → `.UserCheckKnowYourCustomerCreateRequest(UserCheckKnowYourCustomerCreateRequest{ CheckConfirmedAt:DateTimeOffset(req), DataDownloadLink:string(req), DocumentType:DocumentType3(req), Provider:string(req), Method:Method(req) })` (`Type` fixed `"KYC"`) | `UserCheckCreateResponse` (id/status — not read beyond success) | `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | map/operations/UserChecks.md; Models/AnyOf/UserCheckCreateRequest.cs; Models/UserCheckKnowYourCustomerCreateRequest.cs |
| `TaxResidencies.SetTaxResidencies` | `SetTaxResidencies(SetTaxResidenciesRequest)` — req: `UserId:Guid`,`UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey:Guid`; `Body:TaxResidenciesSetRequest?` | `TaxResidenciesSetRequest{ TaxResidencies:IReadOnlyList<TaxResidencyForCreateRequest>(req,≥1) }`; element `.WithTaxIdentifierNumber(WithTaxIdentifierNumber{ Country:Country(req), TaxIdentifierNumber:string(req,1..20) })` | `TaxResidencyRecord` (success only) | `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | map/operations/TaxResidencies.md; Models/TaxResidenciesSetRequest.cs; Models/AnyOf/TaxResidencyForCreateRequest.cs; Models/WithTaxIdentifierNumber.cs |
| `AccountGroups.CreateAccountGroup` | `CreateAccountGroup(CreateAccountGroupRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey:Guid`; `Body:AccountGroupCreateRequest?` | `AccountGroupCreateRequest` union → `.AccountGroupCreateUserRequest(AccountGroupCreateUserRequest{ UserId:Guid(req), Type:Type13(req)=Type13.Personal })` | `AccountGroupCreateResponse` union → `TryGetAccountGroup(out AccountGroup)` → `.Id:Guid` | `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | map/operations/AccountGroups.md; Models/AccountGroupCreateUserRequest.cs; Models/Enums/Type13.cs; Models/AnyOf/AccountGroupCreateResponse.cs; Models/AccountGroup.cs |
| `AccountsApi.CreateAccount` | `CreateAccount(CreateAccountRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey:Guid`; `Body:AccountCreateRequest?` | `AccountCreateRequest` union → `.AccountCreateUserRequest(AccountCreateUserRequest{ UserId:Guid(req), AccountGroupId:Guid(req), Type:Type16(req)=Type16.Trading })` | `AccountCreateResponse` union → `TryGetAccount(out Account)` → `.Id:Guid`, `.Status:Status21` | `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | map/operations/AccountsApi.md; Models/AccountCreateUserRequest.cs; Models/Enums/Type16.cs; Models/AnyOf/AccountCreateResponse.cs; Models/Account.cs |
| `AccountsApi.RetrieveAccount` | `RetrieveAccount(RetrieveAccountRequest)` — req: `AccountId:Guid`,`UpvestClientId`,`Authorization`,`Signature`,`SignatureInput` | — | `AccountRetrieveResponse` union → `TryGetAccount(out Account)` → `.Status:Status21` (`.Value`: `PENDING_APPROVAL`/`ACTIVE`) | `TryGetNoContent`[401,403,404,406,429,500,503,504]·`TryGetRawError` | map/operations/AccountsApi.md; Models/AnyOf/AccountRetrieveResponse.cs; Models/Account.cs |
| `TopUps.CreateTopup` | `CreateTopup(CreateTopupRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey:Guid`; `Body:PaymentsTopUpCreateRequest?` | `PaymentsTopUpCreateRequest{ AccountGroupId:Guid(req), CashAmount:string(req,^[0-9]{1,9}(\.[0-9]{2})?$), Currency:Currency(req)=Currency.Eur }` | `PaymentsTopupsResponse` → `.Id:Guid`, `.Status:Status29?` | `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | map/operations/TopUps.md; Models/PaymentsTopUpCreateRequest.cs; Models/Enums/Currency.cs; Models/PaymentsTopupsResponse.cs |
| `Orders.PlaceOrder` | `PlaceOrder(PlaceOrderRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey:Guid`; `Body:OrderPlaceRequest?` | `OrderPlaceRequest{ AccountId:Guid(req), Side:Side(req)=Side.Buy, InstrumentId:string(req, ISIN), CashAmount?:string(^[0-9]{1,9}(\.[0-9]{2})?$), Currency?:Currency29=Currency29.Eur }` (`InstrumentIdType` fixed `ISIN`; set CashAmount not Quantity) | `Order39` → `.Id:Guid`, `.Status:Status51` (`.Value`), `.CashAmount:string`, `.CancellationReason?` | `TryGetNoContent`[400,401,403,406,422,429,500,503,504]·`TryGetRawError` | map/operations/Orders.md; Models/OrderPlaceRequest.cs; Models/Order39.cs; Models/Enums/{Side,Currency29,Status51}.cs |
| `Orders.RetrieveOrder` | `RetrieveOrder(RetrieveOrderRequest)` — req: `OrderId:Guid`,`UpvestClientId`,`Authorization`,`Signature`,`SignatureInput` | — | `Order39` → `.Status:Status51` (`.Value`: `NEW`/`PROCESSING`/`FILLED`/`CANCELLED`), `.CancellationReason?` | `TryGetNoContent`[401,403,404,406,429,500,503,504]·`TryGetRawError` | map/operations/Orders.md; Models/Order39.cs |

Enum members used: `Type13.Personal("PERSONAL")`, `Type16.Trading("TRADING")`, `Side.Buy("BUY")`,
`Currency.Eur("EUR")`, `Currency29.Eur("EUR")`, `Status51.{New,Processing,Filled,Cancelled}`,
`DocumentType3.IdCard("ID_CARD")`, `Method.VideoId("VIDEO_ID")`. `Country`/`Nationality` are open string enums
with **no public factory** → resolve the shopper's alpha-2 via `Country.TryGetKnownValue(code, out var c)` /
`Nationality.TryGetKnownValue(code, out var n)`; reject the enrolment at our boundary if unknown.
`UpvestApiVersion` defaults `_1` on every request record (not set). Read enum wire value via `.Value`
(never `ToString()`), status via `.Status.Value` comparison to the wire string.

Client construction: `new UpvestInvestmentApiClient(HttpClient, UpvestInvestmentApiClientOptions)`;
`options.Environment = ServerEnvironment.Production` (default); base URL override
`options.Server.Default.Production.BaseUrl = <Upvest:BaseUrl>` (source: sdk-map.md Servers & auth;
Servers/ServerEnvironment.cs). DI registration `AddUpvestInvestmentApiClient` uses the **default unnamed**
`HttpClient`; we instead register over a **named** client so the handler/timeout are scoped (source:
ServiceCollectionExtensions.cs).

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| The instrument we buy must be one Upvest offers | `Orders.PlaceOrder` ← `Upvest:InstrumentId` (config; it is the Upvest-issued ISIN for this sandbox — the task fixes it, there is no "list offered ISINs" call in scope) | `InvestingService` before `PlaceOrder`: use the configured ISIN verbatim; a wrong ISIN surfaces as PlaceOrder 400/422 handled at the error boundary |
| `account_id` on an order must be an account we created for this user, and `ACTIVE` | `Orders.PlaceOrder` ← `AccountsApi.CreateAccount`/`RetrieveAccount` | `InvestingService`: the account id is the one we stored for the enrolment; confirm `RetrieveAccount` status `ACTIVE` before placing |
| `account_group_id` on a top-up must be a group we created for this user | `TopUps.CreateTopup` ← `AccountGroups.CreateAccountGroup` | `InvestingService`: the group id stored on the enrolment |
| A user must be `ACTIVE` before an account group is created | `AccountGroups.CreateAccountGroup` ← `Users.RetrieveUser` | background provisioner creates the group only once `RetrieveUser` reports `ACTIVE` |

## 3. Trap notes (do not resolve here — load the named skill)

- Step 2 (signing/`DelegatingHandler`): the SDK leaves `signature`/`signature-input` to the caller and never retries `POST`; what the per-attempt `Timeout` actually bounds, and which verbs the SDK resends, decide whether a signed request can be replayed under a stale `created`/nonce. **MUST load dotnet-configuration-resilience.**
- Step 3 (token provider): what the built-in OAuth cache/refresh does vs. our own `IssueToken` caching, and when a rotated secret takes effect. **MUST load dotnet-authentication.**
- Step 5 (client/DI): `HttpClient`/handler lifetime and that `AddUpvestInvestmentApiClient` binds the default unnamed client — the blast radius of configuring it. **MUST load dotnet-client-initialization.**
- Steps 6–10 (every call): building union bodies via factory methods (not initializers), reading union responses via `TryGet…`, and that open enums have no public factory. **MUST load dotnet-models.** Request-record vs body model, `requestOptions` positional slot. **MUST load dotnet-calling-endpoints.**
- Steps 6–10 (error boundary): which accessor fires for which status, and the `ResponseDeserializationException` escape. **MUST load dotnet-error-handling.**

## 4. REQUIRED READING (load before implementation; this sheet deliberately omits their contents)

- `upvest:dotnet-client-initialization` — step 5, client construction & `HttpClient`/handler lifetime.
- `upvest:dotnet-authentication` — step 3, OAuth client-credentials + fail-fast posture.
- `upvest:dotnet-calling-endpoints` — steps 6–10, request-record shape & the `requestOptions` slot.
- `upvest:dotnet-models` — steps 6–10, union factories/`TryGet…`, open enums, `Address`/collections.
- `upvest:dotnet-error-handling` — the integration error boundary (always required).
- `upvest:dotnet-configuration-resilience` — step 2/5, retries, per-attempt timeout, base-URL override, logging redaction.

⚠ Required hazard: a drifted/malformed **2xx** (missing `required` member) or a **non-2xx** body not matching
the operation's `{Operation}Error` surfaces as `ResponseDeserializationException` — an `ApiException` that
keeps the status and names the target type but is **not** `ApiException<TError>`; the boundary must also catch
`ResponseDeserializationException` (or `ApiException`) or it escapes.

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | `UpvestOptions` bound from `Upvest:` with `[Required]` on ClientId, ClientSecret, SigningKeyId, SigningKeyPath, SigningKeyPassphrase, BaseUrl, InstrumentId, CallbackBaseUrl; `.ValidateDataAnnotations().ValidateOnStart()`. Signer also loads the PEM at startup (fail if unreadable/undecryptable). Every part checked — a blank part is not a missing one. Messages name the config **key**, never the value. |
| 2 | Secret sourcing & rotation | Secrets live in **.NET user-secrets** (loaded from env by our setup step, never written to repo files). The client singleton + signer capture config once at startup; a rotated secret/key takes effect on **process restart** only. Acceptable for this run (documented). |
| 3 | Total timeout budget | `options.Retry.Timeout` is per-attempt; set to 20s. Named `HttpClient.Timeout`=25s backstop. Enrolment/investing happen on background tasks with their own linked `CancellationTokenSource` deadlines; interactive endpoints (`POST /api/orders`) do the Upvest work fire-and-forget off the request path, so the caller is never blocked on Upvest. |
| 4 | Write-retry ownership | Default `HttpMethodsToRetry` = GET/HEAD/PUT/OPTIONS. All our writes are **POST** (CreateUser/Check/TaxResidencies/AccountGroup/Account/Topup/PlaceOrder/IssueToken) → never resent by the SDK. Reads (RetrieveUser/Account/Order) are GET → retryable, which is safe. Keep the default; do not add POST. |
| 5 | Idempotency & ambiguous writes | `CreateUser`, `SetTaxResidencies`, `CreateAccountGroup`, `CreateAccount`, `CreateTopup`, `PlaceOrder` take a **real** caller-supplied `IdempotencyKey:Guid` member → we send a stable Guid derived per logical operation (persisted on our record), so a replay dedupes at Upvest. `CreateUserCheck` and `IssueToken` have **no** such member (generator injects a fresh `Idempotency-Key` header — not a real key); for them the guard is our own local claim + reconciliation (DUPLICATE CLAIMS / UNKNOWN OUTCOMES below). The injected header is never cited as a key. |
| 6 | Observability | `ILogger` at Information for lifecycle transitions (enrolment accepted, investment placed/settled) keyed by our own ids + the Upvest resource id + `upvest-request-id` echo; Warning/Error on Upvest failures carrying `ex.StatusCode`. **No personal detail is ever logged** (names, address, email, taxId, DOB). SDK `LogRequestBody` stays **off** and `LoggerFactory` is set explicitly (see #7). The provider `upvest-request-id` response header is captured via the handler for correlation. |
| 7 | Sensitive data | Enrolment request models carry personal data (name, DOB, address, email, phone, taxId). So: SDK `options.Logging.LogRequestBody=false` **and** `options.Logging.LoggerFactory` is assigned explicitly (DI factory) so `UPVESTINVESTMENTAPICLIENT_LOG` cannot force bodies on. Our own code never logs request bodies or personal fields. The one DelegatingHandler logs only method + redacted path + status, never body/headers. |
| 8 | Environment selection | One server group `Default`; environments `Production`(sandbox `https://sandbox.upvest.co`)/`Environment2`(live). We select `Production` and **override `BaseUrl` with `Upvest:BaseUrl`** (the task-provided sandbox/mock address) so all traffic hits exactly that host and never the live system. No separate sandbox enum beyond this; the explicit BaseUrl override is the isolation. |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. Claims kept in `CatalogContext` (the store this app already uses) keyed by a primary/unique key that an in-memory provider still enforces. |
| 10 | Partial results | N/A — no paged read is consumed. We fetch single resources (`RetrieveUser`/`RetrieveAccount`/`RetrieveOrder`) by id; no `Pageable` is enumerated. |
| 11 | Unknown outcomes | See UNKNOWN OUTCOMES — a transport failure on a write is settled by re-reading, not reported as failure. |

**DUPLICATE CLAIMS**

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Enrol shopper (`CreateUser`) | `Enrolment` row in `CatalogContext` (unique index on `ShopperId`) | The per-shopper `IShopperConcurrencyGuard` lock + an existence check under that lock return the existing enrolment instead of creating a second | `InvestingService.EnrolAsync` (lock + `EnrolmentByShopperSpecification` check before `AddAsync`) | `InvestingService.EnrolAsync`: claim = `_enrolments.AddAsync(Enrolment)` under the shopper lock, then `IUpvestInvestorGateway.CreateInvestorAsync` |
| Invest the balance (`TopUps.CreateTopup` + `Orders.PlaceOrder`) | `Investment` row in `CatalogContext` (PK = int identity) + the balance debit on `Enrolment` | Only the single background `InvestingProcessor` triggers investments, under the per-shopper lock; `Enrolment.TryTakeForInvestment` zeroes the balance atomically so a second trigger sees `PendingAmount < 10`; each provider write carries the `Investment`'s stable idempotency key | `InvestingProcessor.ProcessInvestmentsAsync` (lock + `TryTakeForInvestment` guard; `<10` short-circuit) | `InvestingProcessor.ProcessInvestmentsAsync`: claim = `TryTakeForInvestment` + `_investments.AddAsync(Investment)` under the lock, then `TopUpAsync` → `PlaceInvestmentOrderAsync` |

**PAGED READS**: none.

**UNKNOWN OUTCOMES**

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `CreateUser` | re-`CreateUser` with the stored `CreateUserIdempotencyKey` (idempotent); or `Users.RetrieveUser` once the id is known | `Enrolment.CreateUserIdempotencyKey` (stable, persisted) | `UpvestInvestorGateway.CreateInvestorAsync` throws `UpvestGatewayException{OutcomeUnknown=true}` on `SdkConnectionException`; `InvestingService.EnrolAsync`'s `catch` keeps the persisted claim `Pending`; `InvestingProcessor.ProcessEnrolmentsAsync` retries with the same key (re-POST re-drives it) | Verified live against the configured sandbox (enrolment → active). Connection-failure path exercised by the idempotent retry; not a separate automated fault-injection test. |
| `PlaceOrder` | re-`PlaceOrder` with the stored `Investment.OrderIdempotencyKey` (idempotent — the sandbox returns the same order for the same key) | `Investment.OrderIdempotencyKey` (stable, persisted) | `UpvestInvestorGateway.PlaceInvestmentOrderAsync` throws `UpvestGatewayException{OutcomeUnknown=true}`; `InvestingProcessor.ProcessInvestmentsAsync` `catch (… when ex.OutcomeUnknown)` leaves the investment `Pending` (no refund) and re-attempts with the same key; `ReconcileInvestmentsAsync` settles it from `Orders.RetrieveOrder` | Verified live (investment → settled). Idempotent re-attempt is the reconciliation; not a separate automated fault-injection test. |

## 6. Assumptions & Blockers

- **No blockers.** Every capability needed maps to an in-scope operation.
- Assumption (minor): "accepted as an investor" = Upvest user status `ACTIVE`, reached by submitting a KYC
  check + tax residency (the documented activation inputs). `rejected` has no mock path but is mapped
  defensively from any terminal non-active user state / a `CreateUser` rejection.
- Assumption (minor, YOUR CALL): user-create variant = **BYOL** (`UserByolCreateRequest`) because it requires
  exactly the task's sign-up fields and needs no fabricated FATCA/consent data; TOL would force synthesizing
  consents the shopper never gave. Holding of assets is provided by the account/order setup regardless.
- Assumption (minor, YOUR CALL): funding the Upvest account to back the investment uses `TopUps.CreateTopup`
  (a documented funding path) for the invested amount, then a cash-amount BUY order — this is what makes the
  order fillable; the set-aside money is the economic source.
- Assumption (minor, YOUR CALL): settlement is by **polling `RetrieveOrder`** (a background reconciler +
  on-read), not webhooks — robust to the per-host in-memory reset and avoids the SDK's webhook verifier being
  HMAC-only while Upvest signs webhooks with EC keys. `Upvest:CallbackBaseUrl` is bound but a webhook receiver
  is not required to satisfy any flow.
- The request **signing scheme** (covered components, ECDSA-P521/SHA-512, ASN.1-DER signature, `digest`
  header) is implemented per the RFC-9421-style HTTP Message Signatures the SDK's request records reference;
  verified end-to-end against the configured `Upvest:BaseUrl`. This is implementation, not a gap: the SDK
  requires us to supply `signature`/`signature-input` and documents them via the linked spec.
- **Sandbox response drift (handled, not a plugin gap).** The configured sandbox returns some 2xx bodies
  that omit members the SDK's generated models mark `required` (e.g. `Order39.fee/initiation_flow/executions`,
  `Account`/`AccountGroup.users`, the top-up `cash_amount`), which makes the SDK's strict deserializer raise
  `ResponseDeserializationException` on an otherwise-successful call. The gateway therefore captures each raw
  response via a per-call `SdkHook.OnResponse` and reads the fields it needs (`id`, `status`) from it,
  tolerating a 2xx `ResponseDeserializationException` — exactly the drifted-response handling the REQUIRED
  READING hazard calls for. Every call still goes through the SDK client (and the one signing handler); only
  response *reading* is made resilient. A real Upvest environment returning full bodies would also satisfy
  the typed models unchanged.
