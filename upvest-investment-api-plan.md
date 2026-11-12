# Upvest Investment API integration plan — managed investment portfolios for eShopOnWeb

Workflow: `dotnet-integrate-upvest-investment-api`. SDK source (and the map): `marketplace/plugins/upvest-v4/sdk/dotnet/` (plugin-relative SDK root). API spec `1.172.0`. Every contract fact below is grounded in that map/source; cells carry a **source** label (§7). This feature is **additive** to the existing catalog/basket/order flow.

> ⚠ SANDBOX REALITY (environment, not a gap): the sandbox client is authorised **only for `users:*` scopes**. Any call needing another scope returns `401 "This client is not authorised to use these scopes"`. Every flow is built in full; live verification is possible only for the user/identifier operations (`Users.CreateUser`, `Users.RetrieveUser`, `UserIdentifiers.CreateIdentifier`, `UserIdentifiers.ListUserIdentifiers`). All other operations are coded to contract and will 401 in this sandbox — that is expected and must NOT be "fixed" by inventing a workaround. Scope per operation is in the CONTRACT SHEET.

---

## 1. Scope & sequence

All endpoints live in `src/PublicApi`, routed under `/api/`, JWT-authenticated (scheme already configured in `src/PublicApi/Program.cs`). Caller identity = the `ClaimTypes.Name` claim (username/email) that `IdentityTokenClaimService` puts on the token (`src/Infrastructure/Identity/IdentityTokenClaimService.cs`). Read it in each endpoint from the `ClaimsPrincipal`/`HttpContext.User` (minimal-API handler parameter). Admin endpoints use `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` (exemplar: `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`).

**Step 0 — Wiring.** Add an `UpvestSettings` options class bound to config section `Upvest` (keys in §5 row 1). Register a singleton `UpvestInvestmentApi.UpvestInvestmentApiClient` (one `HttpClient`, options built once) via DI, extending `Microsoft.eShopWeb.Infrastructure.Dependencies.ConfigureServices` (`src/Infrastructure/Dependencies.cs`) and/or `src/PublicApi/Program.cs`. Add a fail-fast credential check at startup. Add persistence entities (Step 0b).

**Step 0b — Persistence (app-owned store).** The app needs local records that outlive a single request to map shopper↔Upvest resources, back the status endpoints, and serve as duplicate-claim stores. Add EF entities as `IAggregateRoot` to `CatalogContext` (`src/Infrastructure/Data/CatalogContext.cs`), with configs under `src/Infrastructure/Data/Config/`, reached through `IRepository<T>`/`IReadRepository<T>` (`EfRepository`). On this machine the store is EF **in-memory** (`UseOnlyInMemoryDatabase=true`, `src/Infrastructure/Dependencies.cs`) — data lives only within one run, each host has its own store. Proposed aggregates: `InvestorRecord` (PK = ShopperId), `PortfolioModelRecord` (PK = ModelId), `PayInRecord` (PK = PaymentId), `RealignmentRecord` (PK = RealignmentId). Exact fields are `YOUR CALL — not in the map` (settle at implementation; see §6 and DUPLICATE CLAIMS).

**Flow 1 — Becoming an investor** (`POST/GET /api/portfolio/investor`, JWT, non-admin): `Users.CreateUser` (TOL body) → `UserIdentifiers.CreateIdentifier` (identity document) → `TaxResidencies.SetTaxResidencies` (tax) → `AccountGroups.CreateAccountGroup` → `AccountsApi.CreateAccount` (type `PORTFOLIO`). Readiness polled via `Users.RetrieveUser` + `AccountsApi.RetrieveAccount` (and/or `AccountGroups.RetrieveAccountGroup`). Store `InvestorRecord`. Status: `pending`→`active`→`rejected` derived (§CROSS-OP / §6).

**Flow 2 — Model portfolios** (`PUT /api/portfolio/models/{modelId}`, `GET /api/portfolio/models` — admin; `PUT /api/portfolio/investor/model` — investor): PUT model = create-or-replace a `PortfoliosAllocation`: first time `Portfolios.CreatePortfoliosAllocation` (store ModelId→AllocationId), thereafter `Portfolios.UpdatePortfoliosAllocation`. GET models = `Portfolios.ListPortfoliosAllocations` (page). Investor chooses model = `Portfolios.CreatePortfoliosConfiguration` first time / `Portfolios.UpdatePortfoliosConfiguration` to switch (links the investor's account to the allocation).

**Flow 3 — Paying in** (`POST/GET /api/portfolio/payments`, investor): refuse unless InvestorRecord is `active` with a model chosen. Pay-in = `TopUps.CreateTopup` (credit cash to the account **group**) → `Portfolios.CreatePortfoliosOrder` (`side = BUY`, cash invested across the account's configured allocation weights). Store `PayInRecord`. List = `TopUps.ListTopups` / `Portfolios.ListPortfoliosOrders` (page, newest first). Status `pending`→`invested`→`failed` from the order status (§CROSS-OP).

**Flow 4 — Realignment** (`POST /api/portfolio/models/{modelId}/realignments`, `GET /api/portfolio/realignments/{realignmentId}` — admin): trigger = `PortfoliosRebalancing.TriggerPortfolioRebalancing` with the `Allocations` arm = `[AllocationId]`; store `RealignmentRecord` (RealignmentId→ExecutionId). Status = `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` + `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (see §6 — the execution resource carries **no status field**).

**Flow 5 — The investor's portfolio** (`GET /api/portfolio`, investor): model followed = `Portfolios.RetrievePortfoliosConfiguration` (by AccountId → `AllocationId` → ModelId). Holdings + per-fund value + total = `Valuations.GetAccountValuation` (`SecurityPositions` give instrument/quantity/value; `TotalSecurityValue` is the total). `paidIn` = app-summed from `PayInRecord`/`TopUps.ListTopups`. (`Positions.ListPositions` is an alternate holdings source but carries no value.)

**Step N — Error boundary & polling.** No inbound webhook route; all outcomes learned by the app's own polling calls. Translate SDK exceptions at one boundary (§3, REQUIRED READING).

---

## 2. CONTRACT SHEET

> ⚠ **Signatures below are generated code, copied verbatim.** Each operation that takes input takes **ONE request record** as its first parameter, built with an object initializer whose property names are the record's own (e.g. `new CreateUserRequest { IdempotencyKey = …, Body = … }`) — never flat positional arguments. An operation with no inputs takes none. `requestOptions` and `cancellationToken` are optional trailing params.
> ⚠ **Every SDK type is fully qualified** with the namespace its source path implies (taken from the path the map gives for THAT type): `UpvestInvestmentApi.Models.*` for `Models/`, `UpvestInvestmentApi.Models.AnyOf.*` for `Models/AnyOf/`, `UpvestInvestmentApi.Models.Enums.*` for `Models/Enums/`, `UpvestInvestmentApi.Requests.<Controller>.*` for `Requests/<Controller>/`, `UpvestInvestmentApi.Errors.*` for `Errors/`. Do not infer a type's namespace from a neighbouring type.
> **Error case is A (typed) for ALL 160 operations in this SDK** (no-throw `…Result` variants: none). Every row's accessors are `TryGetNoContent([NotNullWhen(true)] out UpvestInvestmentApi.Core.ErrorResponse.RawError? value)` for the listed statuses, plus inherited `TryGetRawError([NotNullWhen(true)] out RawError? error)` fallback. `ResponseDeserializationException` is separate (see REQUIRED READING).
> **Money on the wire is a decimal STRING, not a number.** The app's JSON contract is euros as a 2-dp number; convert to/from the SDK's decimal strings (`cash_amount`, `amount`, `quantity`, `weight`). Top-up `cash_amount` regex `^[0-9]{1,9}(\.[0-9]{2})?$`; order `cash_amount` allows more decimals; valuation `amount` is 2-dp.

### Flow 1 — investor onboarding

| # | Op (controller.method) | Scope (sandbox?) | Request record + members | Body model + fields (wire) | Response → fields read | Error statuses | source |
|---|---|---|---|---|---|---|---|
|1|`client.Users.CreateUser(req)`|`users:admin` ✅|`UpvestInvestmentApi.Requests.Users.CreateUserRequest` { `IdempotencyKey: Guid` **req**; `UpvestApiVersion` =`_1`; `Body: UserCreateRequest?` }|`UpvestInvestmentApi.Models.AnyOf.UserCreateRequest` — union; use **TOL** arm `UserCreateRequest.UserTolCreateRequest(UpvestInvestmentApi.Models.UserTolCreateRequest)`. TOL req fields: `FirstName`(first_name)**req**, `LastName`(last_name)**req**, `Email`(email)**req**, `BirthDate`(birth_date, DateTimeOffset date)**req**, `Nationalities`(nationalities, `IReadOnlyList<Nationality>` min1)**req**, `Address`(address, `Address2`)**req**, `Fatca`(fatca, `Fatca`)**req**; optional: `BirthCity`,`BirthCountry`,`PhoneNumber`. `Address2`: `AddressLine1`,`Postcode`,`Country`,`City` **req**. `Fatca`: `Status:bool`,`ConfirmedAt:DateTimeOffset` **req**.|`UpvestInvestmentApi.Models.AnyOf.UserCreateRequest1` → user `id` (store as UpvestUserId). Resolve arm via `TryGet…`.|400,401,403,406,429,500,503,504|Users.md; UserTolCreateRequest.cs; Address2.cs; Fatca.cs; UserCreateRequest.cs|
|2|`client.UserIdentifiers.CreateIdentifier(req)`|`users:admin` ✅|`UpvestInvestmentApi.Requests.UserIdentifiers.CreateIdentifierRequest` { `UserId: Guid` **req**; `UpvestApiVersion`; `Body: IdentifierCreateRequest?` }|`UpvestInvestmentApi.Models.IdentifierCreateRequest`: `Type`(type, `Type7`, default `NationalId`), `IssuingCountry`(issuing_country, `IssuingCountry`)**req**, `Identifier`(identifier, string, optional) ← task's doc `number`, `IdentifierStandard`(optional). ⚠ **No `expiryDate` field; `type` only NATIONAL_ID/PASSPORT** (see §6 Blocker).|`UpvestInvestmentApi.Models.Identifier`|400,401,403,404,406,429,500,503,504|UserIdentifiers.md; IdentifierCreateRequest.cs|
|3|`client.TaxResidencies.SetTaxResidencies(req)`|`taxes:admin` ❌401|`UpvestInvestmentApi.Requests.TaxResidencies.SetTaxResidenciesRequest` { `UserId: Guid` **req**; `IdempotencyKey: Guid` **req**; `Body: TaxResidenciesSetRequest?` }|`UpvestInvestmentApi.Models.TaxResidenciesSetRequest`: `TaxResidencies`(tax_residencies, `IReadOnlyList<UpvestInvestmentApi.Models.AnyOf.TaxResidencyForCreateRequest>` min1)**req** — carries taxId/taxCountry (confirm shape in `Models/AnyOf/TaxResidencyForCreateRequest.cs` at implement time).|`UpvestInvestmentApi.Models.TaxResidencyRecord`|400,401,403,404,406,429,500,503,504|TaxResidencies.md; TaxResidenciesSetRequest.cs|
|4|`client.AccountGroups.CreateAccountGroup(req)`|`accounts:admin` ❌401|`UpvestInvestmentApi.Requests.AccountGroups.CreateAccountGroupRequest` { `IdempotencyKey: Guid` **req**; `Body: UpvestInvestmentApi.Models.AnyOf.AccountGroupCreateRequest?` } (read body file at implement time)|`AccountGroupCreateRequest` (union: user/business) — use the user arm; a `PERSONAL` user account group.|`UpvestInvestmentApi.Models.AnyOf.AccountGroupCreateResponse` → group `id` (store as AccountGroupId)|400,401,403,404,406,429,500,503,504|AccountGroups.md|
|5|`client.AccountsApi.CreateAccount(req)`|`accounts:admin` ❌401|`UpvestInvestmentApi.Requests.AccountsApi.CreateAccountRequest` { `IdempotencyKey: Guid` **req**; `Body: AccountCreateRequest?` }|`UpvestInvestmentApi.Models.AnyOf.AccountCreateRequest` — use `AccountCreateRequest.AccountCreateUserRequest(UpvestInvestmentApi.Models.AccountCreateUserRequest)`: `UserId`(user_id)**req**, `AccountGroupId`(account_group_id)**req**, `Type`(type, `Type16`)**req** = **`Type16.Portfolio`** (PORTFOLIO account can hold a managed portfolio), `Name`(optional).|`UpvestInvestmentApi.Models.AnyOf.AccountCreateResponse` → account `id` (store as AccountId)|400,401,403,404,406,429,500,503,504|AccountsApi.md; AccountCreateRequest.cs; AccountCreateUserRequest.cs; Type16.cs|
|6|`client.Users.RetrieveUser(req)`|`users:read`/`admin` ✅|`UpvestInvestmentApi.Requests.Users.RetrieveUserRequest` { `UserId: Guid` **req** }|—|`UpvestInvestmentApi.Models.AnyOf.UserGetResponse` → TOL arm `UserTol`: `Status` (`UpvestInvestmentApi.Models.Enums.Status`: ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED)|401,403,404,406,429,500,503,504|Users.md; UserGetResponse.cs; UserTol.cs|
|7|`client.AccountsApi.RetrieveAccount(req)`|`accounts:read`/`admin` ❌401|`UpvestInvestmentApi.Requests.AccountsApi.RetrieveAccountRequest` { `AccountId: Guid` **req** }|—|`UpvestInvestmentApi.Models.AnyOf.AccountRetrieveResponse` → `Account`: `Status` (`UpvestInvestmentApi.Models.Enums.Status21`: PENDING_APPROVAL/ACTIVE/CLOSING/CLOSED/LOCKED), `Type`(`Type16`)|401,403,404,406,429,500,503,504|AccountsApi.md; AccountRetrieveResponse.cs; Account.cs|
|8|`client.AccountGroups.RetrieveAccountGroup(req)` (optional readiness)|`accounts:read`/`admin` ❌401|`UpvestInvestmentApi.Requests.AccountGroups.RetrieveAccountGroupRequest` { `AccountGroupId: Guid` **req** }|—|`UpvestInvestmentApi.Models.AnyOf.AccountGroupRetrieveResponse` → group status (PENDING_APPROVAL→ACTIVE per UserTol remark)|401,403,404,406,429,500,503,504|AccountGroups.md|

### Flow 2 — model portfolios

| # | Op | Scope | Request record + members | Body/response | source |
|---|---|---|---|---|---|
|9|`client.Portfolios.CreatePortfoliosAllocation(req)`|`portfolios:admin` ❌401|`UpvestInvestmentApi.Requests.Portfolios.CreatePortfoliosAllocationRequest` { `IdempotencyKey: Guid` **req**; `Body: PortfoliosAllocationCreateRequest?` }|Body `UpvestInvestmentApi.Models.PortfoliosAllocationCreateRequest`: `Name`(optional), `Allocation`(allocation, `IReadOnlyList<UpvestInvestmentApi.Models.Allocation>`)**req**. `Allocation`: `InstrumentId`(instrument_id, `UpvestInvestmentApi.Models.AnyOf.InstrumentId` — ISIN string via `InstrumentId.String(isin)` or implicit from string)**req**, `InstrumentIdType`(default `InstrumentIdType4.Isin`), `Weight`(weight, string regex `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`)**req**. Resp `UpvestInvestmentApi.Models.PortfoliosAllocation` → `Id` (store as AllocationId).|Portfolios.md; PortfoliosAllocationCreateRequest.cs; Allocation.cs; InstrumentId.cs|
|10|`client.Portfolios.UpdatePortfoliosAllocation(req)`|`portfolios:admin` ❌401|`UpvestInvestmentApi.Requests.Portfolios.UpdatePortfoliosAllocationRequest` { `AllocationId: Guid` **req**; `Body: PortfoliosAllocationUpdateRequest?` }|Body `UpvestInvestmentApi.Models.PortfoliosAllocationUpdateRequest`: `Name`(optional), `Allocation`(same `Allocation` list)**req**. Resp `PortfoliosAllocation`.|Portfolios.md; PortfoliosAllocationUpdateRequest.cs|
|11|`client.Portfolios.ListPortfoliosAllocations(req)`|`portfolios:read`/`admin` ❌401|`UpvestInvestmentApi.Requests.Portfolios.ListPortfoliosAllocationsRequest` { query: `Sort`,`Order`,`Offset`,`Limit`,`StartDate`,`EndDate`,`InstrumentIds` }|Resp `UpvestInvestmentApi.Models.PortfoliosAllocationsListResponse`: `Data: IReadOnlyList<PortfoliosAllocation>` (each `Id`→modelId, `Name`, `Allocation[]`→funds {instrument_id, weight}), `Meta` (offset/limit/count/total_count).|Portfolios.md; PortfoliosAllocationsListResponse.cs|
|12|`client.Portfolios.CreatePortfoliosConfiguration(req)`|`portfolios:admin` ❌401|`UpvestInvestmentApi.Requests.Portfolios.CreatePortfoliosConfigurationRequest` { `IdempotencyKey: Guid` **req**; `Body: PortfoliosConfigurationCreateRequest?` }|Body `UpvestInvestmentApi.Models.PortfoliosConfigurationCreateRequest`: `AccountId`(account_id)**req**, `AllocationId`(allocation_id)**req**, `RebalancingStrategyIds`(optional). Resp `UpvestInvestmentApi.Models.PortfoliosConfiguration`.|Portfolios.md; PortfoliosConfigurationCreateRequest.cs|
|13|`client.Portfolios.UpdatePortfoliosConfiguration(req)`|`portfolios:admin` ❌401|`UpvestInvestmentApi.Requests.Portfolios.UpdatePortfoliosConfigurationRequest` { `AccountId: Guid` **req**; `IdempotencyKey: Guid` **req**; `Body: PortfoliosConfigurationUpdateRequest?` }|Body `UpvestInvestmentApi.Models.PortfoliosConfigurationUpdateRequest`: `AllocationId`(optional) = new model, `RebalancingStrategyIds`(optional). Resp `PortfoliosConfiguration`.|Portfolios.md; PortfoliosConfigurationUpdateRequest.cs|
|14|`client.Portfolios.RetrievePortfoliosConfiguration(req)`|`portfolios:read`/`admin` ❌401|`UpvestInvestmentApi.Requests.Portfolios.RetrievePortfoliosConfigurationRequest` { `AccountId: Guid` **req** }|Resp `PortfoliosConfiguration`: `AccountId`, `AllocationId` (→ModelId via map), `UpdatedAt`.|Portfolios.md; PortfoliosConfiguration.cs|

### Flow 3 — paying in

| # | Op | Scope | Request record + members | Body/response → fields read | source |
|---|---|---|---|---|---|
|15|`client.TopUps.CreateTopup(req)`|`topups:admin` ❌401|`UpvestInvestmentApi.Requests.TopUps.CreateTopupRequest` { `IdempotencyKey: Guid` **req**; `Body: PaymentsTopUpCreateRequest?` }|Body `UpvestInvestmentApi.Models.PaymentsTopUpCreateRequest`: `AccountGroupId`(account_group_id)**req**, `CashAmount`(cash_amount, string 2dp)**req**, `Currency`(`UpvestInvestmentApi.Models.Enums.Currency` = `Currency.Eur`)**req**. Resp `UpvestInvestmentApi.Models.PaymentsTopupsResponse`: `Id`, `Status`(`Status29`: CONFIRMED/SETTLED/CANCELLED), `SettlementReference`. ⚠ Sandbox settlement (SEPA credit transfer ref) is the "whatever Upvest provides" path — unreachable here (topups:admin → 401).|TopUps.md; PaymentsTopUpCreateRequest.cs; PaymentsTopupsResponse.cs|
|16|`client.Portfolios.CreatePortfoliosOrder(req)`|`portfolios:admin` ❌401|`UpvestInvestmentApi.Requests.Portfolios.CreatePortfoliosOrderRequest` { `IdempotencyKey: Guid` **req**; `Body: PortfoliosOrderPlaceRequest?` }|Body `UpvestInvestmentApi.Models.PortfoliosOrderPlaceRequest`: `UserId`**req**, `AccountId`**req**, `CashAmount`(string)**req**, `Currency`(`Currency.Eur`)**req**, `Side`(`UpvestInvestmentApi.Models.Enums.Side15` = `Side15.Buy`)**req**, `PostTax`(optional, default false). Invests cash across the account's configured allocation weights. Resp `UpvestInvestmentApi.Models.PortfoliosOrder`: `Id`, `Status`(`Status65`: NEW/PROCESSING/FILLED/SETTLED/CANCELLED), `CancellationReason`.|Portfolios.md; PortfoliosOrderPlaceRequest.cs; PortfoliosOrder.cs; Side15.cs|
|17|`client.TopUps.ListTopups(req)` / `client.TopUps.RetrieveTopup(req)`|`topups:*` ❌401|List: { `AccountGroupId` **req**; `Sort`,`Order`,`Limit`,`Offset` }. Retrieve: { `TopupId` **req** }|`PaymentsTopUpsListResponse` (data+meta) / `PaymentsTopupsResponse`. paidIn source.|TopUps.md|
|18|`client.Portfolios.RetrievePortfoliosOrder(req)` / `client.Portfolios.ListPortfoliosOrders(req)`|`portfolios:read`/`admin` ❌401|Retrieve: { `PortfolioOrderId` **req** }. List: { `AccountId`,`AllocationId`,`Sort`,`Order`,`Offset`,`Limit`,`StartDate`,`EndDate` }|`PortfoliosOrder` / `PortfoliosOrdersListResponse`. Order `Status` drives pay-in status.|Portfolios.md|

### Flow 4 — realignment

| # | Op | Scope | Request record + members | Body/response → fields read | source |
|---|---|---|---|---|---|
|19|`client.PortfoliosRebalancing.TriggerPortfolioRebalancing(req)`|`portfolios:admin` ❌401|`UpvestInvestmentApi.Requests.PortfoliosRebalancing.TriggerPortfolioRebalancingOperationRequest` { `IdempotencyKey: Guid` **req**; `Body: TriggerPortfolioRebalancingRequest?` }|Body `UpvestInvestmentApi.Models.AnyOf.TriggerPortfolioRebalancingRequest` — use `TriggerPortfolioRebalancingRequest.Allocations(UpvestInvestmentApi.Models.Allocations)`: `AllocationsValue`(allocations, `IReadOnlyList<Guid>`)**req** = `[AllocationId]` (rebalances every account that follows that allocation). Resp `UpvestInvestmentApi.Models.TriggerPortfolioRebalancingResponse`: `Id` (store as ExecutionId→RealignmentId).|PortfoliosRebalancing.md; TriggerPortfolioRebalancingRequest.cs; Allocations.cs; TriggerPortfolioRebalancingResponse.cs|
|20|`client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(req)`|`portfolios:read`/`admin` ❌401|`UpvestInvestmentApi.Requests.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecutionRequest` { `ExecutionId: Guid` **req** }|Resp `UpvestInvestmentApi.Models.PortfoliosRebalancingExecution`: `Id`,`CreatedAt`,`UpdatedAt`,`Accounts`,`Allocations`. ⚠ **No status field** — see §6.|PortfoliosRebalancing.md; PortfoliosRebalancingExecution.cs|
|21|`client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(req)`|`portfolios:read`/`admin` ❌401|{ `ExecutionId: Guid` **req**; `Sort`,`Order`,`Offset`,`Limit`,`StartDate`,`EndDate` }|`PortfoliosRebalancingExecutionOrderListResponse` — per-order statuses are the only completed/failed signal (confirm item shape at implement time; 401 in sandbox).|PortfoliosRebalancing.md|

### Flow 5 — investor's portfolio

| # | Op | Scope | Request record + members | Response → fields read | source |
|---|---|---|---|---|---|
|22|`client.Valuations.GetAccountValuation(req)`|`valuations:read` ❌401|`UpvestInvestmentApi.Requests.Valuations.GetAccountValuationRequest` { `AccountId: Guid` **req**; `PriceQuality: UpvestInvestmentApi.Models.Enums.PriceQuality3` **req** (query `price_quality`) }|`UpvestInvestmentApi.Models.AccountValuation`: `TotalSecurityValue`(`Amount` string 2dp, `Currency`) → totalValue; `SecurityPositions: IReadOnlyList<AccountValuationSecurityPosition>` → each `Instrument`(`Instrument6`: `Uuid`, `Isin`?) → instrumentId, `Quantity`(string), `Value`(`Value?`: `Amount`,`Currency`, nullable when no price) → holdings {instrumentId, quantity, value}.|Valuations.md; AccountValuation.cs; AccountValuationSecurityPosition.cs; Instrument6.cs; Value.cs; TotalSecurityValue.cs|
|23|`client.Positions.ListPositions(req)` (alt holdings)|`positions:read` ❌401|{ `AccountId: Guid` **req**; `Offset`,`Limit` }|`UpvestInvestmentApi.Models.PositionsListResponse`: `Data: IReadOnlyList<Datum1>` (`Instrument`, `Quantity` — **no value**), `Meta`. Prefer op 22 which carries value.|Positions.md; PositionsListResponse.cs; Datum1.cs|

### Enum values used (source: each enum file)

| Enum (fully-qualified) | Member used | wire | source |
|---|---|---|---|
|`UpvestInvestmentApi.Models.Enums.Currency`|`Currency.Eur`|EUR|Currency.cs|
|`UpvestInvestmentApi.Models.Enums.Side15`|`Side15.Buy`|BUY|Side15.cs|
|`UpvestInvestmentApi.Models.Enums.Type16`|`Type16.Portfolio`|PORTFOLIO|Type16.cs|
|`UpvestInvestmentApi.Models.Enums.Type7`|`Type7.NationalId` / `.Passport`|NATIONAL_ID/PASSPORT|IdentifierCreateRequest.cs|
|`…Enums.Country` / `Nationality` / `IssuingCountry` / `BirthCountry`|ISO 3166-1 alpha-2 — **open string enums, private ctor, no public factory**. Resolve a shopper-supplied code with `Country.TryGetKnownValue(code, out var c)` (reject on `false`); every ISO code is a predefined known member.|e.g. DE|Country.cs; StringEnum.cs; TypedEnum.cs|
|`…Enums.Status` (user)|ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED|—|UserTol.cs|
|`…Enums.Status21` (account)|PENDING_APPROVAL/ACTIVE/CLOSING/CLOSED/LOCKED|—|Account.cs|
|`…Enums.Status65` (order)|NEW/PROCESSING/FILLED/SETTLED/CANCELLED|—|PortfoliosOrder.cs|
|`…Enums.Status29` (topup)|CONFIRMED/SETTLED/CANCELLED|—|PaymentsTopupsResponse.cs|

### Client construction / auth / server (source: sdk-map.md "Getting a client", "Servers & auth")

- Client: `new UpvestInvestmentApi.UpvestInvestmentApiClient(httpClient, options)` — only ctor; one `HttpClient`, options built once, held in a singleton.
- OAuth2: `options.OauthClientCredentials = new UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials { ClientId = Upvest:ClientId, ClientSecret = Upvest:ClientSecret }`. Tokens minted per scope set at `<BaseUrl>/auth/token`.
- **Request signing is required on EVERY call** (incl. token request). Set `options.Hooks = [ new UpvestInvestmentApi.Signing.UpvestSigningHook(UpvestInvestmentApi.Signing.UpvestHttpSignatureCredentials.FromFile(keyId: Upvest:SigningKeyId, privateKeyPath: Upvest:SigningKeyPath, upvestClientId: Upvest:ClientId, privateKeyPassphrase: Upvest:SigningKeyPassphrase)) ]`. With no hook and no `UPVEST_API_*` env vars, every call throws `InvalidOperationException` before being sent. No operation takes signature/authorization/upvest-client-id params.
- Server/base URL: default `ServerEnvironment.Production` = `https://sandbox.upvest.co`. Point every call at `Upvest:BaseUrl` via `options.Server.Default.Production.BaseUrl = Upvest:BaseUrl` (override). (`ServerEnvironment.Environment2` = `https://api.upvest.co` is live.)

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
|---|---|---|
| A pay-in / order / configuration / valuation / positions / topup call may use only an **AccountId / AccountGroupId** this app obtained from onboarding — never a client-supplied one. | `Portfolios.CreatePortfoliosOrder`,`TopUps.CreateTopup`,`Portfolios.Create/UpdatePortfoliosConfiguration`,`Valuations.GetAccountValuation`,`Positions.ListPositions` ← `AccountsApi.CreateAccount` / `AccountGroups.CreateAccountGroup` (ids stored in `InvestorRecord`) | Endpoint loads the caller's `InvestorRecord` by `ShopperId` and uses its stored ids; it never accepts an account id from the request body. |
| The `{modelId}` a caller references for "choose model", "realign", or in a fund list must be a model this app defined. | `Portfolios.Create/UpdatePortfoliosConfiguration`,`PortfoliosRebalancing.TriggerPortfolioRebalancing` ← `Portfolios.CreatePortfoliosAllocation` (ModelId→AllocationId in `PortfolioModelRecord`) | Endpoint resolves ModelId→AllocationId from `PortfolioModelRecord` before the write; 404 if absent. (`Portfolios.ListPortfoliosAllocations` is the provider-side set.) |
| A fund `instrumentId` (ISIN) in a model must be an instrument Upvest offers. | `Portfolios.CreatePortfoliosAllocation`/`UpdatePortfoliosAllocation` ← `Instruments.ListInstruments` (defines the legal set) | Validate each ISIN against `Instruments.ListInstruments`/`RetrieveInstrument` before the allocation write. ⚠ `instruments:read` → 401 in sandbox: the validating read must be coded; it will 401 here, so the check runs only against a scoped environment. |
| A pay-in is allowed only for an `active` investor who has chosen a model. | `TopUps.CreateTopup`+`Portfolios.CreatePortfoliosOrder` ← `InvestorRecord.Status==active` ∧ model chosen | Guard at the start of `POST /api/portfolio/payments`; 409/422 otherwise. |

---

## 3. Trap notes

- **Step 0 (signing/auth):** the client refuses every call — including the OAuth token fetch — unless an `UpvestSigningHook` (or the `UPVEST_API_*` env vars) is configured; a missing/misapplied credential surfaces as `AuthSchemeException` / `InvalidOperationException`, not a 401. What each credential maps to and the fail-fast shape — **MUST load `upvest-v4:dotnet-authentication`**.
- **Step 0 (client/DI):** `HttpClient` ownership, singleton lifetime, and how `AddUpvestInvestmentApiClient` vs manual `new` register — getting it wrong leaks sockets or captures a stale secret. **MUST load `upvest-v4:dotnet-client-initialization`**.
- **Every call:** which input is the single request record, what `requestOptions`/`cancellationToken` do, and that a response wraps its payload (reads go one level into `Data`/union arms). **MUST load `upvest-v4:dotnet-calling-endpoints`**.
- **Model building:** `UserCreateRequest`/`AccountCreateRequest`/`TriggerPortfolioRebalancingRequest`/`InstrumentId` are `AnyOf` unions (pick the arm; read via `TryGet…`); open string enums have no public ctor (use `TryGetKnownValue`); dates are `DateTimeOffset` with a date-only converter; money/weights are strings; `AdditionalProperties` carries unknown fields. **MUST load `upvest-v4:dotnet-models`**.
- **Error boundary:** which exception types reach the catch, Case-A `TryGet…` accessors, and reading status/body. **MUST load `upvest-v4:dotnet-error-handling`**.
- **Resilience/polling:** what `Timeout` actually bounds vs a whole call, which verbs the SDK may auto-retry, manual offset/limit pagination, and what `LogRequestBody` + `UPVEST_INVESTMENT_API_LOG` expose. **MUST load `upvest-v4:dotnet-configuration-resilience`**.
- **Testing:** the `HttpClient` stub seam, faking the clock for polling, constructing SDK exceptions. **MUST load `upvest-v4:dotnet-testing`**.

---

## 4. REQUIRED READING (load ALL before implementing)

| Skill (plugin-qualified) | Governs |
|---|---|
|`upvest-v4:dotnet-client-initialization`|Step 0 — client construction & DI|
|`upvest-v4:dotnet-authentication`|Step 0 — OAuth + signing credentials, fail-fast|
|`upvest-v4:dotnet-calling-endpoints`|every operation call|
|`upvest-v4:dotnet-models`|building union/enum/date/money payloads|
|`upvest-v4:dotnet-error-handling`|the error boundary (always)|
|`upvest-v4:dotnet-configuration-resilience`|timeouts, retries, pagination, logging/secrets|
|`upvest-v4:dotnet-testing`|tests & stubs|

**Hazard (verbatim):** a body that does not match its declared type — a drifted/malformed **2xx** (a missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch ladder handling only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
|1|**Credential fail-fast**|Credentials come from `IConfiguration` section `Upvest` (keys `ClientId`,`ClientSecret`,`SigningKeyId`,`SigningKeyPath`,`SigningKeyPassphrase`,`BaseUrl` — exactly these, no hard-coded values). At startup (DI registration in `Program.cs`/`Dependencies.cs`) refuse to start if any of `ClientId`,`ClientSecret`,`SigningKeyId`,`SigningKeyPath`,`BaseUrl` is missing/blank, rather than hitting `AuthSchemeException`/`InvalidOperationException` on first call. `SigningKeyPassphrase` may be legitimately blank (unencrypted key) → presence of the key is required, blank value allowed (`YOUR CALL`, settle per key material). Since every part of a multi-part credential is checked, a blank part is caught.|
|2|**Secret sourcing & rotation**|Secrets (`ClientSecret`,`SigningKeyPassphrase`, key file at `SigningKeyPath`) from config/secret store. Options object is built **once at DI registration** and captured in the singleton client → a rotated secret takes effect only on process restart. No hot-rotation (`YOUR CALL`: acceptable for this sample; document the restart requirement).|
|3|**Total timeout budget**|The caller (shopper/operator) gets the budget enforced at the endpoint via `CancellationToken` passed to each operation; `options.Retry.Timeout` and per-attempt `Timeout` bound retries/attempts, not the whole multi-call flow. Enforce an overall per-request budget in the endpoint. Exact knobs: `dotnet-configuration-resilience`.|
|4|**Write-retry ownership**|SDK auto-retries only the verbs in `RetryOptions.HttpMethodsToRetry`; POST/PUT writes (CreateUser, CreateAccount, CreateTopup, CreatePortfoliosOrder, TriggerPortfolioRebalancing, etc.) are resent only if listed. Confirm the retried-verb set in `dotnet-configuration-resilience` and keep non-idempotent writes off auto-retry (they carry a real idempotency key, so a deliberate resend is safe — row 5).|
|5|**Idempotency & ambiguous writes**|Every write's request record carries a real caller-supplied `IdempotencyKey: Guid` (CreateUser, CreateIdentifier, SetTaxResidencies, CreateAccountGroup, CreateAccount, CreatePortfoliosAllocation, CreatePortfoliosConfiguration/Update, CreatePortfoliosOrder, CreateTopup, TriggerPortfolioRebalancing). The app **persists the key** in its `*Record` before the call and reuses it on retry/reconciliation. The auto-injected `Idempotency-Key` header (fresh GUID per call) is **not** this key. See UNKNOWN OUTCOMES.|
|6|**Observability**|Log at Information the app-level outcome (ShopperId hash, Upvest resource ids, statuses) and at Warning/Error the SDK exception type + HTTP status + the provider correlation id from the error body (`RawError.ReadAsString()`/`ReadAsJson`). **Never log request bodies** (row 7). Which correlation id is present: read it from the error payload per `dotnet-error-handling`.|
|7|**Sensitive data**|Scope carries PII (name, email, birth date/city, address, phone, tax id, national-id/passport number). Therefore: `options.Logging.LogRequestBody` stays **off**; `options.Logging.LoggerFactory` set explicitly in production (so `UPVEST_INVESTMENT_API_LOG=trace` cannot force body logging); the app's own diagnostics never echo request bodies or these fields; "Personal details never written to logs" is honoured.|
|8|**Environment selection**|One server group `Default`. `Production`→`https://sandbox.upvest.co` (sandbox, the default), `Environment2`→`https://api.upvest.co` (live). Every deployment sets `Upvest:BaseUrl` and it overrides `Server.Default.Production.BaseUrl`. This deployment talks to the sandbox only. Test traffic is kept off live by never selecting `Environment2` / never pointing `BaseUrl` at `api.upvest.co`.|
|9|**Duplicate prevention under concurrency**|See DUPLICATE CLAIMS. Claims stored in app-owned EF aggregates keyed by a **primary key** (enforced even by the EF in-memory store). ⚠ The in-memory store lives per host/per run; a horizontally-scaled deployment would need a shared store — noted in §6.|
|10|**Partial results**|See PAGED READS. Reads expose `Meta.TotalCount` vs `Meta.Count`; the app pages until all are read or surfaces a truncation flag on its own response DTO.|
|11|**Unknown outcomes**|See UNKNOWN OUTCOMES. Each write's `catch` on a connection failure re-reads by the stored idempotency key / lists by user/account/group before reporting failure.|

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
|Investor signup (`Users.CreateUser` chain)|`InvestorRecord` (PK = ShopperId) in `CatalogContext` (app-owned)|EF primary-key conflict on inserting a second `InvestorRecord` with the same ShopperId (enforced by in-memory store)|`catch` of the save that inserts the claim (DbUpdate/InvalidOperation from the in-memory PK conflict) before any Upvest call|TBD|
|Pay-in (`TopUps.CreateTopup`+`Portfolios.CreatePortfoliosOrder`)|`PayInRecord` (PK = app-generated PaymentId) with persisted IdempotencyKey|EF primary-key conflict on inserting a second `PayInRecord` with the same PaymentId|`catch` of the claim-insert save before the SDK calls|TBD|
|Realignment (`PortfoliosRebalancing.TriggerPortfolioRebalancing`)|`RealignmentRecord` (PK = app-generated RealignmentId) with persisted IdempotencyKey|EF primary-key conflict on inserting a second `RealignmentRecord`|`catch` of the claim-insert save before the SDK call|TBD|

Order per row: **claim (PK insert) → SDK call → record result**; release the claim if the provider refuses the call. (Model-portfolio PUTs are idempotent create-or-replace, not duplicate-prone → not listed.)

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
|---|---|---|---|
|`Portfolios.ListPortfoliosAllocations` (GET /api/portfolio/models)|`Offset`/`Limit` query params; response `Meta.Limit`|App pages until `sum(count) >= Meta.TotalCount`; if it caps, set a `truncated`/`totalCount` field on the models response DTO|TBD|
|`TopUps.ListTopups` / `Portfolios.ListPortfoliosOrders` (GET /api/portfolio/payments)|`Offset`/`Limit`; `Meta`|Page to completion or expose `totalCount`/`truncated` on the payments response DTO (newest-first ordering applied after fetch)|TBD|
|`Positions.ListPositions` (alt holdings)|`Offset`/`Limit`; `Meta`|Page to completion; prefer `Valuations.GetAccountValuation` (single response) for the portfolio view|TBD|

### UNKNOWN OUTCOMES

| Write | Re-read with | Reference searched by | Where in the code | Test that fails the connection |
|---|---|---|---|---|
|`Users.CreateUser`|`Users.ListUsers`|match by stored IdempotencyKey / email on `InvestorRecord`|TBD|TBD|
|`AccountGroups.CreateAccountGroup`|`AccountGroups.ListUserAccountGroups`|by UpvestUserId|TBD|TBD|
|`AccountsApi.CreateAccount`|`AccountsApi.ListUserAccounts`|by UpvestUserId (+ type PORTFOLIO)|TBD|TBD|
|`TopUps.CreateTopup`|`TopUps.ListTopups`|by AccountGroupId + stored IdempotencyKey/amount on `PayInRecord`|TBD|TBD|
|`Portfolios.CreatePortfoliosOrder`|`Portfolios.ListPortfoliosOrders`|by AccountId + stored IdempotencyKey on `PayInRecord`|TBD|TBD|
|`PortfoliosRebalancing.TriggerPortfolioRebalancing`|`PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution`|by ExecutionId stored on `RealignmentRecord` (safe resend with same IdempotencyKey if id not yet captured)|TBD|TBD|
|`Portfolios.CreatePortfoliosAllocation`|`Portfolios.ListPortfoliosAllocations`|by name/IdempotencyKey; store AllocationId on `PortfolioModelRecord`|TBD|TBD|

Each write settles the outcome in its own `catch` (re-read before reporting failure), or records it unknown for a later reconciliation pass; reusing the persisted idempotency key makes a deliberate resend safe.

---

## 6. Assumptions & Blockers

**Blockers (capability gaps / contract mismatches the SDK cannot satisfy — surface to the user):**

1. **`identityDocument.expiryDate` has no SDK home.** The only identity-document surface is `UserIdentifiers.CreateIdentifier` → `IdentifierCreateRequest`, which carries `type` (enum `Type7`, only NATIONAL_ID / PASSPORT), `issuing_country`, and `identifier` (the number) — but **no `expiryDate` field**, and `type` is not a free string. The task's `identityDocument.expiryDate` cannot be transmitted to Upvest; `type` is constrained to national-id/passport. Decision needed: drop/ignore `expiryDate` (keep only in the app's own record), or confirm this identifier is the intended mapping. Source: `IdentifierCreateRequest.cs`, `UserIdentifiers.md`.
2. **Rebalancing execution exposes no status.** `PortfoliosRebalancingExecution` (returned by `RetrievePortfoliosRebalancingExecution`) has only `id`/`created_at`/`updated_at`/`accounts`/`allocations` — **no status field**. Flow 4 requires `pending`→`completed`/`failed`. The only status signal is per-order via `ListPortfolioRebalancingExecutionOrders`. Decision needed: derive realignment status by polling the execution orders' statuses (confirm that list item carries a status at implement time), or treat "execution retrievable" as the terminal signal. Source: `PortfoliosRebalancingExecution.cs`, `PortfoliosRebalancing.md`.
3. **Sandbox scope limit (environment, not an SDK gap — build in full anyway).** Only `users:*` scopes are authorised; all account/portfolio/payment/valuation/rebalancing/instrument calls return `401 "This client is not authorised to use these scopes"`. End-to-end live verification is possible only for Flow 1's user+identifier steps. Not a blocker to building; a blocker to full live verification.

**Assumptions (intent — proceed unless corrected):**

- **TOL operating model.** `Users.CreateUser` is built with the **TOL** (`UserTolCreateRequest`) arm — Upvest holds the licence and performs KYC — because the task's KYC fields (fatca, address, nationalities) and "becomes an investor with Upvest" match TOL. If the client is BYOL, the body arm and fields differ.
- **`rejected` status derivation.** No Upvest field is literally "rejected": user `Status` is ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED; account `Status21` is PENDING_APPROVAL/ACTIVE/CLOSING/CLOSED/LOCKED. Plan maps: `pending` = account/account-group not yet ACTIVE; `active` = account ACTIVE; `rejected` = user OFFBOARDING/OFFBOARDED (or a failed onboarding check via `UserChecks`, not detailed here). `YOUR CALL — not in the map`; settle in code and confirm with the operator.
- **Pay-in = top-up then portfolio order.** "Credited… and invested across model funds in model weights" is modelled as `CreateTopup` (cash to account group) followed by `CreatePortfoliosOrder` (`BUY`, invests across the configured allocation). Sandbox top-up settlement (SEPA reference) is Upvest's own mechanism and is unreachable here (topups:admin → 401).
- **Model & account identity mapping is app-owned.** Upvest allocations/accounts/executions use server-generated UUIDs; the operator's `{modelId}`, the app's `paymentId`, and `realignmentId` are app-generated ids mapped to Upvest UUIDs in the EF records. `PUT /api/portfolio/models/{modelId}` = create-first-then-update on the mapped AllocationId.
- **Account-group granularity.** One PERSONAL account group + one PORTFOLIO account per investor (top-ups target the group, portfolio ops target the account). `YOUR CALL` if a different grouping is wanted.
- **Persistence & concurrency.** App records live in the EF **in-memory** `CatalogContext` on this machine (per host, per run). Duplicate claims rely on primary-key uniqueness (enforced in-memory). A multi-host/scaled deployment would need a shared/persistent store and is out of scope for this sample — flag if horizontal scale is required.
- **Money conversion.** App JSON uses euro numbers with two decimals; the SDK uses ISO-4217 `Currency.Eur` + decimal **strings**. The integration converts both ways and validates against the wire regexes.

---

## 7. Source labels

Every CONTRACT SHEET **source** cell names the map page (`*.md`) or the single SDK source file (`*.cs`) the fact came from — all under `marketplace/plugins/upvest-v4/sdk/dotnet/` — so any re-check is one targeted open. `YOUR CALL — not in the map` marks application decisions (persistence schema, id mapping, status derivation, account-group granularity, passphrase-blank policy) to be settled in code against the task. `UNVERIFIED` is not used: no contract fact here was left for live traffic — the sandbox 401s are environment facts (§6 Blocker 3), not open contract questions.
