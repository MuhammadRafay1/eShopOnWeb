# Upvest Investment API — integration plan (eShopOnWeb managed portfolios)

Driven by skill `upvest-v4:dotnet-integrate-upvest-investment-api`. Grounded ONLY in the
upvest-v4 SDK map (`sdk-map.md` + `map/operations/*.md`) and the source files those pages name,
at plugin-relative root `sdk/dotnet/`. No web search. Spec version `1.172.0`.

This feature is ADDITIVE to eShopOnWeb: new endpoints on `src/PublicApi` under `/api/portfolio…`,
new EF entities in `ApplicationCore`/`Infrastructure`, a new DI-registered Upvest SDK client.
It does not touch the catalog/basket/order flow.

---

## 1. Scope & sequence

Application plumbing (once):

- **P0 — SDK reference + DI.** Add a `<ProjectReference>` from `src/PublicApi/PublicApi.csproj` to
  `sdk/dotnet/src/UpvestInvestmentApi/UpvestInvestmentApi.csproj` (local to this machine; see §Assumptions).
  Register a singleton `UpvestInvestmentApiClient` built from a `UpvestInvestmentApiClientOptions`
  bound to the `Upvest:` config section, with OAuth client-credentials + an `UpvestSigningHook`.
  `MUST load dotnet-client-initialization`, `dotnet-authentication`.
- **P1 — EF store.** New entities in `ApplicationCore/Entities` (own aggregate folder) + EF config in
  `Infrastructure/Data/Config`, registered on `CatalogContext` (the in-memory "Catalog" store PublicApi
  owns), reached through the existing `IRepository<T>`/`EfRepository<T>`. These persist the app-side
  link between an eShop user and their Upvest ids, the operator's model catalog, and payment/realignment
  records. (Upvest holds the money/holdings; the app holds correlation + status cache.)

Flow 1 — Becoming an investor (`POST`/`GET /api/portfolio/investor`, JWT caller = shopper):
1. `Users.CreateUser` (TOL body) → `userId`.
2. `UserChecks.CreateUserCheck` (KYC arm) → submit the shopper's identity/KYC data → KYC check.
3. `TaxResidencies.SetTaxResidencies` → taxId + taxCountry.
4. `AccountGroups.CreateAccountGroup` (user, type `PERSONAL`) → `accountGroupId`.
5. `AccountsApi.CreateAccount` (user, group, **type `PORTFOLIO`**) → `accountId` (the account able to hold a managed portfolio).
   - `GET` status: `UserChecks.RetrieveUserCheck` (KYC `status`) + `AccountGroups.RetrieveAccountGroup` (`status`). See status-mapping table.

Flow 2 — Model portfolios:
6. `PUT /api/portfolio/models/{modelId}` (admin) → `Portfolios.CreatePortfoliosAllocation`
   (new) or `Portfolios.UpdatePortfoliosAllocation` (replace) → an **allocation** = the model portfolio.
7. `GET /api/portfolio/models` → `Portfolios.ListPortfoliosAllocations`.
8. `PUT /api/portfolio/investor/model` → `Portfolios.CreatePortfoliosConfiguration` (first choice) /
   `Portfolios.UpdatePortfoliosConfiguration` (switch) — links the investor's `accountId` to `allocationId`.

Flow 3 — Paying in (`POST`/`GET /api/portfolio/payments`):
9. `TopUps.CreateTopup` (account group, amount, EUR) → credit request + `settlement_reference`.
10. `Tests.CreateBankTransaction` — sandbox: simulate the inbound SEPA transfer that settles the top-up
    (this is "whatever Upvest provides" for sandbox funding).
11. `Portfolios.CreatePortfoliosOrder` (account, cash_amount, EUR, side `BUY`) → invests the cash across
    the funds of the account's current allocation in the allocation's weights.
    - `GET` status: `TopUps.RetrieveTopup` (`status`) + `Portfolios.RetrievePortfoliosOrder` (`status`).

Flow 4 — Realignment:
12. `POST /api/portfolio/models/{modelId}/realignments` (admin) → `PortfoliosRebalancing.TriggerPortfolioRebalancing`
    with the `Allocations` arm carrying that model's `allocationId` → `realignmentId`.
13. `GET /api/portfolio/realignments/{realignmentId}` → `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution`.

Flow 5 — The investor's portfolio (`GET /api/portfolio`):
14. `Valuations.GetAccountValuation` (account, price_quality) → `total_security_value` (totalValue) and
    `security_positions` (each instrument + quantity + value). modelId from the stored configuration;
    paidIn from the app's own sum of invested payments. (`Positions.ListPositions` gives quantities but
    carries no monetary value — valuation is the source for holdings value.)

No inbound webhook/route is exposed: every status is learned by the app's OWN reads (polling) above.

---

## 2. CONTRACT SHEET

> ⚠ **Signatures below are generated code, verbatim.** Every operation that takes input takes **ONE
> request record** as its first parameter, built with an object initializer using the record's **own**
> property names — never flat positional arguments. An operation with no inputs takes none.
> ⚠ **Every SDK type is fully-qualified from the namespace its source path implies** (per the map's
> *Namespaces by content type*), taken from the path the map gives for THAT type — not a neighbour's.

Namespaces used below: client/options → `UpvestInvestmentApi`; controllers → `UpvestInvestmentApi.Api`;
records → `UpvestInvestmentApi.Models`; enums → `UpvestInvestmentApi.Models.Enums`; unions →
`UpvestInvestmentApi.Models.AnyOf`; request records → `UpvestInvestmentApi.Requests.<Controller>`;
errors → `UpvestInvestmentApi.Errors`; signing → `UpvestInvestmentApi.Signing`; exceptions →
`UpvestInvestmentApi.Core.Exceptions`; raw error → `UpvestInvestmentApi.Core.ErrorResponse`.

All 160 operations are **Case A (typed)**; each error is `ApiException<{Operation}Error>` whose `.Error`
exposes `TryGetNoContent(out RawError?)` (the declared statuses) and `TryGetRawError(out RawError?)`
(fallback). `RawError`: `StatusCode` · `ReadAsString()` · `ReadAsBytes()` · `ReadAsJson<T>()`.
Every non-GET also auto-sends an `Idempotency-Key` header derived from the `IdempotencyKey` request member.

### Per-operation rows

| # | Controller · method (first param = request record) | Request record (ns `…Requests.<Ctrl>`) · required members | Body model (ns `…Models`/`.AnyOf`) · fields (C# — wire — req?) | Response envelope → fields read | Error type · pagination · source |
|---|---|---|---|---|---|
| 1 | `UpvestInvestmentApi.Api.Users.CreateUser(CreateUserRequest)` | `CreateUserRequest`: **`IdempotencyKey: Guid` (req)**; `UpvestApiVersion` (default `_1`); `Body: UserCreateRequest?` | Body `UpvestInvestmentApi.Models.AnyOf.UserCreateRequest` — pick arm `UserTolCreateRequest` (ns `…Models`): `FirstName`(first_name,req) · `LastName`(last_name,req) · `Email`(email,req) · `BirthDate`(birth_date,req `DateTimeOffset` date-only) · `BirthCity`(birth_city,opt) · `BirthCountry`(birth_country,opt enum `BirthCountry`) · `Nationalities`(nationalities,**req** `IReadOnlyList<Nationality>`) · `PhoneNumber`(phone_number,opt) · `Address`(address,**req** `Address2`) · `Fatca`(fatca,**req** `Fatca`) · `TermsAndConditions`/`DataPrivacyAndSharingAgreement`(opt). Build via implicit `UserCreateRequest` ← `UserTolCreateRequest`. | Returns `UpvestInvestmentApi.Models.AnyOf.UserCreateRequest1` → read the created user's `id` (user UUID). | `ApiException<CreateUserError>`; no pagination; `map/operations/Users.md`, `Models/UserTolCreateRequest.cs` |
| 2 | `Users.RetrieveUser(RetrieveUserRequest)` | `RetrieveUserRequest`: **`UserId: Guid` (req)** | — | `UpvestInvestmentApi.Models.AnyOf.UserGetResponse` → user status fields (optional; KYC/account are the authoritative acceptance signals) | `RetrieveUserError`; `map/operations/Users.md` |
| 3 | `Api.UserChecks.CreateUserCheck(CreateUserCheckRequest)` | `CreateUserCheckRequest`: **`UserId: Guid` (req)**; `Body: UserCheckCreateRequest?` | Body `…AnyOf.UserCheckCreateRequest` — arm `UserCheckKnowYourCustomerCreateRequest`: carries identity-document data (document type, expiry, issuing country), KYC method/provider, confirmed address. (See trap: identityDocument.number has no field.) | `UpvestInvestmentApi.Models.UserCheckCreateResponse` → `Id: Guid` (checkId — store it) | `CreateUserCheckError`; `map/operations/UserChecks.md` |
| 4 | `UserChecks.RetrieveUserCheck(RetrieveUserCheckRequest)` | `RetrieveUserCheckRequest`: **`UserId`, `CheckId` (req)** | — | `…AnyOf.UserCheck` → `TryGetUserCheckKnowYourCustomer(out UserCheckKnowYourCustomer)` → `.Status: Status6` (`InProgress`/`Passed`/`Failed`) | `RetrieveUserCheckError`; `map/operations/UserChecks.md`, `Models/UserCheckKnowYourCustomer.cs` |
| 5 | `Api.TaxResidencies.SetTaxResidencies(SetTaxResidenciesRequest)` | `SetTaxResidenciesRequest`: **`UserId`, `IdempotencyKey` (req)**; body `TaxResidenciesSetRequest` (taxId + country — read `Models/TaxResidenciesSetRequest.cs` at impl) | `TaxResidenciesSetRequest` | `TaxResidencyRecord` → confirmation | `SetTaxResidenciesError`; `map/operations/TaxResidencies.md` |
| 6 | `Api.AccountGroups.CreateAccountGroup(CreateAccountGroupRequest)` | `CreateAccountGroupRequest`: **`IdempotencyKey` (req)**; `Body: AccountGroupCreateRequest?` | Body `…AnyOf.AccountGroupCreateRequest` — arm `AccountGroupCreateUserRequest`: `UserId`(user_id,req) · `Type`(type,**req** enum `Type13` → `Personal`) · `SecuritiesAccountNumber`(opt) | `…AnyOf.AccountGroupCreateResponse` → `TryGetAccountGroup(out AccountGroup)` → `.Id: Guid` (accountGroupId), `.Status: Status18` | `CreateAccountGroupError`; `map/operations/AccountGroups.md`, `Models/AccountGroupCreateUserRequest.cs` |
| 7 | `AccountGroups.RetrieveAccountGroup(RetrieveAccountGroupRequest)` | `RetrieveAccountGroupRequest`: **`AccountGroupId` (req)** | — | `…AnyOf.AccountGroupRetrieveResponse` → `AccountGroup.Status: Status18` (`PendingApproval`/`Active`/`Closing`/`Closed`/`Locked`) | `RetrieveAccountGroupError`; `map/operations/AccountGroups.md` |
| 8 | `Api.AccountsApi.CreateAccount(CreateAccountRequest)` | `CreateAccountRequest`: **`IdempotencyKey` (req)**; `Body: AccountCreateRequest?` | Body `…AnyOf.AccountCreateRequest` — arm `AccountCreateUserRequest`: `UserId`(user_id,req) · `AccountGroupId`(account_group_id,req) · `Type`(type,**req** enum `Type16` → **`Portfolio`**) · `Name`(opt) | `…AnyOf.AccountCreateResponse` → account `id` (accountId; read arm at impl) | `CreateAccountError`; `map/operations/AccountsApi.md`, `Models/AccountCreateUserRequest.cs` |
| 9 | `Api.Portfolios.CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest)` | `CreatePortfoliosAllocationRequest`: **`IdempotencyKey` (req)**; `Body: PortfoliosAllocationCreateRequest?` | `Models.PortfoliosAllocationCreateRequest`: `Name`(name,opt) · `Allocation`(allocation,**req** `IReadOnlyList<Allocation>`) · `RebalancingStrategyIds`(opt). `Models.Allocation`: `InstrumentId`(instrument_id,req `…AnyOf.InstrumentId` — ISIN string via implicit op) · `InstrumentIdType`(default `Isin`) · `Weight`(weight,**req** decimal-string `0–100`) | `Models.PortfoliosAllocation` → `Id`(allocationId) · `Name` · `Allocation[]` | `CreatePortfoliosAllocationError`; `map/operations/Portfolios.md`, `Models/PortfoliosAllocationCreateRequest.cs`, `Models/Allocation.cs` |
| 10 | `Portfolios.UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest)` | **`AllocationId` (req)**; body `PortfoliosAllocationUpdateRequest` | replace name/weights | `PortfoliosAllocation` | `UpdatePortfoliosAllocationError` |
| 11 | `Portfolios.ListPortfoliosAllocations(ListPortfoliosAllocationsRequest)` | (no required) query: `Offset`,`Limit`,`InstrumentIds`,… | — | `Models.PortfoliosAllocationsListResponse` → data[] of allocations + meta (read at impl) | `ListPortfoliosAllocationsError`; **offset/limit paged** (see PAGED READS) |
| 12 | `Portfolios.CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest)` | **`IdempotencyKey` (req)**; `Body: PortfoliosConfigurationCreateRequest?` | `Models.PortfoliosConfigurationCreateRequest`: `AccountId`(account_id,req) · `AllocationId`(allocation_id,req) · `RebalancingStrategyIds`(opt) | `Models.PortfoliosConfiguration` → `AccountId`,`AllocationId` | `CreatePortfoliosConfigurationError` (declares 422) |
| 13 | `Portfolios.UpdatePortfoliosConfiguration(UpdatePortfoliosConfigurationRequest)` | **`AccountId`, `IdempotencyKey` (req)**; body `PortfoliosConfigurationUpdateRequest` (switch allocation) | | `PortfoliosConfiguration` | `UpdatePortfoliosConfigurationError` |
| 14 | `Portfolios.RetrievePortfoliosConfiguration(RetrievePortfoliosConfigurationRequest)` | **`AccountId` (req)** | — | `PortfoliosConfiguration` → `AllocationId` (the investor's current modelId) | `RetrievePortfoliosConfigurationError` |
| 15 | `Portfolios.CreatePortfoliosOrder(CreatePortfoliosOrderRequest)` | **`IdempotencyKey` (req)**; `Body: PortfoliosOrderPlaceRequest?` | `Models.PortfoliosOrderPlaceRequest`: `UserId`(user_id,req) · `AccountId`(account_id,req) · `CashAmount`(cash_amount,**req** decimal-string) · `Currency`(currency,**req** enum `Currency.Eur`) · `Side`(side,**req** enum `Side15.Buy`) · `PostTax`(opt,default false) | `Models.PortfoliosOrder` → `Id`(orderId) · `Status: Status65` (`New`/`Processing`/`Filled`/`Settled`/`Cancelled`) · `CancellationReason?` | `CreatePortfoliosOrderError`; `Models/PortfoliosOrderPlaceRequest.cs`, `Models/PortfoliosOrder.cs` |
| 16 | `Portfolios.RetrievePortfoliosOrder(RetrievePortfoliosOrderRequest)` | **`PortfolioOrderId` (req)** | — | `PortfoliosOrder` → `Status: Status65` | `RetrievePortfoliosOrderError` |
| 17 | `Api.TopUps.CreateTopup(CreateTopupRequest)` | **`IdempotencyKey` (req)**; `Body: PaymentsTopUpCreateRequest?` | `Models.PaymentsTopUpCreateRequest`: `AccountGroupId`(account_group_id,req) · `CashAmount`(cash_amount,**req** `^[0-9]{1,9}(\.[0-9]{2})?$`, 2dp) · `Currency`(currency,req `Eur`) | `Models.PaymentsTopupsResponse` → `Id` · `SettlementReference?` · `Status: Status29?` (`Confirmed`/`Settled`/`Cancelled`) | `CreateTopupError`; `Models/PaymentsTopUpCreateRequest.cs` |
| 18 | `TopUps.RetrieveTopup(RetrieveTopupRequest)` | **`TopupId` (req)** | — | `PaymentsTopupsResponse` → `Status: Status29?` | `RetrieveTopupError` |
| 19 | `Api.Tests.CreateBankTransaction(CreateBankTransactionRequest)` | **`IdempotencyKey` (req)**; `Body: PaymentsBankTransactionCreateRequest?` | `Models.PaymentsBankTransactionCreateRequest`: `Account`(account,req `Account29`) · `Amount`(amount,**req** 2dp) · `Currency`(currency,req `BankTransactionCurrency`) · `ClientReference`(client_reference,**req**) · `CounterpartyName`(counterparty_name,**req**) · `CounterpartyAccount`(counterparty_account,**req** `CounterpartyAccount`) · `RemittanceInformation`(opt — carry the top-up `settlement_reference`) · `VirtualBankAccountId`(opt). Read `Account29`/`CounterpartyAccount` shapes at impl. | `Models.PaymentsBankTransactionCreateResponse` (read at impl) | `CreateBankTransactionError`; `map/operations/Tests.md`, `Models/PaymentsBankTransactionCreateRequest.cs` |
| 20 | `Api.PortfoliosRebalancing.TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest)` | **`IdempotencyKey` (req)**; body `…AnyOf.TriggerPortfolioRebalancingRequest` — arm **`Allocations`**: `AllocationsValue`(allocations,**req** `IReadOnlyList<Guid>`) = `[allocationId]`. (Other arm `Accounts`.) Build via implicit op `TriggerPortfolioRebalancingRequest` ← `Allocations`. | | `Models.TriggerPortfolioRebalancingResponse` → `Id: Guid` (realignmentId) | `TriggerPortfolioRebalancingError`; `map/operations/PortfoliosRebalancing.md`, `Models/Allocations.cs` |
| 21 | `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(RetrievePortfoliosRebalancingExecutionRequest)` | **`ExecutionId` (req)** | — | `Models.PortfoliosRebalancingExecution` → `Id`,`Accounts?`,`Allocations?` (**no `status` field** — see trap) | `RetrievePortfoliosRebalancingExecutionError` |
| 22 | `Api.Valuations.GetAccountValuation(GetAccountValuationRequest)` | **`AccountId`, `PriceQuality` (req)**; query `price_quality` enum `PriceQuality3` | — | `Models.AccountValuation` → `TotalSecurityValue`(total_security_value: `Amount` str + `Currency`) = totalValue · `SecurityPositions[]` `AccountValuationSecurityPosition`: `Instrument`(uuid+isin `Instrument6`), `Quantity` str, `Value?`(`Amount`+`Currency`, **null if no price**), `Weight?` | `GetAccountValuationError`; `Models/AccountValuation.cs`, `Models/AccountValuationSecurityPosition.cs` |
| 23 | `Api.Positions.ListPositions(ListPositionsRequest)` | **`AccountId` (req)**; query `Offset`,`Limit` | — | `Models.PositionsListResponse` → `Data: IReadOnlyList<Datum1>` (quantity only, **no value**) + `Meta` | `ListPositionsError`; **offset/limit paged** |

Operations NOT in scope but adjacent (ignore unless a trap forces them): `Portfolios.CreatePortfoliosOrder`
cancel, `CashBalanceTransfers` (possible account-group→account cash move — see UNVERIFIED), `Instruments.*`
(fund metadata/validation, optional).

### Enum value tables (members are PascalCase; wire value differs)

| Enum (ns `…Models.Enums`) | Members used (C# → wire) | Source |
|---|---|---|
| `Status6` (KYC) | `InProgress`→IN_PROGRESS · `Passed`→PASSED · `Failed`→FAILED | `Enums/Status6.cs` |
| `Status18` (account group) | `PendingApproval` · `Active` · `Closing` · `Closed` · `Locked` | `Enums/Status18.cs` |
| `Status65` (portfolio order) | `New` · `Processing` · `Filled` · `Settled` · `Cancelled` | `Enums/Status65.cs` |
| `Status29` (top-up) | `Confirmed` · `Settled` · `Cancelled` | `Enums/Status29.cs` |
| `Side15` | `Buy`→BUY · `Sell`→SELL | `Enums/Side15.cs` |
| `Currency` | `Eur`→EUR · `Gbp`→GBP | `Enums/Currency.cs` |
| `Type16` (account type) | `Trading` · **`Portfolio`** | `Enums/Type16.cs` (confirm member casing at impl) |
| `Type13` (account-group type) | `Personal`,`Child`,`Joint`,`LegalEntity`,… | `Enums/Type13.cs` |
| `InstrumentIdType4` | default `Isin` | `Enums/InstrumentIdType4.cs` |

Open enums: resolve a read-back value with `TryGetKnownValue`/`Match`; the `otherwise` arm receives any
undeclared value. `MUST load dotnet-models`.

### App-facing status mapping (reported statuses must reflect Upvest)

| App status (per task) | Investor (Flow 1) | Payment (Flow 3) | Realignment (Flow 4) |
|---|---|---|---|
| `pending` | KYC `InProgress` OR group `PendingApproval` | top-up not `Settled` OR order `New`/`Processing` | execution exists, no terminal order outcome yet |
| `active`/`invested`/`completed` | KYC `Passed` AND group `Active` | order `Filled`/`Settled` | all execution orders `Filled`/`Settled` |
| `rejected`/`failed` | KYC `Failed` (or group `Closed`/`Locked`) | order `Cancelled` (`CancellationReason`) OR top-up `Cancelled` | any execution order `Cancelled`/failed |

### Client construction / auth / server node (ns `UpvestInvestmentApi`, `.Servers`, `.Signing`)

- `new UpvestInvestmentApiClient(HttpClient, UpvestInvestmentApiClientOptions)` — only ctor. DI: `services.AddUpvestInvestmentApiClient(opts => …)`.
- `options.OauthClientCredentials = new OAuth2ClientCredentials { ClientId = Upvest:ClientId, ClientSecret = Upvest:ClientSecret }` (ns `…Core.Authentication.OAuth2.ClientCredentials`).
- `options.Hooks = [ new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(keyId: Upvest:SigningKeyId, privateKeyPath: Upvest:SigningKeyPath, upvestClientId: Upvest:ClientId, privateKeyPassphrase: Upvest:SigningKeyPassphrase)) ]`. Signing is required on **every** call incl. the token request; without it calls throw `InvalidOperationException` before send.
- `options.Environment = ServerEnvironment.Production` (default; base `https://sandbox.upvest.co`). **Set `options.Server.Default.Production.BaseUrl = Upvest:BaseUrl` verbatim** so the configured URL is used, not a hard-coded env.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
|---|---|---|
| The `modelId` an investor selects (`PUT /investor/model`) must be an allocation the operator defined | `CreatePortfoliosConfiguration`/`UpdatePortfoliosConfiguration` ← `Portfolios.ListPortfoliosAllocations` (the operator's model set; the app also stores each on `PUT /models/{id}`) | In the `PUT /investor/model` handler: reject unless `modelId` is in the app's stored model catalog (which mirrors the allocations the operator created). Add that lookup — it is part of the work. |
| The `modelId` realigned (`POST /models/{id}/realignments`) must be an existing allocation | `TriggerPortfolioRebalancing` ← `ListPortfoliosAllocations` / app model store | In the realignment handler, resolve `modelId`→`allocationId` from the app store before the trigger call; 404 if absent. |
| A `fund.instrumentId` (ISIN) in a model must be a tradable instrument | `CreatePortfoliosAllocation`/`Update…` ← `Instruments.ListInstruments`/`RetrieveInstrument` | OPTIONAL hardening: validate each ISIN via `Instruments.RetrieveInstrument` before creating the allocation; otherwise Upvest rejects the allocation at create (handle the error). Decide at impl — default: rely on Upvest validation + typed error. |
| Payment/portfolio reads must use the caller's OWN `accountId` | all Flow 3/5 ops ← the app's investor record keyed by the JWT caller | Every handler resolves `accountId`/`userId` from the investor record for `HttpContext.User`'s id — never from the request body — so one shopper can never read another's data. |

---

## 3. Trap notes

- **Request signing is mandatory and separate from OAuth.** Missing/incorrect signing config throws
  `InvalidOperationException`/`AuthSchemeException` before any HTTP send — not a 401. `MUST load dotnet-authentication`.
- **Options built once at DI registration** — a rotated `ClientSecret`/passphrase does not take effect until
  process restart. `MUST load dotnet-client-initialization`.
- **Every write auto-sends an `Idempotency-Key` header** derived from the `IdempotencyKey` member; it is a
  real caller-supplied key ONLY because the request record carries `IdempotencyKey`. Reusing the same Guid is
  how a retry deduplicates; a fresh Guid per logical write. `MUST load dotnet-getting-started` (Idempotency).
- **What `Retry.Timeout` bounds vs. the whole-call budget** is not obvious from the option name; a paying-in
  flow chains several writes. `MUST load dotnet-configuration-resilience`.
- **Which verbs the SDK auto-retries** decides whether a `BUY` order can be sent twice. `MUST load dotnet-configuration-resilience`.
- **PII in request bodies.** `UserTolCreateRequest`, the KYC check body and `SetTaxResidencies` carry name,
  birth data, address, tax id, identity-document data — never log these. JSON bodies are logged VERBATIM when
  `LogRequestBody` is on, and the `UPVEST_INVESTMENT_API_LOG=trace` env var can force body logging with no code
  change unless `LoggerFactory` is set explicitly. `MUST load dotnet-configuration-resilience`.
- **`PortfoliosRebalancingExecution` has NO `status` field** (only `id`/`accounts`/`allocations`). The
  realignment `completed`/`failed` outcome must be derived from the execution's orders
  (`ListPortfolioRebalancingExecutionOrders` → each order's status) — not from the execution object. Confirm the
  order-status field in `Models/PortfoliosRebalancingExecutionOrderListResponse.cs` at impl. `MUST load dotnet-models`.
- **`identityDocument.number`** from the task has no field on `UserCheckKnowYourCustomer*`; that model carries
  `document_type`, `document_expiration_date`, issuing-country (`nationality`), method/provider. Carry those;
  the bare document number is dropped (recorded in Assumptions).
- **Open enums + AnyOf unions on every status/body read** — branch with `TryGet…`/`Match`, handle the
  `otherwise`/unknown arm. `MUST load dotnet-models`.
- **A drifted 2xx body or a non-2xx body not matching `{Operation}Error`** surfaces as
  `ResponseDeserializationException` (an `ApiException`, not `ApiException<TError>`). `MUST load dotnet-error-handling`.

---

## 4. REQUIRED READING (load every one BEFORE implementing)

| Skill (plugin-qualified) | Governs |
|---|---|
| `upvest-v4:dotnet-client-initialization` | P0 client construction, singleton lifetime, DI registration |
| `upvest-v4:dotnet-authentication` | OAuth client-credentials + `UpvestSigningHook`; fail-fast on missing secret |
| `upvest-v4:dotnet-calling-endpoints` | every `client.{Group}.{Op}(request)` call; request-record shape |
| `upvest-v4:dotnet-models` | TOL/KYC/account-group/allocation body building; AnyOf unions; open enums; `AdditionalProperties` |
| `upvest-v4:dotnet-configuration-resilience` | timeout budget, retried verbs, base-URL override, pagination, logging/PII |
| `upvest-v4:dotnet-error-handling` | try/catch boundary; Case-A typed errors; **`ResponseDeserializationException`** (a drifted 2xx or a non-2xx body not matching the `{Operation}Error` shape surfaces here — an `ApiException` that keeps the status and names the target type but is NOT `ApiException<TError>`, so the catch ladder must also catch `ResponseDeserializationException` or `ApiException`) |
| `upvest-v4:dotnet-testing` | HttpClient stub seam, faking status polling, constructing SDK exceptions |

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
| 1 | **Credential fail-fast** | DECIDED: `UpvestServiceCollectionExtensions.AddUpvestInvestment` binds `UpvestSettings` with `.ValidateDataAnnotations()` ([Required] on ClientId/ClientSecret/SigningKeyId/SigningKeyPath/BaseUrl), `.Validate(File.Exists(SigningKeyPath))`, and `.ValidateOnStart()`, so the host refuses to boot on a missing value or absent key file rather than failing on first call. `SigningKeyPassphrase` is optional (blank only for an unencrypted key). |
| 2 | **Secret sourcing & rotation** | DECIDED: secrets come from the `Upvest:` config section (loaded into user-secrets here; env/secret-store per deployment); never in source, logs, or responses. The SDK client singleton + its options are built once at registration, so a rotated `ClientSecret`/passphrase takes effect only after a process restart — documented in the verification guide. |
| 3 | **Total timeout budget** | DECIDED: `options.Retry.Timeout = 20s` bounds one attempt and `HttpClient.Timeout = 30s` is the per-attempt backstop; the whole-call bound is the ASP.NET request-aborted `CancellationToken` passed from every endpoint (`ct`) through the services to every SDK call, so a disconnected client cancels the outbound work. `MaxRetries` left at default (writes are POSTs — not retried; see row 4). |
| 4 | **Write-retry ownership** | DECIDED: default `HttpMethodsToRetry` (GET/HEAD/PUT/OPTIONS) is kept, so POST writes (`CreateUser`/`CreateTopup`/`CreateBankTransaction`/`CreatePortfoliosOrder`/`TriggerPortfolioRebalancing`) are never auto-resent on a transient failure; each still carries a stable persisted `IdempotencyKey` so a caller-level retry dedupes, and the one OAuth-401 re-send is harmless (provider did nothing). `UpdatePortfoliosAllocation` is a PUT (retryable + idempotent by resource id). |
| 5 | **Idempotency & ambiguous writes** | Real key = the `IdempotencyKey: Guid` member on each create request record (NOT the injected header value, which is a per-call Guid when no member exists — here the member exists). Reconciliation path: persist the generated key + returned Upvest id on the app record BEFORE/with the call; on retry reuse the same key; on unknown outcome re-read by stored id (see UNKNOWN OUTCOMES). |
| 6 | **Observability** | DECIDED: the gateway logs at Info the operation name + returned Upvest resource id; at Warning/Error the operation name + `ApiException.StatusCode` (never the request body). A client-wide `SdkHook.OnResponse` ("Upvest.Provider" logger) records the provider's error body + `request_id` for any non-2xx (the provider's own error body, not our request body). Request-body logging is off and `LoggerFactory` is set explicitly, so PII never reaches the logs. |
| 7 | **Sensitive data** | YES — Flow 1 (user/KYC/tax) and payment bodies carry PII. `options.Logging.LogRequestBody` stays OFF; `options.Logging.LoggerFactory` set explicitly in all environments (disarms `UPVEST_INVESTMENT_API_LOG=trace`); app diagnostics never echo request bodies on these paths. Bodies are JSON (not form), so there is no form deny-list to populate. |
| 8 | **Environment selection** | One server group `Default`. `ServerEnvironment.Production` → `https://sandbox.upvest.co` (sandbox), `Environment2` → `https://api.upvest.co` (live). This integration sets `options.Server.Default.Production.BaseUrl = Upvest:BaseUrl` verbatim and keeps `Environment = Production`; each deployment points `Upvest:BaseUrl` at its sandbox/live host. The SDK's `Production` member is the sandbox, so test traffic stays off the live system by configuration. |
| 9 | **Duplicate prevention under concurrency** | See DUPLICATE CLAIMS. Each investor-affecting write first claims a row (primary key) in the app's EF store, so a concurrent second caller is refused before reaching Upvest. |
| 10 | **Partial results** | See PAGED READS. Offset/limit lists expose `meta.total_count`/`meta.count`; the handler pages until `offset+count >= total_count` (or sets a `truncated` flag on its DTO). |
| 11 | **Unknown outcomes** | See UNKNOWN OUTCOMES. Each create persists its idempotency key + intent before the call; on `SdkConnectionException`/`SdkTimeoutException` the record stays `pending` and a re-read by stored key/id settles it, rather than reporting failure. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
| Sign-up (`CreateUser`+KYC+group+account) | `Investor` entity in `CatalogContext`, unique index on `ShopperId` | lookup-then-`AddAsync` of the `Investor` row; `ClaimInvestorAsync` catch on a duplicate reloads the existing row | `InvestorService.ClaimInvestorAsync` catch → reload by `InvestorByShopperIdSpecification` | `InvestorService.RegisterAsync` → `ClaimInvestorAsync` (claim) then `_gateway.CreateUserAsync(reg, investor.UserCreateKey, ct)` |
| Pay-in (`CreateTopup`→bank txn→`CreatePortfoliosOrder`) | `Payment` entity, unique index on `PaymentId` | `AddAsync(new Payment(...))` before any provider call | reconciled on next read rather than a second insert (new `PaymentId` per request) | `PaymentService.PayInAsync`: `_payments.AddAsync(payment)` then `_gateway.CreateTopupAsync(..., payment.TopupKey, ct)` |
| Realignment (`TriggerPortfolioRebalancing`) | `Realignment` entity, unique index on `RealignmentId` | `AddAsync(new Realignment(...))` before the trigger call | reconciled on next read | `RealignmentService.TriggerAsync`: `_realignments.AddAsync(realignment)` then `_gateway.TriggerRealignmentAsync(..., realignment.TriggerKey, ct)` |
| Choose/switch model (`Create/UpdatePortfoliosConfiguration`) | `Investor.SelectedAllocationId` column (one per investor) | handler branches Create vs Update on `SelectedAllocationId.HasValue` — never a second Create | `ModelPortfolioService.SelectAsync` (`isSwitch = investor.SelectedAllocationId.HasValue`) | `ModelPortfolioService.SelectAsync` → `_gateway.SetConfigurationAsync(accountId, allocationId, isSwitch, ...)` |

Claim order per row: write the claim (EF insert) → SDK call(s) → record the returned Upvest id + status on
the same row. NOTE (environment): the in-memory EF provider does NOT enforce the unique indexes above, so
cross-process/concurrent duplicate prevention relies on a real relational database; within this single-host
in-memory run, the lookup-then-add guard plus the stable persisted idempotency keys prevent duplicates for the
realistic (sequential) case. On SQL Server the unique index is the hard guard.

### PAGED READS

| Read | What caps it | How the caller learns it was cut short | Where in the code |
|---|---|---|---|
| `ListPortfoliosAllocations` (GET /models) | `limit` (offset/limit; `meta.total_count`) | NOTE: GET /models reads the app's own model store, not this list; the gateway's `ListAllocationsAsync` loops pages until `offset+count >= total_count` (page cap 100) and returns the full set — no silent truncation | `UpvestInvestmentGateway.ListAllocationsAsync` (offset/limit loop with `maxPages`) |
| `ListPositions` | — | Not used: `GetAccountValuation` returns all `security_positions` in one body, which Flow 5 reads instead | `UpvestInvestmentGateway.GetPortfolioAsync` (uses valuation, not positions) |
| `ListPortfolioRebalancingExecutionOrders` (realignment outcome) | `limit`/`meta` | loops all pages aggregating order statuses before deciding `completed`/`failed`; the full aggregate decides the returned state (never a partial) | `UpvestInvestmentGateway.GetRealignmentStateAsync` (offset/limit loop with `maxPages`) |

### UNKNOWN OUTCOMES

| Write | Re-read with | Reference searched by | Where in the code | Test that fails the connection |
|---|---|---|---|---|
| `CreateUser` | `RetrieveUser` (by stored id) / re-POST with same `UserCreateKey` | persisted `UserCreateKey` / returned `UpvestUserId` on `Investor` | gateway `WriteCall` marks `OutcomeUnknown` on `SdkConnection/Timeout`; `InvestorService` leaves the row (no userId) and retries on next POST/GET | `UpvestInvestmentGateway.WriteCall`/`Translate` (sets `OutcomeUnknown`); `InvestorService.RegisterAsync` persists id only on success |
| `CreateTopup` | `RetrieveTopup` (by stored id) / re-POST with `payment.TopupKey` | persisted `TopupKey` + `UpvestTopupId` | `PaymentService.PayInAsync` catch: `OutcomeUnknown` leaves payment `pending`, settled by `GetPaymentStateAsync` on next `GET /payments` | `PaymentService.PayInAsync` catch (`if(!ex.OutcomeUnknown) throw`); `SafeStateAsync` reconciles |
| `CreatePortfoliosOrder` | `RetrievePortfoliosOrder` (by stored id) | persisted `OrderKey` + `UpvestOrderId` | same pay-in catch → `pending`, settled on next read | `PaymentService.PayInAsync` catch; `GetPaymentStateAsync` reads order status |
| `TriggerPortfolioRebalancing` | `ListPortfolioRebalancingExecutionOrders` / stored execution id | persisted `TriggerKey` + `UpvestExecutionId` | `RealignmentService.TriggerAsync` catch → `pending`, settled on `GET /realignments/{id}` | `RealignmentService.TriggerAsync` catch; `SafeStateAsync`/`GetRealignmentStateAsync` |
| `CreateAccountGroup` / `CreateAccount` | re-POST with stored `AccountGroupCreateKey`/`AccountCreateKey` | persisted keys + `UpvestUserId` | best-effort in `InvestorService` (TryStepAsync); resumed on next `GET /investor` | `InvestorService.RegisterAsync`/`GetAsync` TryStepAsync blocks (persist id only on success) |
| `Tests.CreateBankTransaction` (sandbox only) | re-POST with `payment.BankTransferKey` | `ClientReference` = `ESHOP-{paymentId}`, remittance = top-up settlement reference | part of pay-in catch; idempotent on `BankTransferKey` | `PaymentService.PayInAsync` → `_gateway.SimulateInboundTransferAsync(..., payment.BankTransferKey, ct)` |

---

## 6. Assumptions & Blockers

**Assumptions (intent):**
- **"Model portfolio" = Upvest *allocation*; "investor's chosen model" = *portfolio configuration*; "paying in" =
  fund the account then place a portfolio *order* (BUY); "realignment" = *portfolio rebalancing*.** This is the
  natural mapping of the task onto the Portfolios/PortfoliosRebalancing surface; recorded so it can be corrected.
- **"Account able to hold a managed portfolio" = an `AccountsApi.CreateAccount` account of type `PORTFOLIO`**
  inside a `PERSONAL` account group, per `Type16`/`AccountCreateUserRequest` docs.
- **Sandbox funding uses `Tests.CreateBankTransaction`** to simulate the inbound SEPA transfer settling the
  top-up ("whatever Upvest provides"). If the operator expects a different sandbox funding primitive, this row
  changes.
- **TOL onboarding arm** (`UserTolCreateRequest`) is used (Upvest performs/records KYC via the KYC check), not
  BYOL. The task's `identityDocument.number` has no field on the KYC model and is dropped; `document type /
  expiryDate / issuingCountry` map to the KYC check body.
- `modelId`/`paymentId`/`realignmentId` in app routes are the app's own identifiers (stored alongside the Upvest
  UUIDs), so the app controls their format and uniqueness.
- New Upvest-linked entities live on `CatalogContext` (the in-memory store PublicApi owns) via the existing
  `IRepository<T>` pattern; a separate `DbContext` is an acceptable alternative decided at impl.
- Admin-only endpoints reuse the existing `Administrators` role: `[Authorize(Roles = Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` (exemplar: `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`). Caller identity for shopper endpoints comes from the JWT (`HttpContext.User`), never a request field.

**Environmental note (NOT a blocker — plan is full regardless):**
- The sandbox client is authorised ONLY for user-scope operations. Every flow here needs broader scopes
  (`accounts:admin`, `portfolios:admin`, `topups:admin`, `payments:*`, `positions:read`, `valuations:read`,
  `taxes:admin`, `checks:admin`, `tests:admin`). In the sandbox those calls return `401 "This client is not
  authorised to use these scopes"`. That is the environment, not a missing capability; the error boundary must
  surface it clearly (map to `502/503` upstream-auth, log the provider message). All flows are planned in full.

**UNVERIFIED (settle via live traffic / defensive coding — not a planning blocker):**
- Whether top-up cash credited to the **account group** is automatically available to the **account** for a
  portfolio BUY, or whether an explicit account-group→account move (`CashBalanceTransfers`) is required between
  steps 10 and 11. STILL UNVERIFIED: the sandbox client lacks `payments:*`/`portfolios:admin`, so this path
  cannot be exercised here. Implemented path: top-up → simulate inbound transfer → BUY, with no cash transfer.
  If a fully-scoped environment returns an "insufficient cash" typed error on the BUY, a `CashBalanceTransfers`
  step must be inserted. Documented in the final report.
- RESOLVED at impl (shapes read from source): `Account29`/`CounterpartyAccount` = `{ iban }`,
  `BankTransactionCurrency.Eur`, and the rebalancing order status is `PortfoliosRebalancingExecutionOrder.Status`
  (`Status71`: New/Processing/Filled/Cancelled — no "Settled").
- RESOLVED via LIVE sandbox traffic (user scope is permitted): `CreateUser` succeeds; `phone_number` must match
  `^([0-9]{8,15})?$` (gateway strips to digits, omits if out of range); the user must have an **EEA** nationality
  (test data uses DE); `birth_date` is serialized date-only by the SDK and accepted. All other flows return
  `401 "This client is not authorised to use these scopes"` at the token request — surfaced as `502`/`503`.

**Blockers:** none. Every capability the task requires is present in the SDK surface.

---

## 7. Source labels
Every per-operation row cites its map page (`map/operations/*.md`) and the declaring model file read this
session. Application-shape decisions are labelled `YOUR CALL — not in the map`. Two runtime behaviours are
labelled `UNVERIFIED` (§6). No row is deferred to a later source lookup except the few explicit "read at impl"
model shapes named inline, each with its file path.
