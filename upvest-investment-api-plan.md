# Upvest Investment API — integration plan ("Invest your change" for eShopOnWeb)

SDK: `Up-v-ApimaticSDK` (root ns `UpvestInvestmentApi`), spec v1.150.0. All contract facts below come from
the SDK map / source and were validated live against the task's local Upvest mock
(`UPVEST_BASE_URL`), which enforces the HTTP-signature scheme described under *Signing*.

## 1. Scope & sequence

Additive feature on `src/PublicApi` (JWT). Upvest access lives in `src/Infrastructure` behind an
ApplicationCore interface `IUpvestGateway`; domain entities (Enrolment, Investment) in ApplicationCore,
EF-mapped in Infrastructure's `CatalogContext` (in-memory per run). A `BackgroundService` reconciles
async Upvest state (user/account activation, order settlement) by polling.

Build order:
1. Signing: `UpvestRequestSigner` (EC P-521/SHA-512, DER) + `UpvestSigningHandler` (DelegatingHandler) — the single auth point.
2. `UpvestTokenProvider` — caches a bearer via `AccessTokens.IssueToken`.
3. `UpvestGateway` — wraps SDK ops; reads response fields from the raw body captured by the handler (tolerating `ResponseDeserializationException`, see Trap notes).
4. Domain: `Enrolment`, `Investment` aggregates + repos + `InvestingService` (ledger/round-up).
5. PublicApi endpoints (MinimalApi.Endpoint `IEndpoint<>` style) + webhook receiver.
6. `UpvestReconciliationService` background worker.

Operations used (controller · operation):
- `AccessTokens.IssueToken` — bearer token (form body client_id/secret/grant_type/scope).
- `Users.CreateUser` (TOL), `Users.RetrieveUser` — enrol investor, poll activation.
- `UserChecks.CreateUserCheck` (KYC) — required for activation.
- `TaxResidencies.SetTaxResidencies` — required for activation.
- `AccountGroups.CreateAccountGroup` (PERSONAL), `AccountsApi.CreateAccount` (TRADING), `AccountsApi.RetrieveAccount` — holding structure.
- `TopUps.CreateTopup` — fund the account group with the set-aside cash before investing.
- `Orders.PlaceOrder` (nominal BUY, cash_amount), `Orders.RetrieveOrder`, `Orders.ListAccountOrders` — invest + settle + reconcile.

## 2. CONTRACT SHEET

⚠ Signatures are generated code, verbatim: each operation that takes input takes ONE request record as its
first parameter, built with an object initializer whose property names are the record's own (never flat args).
⚠ Every SDK type is written fully-qualified with the namespace its source path implies (taken from that type's own map path).

Every request record carries header members `UpvestClientId: Guid`, `Authorization: string`, `Signature: string`,
`SignatureInput: string` (all required) and, where noted, `IdempotencyKey: Guid` (required). **These are set to
placeholders at the call site** (`Authorization="Bearer AAAA"`, `Signature="AAAA"`, `SignatureInput="AAAA"`,
`UpvestClientId=<config client id>`); the `UpvestSigningHandler` overwrites the real `authorization`/`signature`/
`signature-input`/`upvest-client-id` headers (so no call site attaches credentials). Body goes on the record's `Body`.

| Operation | Request record + members (first param) | Body model + fields (wire) | Response envelope + fields read | Error | Idem key | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `AccessTokens.IssueToken` | `IssueTokenRequest` { UpvestClientId, Signature, SignatureInput, ClientId: Guid, ClientSecret: string, Scope: string, GrantType="client_credentials" } | n/a (form) | `AuthAccessToken` { AccessToken (access_token), ExpiresIn (expires_in), Scope } — deserializes cleanly | A `IssueTokenError`; `TryGetNoContent` | no | map/operations/AccessTokens.md; Models/AuthAccessToken.cs |
| `Users.CreateUser` | `CreateUserRequest` { …hdrs, IdempotencyKey, Body } | `UserCreateRequest` ← `UserTolCreateRequest` { FirstName (first_name), LastName (last_name), Email (email), BirthDate (birth_date): DateTimeOffset, Nationalities (nationalities): IReadOnlyList<Nationality>, Address (address): Models.Address, Fatca (fatca): Fatca, PhoneNumber? (phone_number) } | `UserCreateRequest1` (union) — **read `id`,`status` from raw body** | A `CreateUserError` | yes | Requests/Users/CreateUserRequest.cs; Models/UserTolCreateRequest.cs |
| `Users.RetrieveUser` | `RetrieveUserRequest` { UserId: Guid, …hdrs } | — | `UserGetResponse` (union) — **read `status` from raw body** | A `RetrieveUserError` | no | map/operations/Users.md |
| `UserChecks.CreateUserCheck` | `CreateUserCheckRequest` { UserId, …hdrs, Body } (no IdempotencyKey member; SDK injects `Idempotency-Key` header — mock requires it) | `UserCheckCreateRequest` ← `UserCheckKnowYourCustomerCreateRequest` { Type="KYC", CheckConfirmedAt (check_confirmed_at): DateTimeOffset, DataDownloadLink (data_download_link), DocumentType (document_type): DocumentType3.Passport, Provider (provider), Method (method): Method.VideoId, DocumentExpirationDate?, Nationality? } | `UserCheckCreateResponse` (ignored; 202) | A `CreateUserCheckError` | injected | Models/UserCheckKnowYourCustomerCreateRequest.cs |
| `TaxResidencies.SetTaxResidencies` | `SetTaxResidenciesRequest` { UserId, …hdrs, IdempotencyKey, Body } | `TaxResidenciesSetRequest` { TaxResidencies (tax_residencies): IReadOnlyList<TaxResidencyForCreateRequest> ← `WithTaxIdentifierNumber` { Country (country): Country, TaxIdentifierNumber (tax_identifier_number) } } | `TaxResidencyRecord` — **response does not match model → ResponseDeserializationException, tolerated** | A | yes | Models/TaxResidenciesSetRequest.cs; Models/AnyOf/TaxResidencyForCreateRequest.cs |
| `AccountGroups.CreateAccountGroup` | `CreateAccountGroupRequest` { …hdrs, IdempotencyKey, Body } | `AccountGroupCreateRequest` ← `AccountGroupCreateUserRequest` { UserId (user_id), Type (type): Type13.Personal } | `AccountGroupCreateResponse` (union) — **read `id`,`status` from raw body** | A | yes | Models/AccountGroupCreateUserRequest.cs |
| `AccountsApi.CreateAccount` | `CreateAccountRequest` { …hdrs, IdempotencyKey, Body } | `AccountCreateRequest` ← `AccountCreateUserRequest` { UserId (user_id), AccountGroupId (account_group_id), Type (type): Type16.Trading, Name? (name) } | `AccountCreateResponse` (union) — **read `id`,`status` from raw body** | A | yes | Models/AccountCreateUserRequest.cs |
| `AccountsApi.RetrieveAccount` | `RetrieveAccountRequest` { AccountId, …hdrs } | — | `AccountRetrieveResponse` — **read `status` from raw body** | A | no | map/operations/AccountsApi.md |
| `TopUps.CreateTopup` | `CreateTopupRequest` { …hdrs, IdempotencyKey, Body } | `PaymentsTopUpCreateRequest` { AccountGroupId (account_group_id), CashAmount (cash_amount), Currency (currency): Currency.Eur } | `PaymentsTopupsResponse` — read `id`/`status` from raw body if needed | A | yes | Models/PaymentsTopUpCreateRequest.cs |
| `Orders.PlaceOrder` | `PlaceOrderRequest` { …hdrs, IdempotencyKey, Body } | `OrderPlaceRequest` { AccountId (account_id), UserId? (user_id), Side (side): Side.Buy, InstrumentId (instrument_id)=config ISIN, InstrumentIdType="ISIN", CashAmount (cash_amount): "d.dd", Currency (currency): Currency29.Eur, OrderType (order_type): OrderType.Market, ClientReference? (client_reference)=investment id } | `Order39` { Id, Status (Status51), CashAmount } — **read `id`,`status` from raw body** | A `PlaceOrderError` (incl 422) | yes | Models/OrderPlaceRequest.cs; Models/Order39.cs |
| `Orders.RetrieveOrder` | `RetrieveOrderRequest` { OrderId, …hdrs } | — | `Order39` — **read `status` from raw body** | A | no | map/operations/Orders.md |
| `Orders.ListAccountOrders` | `ListAccountOrdersRequest` { AccountId, …hdrs } | — | `OrdersListResponse` — reconcile by `client_reference`, read from raw body | A | no | map/operations/Orders.md |

Enum members (Models/Enums/, PascalCase of wire): `Side.Buy`, `Currency29.Eur`, `Currency.Eur`, `OrderType.Market`,
`Type13.Personal`, `Type16.Trading`, `DocumentType3.Passport`, `Method.VideoId`. ISO codes from runtime strings via
`Country.TryGetKnownValue(code,out)` / `Nationality.TryGetKnownValue(code,out)` (no public ctor; `TryGetKnownValue`
resolves any enumerated ISO-3166 alpha-2 value).

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| Order `instrument_id` must be an ISIN Upvest offers | `Orders.PlaceOrder` ← config `Upvest:InstrumentId` (an ISIN; validated available via `Instruments` if desired) | `UpvestGateway.PlaceBuyOrder` uses the configured ISIN verbatim; mock/API rejects unknown ISIN with 400 → surfaced as failed investment |
| Order `account_id` must be an ACTIVE account this app created | `Orders.PlaceOrder` ← `AccountsApi.CreateAccount`/`RetrieveAccount` | Reconciliation only invests once the stored account is ACTIVE |
| Topup/order `account_group_id`/account belong to the enrolled user | `TopUps.CreateTopup`,`Orders.PlaceOrder` ← `AccountGroups.CreateAccountGroup`,`AccountsApi.CreateAccount` | Enrolment stores the ids it created; gateway only uses stored ids for that shopper |

## 3. Trap notes
- Response bodies for tax/account-group/account/order omit SDK-`required` fields (`users`,`account_number`,`fee`,`TaxResidencyRecord` shape) → the 2xx deserialize throws `ResponseDeserializationException` even though the write succeeded. Read fields from the raw captured body and tolerate that exception. **MUST load upvest:dotnet-error-handling.**
- `POST` is never resent by the SDK's retry (default `HttpMethodsToRetry`), so writes (CreateUser/Topup/PlaceOrder) are at-most-once from the SDK; a connection failure after send is an unknown outcome to reconcile. **MUST load upvest:dotnet-configuration-resilience.**
- `Timeout` is per-attempt not total; a whole-call budget needs a CancellationToken. **MUST load upvest:dotnet-configuration-resilience.**
- Personal data (name, email, birth date, address, tax id) is in the CreateUser/KYC request bodies → request-body logging must stay off and LoggerFactory set explicitly. **MUST load upvest:dotnet-configuration-resilience.**
- Enums are `OpenStringEnum<T>` not C# enums; build from runtime strings via `TryGetKnownValue`, read via `.Value`. **MUST load upvest:dotnet-models.**
- Client/HttpClient lifetime + DI registration (handler pipeline long-lived, token↔client cycle broken by lazy resolution). **MUST load upvest:dotnet-client-initialization.**
- Credentials applied once at registration; the token call must itself be signed. **MUST load upvest:dotnet-authentication.**

## 4. REQUIRED READING (load before implementing)
- upvest:dotnet-client-initialization — client construction + DI (step 1/2).
- upvest:dotnet-authentication — credentials/token (step 2).
- upvest:dotnet-calling-endpoints — request records + `Body` (step 3).
- upvest:dotnet-models — enums/unions/wire names (step 3).
- upvest:dotnet-error-handling — error boundary + `ResponseDeserializationException` (always).
- upvest:dotnet-configuration-resilience — retries/timeout/base-URL/logging (step 1/6).
These carry the how-to; this sheet deliberately does not.
⚠ Hazard: a drifted/malformed 2xx (missing required member) or a non-2xx body that doesn't match `{Operation}Error`
surfaces as `ResponseDeserializationException` — an `ApiException` that is **not** `ApiException<TError>`; the catch
ladder must also catch `ResponseDeserializationException` (or `ApiException`). This is exactly what the mock does on
success bodies, so the gateway's normal path depends on catching it.

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | `UpvestOptions` bound from `Upvest:` section; a startup validator throws if ClientId/ClientSecret/SigningKeyId/SigningKeyPath/SigningKeyPassphrase/BaseUrl/InstrumentId/CallbackBaseUrl is missing or blank (each part checked). Signing key is loaded + decrypted at startup (fails fast on bad passphrase). |
| 2 | Secret sourcing & rotation | Secrets come from .NET user-secrets (loaded from env by a setup script, never in repo). Options bound once at registration and captured in singletons (signer, token provider) → rotation needs a process restart (acceptable; documented). |
| 3 | Total timeout budget | Each outbound Upvest call bounded by a `CancellationToken` (linked, ~30s) in the gateway; SDK `Timeout` left default (per-attempt) but the token governs the whole call. Background reconciliation uses its own per-iteration token. |
| 4 | Write-retry ownership | Default retry resends only GET/HEAD/PUT/OPTIONS; our writes are POST → never auto-resent. Reconciliation settles ambiguous writes (row 11). |
| 5 | Idempotency & ambiguous writes | Create* and PlaceOrder/Topup take a real `IdempotencyKey` (Guid) member → we generate and **persist** one per logical write (per enrolment step, per investment) so a retry reuses it. CreateUserCheck has no member; SDK injects a per-call header (acceptable: check creation is naturally idempotent at the mock by (user,type)). |
| 6 | Observability | Structured logs at Info (state transitions) / Warning (API errors w/ upvest-request-id from response headers) / Error (unexpected). No request bodies logged. `upvest-request-id` captured from responses via the handler. |
| 7 | Sensitive data | Request bodies carry PII → `LogRequestBody` off, `LoggerFactory` set explicitly on the SDK options, and our own code never logs bodies or the form token request. |
| 8 | Environment selection | One server group `Default`; `options.Environment=Production`, `options.Server.Default.Production.BaseUrl` set from `Upvest:BaseUrl` verbatim (the local mock). No live-system traffic possible since BaseUrl is pinned to the configured value. |
| 9 | Duplicate prevention under concurrency | Enrolment: one per shopper, guarded by a per-shopper `SemaphoreSlim` + existence check before creating the Upvest user (see DUPLICATE CLAIMS). Investment: created only by the single-threaded reconciliation worker under a ledger lock. |
| 10 | Partial results | Only paged read is `ListAccountOrders` (reconciliation); we fetch with a large limit and match by `client_reference`; not a user-facing list, so no truncation signal needed (see PAGED READS). |
| 11 | Unknown outcomes | PlaceOrder/Topup connection failure after send → the investment row persists in `Placing` with its `client_reference`; the worker re-reads via `ListAccountOrders` matching `client_reference` and adopts the order id/status (see UNKNOWN OUTCOMES). |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Create investor (CreateUser) | `Enrolment` row keyed by BuyerId in CatalogContext | existence check under a per-buyer `SemaphoreSlim` before CreateUser; one Enrolment per buyer | the check returns the existing enrolment instead of creating | TBD |
| Invest (PlaceOrder) | `Investment` row created by the single worker; pending reset to 0 atomically under the ledger lock | the worker is single-threaded and zeroes pending when it creates the investment, so no second investment for the same balance | n/a (single writer) | TBD |

### PAGED READS

| Read | What caps it | How the caller learns it was cut short | Where in the code |
| --- | --- | --- | --- |
| `ListAccountOrders` (reconciliation only) | `limit` query (default/large) | internal reconciliation, not surfaced; match is by `client_reference` and the order is created once | TBD |

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `Orders.PlaceOrder` | `Orders.ListAccountOrders` then `Orders.RetrieveOrder` | `client_reference` == Investment id | TBD | TBD |
| `TopUps.CreateTopup` | `TopUps.ListTopups` (by account group) | account group id + amount | TBD | TBD |

## 6. Assumptions & Blockers
- **Signing scheme**: the plugin/workflow reference deliberately leaves request signing as a developer-supplied
  implementation (placeholder). Implemented HTTP Message Signatures (the IETF draft the SDK's own request records
  cite) with the provided EC P-521 key and validated it live against `UPVEST_BASE_URL`. Not a gap — it is the
  mandated DelegatingHandler's job and the sandbox confirms acceptance.
- **Funding**: the mock only FILLS a BUY order when the account group holds enough cash, so investing requires a
  prior `TopUps.CreateTopup` of the invested amount (models the shop moving the set-aside cash to Upvest). Design
  decision, not a gap.
- **Nationality DE** used for test investors so the mock generates the CONCAT identifier itself (no UserIdentifiers
  call needed); real nationalities from the form are passed through, and non-CONCAT countries would need identifiers
  — out of scope for the mandated flows and the test fixture uses DE.
- Enrolment "rejected" has no async path in the mock (users always activate); it is produced only when Upvest
  rejects an onboarding call (4xx) — mapped accordingly.
- No Blockers.
