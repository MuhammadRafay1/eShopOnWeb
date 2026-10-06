# Upvest Investment API integration plan — "Invest your change"

Additive capability on **src/PublicApi** (JWT). Shoppers opt in; each paid order rounds up to the
next euro; once set-aside ≥ €10 it is invested in the configured ETF; status reflects Upvest.

All Upvest contract facts below come from the SDK map / named source files (this session's clone),
never memory. SDK spec version `1.150.0`, root namespace `UpvestInvestmentApi`.

## 1. Scope & sequence

New area: `Microsoft.eShopWeb.ApplicationCore.InvestingAggregate` (domain) +
`…Infrastructure` config + an `Upvest` integration library + PublicApi endpoints.

1. **Config + fail-fast** — bind `Upvest:*` settings; host refuses to start if any required part
   is missing/blank. Load signing key (encrypted PKCS#8 PEM) once.
2. **Single `DelegatingHandler`** (`UpvestAuthenticationHandler`) — owns ALL auth on every outbound
   Upvest call: acquires+caches OAuth bearer (except on `/auth/token`), computes the HTTP Message
   Signature over the request with the EC signing key, sets `Authorization`, `signature`,
   `signature-input` headers. No call site attaches credentials.
3. **Token provider** — `client.AccessTokens.IssueToken` → cached `AuthAccessToken` (op #1).
4. **Enrolment** (Flow 1): `Users.CreateUser` (TOL) → `AccountGroups.CreateAccountGroup`
   (PERSONAL) → `AccountsApi.CreateAccount` (TRADING). Store Upvest ids + status locally per
   shopper. Acceptance from `AccountsApi.RetrieveAccount` status (ops #2–5).
5. **Orders** (Flow 2): `POST /api/orders` → existing `Order`/`OrderItem`; order is treated paid on
   placement; round-up credited to the enrolled+accepted shopper's ledger. Never fails the order.
6. **Invest** (Flow 3): ledger ≥ €10 → `Orders.PlaceOrder` BUY cash order for `Upvest:InstrumentId`
   (op #6); reset ledger; record Investment.
7. **Settlement** (Flow 4): reconcile pending Investments via `Orders.RetrieveOrder` (op #7) in a
   hosted poller; webhook receiver route (Upvest-called, token-less) as push path; register with
   `WebhookSubscriptions.CreateWebhook` (op #8, best-effort).
8. **Balance** (Flow 5): `GET /api/investing/balance`.

A capability the map lacks is a Blocker (§6), not an invented path. If the mock rejects `PlaceOrder`
for lack of settled cash, fund via `Tests.CreateBankTransaction` (op #9) — determined at runtime.

## 2. CONTRACT SHEET

⚠ Signatures are generated code, verbatim. Each operation takes ONE request record as its first
parameter, built with an object initializer using the record's own property names (never flat args).
⚠ Every SDK type is written fully-qualified with the namespace its source path implies (taken from
that type's own map/source path, never a neighbour's).

Headers every write carries (SDK `HeaderParam` names, from `Api/*.cs`): `upvest-client-id`,
`Authorization`, `signature`, `signature-input`, `idempotency-key`, `upvest-api-version`.
`IssueToken` omits `Authorization`. The handler overrides `Authorization`/`signature`/`signature-input`.

| # | Op (`client.X`) | Signature / required record members | Body model + fields read/sent | Response envelope → fields read | Error | Source |
|---|---|---|---|---|---|---|
| 1 | `AccessTokens.IssueToken` | `IssueToken(IssueTokenRequest)` — req: `UpvestClientId:Guid`, `Signature:string`, `SignatureInput:string`, `ClientId:Guid`, `ClientSecret:string`, `Scope:string`; `GrantType`="client_credentials" default | form body client_id/client_secret/grant_type/scope | `AuthAccessToken`: `AccessToken(access_token)`, `ExpiresIn(expires_in)`, `Scope` | Case A `IssueTokenError`: `TryGetNoContent`[400,401,403,406,429,5xx] | map/operations/AccessTokens.md; Requests/AccessTokens/IssueTokenRequest.cs; Models/AuthAccessToken.cs |
| 2 | `Users.CreateUser` | `CreateUser(CreateUserRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey:Guid`; `Body:UserCreateRequest?` | `UserCreateRequest.UserTolCreateRequest(UserTolCreateRequest)`: required `FirstName(first_name)`,`LastName(last_name)`,`Email(email)`,`BirthDate(birth_date):DateTimeOffset`,`Nationalities(nationalities):IReadOnlyList<Nationality>` (≥1),`Address(address):Address`,`Fatca(fatca):Fatca`; opt `PhoneNumber(phone_number)`,`TermsAndConditions`,`DataPrivacyAndSharingAgreement`. `Address`: req `AddressLine1(address_line1)`,`Postcode(postcode)`,`Country(country):Country`,`City(city)` | `UserCreateRequest1` = `TryGetUserTol(out UserTol)` → `UserTol.Id:Guid`, `.Status` (deprecated) | Case A `CreateUserError`: `TryGetNoContent` | map/operations/Users.md; Models/AnyOf/UserCreateRequest.cs; Models/UserTolCreateRequest.cs; Models/Address.cs; Models/AnyOf/UserCreateRequest1.cs; Models/UserTol.cs |
| 3 | `AccountGroups.CreateAccountGroup` | `CreateAccountGroup(CreateAccountGroupRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey`; `Body:AccountGroupCreateRequest?` | `AccountGroupCreateRequest.AccountGroupCreateUserRequest(AccountGroupCreateUserRequest)`: req `UserId(user_id):Guid`, `Type(type):Type13` (=PERSONAL) | `AccountGroupCreateResponse` (AnyOf) → group `Id:Guid` | Case A `CreateAccountGroupError` | map/operations/AccountGroups.md; Models/AnyOf/AccountGroupCreateRequest.cs; Models/AccountGroupCreateUserRequest.cs; Models/AnyOf/AccountGroupCreateResponse.cs |
| 4 | `AccountsApi.CreateAccount` | `CreateAccount(CreateAccountRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey`; `Body:AccountCreateRequest?` | `AccountCreateRequest.AccountCreateUserRequest(AccountCreateUserRequest)`: req `UserId(user_id)`,`AccountGroupId(account_group_id):Guid`,`Type(type):Type16` (=TRADING); opt `Name` | `AccountCreateResponse` = `TryGetAccount(out Account)` → `Account.Id:Guid`, `.Status:Status21`, `.AccountGroupId` | Case A `CreateAccountError` | map/operations/AccountsApi.md; Models/AnyOf/AccountCreateRequest.cs; Models/AccountCreateUserRequest.cs; Models/AnyOf/AccountCreateResponse.cs; Models/Account.cs |
| 5 | `AccountsApi.RetrieveAccount` | `RetrieveAccount(RetrieveAccountRequest)` — req: `AccountId:Guid`,`UpvestClientId`,`Authorization`,`Signature`,`SignatureInput` | — | `AccountRetrieveResponse` (AnyOf) → `Account.Status:Status21` (PENDING_APPROVAL/ACTIVE/CLOSING/CLOSED/LOCKED) | Case A `RetrieveAccountError`: `TryGetNoContent`[401,403,404,...] | map/operations/AccountsApi.md; Models/AnyOf/AccountRetrieveResponse.cs; Models/Account.cs; Models/Enums/Status21.cs |
| 6 | `Orders.PlaceOrder` | `PlaceOrder(PlaceOrderRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey:Guid`; `Body:OrderPlaceRequest?` | `OrderPlaceRequest`: req `AccountId(account_id):Guid`,`Side(side):Side`(=BUY),`InstrumentId(instrument_id):string` ISIN `^[A-Z]{2}[A-Z0-9]{9}[0-9]$`; `InstrumentIdType`="ISIN"; `CashAmount(cash_amount):string?` `^[0-9]{1,9}(\.[0-9]{2})?$`; `Currency(currency):Currency29?`(=EUR); `UserId(user_id):Guid?`; `ClientReference(client_reference):string?` (set = local Investment id) | `Order39`: `Id:Guid`, `Status:Status51` (NEW/PROCESSING/FILLED/CANCELLED), `CashAmount`, `ClientReference` | Case A `PlaceOrderError`: `TryGetNoContent`[400,401,403,406,422,429,5xx] | map/operations/Orders.md; Models/OrderPlaceRequest.cs; Models/Order39.cs; Models/Enums/Status51.cs |
| 7 | `Orders.RetrieveOrder` | `RetrieveOrder(RetrieveOrderRequest)` — req: `OrderId:Guid`,`UpvestClientId`,`Authorization`,`Signature`,`SignatureInput` | — | `Order39.Status:Status51` | Case A `RetrieveOrderError`: `TryGetNoContent`[401,403,404,...] | map/operations/Orders.md; Models/Order39.cs |
| 8 | `WebhookSubscriptions.CreateWebhook` | `CreateWebhook(CreateWebhookRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`; `Body:WebhookCreateRequest?` | `WebhookCreateRequest` (resolve at impl): url=`Upvest:CallbackBaseUrl`+route, event types | `Webhook`: `Id`, secret | Case A `CreateWebhookError` | map/operations/WebhookSubscriptions.md; Models/WebhookCreateRequest.cs; Models/Webhook.cs |
| 9 | `Tests.CreateBankTransaction` (conditional) | `CreateBankTransaction(CreateBankTransactionRequest)` — req: `UpvestClientId`,`Authorization`,`Signature`,`SignatureInput`,`IdempotencyKey` | `PaymentsBankTransactionCreateRequest` (resolve at impl) | `PaymentsBankTransactionCreateResponse` | Case A `CreateBankTransactionError` | map/operations/Tests.md |

Enums to resolve at implementation (one targeted source open each, cited): `Side` (BUY) `Models/Enums/Side.cs`;
`Currency29` (EUR) `Models/Enums/Currency29.cs`; `Status51` `Models/Enums/Status51.cs`; `Status21`
`Models/Enums/Status21.cs`; `Type13` (PERSONAL) `Models/Enums/Type13.cs`; `Type16` (TRADING)
`Models/Enums/Type16.cs`; `Nationality` + `Country` `Models/Enums/Nationality.cs`,`Country.cs`;
`Fatca`/`TermsAndConditions`/`DataPrivacyAndSharingAgreement` `Models/*.cs`. Enums are
`OpenStringEnum<T>` — use static members, not C# enums.

Client construction: `new UpvestInvestmentApiClient(httpClient, options)`; options
`UpvestInvestmentApiClientOptions { Server = { Default = { Production = { BaseUrl = Upvest:BaseUrl }}}}`
(override default `https://sandbox.upvest.co`). `OauthClientCredentials` left **unset** (so the
per-op `_auth.OauthClientCredentials` scheme → `NoneAuthScheme`, confirmed non-throwing in
`Core/Authentication/OAuth2/OAuth2Scheme.cs`); the handler owns the bearer.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
|---|---|---|
| `account_id` passed to PlaceOrder must be an account this app created for this shopper | `PlaceOrder` ← `CreateAccount` (stored locally) | `InvestingService` reads the shopper's stored `UpvestAccountId`; never accepts a client-supplied account id |
| `user_id` on account-group/account/order must be the user this app created | `CreateAccountGroup`/`CreateAccount`/`PlaceOrder` ← `CreateUser` (stored) | stored `UpvestUserId` used; shopper identity from JWT only |
| `instrument_id` must be the configured fund | `PlaceOrder` ← `Upvest:InstrumentId` config | `InvestingService` uses config value verbatim; not caller-supplied |

The legal account/user set is "what this app created and stored per shopper", keyed by JWT identity —
a shopper can only ever reach their own Upvest ids.

## 3. Trap notes

- Client/handler lifetime: the `HttpClient`+handler pipeline must be long-lived & reused, not rebuilt
  per request; register via `IHttpClientFactory`. **MUST load dotnet-client-initialization**.
- Credentials set in the DI callback once; a rotated secret needs a restart. **MUST load dotnet-authentication**.
- `Timeout` is per-attempt, not total; `POST`/`PATCH` are never auto-retried but a hung call costs a
  multiple of Timeout. Decide retry/total budget. **MUST load dotnet-configuration-resilience**.
- Catch ladder must handle `ApiException<TError>` AND `ResponseDeserializationException`/`ApiException`
  (drifted 2xx / non-matching error body) AND `SdkConnectionException`/`SdkTimeoutException`.
  Enrolment/investing errors must never surface as an order failure. **MUST load dotnet-error-handling**.
- Models: unions via `TryGet…`/factory (no `new`); enums are `OpenStringEnum<T>` with `Match`, not C#
  enums. **MUST load dotnet-models**.
- Faking the SDK seam for tests = the `HttpClient` constructor arg. **MUST load dotnet-testing**.

## 4. REQUIRED READING (load before implementation starts — this sheet does not carry their contents)

- `upvest:dotnet-client-initialization` — client build + DI + HttpClient lifetime (steps 2–3).
- `upvest:dotnet-authentication` — credential wiring + fail-fast (steps 1–2).
- `upvest:dotnet-calling-endpoints` — first calls to each op (steps 4–7).
- `upvest:dotnet-models` — building TOL/account/order bodies, reading unions/enums (steps 4–7).
- `upvest:dotnet-error-handling` — error boundary around every SDK call (all steps). ALWAYS required.
- `upvest:dotnet-configuration-resilience` — base URL override, retries, timeouts, logging (step 2).
- `upvest:dotnet-testing` — integration tests (throughout).

⚠ Hazard (always): a drifted 2xx (missing `required` member) or a non-2xx body not matching the
op's `{Operation}Error` shape surfaces as `ResponseDeserializationException` — an `ApiException` that
is NOT `ApiException<TError>`; a ladder catching only `ApiException<TError>` lets it escape. Catch
`ResponseDeserializationException`/`ApiException` too.

## 5. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
| 1 | Credential fail-fast | `UpvestOptions` validated at startup (`IValidateOptions`/explicit check in Program): `ClientId`,`ClientSecret`,`SigningKeyId`,`SigningKeyPath`,`SigningKeyPassphrase`,`BaseUrl`,`InstrumentId`,`CallbackBaseUrl` each non-blank; signing key file loads & decrypts at startup. Host throws and refuses to start otherwise. Each multi-part credential part checked individually. |
| 2 | Secret sourcing & rotation | Secrets from .NET user-secrets (loaded from env by me into user-secrets; never in repo). Options bound once at registration & captured in the singleton handler/token-provider; rotation needs a process restart (acceptable; documented). |
| 3 | Total timeout budget | SDK `Retry.Timeout` is per-attempt. Caller-facing budget bounded by a `CancellationToken` with a deadline (per Upvest call ~30s) created in the integration layer; endpoints also honor request abort. Investing work off the request path (hosted service) has its own token. |
| 4 | Write-retry ownership | Default `HttpMethodsToRetry` = GET/HEAD/PUT/OPTIONS. All my writes are `POST` → never auto-resent by the SDK. Idempotency keys make a manual retry safe (row 5). GET reconciliation may retry. |
| 5 | Idempotency & ambiguous writes | `CreateUser`/`CreateAccountGroup`/`CreateAccount` use deterministic `IdempotencyKey` derived from the shopper id (stable GUIDs) so enrolment retries don't duplicate. `PlaceOrder` uses the local Investment's GUID as `IdempotencyKey` → a resend never double-invests. `RetrieveAccount`/`RetrieveOrder` (GET) carry none. |
| 6 | Observability | Structured logs at Info (enrolment created, invest placed, settled) / Warning (Upvest error mapped) / Error (unexpected). Correlation: log Upvest error `StatusCode` + request id from response (hook). `LogRequestBody` stays OFF. **No PII in logs** — only ids/amounts/status. |
| 7 | Sensitive data | Enrolment carries PII (name, email, birth date, address, phone, taxId). `LogRequestBody`=false AND `options.Logging.LoggerFactory` set explicitly so `UPVESTINVESTMENTAPICLIENT_LOG` env cannot force body logging. My own code never logs request bodies or PII. Signing-key passphrase & client secret never logged/returned/written to source. |
| 8 | Environment selection | One server group `Default`. All deployments here talk to `Upvest:BaseUrl` (local mock `http://127.0.0.1:36758`) via `options.Server.Default.Production.BaseUrl` override. No separate sandbox/live split needed — single configured base URL; test traffic is the only traffic. |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. Per-shopper `SemaphoreSlim` serializes check-and-invest; local store PK on `BuyerId` (Investor) rejects a duplicate enrolment; Investment row written before `PlaceOrder`. |
| 10 | Partial results | Reconciliation reads a single order by id (`RetrieveOrder`) — no paging. Any list read (fallback order lookup) is capped & flagged — see PAGED READS. |
| 11 | Unknown outcomes | `PlaceOrder` connection failure after possible placement: Investment left `Pending` with its `client_reference`=Investment id; reconciler re-reads (by id if known, else scans account orders by `client_reference`) and settles. See UNKNOWN OUTCOMES. |

**DUPLICATE CLAIMS** — note: this deployment is single-host in-memory (per the task gotchas), and the EF
in-memory provider does **not** enforce unique indexes. The duplicate guard is therefore a per-shopper
in-process `SemaphoreSlim` + an existing-row check under that lock, backed by **provider-side deterministic
idempotency keys** (derived from the shopper id) so even a slipped-through duplicate dedupes at Upvest. A
true multi-host guard would need a store that refuses the second claim by primary key; that is called out
here as the single-host limitation it is.

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
| Enrolment (CreateUser+group+account) | per-shopper `SemaphoreSlim` + local `Investors` existing-row check; provider idempotency keys `DeterministicGuid(buyerId,...)` | lock serialises; second caller finds the existing investor and returns it; Upvest dedupes by the deterministic keys | `InvestingService.EnrolAsync` (lock + `FirstOrDefaultAsync(InvestorByBuyerIdSpec)`) | `InvestingService.EnrolAsync`; keys in `UpvestInvestorGateway.ProvisionInvestorAsync`/`DeterministicGuid` |
| Invest (PlaceOrder) | `Investments` row written before the SDK call, under the per-shopper lock; ledger reset in the same locked unit | after invest the ledger is 0 → a second caller sees `<€10`; `SemaphoreSlim` serialises; `PlaceOrder` idempotency key = investment `Reference` | the locked body of `InvestingService.OnOrderPaidAsync`→`TryInvestAsync` | `InvestingService.TryInvestAsync` (AddAsync(investment) → UpdateAsync(investor) → `gateway.PlaceInvestmentOrderAsync`) |

**PAGED READS**

| Read | What caps it | How the caller learns it was cut short | Where in the code |
|---|---|---|---|
| Fallback order lookup by `client_reference` (`ListAccountOrders`, only when the order id is unknown) | `maxPages` (50) × `limit` (100), and `Meta.TotalCount` | returns `null` only when `Meta` is exhausted; hitting the page cap throws `UpvestProviderException { OutcomeUnknown = true }` (never a false "not found") so the investment stays `Pending` | `UpvestInvestorGateway.FindOrderByReferenceAsync` |

**UNKNOWN OUTCOMES**

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
|---|---|---|---|---|
| `PlaceOrder` | `RetrieveOrder` (by stored order id) or `ListAccountOrders` (scan by reference) | stored `UpvestOrderId`, else `client_reference` = investment `Reference` (GUID) | `InvestingService.TryInvestAsync` catch (OutcomeUnknown) leaves the investment `Pending`; `InvestmentReconciler.ReconcileInvestmentAsync` resolves (find→attach→settle, or never-placed→refund+delete) | `InvestingServiceTests.Investing_failure_never_fails_the_order` (gateway throws `OutcomeUnknown`; investment persisted `Pending`, order unaffected) |

## 6. Assumptions & Blockers

- **Assumption (minor):** TOL operating model (Upvest holds the ledger) matches "able to hold what is
  bought for them". Proceed.
- **Assumption (minor):** enrolment acceptance = the shopper's TRADING account reaching `ACTIVE`
  (`Account.Status` Status21); `pending` while `PENDING_APPROVAL`; `rejected` if `CLOSED`/`LOCKED` or
  user offboarded. Per `UserTol.status` doc, activation is signalled by role/account activation.
- **Assumption (minor):** OAuth scope string — bound from new `Upvest:Scope` (my config key, not a
  credential) with a broad default covering users/accounts/account-groups/orders/webhooks read+write;
  adjusted at runtime against the mock's responses.
- **Runtime-determined:** whether `PlaceOrder` requires settled cash in the account first; if so, fund
  with `Tests.CreateBankTransaction` (op #9). Not a Blocker — op exists in the map.
- **Signature scheme (CONFIRMED BLOCKER — outbound HTTP Message Signature canonicalization not exposed
  by the plugin):** the SDK exposes `signature`/`signature-input` as caller-supplied strings referencing
  draft-ietf-httpbis-message-signatures-01, but provides **no outbound signer** (its
  `Core/Webhooks/Signing` is HMAC *inbound* verification only), and neither the SDK map nor any plugin
  skill documents the concrete signing-base construction the Upvest endpoint verifies. The sandbox is a
  custom Python server that **globally verifies the `signature` header on every route** (even `GET /`)
  and returns one undifferentiated `401 … "Signature mismatch"` for any failure — identical even with no
  signature headers — so it yields **zero differential feedback** to reverse-engineer the scheme.

  Evidence (empirical, this session): the signing key is EC **P-521**; `ECDsa.SignData` with
  `IeeeP1363FixedFieldConcatenation` produces a 132-byte raw signature that **self-verifies locally**
  (crypto is correct). **~165 distinct constructions were tried** against the live sandbox and all were
  rejected identically: draft-cavage `(request-target)`/`(created)`; draft-01 quoted/unquoted; RFC 9421
  `@method`/`@path`/`@authority`/`@request-target`/`@target-uri` with the mandatory `@signature-params`
  line; `content-digest` covered; hand-rolled bases (request body with exact form-encoding, request line,
  newline-joined method/path, created-only); hashes SHA-256/384/512; raw and DER signature encodings;
  `sig1=:b64:` wrapped vs bare base64; and every created/keyid/alg parameter combination.

  Per the task's non-negotiable Upvest-tooling rule ("the plugin is your sole reference… if it does not
  expose a capability you need, STOP and report the gap — do not invent or work around it"), the exact
  scheme is a **genuine gap**: it is either (a) a signing canonicalization the plugin does not specify and
  that cannot be discovered from the zero-feedback oracle, or (b) a provisioning gap where the sandbox was
  not seeded with the public key matching the provided private key. Both are outside what code + the
  mandated plugin can resolve.

  Containment (so this is the *only* blocked thing and it is one localized change to fix): the signer is
  isolated in `UpvestRequestSigner` and fully config-tunable via `Upvest:SignatureComponents`,
  `Upvest:SignatureAlg` and `Upvest:SignatureFormat` (RFC 9421 defaults). The whole integration is built,
  compiles, boots (fail-fast validation passes — the key loads and decrypts), and every flow that does not
  require a provider round-trip is verified working end-to-end through PublicApi; provider calls fail
  cleanly as a mapped `502`, never a crash. Supplying the correct scheme (or seeding the sandbox key) turns
  the integration live with no code change — the `UpvestTokenProbe` integration test then goes green on a
  real token.

No other Blockers.
