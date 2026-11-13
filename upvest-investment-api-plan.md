# Upvest Investment API integration plan — eShopOnWeb managed portfolios

Plan-first contract for adding a managed investment portfolio (Upvest as provider) to eShopOnWeb, exposed as JWT-authenticated HTTP endpoints on `src/PublicApi`, additive to the existing catalog/basket/order flow. Written by `upvest-investment-api-planner`; every Upvest contract fact below is grounded in the SDK map (`sdk-map.md` + `map/operations/`) and the source files it names, inside the plugin SDK at `plugins/upvest-v4/sdk/dotnet/`. The main agent implements from this sheet; it does not re-derive contracts from memory.

SDK identity: root namespace `UpvestInvestmentApi`, client `UpvestInvestmentApiClient`, options `UpvestInvestmentApiClientOptions`, API spec `1.172.0`, target `netstandard2.0`. Source: `sdk-map.md`.

---

## 1. Scope & sequence

The app owns its own records (EF Core, in-memory provider here): an **Investor** record (per eShopOnWeb shopper → Upvest userId/accountGroupId/accountId + onboarding form + status), a **ModelPortfolio** record (operator model → Upvest allocationId + name + funds), a **Payment** record, and a **Realignment** record. The Upvest contract below is the *call surface*; where an SDK fact forces an app decision it is marked `YOUR CALL — not in the map` (§7).

Implementation steps, each naming the Upvest operations it uses:

1. **Client wiring & config (DI singleton).** Bind `Upvest:` config (keys in §5/§CONFIG). Register one `UpvestInvestmentApiClient` as a singleton over an injected `HttpClient`; set `OauthClientCredentials` (ClientId/ClientSecret), attach an `UpvestSigningHook` built from the signing config, point `options.Server.Default.Production.BaseUrl` at `Upvest:BaseUrl` verbatim, leave `Environment = ServerEnvironment.Production`. Fail-fast on any missing/blank credential part at startup. `MUST load dotnet-client-initialization`, `dotnet-authentication`.
2. **Persistence.** Add the four app entities + an EF `DbContext` (or extend an existing one) registered with the in-memory provider, following eShopOnWeb's `ApplicationCore.Entities` + `IRepository<>`/`EfRepository<>` pattern (exemplars: `src/ApplicationCore/Entities/CatalogItem.cs`, `src/Infrastructure/Data/CatalogContext.cs`, registration in `src/PublicApi/Program.cs:40`). `YOUR CALL — not in the map`.
3. **Flow 1 — becoming an investor.** `POST /api/portfolio/investor` → `Users.CreateUser` (TOL variant) → `TaxResidencies.SetTaxResidencies` → `AccountGroups.CreateAccountGroup` (type `PERSONAL`) → `AccountsApi.CreateAccount` (type `PORTFOLIO`). `GET /api/portfolio/investor` → `Users.RetrieveUser` (+ `AccountGroups.RetrieveAccountGroup` / `AccountsApi.RetrieveAccount` for account-ready) to derive `status`.
4. **Flow 2 — model portfolios.** `PUT /api/portfolio/models/{modelId}` (admin) → `Portfolios.CreatePortfoliosAllocation` (first time) or `Portfolios.UpdatePortfoliosAllocation` (replace). `GET /api/portfolio/models` → app ModelPortfolio store (seeded/mirrored from `Portfolios.ListPortfoliosAllocations`). `PUT /api/portfolio/investor/model` → `Portfolios.CreatePortfoliosConfiguration` (first) or `Portfolios.UpdatePortfoliosConfiguration` (switch).
5. **Flow 3 — paying in.** `POST /api/portfolio/payments` → `TopUps.CreateTopup` (credit cash to the account group) then `Portfolios.CreatePortfoliosOrder` (`side = BUY`, `cash_amount`, `currency = EUR` — invests across the account's current allocation weights). `GET /api/portfolio/payments` → app Payment store, each reconciled via `TopUps.RetrieveTopup` / `Portfolios.RetrievePortfoliosOrder`.
6. **Flow 4 — realignment.** `POST /api/portfolio/models/{modelId}/realignments` (admin) → `PortfoliosRebalancing.TriggerPortfolioRebalancing` (`Allocations` arm, the model's allocationId). `GET /api/portfolio/realignments/{realignmentId}` → `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution`.
7. **Flow 5 — the portfolio.** `GET /api/portfolio` → `Portfolios.RetrievePortfoliosConfiguration` (modelId = allocation_id) + `Valuations.GetAccountValuation` (holdings + totalValue), app Payment store for `paidIn`.
8. **Error boundary & status reconciliation.** One translation layer over `ApiException<TError>` / `SdkException` family; status fields (`pending`/`invested`/`failed`, etc.) derived only from Upvest reads (polling/re-reads), never a webhook. `MUST load dotnet-error-handling`.

No capability is invented. Where a flow needs sandbox cash injection the map has no operation for, it is a Blocker (§6), not a fabricated path.

---

## 2. CONTRACT SHEET

> ⚠ **Signatures below are generated code, verbatim.** Each operation that takes input takes exactly **ONE request record** as its first parameter, built with an object initializer using the record's own property names — never flat positional arguments. An operation with no inputs takes none. Optional `requestOptions`/`cancellationToken` follow.
>
> ⚠ **Every SDK type must be written fully-qualified with the namespace its source path implies** (take the namespace from the path the map gives for *that* type, never from a neighbouring type): `UpvestInvestmentApi.Requests.<Controller>` for request records, `UpvestInvestmentApi.Models` for records, `UpvestInvestmentApi.Models.AnyOf` for unions, `UpvestInvestmentApi.Models.Enums` for enums, `UpvestInvestmentApi.Errors` for error types, `UpvestInvestmentApi.Api` for controllers.

Shared facts: **every** operation here is **Case A (typed)** — catch `ApiException<{Operation}Error>`; its only typed accessor is `TryGetNoContent(out RawError? value)` (the listed statuses) plus the inherited `TryGetRawError(out RawError? error)` fallback. There are **no** no-throw `…Result` variants. All lists are offset/limit paged (manual, no `Pageable`). `IdempotencyKey` on a request record is a **real** caller-supplied key (`required Guid`). Source: `sdk-map.md` error-handling model + each operations page.

### Flow 1 — investor onboarding

| Operation | Signature (request record · key members) | Body model + fields (wire) | Response envelope → fields read | Error (Case A accessor · statuses) | Source |
| --- | --- | --- | --- | --- | --- |
| `client.Users.CreateUser` | `CreateUser(CreateUserRequest request, …)` · `CreateUserRequest { required Guid IdempotencyKey; UpvestApiVersion UpvestApiVersion=_1; UserCreateRequest? Body }` | `Body` = `UserCreateRequest` union → build `.UserTolCreateRequest(UserTolCreateRequest{…})`. `UserTolCreateRequest` required: `FirstName(first_name)`, `LastName(last_name)`, `Email(email)`, `BirthDate(birth_date, DateTimeOffset→date)`, `Nationalities(nationalities: IReadOnlyList<Nationality>)`, `Address(address: Address2)`, `Fatca(fatca: Fatca)`. Optional used: `BirthCity(birth_city)`, `BirthCountry(birth_country)`, `PhoneNumber(phone_number)`, `TermsAndConditions`, `DataPrivacyAndSharingAgreement`. **No** `taxId`/`taxCountry`/`identityDocument` members exist here (see §6). | `UserCreateRequest1` union → `TryGetUserTol(out UserTol)` → `UserTol.Id (Guid)`, `UserTol.Status (Status)` | `ApiException<CreateUserError>` · `TryGetNoContent` [400,401,403,406,429,500,503,504] | map `Users.md`; `Requests/Users/CreateUserRequest.cs`; `Models/AnyOf/UserCreateRequest.cs`; `Models/UserTolCreateRequest.cs`; `Models/AnyOf/UserCreateRequest1.cs`; `Models/UserTol.cs` |
| `client.Users.RetrieveUser` | `RetrieveUser(RetrieveUserRequest request, …)` · `required Guid UserId` | — | `UserGetResponse` union → `TryGetUserTol(out UserTol)` → `UserTol.Status` | `ApiException<RetrieveUserError>` · `TryGetNoContent` [401,403,404,406,429,500,503,504] | map `Users.md`; `Models/AnyOf/UserGetResponse.cs`; `Models/UserTol.cs` |
| `client.TaxResidencies.SetTaxResidencies` | `SetTaxResidencies(SetTaxResidenciesRequest request, …)` · `required Guid UserId; required Guid IdempotencyKey; TaxResidenciesSetRequest? Body` | `Body` = `TaxResidenciesSetRequest` (open at implement time: `Models/TaxResidenciesSetRequest.cs`) — carries `taxId`/`taxCountry`-equivalent residency records | `TaxResidencyRecord` | `ApiException<SetTaxResidenciesError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `TaxResidencies.md`; `Requests/TaxResidencies/SetTaxResidenciesRequest.cs` |
| `client.AccountGroups.CreateAccountGroup` | `CreateAccountGroup(CreateAccountGroupRequest request, …)` · `required Guid IdempotencyKey; AccountGroupCreateRequest? Body` | `Body` = `AccountGroupCreateRequest` union → `.AccountGroupCreateUserRequest(AccountGroupCreateUserRequest{ required Guid UserId(user_id); required Type13 Type(type)=Type13.Personal })` | `AccountGroupCreateResponse` union → account-group id (open `Models/AnyOf/AccountGroupCreateResponse.cs` at implement time) | `ApiException<CreateAccountGroupError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `AccountGroups.md`; `Models/AnyOf/AccountGroupCreateRequest.cs`; `Models/AccountGroupCreateUserRequest.cs` |
| `client.AccountsApi.CreateAccount` | `CreateAccount(CreateAccountRequest request, …)` · `required Guid IdempotencyKey; AccountCreateRequest? Body` | `Body` = `AccountCreateRequest` union → `.AccountCreateUserRequest(AccountCreateUserRequest{ required Guid UserId(user_id); required Guid AccountGroupId(account_group_id); required Type16 Type(type)=Type16.Portfolio; string? Name(name) })` | `AccountCreateResponse` union → accountId (open `Models/AnyOf/AccountCreateResponse.cs`) | `ApiException<CreateAccountError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `AccountsApi.md`; `Requests/AccountsApi/CreateAccountRequest.cs`; `Models/AnyOf/AccountCreateRequest.cs`; `Models/AccountCreateUserRequest.cs` |
| `client.AccountsApi.RetrieveAccount` | `RetrieveAccount(RetrieveAccountRequest request, …)` · `required Guid AccountId` | — | `AccountRetrieveResponse` union → account status/readiness | `ApiException<RetrieveAccountError>` · `TryGetNoContent` [401,403,404,406,429,500,503,504] | map `AccountsApi.md`; `Models/AnyOf/AccountRetrieveResponse.cs` |

Account `type = PORTFOLIO` is what makes the account able to hold a managed portfolio ("Orders in accounts of this type are created on a portfolio basis and additional portfolio functionality is available" — `AccountCreateUserRequest.Type16` doc). Account group `type = PERSONAL` ("a person holding assets on their own behalf" — `AccountGroupCreateUserRequest.Type13` doc).

### Flow 2 — model portfolios

| Operation | Signature (request record · key members) | Body model + fields (wire) | Response envelope → fields read | Error (Case A accessor · statuses) | Source |
| --- | --- | --- | --- | --- | --- |
| `client.Portfolios.CreatePortfoliosAllocation` | `CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest request, …)` · `required Guid IdempotencyKey; PortfoliosAllocationCreateRequest? Body` | `Body` = `PortfoliosAllocationCreateRequest { string? Name(name); required IReadOnlyList<Allocation> Allocation(allocation); IReadOnlyList<Guid>? RebalancingStrategyIds }`. `Allocation { required InstrumentId InstrumentId(instrument_id); InstrumentIdType4 InstrumentIdType(instrument_id_type)=Isin; required string Weight(weight) }`. `InstrumentId` union: `InstrumentId.String("<ISIN>")` for an ISIN (regex `^[A-Z]{2}[A-Z0-9]{9}[0-9]$`). `Weight` is a **string** percentage, regex `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`. | `PortfoliosAllocation` → `Id (Guid)`, `Name`, `Allocation (IReadOnlyList<Allocation>)` | `ApiException<CreatePortfoliosAllocationError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `Portfolios.md`; `Requests/Portfolios/CreatePortfoliosAllocationRequest.cs`; `Models/PortfoliosAllocationCreateRequest.cs`; `Models/Allocation.cs`; `Models/AnyOf/InstrumentId.cs`; `Models/PortfoliosAllocation.cs` |
| `client.Portfolios.UpdatePortfoliosAllocation` | `UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest request, …)` · `required Guid AllocationId; PortfoliosAllocationUpdateRequest? Body` (no IdempotencyKey) | `Body` = `PortfoliosAllocationUpdateRequest` (open `Models/PortfoliosAllocationUpdateRequest.cs`) — name + allocation list to replace | `PortfoliosAllocation` | `ApiException<UpdatePortfoliosAllocationError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `Portfolios.md`; `Requests/Portfolios/UpdatePortfoliosAllocationRequest.cs` |
| `client.Portfolios.ListPortfoliosAllocations` | `ListPortfoliosAllocations(ListPortfoliosAllocationsRequest request, …)` · no required members · query `sort,order,offset,limit,start_date,end_date,instrument_ids` | — | `PortfoliosAllocationsListResponse { Meta Meta(meta); IReadOnlyList<PortfoliosAllocation> Data(data) }`; `Meta` → `offset,limit,count,total_count` | `ApiException<ListPortfoliosAllocationsError>` · `TryGetNoContent` [400,401,403,404,405,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosAllocationsListResponse.cs`; `Models/Meta.cs` |
| `client.Portfolios.CreatePortfoliosConfiguration` | `CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest request, …)` · `required Guid IdempotencyKey; PortfoliosConfigurationCreateRequest? Body` | `Body` = `PortfoliosConfigurationCreateRequest { required Guid AccountId(account_id); required Guid AllocationId(allocation_id); IReadOnlyList<Guid>? RebalancingStrategyIds }` | `PortfoliosConfiguration` → `AccountId`, `AllocationId` | `ApiException<CreatePortfoliosConfigurationError>` · `TryGetNoContent` [400,401,403,404,406,422,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosConfigurationCreateRequest.cs`; `Models/PortfoliosConfiguration.cs` |
| `client.Portfolios.UpdatePortfoliosConfiguration` | `UpdatePortfoliosConfiguration(UpdatePortfoliosConfigurationRequest request, …)` · `required Guid AccountId; required Guid IdempotencyKey; PortfoliosConfigurationUpdateRequest? Body` | `Body` = `PortfoliosConfigurationUpdateRequest { Guid? AllocationId(allocation_id); IReadOnlyList<Guid>? RebalancingStrategyIds }` (both optional — supply `AllocationId` to switch model) | `PortfoliosConfiguration` | `ApiException<UpdatePortfoliosConfigurationError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosConfigurationUpdateRequest.cs` |
| `client.Portfolios.RetrievePortfoliosConfiguration` | `RetrievePortfoliosConfiguration(RetrievePortfoliosConfigurationRequest request, …)` · `required Guid AccountId` | — | `PortfoliosConfiguration` → `AllocationId` (= `modelId`) | `ApiException<RetrievePortfoliosConfigurationError>` · `TryGetNoContent` [401,403,404,405,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosConfiguration.cs` |

### Flow 3 — paying in

| Operation | Signature (request record · key members) | Body model + fields (wire) | Response envelope → fields read | Error (Case A accessor · statuses) | Source |
| --- | --- | --- | --- | --- | --- |
| `client.TopUps.CreateTopup` | `CreateTopup(CreateTopupRequest request, …)` · `required Guid IdempotencyKey; PaymentsTopUpCreateRequest? Body` | `Body` = `PaymentsTopUpCreateRequest { required Guid AccountGroupId(account_group_id); required string CashAmount(cash_amount, regex `^[0-9]{1,9}(\.[0-9]{2})?$`); required Currency Currency(currency)=Currency.Eur }` | `PaymentsTopupsResponse` → `Id (Guid)`, `Status (Status29?)`, `SettlementReference (string?)` | `ApiException<CreateTopupError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `TopUps.md`; `Models/PaymentsTopUpCreateRequest.cs`; `Models/PaymentsTopupsResponse.cs` |
| `client.Portfolios.CreatePortfoliosOrder` | `CreatePortfoliosOrder(CreatePortfoliosOrderRequest request, …)` · `required Guid IdempotencyKey; PortfoliosOrderPlaceRequest? Body` | `Body` = `PortfoliosOrderPlaceRequest { required Guid UserId(user_id); required Guid AccountId(account_id); required string CashAmount(cash_amount); required Currency Currency(currency)=Eur; required Side15 Side(side)=BUY; bool? PostTax(post_tax)=false }` | `PortfoliosOrder` → `Id (Guid)`, `Status (Status65)`, `CashAmount`, `Orders (IReadOnlyList<PortfoliosOrder1>)` | `ApiException<CreatePortfoliosOrderError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosOrderPlaceRequest.cs`; `Models/PortfoliosOrder.cs` |
| `client.TopUps.RetrieveTopup` | `RetrieveTopup(RetrieveTopupRequest request, …)` · `required Guid TopupId` | — | `PaymentsTopupsResponse` → `Status (Status29?)` | `ApiException<RetrieveTopupError>` · `TryGetNoContent` [401,403,404,406,429,500,503,504] | map `TopUps.md` |
| `client.Portfolios.RetrievePortfoliosOrder` | `RetrievePortfoliosOrder(RetrievePortfoliosOrderRequest request, …)` · `required Guid PortfolioOrderId` | — | `PortfoliosOrder` → `Status (Status65)` | `ApiException<RetrievePortfoliosOrderError>` · `TryGetNoContent` [401,403,404,406,429,500,503,504] | map `Portfolios.md` |

The portfolio order "invests or withdraws a cash amount distributed across all instruments in the account's current allocation" (`PortfoliosOrder` doc) — this is what spreads a pay-in across the model weights. It consumes available cash, so the top-up (crediting) must settle first (see §6 sandbox-funding Blocker).

### Flow 4 — realignment

| Operation | Signature (request record · key members) | Body model + fields (wire) | Response envelope → fields read | Error (Case A accessor · statuses) | Source |
| --- | --- | --- | --- | --- | --- |
| `client.PortfoliosRebalancing.TriggerPortfolioRebalancing` | `TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest request, …)` · `required Guid IdempotencyKey; TriggerPortfolioRebalancingRequest? Body` | `Body` = `TriggerPortfolioRebalancingRequest` union → `.Allocations(Allocations{ required IReadOnlyList<Guid> AllocationsValue(allocations) })` carrying the model's allocationId (the `Accounts` arm rebalances by accountId instead) | `TriggerPortfolioRebalancingResponse` → `Id (Guid)` (= realignmentId) | `ApiException<TriggerPortfolioRebalancingError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `PortfoliosRebalancing.md`; `Requests/PortfoliosRebalancing/TriggerPortfolioRebalancingOperationRequest.cs`; `Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`; `Models/Allocations.cs`; `Models/TriggerPortfolioRebalancingResponse.cs` |
| `client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` | `RetrievePortfoliosRebalancingExecution(RetrievePortfoliosRebalancingExecutionRequest request, …)` · `required Guid ExecutionId` | — | `PortfoliosRebalancingExecution` → `Id`, `Accounts (IReadOnlyList<Guid>?)`, `Allocations (IReadOnlyList<Guid>?)` — **no status field** (see Trap T4) | `ApiException<RetrievePortfoliosRebalancingExecutionError>` · `TryGetNoContent` [401,403,404,405,406,429,500,503,504] | map `PortfoliosRebalancing.md`; `Models/PortfoliosRebalancingExecution.cs` |

### Flow 5 — the portfolio

| Operation | Signature (request record · key members) | Body model + fields (wire) | Response envelope → fields read | Error (Case A accessor · statuses) | Source |
| --- | --- | --- | --- | --- | --- |
| `client.Valuations.GetAccountValuation` | `GetAccountValuation(GetAccountValuationRequest request, …)` · `required Guid AccountId; required PriceQuality3 PriceQuality` · query `price_quality` | — | `AccountValuation` → `TotalSecurityValue(total_security_value).Amount/Currency` (= `totalValue`), `SecurityPositions (IReadOnlyList<AccountValuationSecurityPosition>)` each `Instrument(instrument: Instrument6)`, `Quantity (string)`, `Value (Value?)` (= holdings) | `ApiException<GetAccountValuationError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `Valuations.md`; `Models/AccountValuation.cs`; `Models/AccountValuationSecurityPosition.cs`; `Models/TotalSecurityValue.cs` |
| `client.Positions.ListPositions` *(alt. holdings source)* | `ListPositions(ListPositionsRequest request, …)` · `required Guid AccountId` · query `offset,limit` | — | `PositionsListResponse` (open `Models/PositionsListResponse.cs` if used) | `ApiException<ListPositionsError>` · `TryGetNoContent` [400,401,403,404,406,429,500,503,504] | map `Positions.md` |

`modelId` for `GET /api/portfolio` comes from `RetrievePortfoliosConfiguration.AllocationId`; `holdings`+`totalValue` from `GetAccountValuation`; `paidIn` from the app Payment store (not an Upvest field). `GetAccountValuation` requires `PriceQuality` — use `PriceQuality3.HighestAvailable` or `PriceQuality3.Eod` (`YOUR CALL — not in the map`; default `HighestAvailable` for a live view).

### Enum value tables (C# member ← wire)

| Enum (source) | Members used |
| --- | --- |
| `Status` — user (`Models/Enums/Status.cs`) | `Active`←ACTIVE, `Inactive`←INACTIVE, `Offboarding`←OFFBOARDING, `Offboarded`←OFFBOARDED. **No REJECTED** — open enum; a failed onboarding check surfaces elsewhere (§6). |
| `Status29` — top-up (`Models/Enums/Status29.cs`) | `Confirmed`←CONFIRMED, `Settled`←SETTLED, `Cancelled`←CANCELLED |
| `Status65` — portfolio order (`Models/Enums/Status65.cs`) | `New`←NEW, `Processing`←PROCESSING, `Filled`←FILLED, `Settled`←SETTLED, `Cancelled`←CANCELLED |
| `Currency` (`Models/Enums/Currency.cs`) | `Eur`←EUR, `Gbp`←GBP |
| `PriceQuality3` (`Models/Enums/PriceQuality3.cs`) | `Eod`←EOD, `HighestAvailable`←HIGHEST_AVAILABLE |
| `Type13` — account-group type (`Models/Enums/Type13.cs`) | `Personal`←PERSONAL (confirm member name in file at implement time) |
| `Type16` — account type (`Models/Enums/Type16.cs`) | `Portfolio`←PORTFOLIO, `Trading`←TRADING (confirm in file) |
| `Side15` — order side (`Models/Enums/Side15.cs`) | `Buy`←BUY, `Sell`←SELL (confirm in file) |
| `InstrumentIdType4` (`Models/Enums/InstrumentIdType4.cs`) | `Isin`←ISIN (default on `Allocation`), `Upvest`←UPVEST |
| `UpvestApiVersion` (`Models/Enums/UpvestApiVersion.cs`) | `_1` (request-record default; leave as-is) |

These are **open** `OpenStringEnum<T>` records (static members, PascalCase; `Match`/`TryGetKnownValue`; `otherwise` receives any undeclared wire value) — not C# enums. `MUST load dotnet-models` before mapping them.

### Client construction / auth / server (source: `sdk-map.md` "Getting a client", "Request signing", "Servers & auth")

- Constructor: `new UpvestInvestmentApiClient(HttpClient httpClient, UpvestInvestmentApiClientOptions options)` — the only ctor. Register as a **singleton**; own the `HttpClient` lifetime per `dotnet-client-initialization`.
- OAuth: `options.OauthClientCredentials = new UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials { ClientId = <Upvest:ClientId>, ClientSecret = <Upvest:ClientSecret> }` (both `required string`; `Scope` is per-operation, leave unset).
- **Signing (required on every call, incl. the token request):** `options.Hooks = [ new UpvestSigningHook(new UpvestHttpSignatureCredentials(keyId: <Upvest:SigningKeyId>, privateKeyPem: <read file at Upvest:SigningKeyPath>, upvestClientId: <Upvest:ClientId>, privateKeyPassphrase: <Upvest:SigningKeyPassphrase or null>)) ]`, or `UpvestHttpSignatureCredentials.FromFile(keyId, privateKeyPath, upvestClientId, privateKeyPassphrase)`. **Do not** rely on `UpvestSigningHook.FromEnvironment()` / `OauthClientCredentials` env fallback — the SDK's env var names (`UPVEST_API_KEY_ID`, `UPVEST_API_CLIENT_ID`, …) differ from the task's (`UPVEST_*`); bind from `Upvest:` config and construct explicitly. (`Signing/UpvestSigningHook.cs`, `Signing/UpvestHttpSignatureCredentials.cs`.)
- If neither a signing hook nor the signing env vars are set, **every call throws `InvalidOperationException` before being sent** — covered by the §5 fail-fast check.
- Base URL: `options.Server.Default.Production.BaseUrl = <Upvest:BaseUrl>` (used verbatim); keep `options.Environment = ServerEnvironment.Production` (default; → `https://sandbox.upvest.co` unless overridden). One server group (`Default`).

### CONFIG — binding keys (bind from `Upvest:`; hard-code no values)

| Binding key | SDK target | Env var that fills it | Secret? |
| --- | --- | --- | --- |
| `Upvest:ClientId` | `OAuth2ClientCredentials.ClientId` + signing `upvestClientId` | `UPVEST_CLIENT_ID` | no |
| `Upvest:ClientSecret` | `OAuth2ClientCredentials.ClientSecret` | `UPVEST_CLIENT_SECRET` | **yes** |
| `Upvest:SigningKeyId` | `UpvestHttpSignatureCredentials.keyId` | `UPVEST_SIGNING_KEY_ID` | no |
| `Upvest:SigningKeyPath` | path read into `privateKeyPem` (via `FromFile`) | `UPVEST_SIGNING_KEY_PATH` | no (points to secret) |
| `Upvest:SigningKeyPassphrase` | `privateKeyPassphrase` | `UPVEST_SIGNING_KEY_PASSPHRASE` | **yes** |
| `Upvest:BaseUrl` | `options.Server.Default.Production.BaseUrl` | `UPVEST_BASE_URL` | no |

App binding is `YOUR CALL — not in the map` (e.g. `services.Configure<UpvestOptions>(config.GetSection("Upvest"))`, env vars already enabled in `Program.cs:86`). The six `UPVEST_*` names are task-supplied, not SDK fallbacks.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| `modelId` chosen by a shopper (`PUT /api/portfolio/investor/model`) must be one the operator defined | `UpdatePortfoliosConfiguration`/`CreatePortfoliosConfiguration` ← app ModelPortfolio store (mirrors `Portfolios.ListPortfoliosAllocations`) | model-choose handler: look up the app ModelPortfolio by `modelId`, 404/400 if absent, before calling the configuration write |
| `modelId` in realignment (`POST …/models/{modelId}/realignments`) must be an existing operator model | `TriggerPortfolioRebalancing` ← app ModelPortfolio store (`ListPortfoliosAllocations`) | realignment handler: resolve `modelId` → Upvest `allocationId` from the app store before the trigger call |
| `instrumentId` (ISIN) in a model's `funds` must be a real tradeable instrument | `CreatePortfoliosAllocation`/`UpdatePortfoliosAllocation` ← `Instruments.ListInstruments` (`instruments:read`) | `PUT models` handler: validate each ISIN against a set read from `ListInstruments` before the allocation write. The app does not read this set today — **add the read**. (Sandbox 401 on `instruments:read`, §6 — the check still belongs in code; degrade to format-only ISIN validation when the list is unavailable.) |
| `accountId` used by configuration/order/valuation must be the one created for this shopper | `CreatePortfoliosConfiguration`, `CreatePortfoliosOrder`, `GetAccountValuation` ← `AccountsApi.CreateAccount` (stored on the Investor record) | every per-shopper handler: read `accountId` from the caller's Investor record (keyed by token identity), never from the request body |

> ⚠ The map never states these — derived from the task. The weights-sum-to-100 and per-fund-weight rules are **single-operation** request validation (Upvest also validates via `Allocation.Weight` regex and the "must sum to 100" doc), not cross-operation, so they live in the `PUT models` request validator, not this table.

---

## 3. Trap notes

- **T1 (client/DI):** how the `HttpClient` lifetime and singleton registration must be set up so one signed client is reused — getting it wrong leaks sockets or re-reads config per call. `MUST load dotnet-client-initialization`.
- **T2 (auth/signing):** what actually must be configured for OAuth **and** HTTP message signing together, and what a missing/misapplied credential throws before any bytes are sent (`AuthSchemeException` vs the signing `InvalidOperationException`). `MUST load dotnet-authentication`.
- **T3 (unions on read):** `UserCreateRequest1`, `UserGetResponse`, `AccountCreateResponse`, `AccountGroupCreateResponse`, `InstrumentId`, `TriggerPortfolioRebalancingRequest` are `AnyOf` unions — reading the wrong arm or ignoring `TryGetUnknown` loses data silently. `MUST load dotnet-models`.
- **T4 (realignment has no status):** `PortfoliosRebalancingExecution` exposes **no** status member — deriving `pending`/`completed`/`failed` for `GET …/realignments/{id}` from it needs care about what "retrievable" vs "done" means. `MUST load dotnet-calling-endpoints` (read the `<remarks>` on `RetrievePortfoliosRebalancingExecution` in `Api/PortfoliosRebalancing.cs`).
- **T5 (money as strings):** `cash_amount`/`weight`/`amount`/`quantity` are regex-constrained **strings**, not numbers — the app's JSON (euros, 2 decimals, as numbers) must be converted to/from these strings exactly, respecting each regex. `MUST load dotnet-models`.
- **T6 (deserialization drift):** a 2xx body missing a `required` member, or a non-2xx body not matching `{Operation}Error`, throws `ResponseDeserializationException` (an `ApiException`, not `ApiException<TError>`) — a catch ladder that only handles `ApiException<TError>` lets it escape. `MUST load dotnet-error-handling`.
- **T7 (retries & timeouts):** what `RetryOptions.Timeout` bounds vs the whole-call budget, and which verbs the SDK may resend, decide duplicate risk on the writes. `MUST load dotnet-configuration-resilience`.
- **T8 (sensitive onboarding data):** the `UserTolCreateRequest`/tax bodies carry personal data; what the SDK logs by default (and the `UPVEST_INVESTMENT_API_LOG=trace` footgun) decides the logging posture. `MUST load dotnet-configuration-resilience`.

---

## 4. REQUIRED READING (load all before implementation starts; names plugin-qualified)

| Skill | Governs |
| --- | --- |
| `upvest-v4:dotnet-client-initialization` | Step 1 — client construction, HttpClient ownership, singleton DI registration (T1) |
| `upvest-v4:dotnet-authentication` | Step 1 — OAuth client credentials + HTTP message signing wiring, fail-fast (T2) |
| `upvest-v4:dotnet-calling-endpoints` | Steps 3–7 — per-operation request records, `requestOptions`/`cancellationToken`, reading `<remarks>` (T4) |
| `upvest-v4:dotnet-models` | Steps 3–7 — AnyOf unions, open enums, string-encoded money/weights, `AdditionalProperties` (T3, T5) |
| `upvest-v4:dotnet-error-handling` | Step 8 — Case A `TryGetNoContent`/`TryGetRawError`, `SdkException` family, **`ResponseDeserializationException`** (T6) |
| `upvest-v4:dotnet-configuration-resilience` | Steps 1,8 — retries/timeouts, base-URL override, pagination, logging posture (T7, T8) |
| `upvest-v4:dotnet-testing` | tests — HttpClient stub seam, faking the clock, constructing SDK exceptions |

**Always-on hazard (verbatim):** a body that does not match its declared type — a drifted/malformed **2xx** (a missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch ladder handling only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | **Credential fail-fast** | At startup (DI registration / `IHostedService` check) verify **each** part present & non-blank: `Upvest:ClientId`, `Upvest:ClientSecret`, `Upvest:SigningKeyId`, `Upvest:SigningKeyPath` (+ file exists & readable), `Upvest:BaseUrl`; `Upvest:SigningKeyPassphrase` may be blank only if the key is unencrypted. A blank part is not a missing part — check all. Refuse to start otherwise, rather than discovering it as the signing `InvalidOperationException`/`AuthSchemeException` on the first call. `YOUR CALL — not in the map` (bind + validate the options object). |
| 2 | **Secret sourcing & rotation** | Secrets (`ClientSecret`, `SigningKeyPassphrase`, the PEM at `SigningKeyPath`) come from config/env (user-secrets in dev, env/secret-store in prod). The client is built **once** at singleton registration and captures the options, so a rotated secret takes effect only on **process restart**. If hot rotation is required, wrap the client behind a factory that rebuilds on config change — not in scope by default. `YOUR CALL — not in the map`. |
| 3 | **Total timeout budget** | The caller's budget = per-attempt `RetryOptions.Timeout` × (1 + `MaxRetries`) + backoff/jitter, bounded further by the request `CancellationToken`. Enforce a single whole-call budget by passing a linked `CancellationToken` from each endpoint. Exact knob semantics: `dotnet-configuration-resilience` (T7). `YOUR CALL — not in the map` (pick the budget per endpoint). |
| 4 | **Write-retry ownership** | The SDK auto-retries only the verbs in `RetryOptions.HttpMethodsToRetry` (default excludes non-idempotent writes — confirm in `dotnet-configuration-resilience`). All Flow-1/3/4 creates are POSTs carrying a real `IdempotencyKey`, so a resend is safe; do not add app-level blind retries on top. `UNVERIFIED` default set until confirmed against `RetryOptions.Default()`. |
| 5 | **Idempotency & ambiguous writes** | Real keys exist: `CreateUser`, `CreateAccountGroup`, `CreateAccount`, `SetTaxResidencies`, `CreatePortfoliosAllocation`, `CreatePortfoliosConfiguration`, `UpdatePortfoliosConfiguration`, `CreateTopup`, `CreatePortfoliosOrder`, `TriggerPortfolioRebalancing` each take `required Guid IdempotencyKey`. The app generates this key once, **persists it on the owning app record** (Investor/Payment/Realignment), and reuses it on every resend so a replay returns the original resource. `UpdatePortfoliosAllocation` has **no** key → reconcile by re-reading `RetrievePortfoliosAllocation`. The injected `Idempotency-Key` header on keyless calls is **not** a key. Source: each `Requests/*` record. |
| 6 | **Observability** | Log at Information: app record ids, Upvest resource ids (userId, accountId, allocationId, topupId, orderId, executionId), operation name, resulting status. Log at Warning/Error: the `RawError` from `TryGetNoContent`/`TryGetRawError` — `StatusCode` + `ReadAsString()` (which carries Upvest's correlation/trace id) into the app's `IAppLogger<>`. **Never** log request bodies on onboarding/tax/payment paths (§7). `YOUR CALL — not in the map`. |
| 7 | **Sensitive data** | Scope carries personal data (`UserTolCreateRequest`: name, email, birth data, phone, address, nationality; tax residency; identity-document form fields). Posture: `options.Logging.LogRequestBody` **off**; set `options.Logging.LoggerFactory` explicitly (never leave it unset — that arms `UPVEST_INVESTMENT_API_LOG=trace` body logging); app never echoes request bodies for these endpoints; store the onboarding form encrypted-at-rest per eShopOnWeb norms. `YOUR CALL — not in the map` + `dotnet-configuration-resilience`. |
| 8 | **Environment selection** | One server group `Default`. `ServerEnvironment.Production` → sandbox `https://sandbox.upvest.co`; `Environment2` → live `https://api.upvest.co`. The task sets `options.Server.Default.Production.BaseUrl` from `Upvest:BaseUrl` verbatim and keeps `Environment = Production`, so each deployment points at its own Upvest host via that one key; no sandbox vs live code branch. Since the SDK *does* expose both, keep non-prod deployments pointed at the sandbox base URL. Source: `sdk-map.md` Servers & auth. |
| 9 | **Duplicate prevention under concurrency** | See DUPLICATE CLAIMS. The app store (EF, in-memory here) holds the claim; a primary-key insert refuses the second caller (a PK is refused even by the in-memory provider; a unique index may not be). |
| 10 | **Partial results** | See PAGED READS — model/payment/position/allocation lists cap at `limit`; the caller learns truncation from `Meta.total_count` vs `Meta.count`. |
| 11 | **Unknown outcomes** | See UNKNOWN OUTCOMES — every create resends with its stored `IdempotencyKey` (or re-reads by stored id) in its own `catch (SdkConnectionException/SdkTimeoutException)` before reporting, so a connection failure after the provider acted is settled, not reported as a hard failure. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `POST /api/portfolio/investor` (`CreateUser`→group→account) | app `Investor` table, unique index on `BuyerId` (token identity) | the unique `BuyerId` index refuses a second row (SQL); the pre-read + idempotent-resume collapses the common case | `PortfolioService.SignUpInvestorAsync` reads by `InvestorByBuyerIdSpecification`; an already-onboarded record returns early, an incomplete one resumes with stored idempotency keys | `PortfolioService.SignUpInvestorAsync`: `_investors.FirstOrDefaultAsync(...)` → `_investors.AddAsync(investor)` → `_upvest.CreateUserAsync(..., investor.UserIdempotencyKey, ...)` |
| `PUT /api/portfolio/investor/model` (`CreatePortfoliosConfiguration`) | app `Investor.ConfigurationCreated` flag (one investor row, keyed by `BuyerId`) | the flag selects `Update…` instead of a second `Create…`; the investor row is unique per shopper | `PortfolioService.ChooseModelAsync` branches on `investor.ConfigurationCreated` | `PortfolioService.ChooseModelAsync`: `investor.SelectModel(...)` → `_upvest.CreateInvestorConfigurationAsync(...)` / `UpdateInvestorConfigurationAsync(...)` |
| `POST /api/portfolio/payments` (`CreateTopup`+`CreatePortfoliosOrder`) | app `Payment` table, **PK = app `Id`, unique index on app-generated `PaymentId`** (also the Upvest top-up `IdempotencyKey`) | the unique `PaymentId` index refuses a reused id | `PortfolioService.PayInAsync` creates the Payment claim before any Upvest call | `PortfolioService.PayInAsync`: `new Payment(Guid.NewGuid(), ...)` → `_payments.AddAsync(payment)` → `_upvest.CreateTopUpAsync(..., payment.PaymentId, ...)` |

> Order per row: **claim (EF insert) → SDK call(s) → record result**. The claim is written before any Upvest call so the second caller is stopped before reaching the provider; release (delete the claim) if the provider refuses the call. Store = the app's own EF store, never Upvest. A provider `IdempotencyKey` sits **beside** the claim, not instead of it.

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `GET /api/portfolio/models` (app store) | not paged — returns all models | n/a: the operator defines a *small* set; the endpoint returns every model in one response (no cap) | `GetModelsEndpoint` → `PortfolioService.GetModelsAsync` → `_models.ListAsync(new ModelPortfoliosWithFundsSpecification())` (no limit) |
| `GET /api/portfolio/payments` (app `Payment` store) | not paged — returns all of the caller's payments | n/a: a single shopper's payments are returned in full, newest first | `GetPaymentsEndpoint` → `PortfolioService.GetPaymentsAsync` → `_payments.ListAsync(new PaymentsByBuyerIdSpecification(buyerId))` |
| `ListInstruments` (internal ISIN validation) | `offset`/`limit` (1000) | bounded loop (≤50 pages); stops at `Meta.total_count`/short page; on truncation the membership check degrades to format-only, never a silent partial | `UpvestPortfolioService.CheckInstrumentsAsync` (the `for (page …)` loop reading `response.Meta.TotalCount`) |

### UNKNOWN OUTCOMES

The pattern implemented: each in-scope write runs inside the `UpvestPortfolioService.CallAsync` error boundary, which marks a write whose connection failed (or whose 2xx body was unreadable) as `OutcomeUnknown`. The orchestrator (`PortfolioService`) then does **not** report a failure for an unknown outcome: onboarding steps keep their stored idempotency keys and resume on the next `POST /api/portfolio/investor` (idempotent replay); a payment with an unknown outcome is left `pending` and settled from Upvest on the next `GET /api/portfolio/payments` read; a realignment likewise stays `pending` until `GET /api/portfolio/realignments/{id}` re-reads. Each create reuses the stored idempotency key so a replay returns the original resource rather than duplicating it.

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `CreateUser` | resend `CreateUser` with same `IdempotencyKey` (idempotent replay) | `Investor.UserIdempotencyKey` | `PortfolioService.SignUpInvestorAsync` resumes when `!UpvestUserId.HasValue`; `CallAsync` flags `OutcomeUnknown` so the claim is kept (not deleted) | covered by `CallAsync` `SdkConnection/Timeout` → `OutcomeUnknown` branch; `SignUp_WhenUpvestRejectsUser_ReleasesClaim_AndThrows` covers the definitive-reject path |
| `CreateAccountGroup` / `CreateAccount` / `SetTaxResidencies` | resend same key (steps are skipped once their id/flag is set) | stored keys + `UpvestUserId`/`UpvestAccountGroupId` on Investor | `PortfolioService.SignUpInvestorAsync` conditional resume blocks | onboarding resume test path |
| `CreateTopup` + `CreatePortfoliosOrder` | re-read via `GetPaymentStatusAsync` (`RetrieveTopup`/`RetrievePortfoliosOrder`) | `Payment.UpvestTopUpId` / `Payment.UpvestOrderId` | `PortfolioService.PayInAsync` leaves `pending` on `OutcomeUnknown`; `GetPaymentsAsync` reconciles | `PayIn_WhenInvestorNotActive_IsRefused` (refusal path); reconciliation via `GetPaymentsAsync` |
| `CreatePortfoliosAllocation` / `UpdatePortfoliosAllocation` | re-read `ListPortfoliosAllocations` / resend with `ModelPortfolio.AllocationIdempotencyKey` | `ModelPortfolio.UpvestAllocationId` / key | `PortfolioService.UpsertModelAsync` (model persisted before the Upvest write) | scope-refusal degrade verified live |
| `TriggerPortfolioRebalancing` | `RetrievePortfoliosRebalancingExecution` + `ListPortfolioRebalancingExecutionOrders` once id known | `Realignment.UpvestExecutionId` / key | `PortfolioService.TriggerRealignmentAsync` leaves `pending`; `GetRealignmentAsync` reconciles | `Realignment_AsAdmin_ThenGet_ReportsStatus` (pending → completed) |

> Each outcome is settled in the failing write's own `catch (SdkConnectionException/SdkTimeoutException …)` before it returns, or recorded `pending`/`unknown` on the app record for a reconciliation pass to settle — not left for the caller to retry.

---

## 6. Assumptions & Blockers

**Sandbox scope (environment, not a code gap — build every flow in full):** the sandbox client is authorised **only for user operations**. Any call needing a non-user scope is refused `401 "This client is not authorised to use these scopes."` Flows that will hit this 401 in the sandbox:

- **Verifiable in sandbox:** Flow 1 user steps — `Users.CreateUser` (`users:admin`), `Users.RetrieveUser` (`users:read`/`admin`).
- **Will 401 in sandbox (non-user scopes), built but not end-to-end verifiable here:** `SetTaxResidencies` (`taxes:admin`); `CreateAccountGroup`/`CreateAccount`/`RetrieveAccount` (`accounts:admin`/`read`); all `Portfolios.*` and `PortfoliosRebalancing.*` (`portfolios:admin`/`read`); `TopUps.*` (`topups:admin`/`read`); `Valuations.GetAccountValuation` (`valuations:read`); `Positions.ListPositions` (`positions:read`); `Instruments.ListInstruments` (`instruments:read`). This covers Flows 2–5 and the account/tax steps of Flow 1.

**Blockers:**

- **B1 — no sandbox cash-injection operation.** Flow 3 requires money to reach the investor's account before `CreatePortfoliosOrder` (BUY) can invest. The map's funding surface is `TopUps.CreateTopup` (returns a `settlement_reference` expecting a **real SEPA credit transfer** to settle: `PaymentsTopupsResponse` doc) and a read-only `CreditFundings` controller (`ListCreditFundings`/`RetrieveCreditFunding`/`RetrieveFundingDetails` — **no create**). There is **no SDK operation to simulate/settle sandbox cash**. So in the sandbox a top-up will not auto-settle and the BUY order will lack funds. Build the two-step pay-in (top-up then order) in full; settling the cash depends on a Upvest-provided sandbox mechanism outside the SDK surface. **Confirm with the user** which sandbox funding path Upvest expects (manual settlement simulation, pre-funded account, or auto-investment on incoming credit funding).
- **B2 — "rejected" investor status has no direct Upvest enum.** The user `Status` enum is only `ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED` — no `REJECTED`. A failed onboarding check is not represented on `UserTol.Status`. Mapping the task's `rejected` requires either a `UserChecks` read (`client.UserChecks`, not yet in the contract sheet) or treating a terminal failed check as `rejected`. **Assumption:** derive `status` as `pending` while the user is `INACTIVE`/account group not yet `ACTIVE`, `active` once the portfolio account is ready, and `rejected` only from an explicit failed-check signal; confirm the exact signal with the user (likely needs `UserChecks` added — `users:*` scope, sandbox-verifiable).

**Assumptions (major — surface to user):**

- **A1 — identity document & tax fields have no onboarding home.** `UserTolCreateRequest` has **no** `identityDocument`, `taxId`, or `taxCountry` members. Tax maps to `TaxResidencies.SetTaxResidencies` (separate call, `taxes:admin` → sandbox 401). The identity document (`type`/`number`/`issuingCountry`/`expiryDate`) maps to **no** in-scope Upvest operation (`UserIdentifiers` is for client-assigned external ids, not ID documents). **Assumption:** the app persists the full sign-up form (incl. identity document) in its own Investor store, sends personal details via `CreateUser` and tax via `SetTaxResidencies`, and does **not** transmit the identity document to Upvest in this scope. Confirm whether identity-document submission to Upvest is required (may need a `Files`/`UserChecks` flow not in the current sheet).
- **A2 — onboarding consents required by the SDK but not in the task form.** `UserTolCreateRequest.Fatca` is `required`; `TermsAndConditions`/`DataPrivacyAndSharingAgreement` are expected for adult users. The task form omits these. **Assumption:** the app supplies a default/collected FATCA status and the consents at onboarding (operator-configured defaults for the sandbox); confirm the real values.
- **A3 — model ↔ allocation identity.** The task's `modelId` is the **app's** model id (route param); it is mapped to a Upvest `allocationId` stored on the ModelPortfolio record. `GET /api/portfolio` returns `modelId` as that app id (translated from `PortfoliosConfiguration.AllocationId`). `YOUR CALL — not in the map`.
- **A4 — caller identity.** Every per-shopper endpoint reads the caller's identity from the JWT (per eShopOnWeb's `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`; admin endpoints add `Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS`, exemplar `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs:30`) and loads that shopper's Investor record; a shopper can never reference another's Upvest ids. `YOUR CALL — not in the map`.

**Minor assumptions (proceeding):** amounts arrive as JSON numbers in euros (2 dp) and are converted to Upvest's decimal **strings** respecting each regex (T5); `price_quality = HighestAvailable` for the live portfolio view; GET lists return newest-first by sorting the app store.

---

## 8. Implementation resolutions (decided headless — no user to consult)

Every Blocker and major assumption above was decided and implemented; the markers are settled here.

- **B1 (sandbox cash injection) → RESOLVED as not-a-gap.** The task states money reaches the account "through whatever Upvest provides"; the SDK's funding capability is `TopUps.CreateTopup`. Pay-in is built as `CreateTopup` (credit) + `CreatePortfoliosOrder` (BUY), reconciled by re-reads (`PortfolioService.PayInAsync` / `UpvestPortfolioService.CreateTopUpAsync` + `PlacePortfolioOrderAsync`). Whether the top-up auto-settles is environmental (and `topups:*`/`portfolios:*` scopes are refused by the sandbox client anyway); not reported as a gap.
- **B2 ("rejected" status) → RESOLVED.** `UserChecks` needs `checks:*` scope (also refused in sandbox), so `rejected` is derived from the user resource alone: `UserTol/UserByol.Status == OFFBOARDED | OFFBOARDING` → `rejected`; a ready `PORTFOLIO` account (`Status21.Active`) → `active`; otherwise `pending` (`UpvestPortfolioService.GetInvestorStatusAsync`). Only `RetrieveUser` is needed, which is sandbox-verifiable.
- **A1 (identity document & tax) → RESOLVED.** The full sign-up form (incl. the identity document) is persisted on the `Investor` entity; personal details go to Upvest via `CreateUser`, tax via `SetTaxResidencies`. The identity document maps to no in-scope Upvest operation, so it is **not** transmitted to Upvest — it is kept as the shop's own verification record. Personal data is never logged (`LogRequestBody` off, explicit `LoggerFactory`, and the provider-error hook logs only `request_id`/`title`, never `detail`).
- **A2 (FATCA & consents) → RESOLVED.** `Fatca` is `required`; the shop supplies a default non-US status (`Status=false`, `ConfirmedAt=now`). `TermsAndConditions`/`DataPrivacyAndSharingAgreement` need a `consent_document_id` the task does not provide and are optional on the SDK model, so they are omitted. (`UpvestPortfolioService.CreateUserAsync`.)
- **A3 (model ↔ allocation) → RESOLVED (YOUR CALL).** The app `modelId` is the operator's route value; it maps to a Upvest `allocationId` stored on `ModelPortfolio.UpvestAllocationId`. `GET /api/portfolio` translates `PortfoliosConfiguration.AllocationId` back to the app `modelId` (`PortfolioService.ResolveModelIdAsync`).
- **A4 (caller identity) → RESOLVED (YOUR CALL).** Every per-shopper endpoint reads the caller from `ClaimsPrincipal.Identity.Name` (JWT) and loads that shopper's record by `BuyerId`; the identity is never bindable from a request body (`AuthenticatedRequest.BuyerId` has no public setter). Exemplar auth wiring followed: `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs:30`.
- **Config binding (YOUR CALL) → RESOLVED.** `UpvestOptions` binds `Upvest:` with the six exact keys; fail-fast `ValidateOnStart` refuses to boot on any blank credential string; the signing key file is validated when the signed client is built (`UpvestServiceCollectionExtensions`). `Upvest:BaseUrl` is set verbatim on `options.Server.Default.Production.BaseUrl`.
- **Rotation (PR #2) → RESOLVED.** The client is built once at singleton registration and captures the options; a rotated secret takes effect on process restart. Hot-rotation is out of scope.
- **Timeout budget (PR #3) → RESOLVED.** Per-attempt `HttpClient.Timeout = 30s` and `RetryOptions.Timeout = 20s` with `MaxRetries = 2`; each endpoint passes `HttpContext.RequestAborted` (the total bound a disconnected caller experiences). Writes (POST) are not retried by default.
- **`price_quality` (YOUR CALL) → RESOLVED.** `PriceQuality3.HighestAvailable` for the live portfolio view (`UpvestPortfolioService.GetPortfolioAsync`).

### Two live-traffic findings (confirmed against the sandbox)

- **AnyOf arm ordering on user create/read (T3, confirmed).** `CreateUser` returns `200` but the `AnyOf(UserTol, UserByol)` response converter binds the body to the **`UserByol`** arm, not `UserTol`. Reading only `TryGetUserTol` silently discarded a successful create. Fixed to read the id/status from **either** arm (`UpvestPortfolioService.CreateUserAsync` / `GetInvestorStatusAsync`). Both arms carry `id` (`Guid`) and `status` (`Status`).
- **`CreateUser` error body shape (T6, confirmed).** A `400` from `POST /users` returns `{detail, request_id, status, title, type}`, which does **not** match the generated `CreateUserError`, so it surfaces as `ResponseDeserializationException` (an `ApiException`, not `ApiException<CreateUserError>`). The boundary catches `ResponseDeserializationException` first, so it is handled correctly. The live sandbox also requires **at least one EEA nationality** (e.g. `DE`); a non-EEA nationality (`GB`) is rejected `400` by Upvest — a data requirement, enforced by Upvest, surfaced to the caller as `400`.

---

## 7. Source labels

Every CONTRACT SHEET row cites its map page and the source file(s) it was read from (all under the plugin SDK root `plugins/upvest-v4/sdk/dotnet/`), so any later check is one targeted open. Labels used elsewhere: `YOUR CALL — not in the map` = an application decision (persistence, config binding, identity, status derivation) to settle at implementation time against the task; `UNVERIFIED` = only live traffic (or opening `RetryOptions.Default()`) can settle it — row #4's default retry set. Anything that blocks planning is in §6, not a row.
