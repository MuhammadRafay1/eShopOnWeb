# Upvest Investment API integration plan — eShopOnWeb managed portfolios

Scope: additive, JWT-authenticated HTTP endpoints under `/api/` on `src/PublicApi`, backed by the
Upvest Investment API .NET SDK (`UpvestInvestmentApi`, root namespace). Every Upvest fact below is
grounded in the SDK map and SDK source that ship in this plugin at
`marketplace/plugins/upvest-v4/sdk/dotnet/` (cited as `sdk/dotnet/...` in the **source** cells).
The SDK is referenced as a `<ProjectReference>` to
`sdk/dotnet/src/UpvestInvestmentApi/UpvestInvestmentApi.csproj` (local to this machine; see getting-started).

> SANDBOX REALITY (this environment): the sandbox client is authorised ONLY for **user operations**
> (`users:*` scopes). Any call needing another scope returns **401 "This client is not authorised to
> use these scopes"**. That is the environment, NOT a gap and NOT a bug. Every flow is planned in full;
> each operation row carries a **sandbox** note saying whether it runs or 401s here. Code every flow
> completely and defensively (treat 401-not-authorised as a known, reportable outcome, not a crash).

> PERSISTENCE: run with the in-memory EF provider (`UseOnlyInMemoryDatabase=true`,
> `src/Infrastructure/Dependencies.cs`). All app-side records (investor, model↔allocation mapping,
> model selection, payments, realignments) live in the app data layer — see Scope step 0.

---

## 1. Scope & sequence

App-facing identifiers are the application's own; the app maps them to Upvest UUIDs it stores:
`investorId` = Upvest user id (Guid); `modelId` = Upvest portfolio **allocation** id (Guid);
`paymentId` = Upvest portfolio **order** id (Guid); `realignmentId` = Upvest rebalancing **execution**
id (Guid). The caller (signed-in shopper) is identified by the JWT `ClaimTypes.Name` claim (username/email;
`src/Infrastructure/Identity/IdentityTokenClaimService.cs`); all per-shopper records key on it.

**Step 0 — Persistence + client wiring (no Upvest call).**
New entities in `src/ApplicationCore/Entities` persisted through a DbContext registered for the
in-memory provider, read/written with `IRepository<T>`/`IReadRepository<T>` (Ardalis EfRepository;
registered in `src/PublicApi/Program.cs`). Register `UpvestInvestmentApiClient` as a singleton (one
`HttpClient`, one options object) bound from the `Upvest:` config section; wire OAuth2 + signing hook
(§CONTRACT SHEET → client/auth). Fail-fast on missing credentials (READINESS #1).
Entities to persist: `InvestorRecord` (shopperUserName PK/unique, upvestUserId, accountGroupId,
accountId, status, createdAt); `ModelPortfolio` (app modelId, upvestAllocationId, name, funds);
`PortfolioPayment` (app paymentId, shopperUserName, upvestOrderId, amount, currency, status, createdAt,
upvestIdempotencyKey); `Realignment` (app realignmentId, upvestExecutionId, modelId, status).

**Step 1 — Flow 1, becoming an investor** (`POST`/`GET /api/portfolio/investor`).
Sign-up orchestrates, in order: `Users.CreateUser` (TOL arm) → `TaxResidencies.SetTaxResidencies`
→ `UserIdentifiers.CreateIdentifier` → `AccountGroups.CreateAccountGroup` (PERSONAL) →
`AccountsApi.CreateAccount` (PORTFOLIO). Persist the ids + `status`. `GET` reads the app record and
refreshes status via `Users.RetrieveUser` (+ `AccountGroups.RetrieveAccountGroup` for account-ready).

**Step 2 — Flow 2, model portfolios.**
`PUT /api/portfolio/models/{modelId}` (operator): `Portfolios.CreatePortfoliosAllocation` (first time)
or `Portfolios.UpdatePortfoliosAllocation` (replace), storing the app modelId ↔ Upvest allocation id.
`GET /api/portfolio/models`: `Portfolios.ListPortfoliosAllocations`.
`PUT /api/portfolio/investor/model` (investor): `Portfolios.CreatePortfoliosConfiguration` first time,
else `Portfolios.UpdatePortfoliosConfiguration` (ONE active configuration per account — see trap).

**Step 3 — Flow 3, paying in** (`POST`/`GET /api/portfolio/payments`).
Invest = `Portfolios.CreatePortfoliosOrder` (`side = BUY`, `cash_amount`, `currency = EUR`) which
distributes the cash across the account's allocation weights. Making cash appear in the sandbox is
`Tests.CreateBankTransaction` ("whatever Upvest provides"; needs a virtual bank account — see Blockers).
`GET` reads app records; refresh status via `Portfolios.RetrievePortfoliosOrder`.

**Step 4 — Flow 4, realignment.**
`POST /api/portfolio/models/{modelId}/realignments` (operator):
`PortfoliosRebalancing.TriggerPortfolioRebalancing` with the **Allocations** arm
(`{ "allocations": [upvestAllocationId] }`). `GET /api/portfolio/realignments/{realignmentId}`:
`PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` — which carries **no status** — so status
is derived from `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (see trap + Blocker).

**Step 5 — Flow 5, the portfolio** (`GET /api/portfolio`).
`Portfolios.RetrievePortfoliosConfiguration` (→ `modelId` = allocation_id) +
`Positions.ListPositions` (holdings: instrumentId, quantity) + `Valuations.GetAccountValuation`
(per-instrument `value` + `total_security_value`). `paidIn` is app-tracked (sum of invested payments).

---

## 2. CONTRACT SHEET

⚠ **Signatures below are generated code, copied verbatim. Each operation that takes input takes ONE
request record as its first parameter** (an operation with no inputs takes none), built with an object
initializer using the record's own property names — never flat positional arguments. The request record's
`Body` property carries the JSON body model.

⚠ **Every SDK type must be written fully-qualified with the namespace its source path implies** (taken
from the path given for THAT type, never a neighbour): `src/.../Models/*` → `UpvestInvestmentApi.Models`;
`src/.../Models/AnyOf/*` → `UpvestInvestmentApi.Models.AnyOf`; `src/.../Models/Enums/*` →
`UpvestInvestmentApi.Models.Enums`; `src/.../Requests/<Ctrl>/*` →
`UpvestInvestmentApi.Requests.<Ctrl>`; `src/.../Errors/*` → `UpvestInvestmentApi.Errors`;
`RawError` → `UpvestInvestmentApi.Core.ErrorResponse`; `ApiException<T>` →
`UpvestInvestmentApi.Core.Exceptions`.

**ALL operations in scope are Case A (typed):** error is `ApiException<{Operation}Error>`; the typed
error exposes `TryGetNoContent(out RawError? value)` (covers the statuses listed on its map row,
including **401/403**) and the inherited `TryGetRawError(out RawError? error)` fallback. A **401 "not
authorised to use these scopes"** in this sandbox surfaces through `TryGetNoContent`. Also catch
`ResponseDeserializationException`/`ApiException` (see REQUIRED READING).

### 2a. Operations

| # | Controller · method (signature) | Request record → required members | Body model → fields read/set (wire) | Response → fields read | Error (Case A) accessors | Pagination | Scope · sandbox | Source |
|---|---|---|---|---|---|---|---|---|
| 1 | `client.Users.CreateUser(CreateUserRequest request, RequestOptions? = null, CancellationToken = default)` → `UserCreateRequest1` | `CreateUserRequest`: `IdempotencyKey: Guid, required`; `UpvestApiVersion` (dflt `_1`); `Body: UserCreateRequest?` | `UserCreateRequest` (AnyOf) — use `UserCreateRequest.UserTolCreateRequest(...)` arm → `UserTolCreateRequest`: `FirstName (first_name) req`, `LastName (last_name) req`, `Email (email) req`, `BirthDate (birth_date) req DateTimeOffset`, `BirthCity (birth_city) opt`, `BirthCountry (birth_country) opt enum`, `Nationalities (nationalities) req IReadOnlyList<Nationality>` (one-item list from task `nationality`), `PhoneNumber (phone_number) opt`, `Address (address) req Address2`, `Fatca (fatca) req` | `UserCreateRequest1` (AnyOf) → `TryGetUserTol(out UserTol)`: `Id: Guid` (= investorId), `Status` (user status) | `ApiException<CreateUserError>`; `TryGetNoContent`[400,401,403,406,429,500,503,504]·`TryGetRawError` | none | `users:admin` · **RUNS** | `sdk/dotnet/map/operations/Users.md`; `sdk/dotnet/src/UpvestInvestmentApi/Requests/Users/CreateUserRequest.cs`; `.../Models/AnyOf/UserCreateRequest.cs`; `.../Models/UserTolCreateRequest.cs`; `.../Models/Address2.cs`; `.../Models/Fatca.cs` |
| 2 | `client.Users.RetrieveUser(RetrieveUserRequest ...)` → `UserGetResponse` | `RetrieveUserRequest`: `UserId: Guid, required`; `UpvestApiVersion` | — (GET) | `UserGetResponse` (AnyOf) → `TryGetUserTol(out UserTol)`: `Status` (ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED) | `ApiException<RetrieveUserError>`; `TryGetNoContent`[401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `users:read`/`users:admin` · **RUNS** | `sdk/dotnet/map/operations/Users.md`; `.../Models/AnyOf/UserGetResponse.cs`; `.../Models/UserTol.cs` |
| 3 | `client.TaxResidencies.SetTaxResidencies(SetTaxResidenciesRequest ...)` → `TaxResidencyRecord` | `SetTaxResidenciesRequest`: `UserId: Guid, required`, `IdempotencyKey: Guid, required`; `UpvestApiVersion`; `Body: TaxResidenciesSetRequest?` | `TaxResidenciesSetRequest`: `TaxResidencies (tax_residencies) req IReadOnlyList<TaxResidencyForCreateRequest>` — use `WithTaxIdentifierNumber` arm (carries country + TIN from task `taxCountry`/`taxId`) | `TaxResidencyRecord` (confirmation) | `ApiException<SetTaxResidenciesError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `taxes:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/TaxResidencies.md`; `.../Models/TaxResidenciesSetRequest.cs`; `.../Models/AnyOf/TaxResidencyForCreateRequest.cs` (arm `.../Models/WithTaxIdentifierNumber.cs`) |
| 4 | `client.UserIdentifiers.CreateIdentifier(CreateIdentifierRequest ...)` → `Identifier` | `CreateIdentifierRequest`: `UserId: Guid, required`; `UpvestApiVersion`; `Body: IdentifierCreateRequest?` (**NO idempotency key**) | `IdentifierCreateRequest`: `Type (type) Type7` (dflt NATIONAL_ID; only NATIONAL_ID/PASSPORT), `IssuingCountry (issuing_country) req enum`, `Identifier (identifier) opt string` (= task `number`). **No expiryDate field exists** (see Blocker) | `Identifier` | `ApiException<CreateIdentifierError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `users:admin` · **RUNS** | `sdk/dotnet/map/operations/UserIdentifiers.md`; `.../Requests/UserIdentifiers/CreateIdentifierRequest.cs`; `.../Models/IdentifierCreateRequest.cs` |
| 5 | `client.AccountGroups.CreateAccountGroup(CreateAccountGroupRequest ...)` → `AccountGroupCreateResponse` | `CreateAccountGroupRequest`: `IdempotencyKey: Guid, required`; `UpvestApiVersion`; `Body: AccountGroupCreateRequest?` | `AccountGroupCreateRequest` (AnyOf) — use `AccountGroupCreateUserRequest`: `UserId (user_id) req Guid`, `Type (type) req Type13` (= `Personal`) | `AccountGroupCreateResponse` (AnyOf) → account group `id` (= accountGroupId) | `ApiException<CreateAccountGroupError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `accounts:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/AccountGroups.md`; `.../Models/AnyOf/AccountGroupCreateRequest.cs`; `.../Models/AccountGroupCreateUserRequest.cs` |
| 6 | `client.AccountsApi.CreateAccount(CreateAccountRequest ...)` → `AccountCreateResponse` | `CreateAccountRequest`: `IdempotencyKey: Guid, required`; `UpvestApiVersion`; `Body: AccountCreateRequest?` | `AccountCreateRequest` (AnyOf) — use `AccountCreateUserRequest`: `UserId (user_id) req`, `AccountGroupId (account_group_id) req`, `Type (type) req Type16` (= `Portfolio`), `Name (name) opt` | `AccountCreateResponse` (AnyOf) → account `id` (= accountId) | `ApiException<CreateAccountError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `accounts:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/AccountsApi.md`; `.../Requests/AccountsApi/CreateAccountRequest.cs`; `.../Models/AnyOf/AccountCreateRequest.cs`; `.../Models/AccountCreateUserRequest.cs` |
| 7 | `client.AccountGroups.RetrieveAccountGroup(RetrieveAccountGroupRequest ...)` → `AccountGroupRetrieveResponse` | `RetrieveAccountGroupRequest`: `AccountGroupId: Guid, required`; `UpvestApiVersion` | — (GET) | `AccountGroupRetrieveResponse` (AnyOf) → account-group status (account-ready signal) | `ApiException<RetrieveAccountGroupError>`; `TryGetNoContent`[401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `accounts:read`/`accounts:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/AccountGroups.md`; `.../Models/AnyOf/AccountGroupRetrieveResponse.cs` |
| 8 | `client.Portfolios.CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest ...)` → `PortfoliosAllocation` | `CreatePortfoliosAllocationRequest`: `IdempotencyKey: Guid, required`; `UpvestApiVersion`; `Body: PortfoliosAllocationCreateRequest?` | `PortfoliosAllocationCreateRequest`: `Name (name) opt`, `Allocation (allocation) req IReadOnlyList<Allocation>`, `RebalancingStrategyIds opt`. `Allocation`: `InstrumentId (instrument_id) req` (ISIN string via implicit `string`→`InstrumentId`), `InstrumentIdType (instrument_id_type) dflt Isin`, `Weight (weight) req string` (regex `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`) | `PortfoliosAllocation`: `Id: Guid` (= upvestAllocationId), `Name`, `Allocation` | `ApiException<CreatePortfoliosAllocationError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Portfolios.md`; `.../Requests/Portfolios/CreatePortfoliosAllocationRequest.cs`; `.../Models/PortfoliosAllocationCreateRequest.cs`; `.../Models/Allocation.cs`; `.../Models/AnyOf/InstrumentId.cs`; `.../Models/PortfoliosAllocation.cs` |
| 9 | `client.Portfolios.UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest ...)` → `PortfoliosAllocation` | `UpdatePortfoliosAllocationRequest`: `AllocationId: Guid, required`; `UpvestApiVersion`; `Body: PortfoliosAllocationUpdateRequest?` (**NO idempotency key**) | `PortfoliosAllocationUpdateRequest`: `Name (name) opt`, `Allocation (allocation) req IReadOnlyList<Allocation>` | `PortfoliosAllocation` | `ApiException<UpdatePortfoliosAllocationError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Portfolios.md`; `.../Models/PortfoliosAllocationUpdateRequest.cs` |
| 10 | `client.Portfolios.ListPortfoliosAllocations(ListPortfoliosAllocationsRequest ...)` → `PortfoliosAllocationsListResponse` | `ListPortfoliosAllocationsRequest` (no required); `Sort/Order/Offset/Limit`, `StartDate/EndDate`, `InstrumentIds`; `UpvestApiVersion` | — (GET) | `PortfoliosAllocationsListResponse`: `data` (list of `PortfoliosAllocation`) + `meta` (offset/limit/count/total_count) | `ApiException<ListPortfoliosAllocationsError>`; `TryGetNoContent`[400,401,403,404,405,406,429,500,503,504]·`TryGetRawError` | **offset/limit** (`meta.total_count`) | `portfolios:read`/`portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Portfolios.md`; `.../Models/PortfoliosAllocationsListResponse.cs` |
| 11 | `client.Portfolios.CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest ...)` → `PortfoliosConfiguration` | `CreatePortfoliosConfigurationRequest`: `IdempotencyKey: Guid, required`; `UpvestApiVersion`; `Body: PortfoliosConfigurationCreateRequest?` | `PortfoliosConfigurationCreateRequest`: `AccountId (account_id) req Guid`, `AllocationId (allocation_id) req Guid` (= chosen modelId), `RebalancingStrategyIds opt` | `PortfoliosConfiguration`: `AccountId`, `AllocationId` (= modelId chosen) | `ApiException<CreatePortfoliosConfigurationError>`; `TryGetNoContent`[400,401,403,404,406,422,429,500,503,504]·`TryGetRawError` | none | `portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Portfolios.md`; `.../Models/PortfoliosConfigurationCreateRequest.cs`; `.../Models/PortfoliosConfiguration.cs` |
| 12 | `client.Portfolios.UpdatePortfoliosConfiguration(UpdatePortfoliosConfigurationRequest ...)` → `PortfoliosConfiguration` | `UpdatePortfoliosConfigurationRequest`: `AccountId: Guid, required`, `IdempotencyKey: Guid, required`; `UpvestApiVersion`; `Body: PortfoliosConfigurationUpdateRequest?` | `PortfoliosConfigurationUpdateRequest`: new `allocation_id` / strategies (replace selection) | `PortfoliosConfiguration` | `ApiException<UpdatePortfoliosConfigurationError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Portfolios.md`; `.../Models/PortfoliosConfigurationUpdateRequest.cs` |
| 13 | `client.Portfolios.RetrievePortfoliosConfiguration(RetrievePortfoliosConfigurationRequest ...)` → `PortfoliosConfiguration` | `RetrievePortfoliosConfigurationRequest`: `AccountId: Guid, required`; `UpvestApiVersion` | — (GET) | `PortfoliosConfiguration`: `AllocationId` (= current modelId) | `ApiException<RetrievePortfoliosConfigurationError>`; `TryGetNoContent`[401,403,404,405,406,429,500,503,504]·`TryGetRawError` | none | `portfolios:read`/`portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Portfolios.md`; `.../Models/PortfoliosConfiguration.cs` |
| 14 | `client.Portfolios.CreatePortfoliosOrder(CreatePortfoliosOrderRequest ...)` → `PortfoliosOrder` | `CreatePortfoliosOrderRequest`: `IdempotencyKey: Guid, required`; `UpvestApiVersion`; `Body: PortfoliosOrderPlaceRequest?` | `PortfoliosOrderPlaceRequest`: `UserId (user_id) req`, `AccountId (account_id) req`, `CashAmount (cash_amount) req string` (positive decimal), `Currency (currency) req` (= `Eur`), `Side (side) req Side15` (= `Buy`), `PostTax (post_tax) opt dflt false` | `PortfoliosOrder`: `Id: Guid` (= paymentId's upvestOrderId), `Status: Status65` (NEW/PROCESSING/FILLED/SETTLED/CANCELLED), `CashAmount`, `Currency`, `AllocationId?`, `Orders`, `ClientReference` | `ApiException<CreatePortfoliosOrderError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Portfolios.md`; `.../Models/PortfoliosOrderPlaceRequest.cs`; `.../Models/PortfoliosOrder.cs`; `.../Models/Enums/Status65.cs` |
| 15 | `client.Portfolios.RetrievePortfoliosOrder(RetrievePortfoliosOrderRequest ...)` → `PortfoliosOrder` | `RetrievePortfoliosOrderRequest`: `PortfolioOrderId: Guid, required`; `UpvestApiVersion` | — (GET) | `PortfoliosOrder`: `Status: Status65` | `ApiException<RetrievePortfoliosOrderError>`; `TryGetNoContent`[401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `portfolios:read`/`portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Portfolios.md`; `.../Models/PortfoliosOrder.cs` |
| 16 | `client.Portfolios.ListPortfoliosOrders(ListPortfoliosOrdersRequest ...)` → `PortfoliosOrdersListResponse` | `ListPortfoliosOrdersRequest` (no required); `AccountId`, `AllocationId`, `Sort/Order/Offset/Limit`, `StartDate/EndDate` | — (GET) | `PortfoliosOrdersListResponse`: `data` (PortfoliosOrder, filter by `account_id`, match `client_reference`) + `meta` | `ApiException<ListPortfoliosOrdersError>`; `TryGetNoContent`[400,401,403,404,405,406,429,500,503,504]·`TryGetRawError` | **offset/limit** | `portfolios:read`/`portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Portfolios.md`; `.../Models/PortfoliosOrdersListResponse.cs` |
| 17 | `client.Tests.CreateBankTransaction(CreateBankTransactionRequest ...)` → `PaymentsBankTransactionCreateResponse` | `CreateBankTransactionRequest`: `IdempotencyKey: Guid, required`; `UpvestApiVersion`; `Body: PaymentsBankTransactionCreateRequest?` | `PaymentsBankTransactionCreateRequest`: `Account (account) req Account29`, `Amount (amount) req string` (`^[0-9]{1,9}(\.[0-9]{2})?$`), `Currency (currency) req`, `ClientReference (client_reference) req`, `CounterpartyName (counterparty_name) req`, `CounterpartyAccount (counterparty_account) req`, `VirtualBankAccountId (virtual_bank_account_id) opt` | `PaymentsBankTransactionCreateResponse` | `ApiException<CreateBankTransactionError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `tests:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/Tests.md`; `.../Models/PaymentsBankTransactionCreateRequest.cs` |
| 18 | `client.PortfoliosRebalancing.TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest ...)` → `TriggerPortfolioRebalancingResponse` | `TriggerPortfolioRebalancingOperationRequest`: `IdempotencyKey: Guid, required`; `UpvestApiVersion`; `Body: TriggerPortfolioRebalancingRequest?` | `TriggerPortfolioRebalancingRequest` (AnyOf) — use `Allocations` arm: `AllocationsValue (allocations) req IReadOnlyList<Guid>` (= `[upvestAllocationId]`) | `TriggerPortfolioRebalancingResponse`: `Id: Guid` (= upvestExecutionId) | `ApiException<TriggerPortfolioRebalancingError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/PortfoliosRebalancing.md`; `.../Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`; `.../Models/Allocations.cs`; `.../Models/TriggerPortfolioRebalancingResponse.cs` |
| 19 | `client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(RetrievePortfoliosRebalancingExecutionRequest ...)` → `PortfoliosRebalancingExecution` | `RetrievePortfoliosRebalancingExecutionRequest`: `ExecutionId: Guid, required`; `UpvestApiVersion` | — (GET) | `PortfoliosRebalancingExecution`: `Id`, `Accounts?`, `Allocations?` — **NO status field** | `ApiException<RetrievePortfoliosRebalancingExecutionError>`; `TryGetNoContent`[401,403,404,405,406,429,500,503,504]·`TryGetRawError` | none | `portfolios:read`/`portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/PortfoliosRebalancing.md`; `.../Models/PortfoliosRebalancingExecution.cs` |
| 20 | `client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(ListPortfolioRebalancingExecutionOrdersRequest ...)` → `PortfoliosRebalancingExecutionOrderListResponse` | `ListPortfolioRebalancingExecutionOrdersRequest`: `ExecutionId: Guid, required`; `Sort/Order/Offset/Limit`, `StartDate/EndDate`; `UpvestApiVersion` | — (GET) | `...OrderListResponse`: `Data` (`PortfoliosRebalancingExecutionOrder`: `Status: Status71` NEW/PROCESSING/FILLED/CANCELLED) + `Meta` — status of the execution is **derived** from these | `ApiException<ListPortfolioRebalancingExecutionOrdersError>`; `TryGetNoContent`[400,401,403,404,405,406,429,500,503,504]·`TryGetRawError` | **offset/limit** (`meta.total_count`) | `portfolios:read`/`portfolios:admin` · **401 in sandbox** | `sdk/dotnet/map/operations/PortfoliosRebalancing.md`; `.../Models/PortfoliosRebalancingExecutionOrderListResponse.cs`; `.../Models/PortfoliosRebalancingExecutionOrder.cs` |
| 21 | `client.Positions.ListPositions(ListPositionsRequest ...)` → `PositionsListResponse` | `ListPositionsRequest`: `AccountId: Guid, required`; `Offset/Limit`; `UpvestApiVersion` | — (GET) | `PositionsListResponse`: `Data` (`Datum1`: `Instrument` (uuid+isin → instrumentId), `Quantity: string`) + `Meta` | `ApiException<ListPositionsError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | **offset/limit** (`meta.total_count`) | `positions:read` · **401 in sandbox** | `sdk/dotnet/map/operations/Positions.md`; `.../Models/PositionsListResponse.cs`; `.../Models/Datum1.cs` |
| 22 | `client.Valuations.GetAccountValuation(GetAccountValuationRequest ...)` → `AccountValuation` | `GetAccountValuationRequest`: `AccountId: Guid, required`, `PriceQuality: PriceQuality3, required`; `UpvestApiVersion` | — (GET, `price_quality` query) | `AccountValuation`: `TotalSecurityValue` (amount+currency → `totalValue`), `SecurityPositions` (`AccountValuationSecurityPosition`: `Instrument`, `Quantity`, `Value?` → per-holding `value`) | `ApiException<GetAccountValuationError>`; `TryGetNoContent`[400,401,403,404,406,429,500,503,504]·`TryGetRawError` | none | `valuations:read` · **401 in sandbox** | `sdk/dotnet/map/operations/Valuations.md`; `.../Models/AccountValuation.cs`; `.../Models/AccountValuationSecurityPosition.cs`; enum `.../Models/Enums/PriceQuality3.cs` |
| 23 | `client.Instruments.ListInstruments(ListInstrumentsRequest ...)` → `InstrumentsListResponse` | `ListInstrumentsRequest` (no required); `TradingStatus`, `Sort/Order/Offset/Limit`; `UpvestApiVersion` | — (GET) | `InstrumentsListResponse`: `data` (instruments — defines the set of valid fund ISINs) + `meta` | `ApiException<ListInstrumentsError>`; `TryGetNoContent`[400,401,403,406,429,500,503,504]·`TryGetRawError` | **offset/limit** | `instruments:read` · **401 in sandbox** | `sdk/dotnet/map/operations/Instruments.md`; `.../Models/InstrumentsListResponse.cs` |

Operations deliberately **not used** (adjacent but out of scope): `Portfolios.Create/Retrieve/List/Cancel…Order`
variants beyond the above, `TopUps.*`/`CreditFundings.*`/`CashBalances.*`/`DirectDebits.*` (alternative
funding rails — see Blocker on money-in), `Users.ListUsers/OffboardUser/UserDataChange`.

### 2b. CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
|---|---|---|
| The `modelId` an investor selects (`allocation_id` sent to `CreatePortfoliosConfiguration`/`UpdatePortfoliosConfiguration`) must be a model the operator has defined | `CreatePortfoliosConfiguration`/`UpdatePortfoliosConfiguration` (write) ← `ListPortfoliosAllocations` (defines the set; the app also stores each created allocation id in `ModelPortfolio`) | In the `PUT /api/portfolio/investor/model` handler: look up the app `ModelPortfolio` by app `modelId` (which the app populated from allocation creates / `ListPortfoliosAllocations`); reject with 400/404 before the SDK call if absent. |
| The `allocation`(s) a realignment targets must be an existing model | `TriggerPortfolioRebalancing` (write) ← `ListPortfoliosAllocations` / app `ModelPortfolio` | In the `POST /api/portfolio/models/{modelId}/realignments` handler: resolve app `modelId` → `upvestAllocationId` from `ModelPortfolio`; reject before the SDK call if unknown. |
| A model's fund `instrumentId`s (ISINs) must be instruments Upvest offers | `CreatePortfoliosAllocation`/`UpdatePortfoliosAllocation` (write) ← `ListInstruments` (defines the offered set) | In the `PUT /api/portfolio/models/{modelId}` handler: validate each submitted ISIN against `ListInstruments` before the create/update. **The app does not currently read instruments — add this read.** (In this sandbox `ListInstruments` 401s; where it is unavailable, skip the pre-check and rely on Upvest's own 400 — document that fallback.) |
| A payment / model-selection requires the caller to be an accepted investor with a model chosen | `CreatePortfoliosOrder` / `CreatePortfoliosConfiguration` (write) ← the app `InvestorRecord` (populated by Flow 1; status must be `active`) and, for payments, `PortfoliosConfiguration` existing | In the `POST /api/portfolio/payments` and `PUT /api/portfolio/investor/model` handlers: check `InvestorRecord.status == active` (and a configuration exists for payments) before the SDK call. |

⚠ These rules are not on the map — derived from the task. "Operations" names the call that defines the
legal set (`ListPortfoliosAllocations`, `ListInstruments`) or the earlier write the app stored.

---

## 3. Trap notes

- **Step 0 (client/DI):** `UpvestInvestmentApiClient` owns no `HttpClient` lifetime for you and the options
  object is captured once; registering it wrong (transient, per-request options) breaks pooling/rotation.
  What the constructor expects and how to DI-register a singleton with one `HttpClient` — **MUST load
  upvest-v4:dotnet-client-initialization**.
- **Step 0 (auth):** Upvest needs BOTH OAuth2 client credentials AND an HTTP message signature on *every*
  call (incl. the token request); a missing/misapplied credential throws `AuthSchemeException` (signing
  misconfig throws `InvalidOperationException`) before anything is sent. Which property/hook each of the
  six `Upvest:` keys goes on, and the env-var fallbacks that fire when a property is left null — **MUST
  load upvest-v4:dotnet-authentication**.
- **Step 0 (base URL):** `Upvest:BaseUrl` must be used verbatim for every call; the SDK resolves the base
  URL through `options.Server.Default.<Environment>.BaseUrl`, not a single field, and `Environment`
  defaults to `Production` (= `https://sandbox.upvest.co`). What exactly to override so BaseUrl wins —
  **MUST load upvest-v4:dotnet-configuration-resilience**.
- **Steps 1–5 (models):** request bodies are AnyOf unions (`UserCreateRequest`, `AccountCreateRequest`,
  `AccountGroupCreateRequest`, `TriggerPortfolioRebalancingRequest`, `TaxResidencyForCreateRequest`) and
  open enums (`Currency`, `Side15`, `Type13/16/7`, `Status65/71`, `PriceQuality3`, `Nationality`,
  `Country`); picking the wrong arm or constructing an enum by string silently misbehaves. How to build
  union arms and resolve open-enum members (and read unknown arms) — **MUST load upvest-v4:dotnet-models**.
- **Step 2/3 money & weights are strings:** `Allocation.Weight`, `PortfoliosOrderPlaceRequest.CashAmount`,
  position `Quantity`, `PaymentsBankTransactionCreateRequest.Amount` are regex-validated decimal
  **strings**, not numbers; the task's JSON euro numbers (2 decimals) and percentage weights must be
  formatted to those patterns (invariant culture) or the call is rejected. Exact patterns are on the
  model rows above — formatting rule — **MUST load upvest-v4:dotnet-models**.
- **Step 2 (one config per account):** `CreatePortfoliosConfiguration` remarks say "Only one active
  configuration per account is permitted" — a second create fails; switching a model must use
  `UpdatePortfoliosConfiguration`. Which error that collision surfaces as and how to branch create-vs-update
  — **MUST load upvest-v4:dotnet-error-handling**.
- **Step 4 (no execution status):** `PortfoliosRebalancingExecution` has **no status field**; realignment
  status must be aggregated from `ListPortfolioRebalancingExecutionOrders` (`Status71` per account order).
  Reading paged list results and interpreting a cursor/offset cut-off — **MUST load
  upvest-v4:dotnet-configuration-resilience**.
- **All steps (errors incl. sandbox 401):** every operation is Case A; a 401 "not authorised to use these
  scopes" arrives via `TryGetNoContent`, and a drifted body arrives as `ResponseDeserializationException`
  (not `ApiException<TError>`). Which exceptions actually reach the catch and how to read the typed body —
  **MUST load upvest-v4:dotnet-error-handling**.
- **All writes (idempotency/unknown outcome):** every write except `CreateIdentifier` and
  `UpdatePortfoliosAllocation` takes a real caller-supplied `IdempotencyKey: Guid` on its request record;
  resending the same key is the reconciliation path after a dropped connection. What the retained key
  guarantees and the resend semantics — **MUST load upvest-v4:dotnet-configuration-resilience** (Retries)
  and **upvest-v4:dotnet-getting-started** (Idempotency).
- **Testing:** stub the SDK at the `HttpClient` seam and construct SDK exceptions directly to exercise the
  401/`ResponseDeserializationException`/unknown-outcome paths — **MUST load upvest-v4:dotnet-testing**.

---

## 4. REQUIRED READING (load all before implementation starts)

- `upvest-v4:dotnet-client-initialization` — Step 0 client construction + DI singleton.
- `upvest-v4:dotnet-authentication` — Step 0 OAuth2 + signing credentials from the `Upvest:` keys.
- `upvest-v4:dotnet-configuration-resilience` — Step 0 base-URL override; retries/timeout budget; pagination.
- `upvest-v4:dotnet-calling-endpoints` — Steps 1–5 request-record + `requestOptions`/`cancellationToken` call shape.
- `upvest-v4:dotnet-models` — Steps 1–5 AnyOf arms, open enums, decimal-string formatting, dates.
- `upvest-v4:dotnet-error-handling` — all steps; Case A typed errors; **always loaded**.
- `upvest-v4:dotnet-getting-started` — Idempotency keys; map lookups mid-implementation.
- `upvest-v4:dotnet-testing` — stubbing the SDK for the error/retry/unknown-outcome paths.

Mandatory hazard row (verbatim): a body that does not match its declared type — a drifted or malformed
**2xx** response (a missing `required` member) or a **non-2xx** body that does not match its operation's
generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException`
that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch
ladder that handles only `ApiException<TError>` lets it escape, so it must also catch
`ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
| 1 | Credential fail-fast | Bind `Upvest:` via `IConfiguration`/options at registration. At startup verify **all five non-secret-optional parts are present and non-blank**: `Upvest:ClientId`, `Upvest:ClientSecret`, `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:BaseUrl` (throw and refuse to start otherwise); `Upvest:SigningKeyPassphrase` is optional (null allowed). Because the SDK would otherwise surface a missing credential only as `AuthSchemeException`/`InvalidOperationException` on first call, the check is in the registration path (`src/PublicApi/Program.cs` / an `AddUpvest…` extension). Each of ClientId/ClientSecret (OAuth) and SigningKeyId/SigningKeyPath/ClientId (signing) is checked because a blank part is not a missing one. |
| 2 | Secret sourcing & rotation | `ClientSecret` and `SigningKeyPassphrase` come from config (env/user-secrets in dev; a secret store in prod) — never in source, never logged, never returned. DI builds the options object **once at registration** and the singleton client captures it, so a rotated secret/passphrase takes effect only on process restart. Rotation-without-restart is out of scope; documented as restart-required. |
| 3 | Total timeout budget | DECIDED: per-attempt `options.Retry.Timeout = 30s` and `HttpClient.Timeout = 30s` (named client), and every SDK call goes through `UpvestPortfolioService.CallAsync`, which links the caller's `CancellationToken` to a `CancellationTokenSource` with a `CallBudget = 45s` whole-call ceiling. A budget overrun surfaces as an unknown-outcome `UpvestException` → 502. (`UpvestPortfolioService.CallAsync`, `UpvestServiceCollectionExtensions.PerAttemptTimeout`.) |
| 4 | Write-retry ownership | DECIDED: kept the SDK default `HttpMethodsToRetry` (GET/HEAD/PUT/OPTIONS) — POST writes are **not** auto-resent on transient failures, and every POST write carries a real `IdempotencyKey` (persisted on the app record) so the OAuth-401 single re-send is harmless. PUT writes (`UpdatePortfoliosAllocation`, allocation update) are full replaces, so a resend is idempotent. No `HttpMethodsToRetry` override. |
| 5 | Idempotency & ambiguous writes | Real keys: `CreateUser`, `SetTaxResidencies`, `CreateAccountGroup`, `CreateAccount`, `CreatePortfoliosAllocation`, `CreatePortfoliosConfiguration`, `UpdatePortfoliosConfiguration`, `CreatePortfoliosOrder`, `TriggerPortfolioRebalancing`, `CreateBankTransaction` each carry a required `IdempotencyKey: Guid` on the request record → the app generates it once, **persists it** on the matching app record, and resends the same value to reconcile. **No real key:** `CreateIdentifier`, `UpdatePortfoliosAllocation` → reconcile by re-reading (`ListUserIdentifiers` / the allocation). The injected `Idempotency-Key` header on other calls is NOT a key. |
| 6 | Observability | Log at Information: operation name, app record ids (shopperUserName, app modelId/paymentId/realignmentId), Upvest resource ids, and HTTP status/outcome. On error log the status and the provider correlation id read from the `RawError` body (`ReadAsString()`/`ReadAsJson<T>()` via `TryGetNoContent`/`TryGetRawError`). **Never** log request bodies on Flow-1 paths (PII) or any secret. Log the known sandbox 401 at Warning with its message. |
| 7 | Sensitive data | **Yes — heavy PII** in Flow 1 (`UserTolCreateRequest`, `Address2`, `IdentifierCreateRequest`, `TaxResidenciesSetRequest`: names, birth date/city/country, nationality, phone, email, address, tax id, identity-document number). Posture: `options.Logging.LogRequestBody` OFF; `options.Logging.LoggerFactory` set explicitly (so the `UPVEST_INVESTMENT_API_LOG=trace` env var cannot force body logging); add every sensitive form/body key to the logging deny-list; app diagnostics never echo these bodies. Confirm the exact options in dotnet-configuration-resilience. |
| 8 | Environment selection | One server group `Default`. `ServerEnvironment.Production` → `https://sandbox.upvest.co` (sandbox), `ServerEnvironment.Environment2` → `https://api.upvest.co` (live). This deployment keeps `Environment = Production` and overrides `options.Server.Default.Production.BaseUrl` with `Upvest:BaseUrl` verbatim. Test traffic stays on the sandbox host; prod deployments set `Upvest:BaseUrl` to the live base and must not reuse sandbox credentials. |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. The app store (in-memory EF here, isolated per PublicApi process) holds each claim; a primary-key/unique constraint refuses the second claim even on the in-memory provider. |
| 10 | Partial results | See PAGED READS. Each list read walks `meta.offset/limit/total_count` to completion; where a cap is applied, the app DTO exposes a `truncated`/`totalCount` field so the HTTP caller (not a log) sees it. |
| 11 | Unknown outcomes | See UNKNOWN OUTCOMES. Each write's `catch` on `SdkConnectionException`/`SdkTimeoutException` reconciles by resending the stored `IdempotencyKey` (or re-reading, for keyless writes) before reporting; where the outcome cannot be settled synchronously the app record stays `pending` and the status-refresh read settles it later. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
| Investor sign-up (`CreateUser`+chain) | `InvestorRecord`, unique index on `ShopperUserName` (`InvestorRecordConfiguration`) | existing-record check + unique index; a repeat sign-up returns the existing status rather than a second CreateUser | `SignUpInvestorAsync`: `FirstOrDefaultAsync(InvestorByShopperSpecification)` returns early; `catch (…) when (IsUniqueViolation)` on `AddAsync` reloads | `UpvestPortfolioService.SignUpInvestorAsync` — the `FirstOrDefaultAsync` guard then `_investors.AddAsync` + `CreateUser` |
| Pay in (`CreatePortfoliosOrder`) | `PortfolioPayment` (PK), `OrderIdempotencyKey` generated in ctor, written before the SDK call | each pay-in is a distinct intent; the stored `OrderIdempotencyKey` makes the SDK call itself safe to resend | `PayInAsync`: `catch (UpvestException ex) when (ex.UnknownOutcome)` leaves it pending | `UpvestPortfolioService.PayInAsync` — `_payments.AddAsync` then `CreatePortfoliosOrder` with `payment.OrderIdempotencyKey` |
| Trigger realignment (`TriggerPortfolioRebalancing`) | `Realignment` (PK), `IdempotencyKey` generated in ctor, written before the SDK call | claim written before the call; `IdempotencyKey` resend-safe | `TriggerRealignmentAsync`: `catch (UpvestException ex) when (ex.UnknownOutcome)` | `UpvestPortfolioService.TriggerRealignmentAsync` — `_realignments.AddAsync` then `TriggerPortfolioRebalancing` with `realignment.IdempotencyKey` |
| Create/replace model (`CreatePortfoliosAllocation`/`Update…`) | `ModelPortfolio`, unique index on `ModelId` (`ModelPortfolioConfiguration`) | PUT is keyed by `modelId`: existing row is replaced; a concurrent first-create is refused by the unique index | `UpsertModelAsync`: `catch (…) when (IsUniqueViolation)` on `AddAsync` reloads and replaces | `UpvestPortfolioService.UpsertModelAsync` — `_models.AddAsync` then `Create/UpdatePortfoliosAllocation` |
| Select model (`CreatePortfoliosConfiguration`/`Update…`) | one config per account at Upvest, keyed by `accountId` | `RetrievePortfoliosConfiguration` decides create-vs-update; a 404 means none yet | `SelectModelAsync`: `catch (UpvestException ex) when (ex.ProviderStatusCode == 404)` → create, else update | `UpvestPortfolioService.SelectModelAsync` — `RetrievePortfoliosConfiguration` then `Create/UpdatePortfoliosConfiguration` |

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
|---|---|---|---|
| `ListPortfolioRebalancingExecutionOrders` (GET /api/portfolio/realignments/{id}) | `limit`/`offset`; `meta.total_count`; `MaxPages=50` backstop | if the page cap is hit, `PageAsync` returns `Truncated=true` and `DeriveRealignmentStatus` reports `pending` (never a false `completed`) | `UpvestPortfolioService.PageAsync` + `DeriveRealignmentStatus` (called from `GetRealignmentAsync`) |

Reads removed from the final design: GET /api/portfolio/models is served from the app's own `ModelPortfolio` store (not `ListPortfoliosAllocations`), so it is not paged. GET /api/portfolio holdings use `GetAccountValuation` (whose `security_positions` is a full, non-paged list carrying quantity **and** value) instead of `ListPositions`, so no position paging is needed. `ListInstruments` ISIN pre-validation was dropped (see Blocker resolution 5); the provider's own 400 is the fallback. `ListPortfoliosOrders` is not used — payment reconciliation re-reads the specific order by id via `RetrievePortfoliosOrder`.

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
|---|---|---|---|---|
| `CreateUser` | persisted `InvestorRecord` + stored `UserIdempotencyKey` | `shopperUserName`; `UserIdempotencyKey` | transport failure → unknown-outcome `UpvestException` (record kept, no `UpvestUserId`); a repeat sign-up re-reads the record and `GET /investor` refreshes | `UpvestPortfolioService.CallAsync` boundary + `GetInvestorAsync`/`DeriveInvestorStatusAsync` |
| `CreateAccountGroup` / `CreateAccount` | `InvestorRecord` + stored `AccountGroup/AccountIdempotencyKey` | `UpvestUserId`; stored keys | best-effort chain steps; on failure investor stays `pending`, settled later by `GET /investor` | `TryStepAsync` + `DeriveInvestorStatusAsync` |
| `SetTaxResidencies` | `InvestorRecord` + stored `TaxIdempotencyKey` (replaces the set) | `UpvestUserId`; `TaxIdempotencyKey` | best-effort step; failure logged, investor stays pending | `SignUpInvestorAsync` → `TryStepAsync` |
| `CreateIdentifier` (no key) | `InvestorRecord.IdentifierSet` flag | `UpvestUserId` | best-effort step; failure logged, flag not set | `SignUpInvestorAsync` → `TryStepAsync` |
| `CreatePortfoliosOrder` | `PortfolioPayment` claim + stored `OrderIdempotencyKey` | `PaymentId`; `OrderIdempotencyKey`; `UpvestOrderId` | `catch (UpvestException) when (UnknownOutcome)` leaves payment `pending`; `ListPaymentsAsync` settles via `RetrievePortfoliosOrder` | `UpvestPortfolioService.PayInAsync` + `ListPaymentsAsync` |
| `CreatePortfoliosConfiguration`/`Update…` | investor's `accountId` (one config per account) | `accountId` | retry re-derives create-vs-update via `RetrievePortfoliosConfiguration` (404 ⇒ create) | `UpvestPortfolioService.SelectModelAsync` |
| `TriggerPortfolioRebalancing` | `Realignment` claim + stored `IdempotencyKey` → execution `id` | `RealignmentId`; `IdempotencyKey`; `UpvestExecutionId` | `catch (UpvestException) when (UnknownOutcome)` leaves it `pending`; `GetRealignmentAsync` settles via the execution-orders list | `UpvestPortfolioService.TriggerRealignmentAsync` + `GetRealignmentAsync` |
| `CreateBankTransaction` | n/a — see Blocker 2 resolution (explicit cash-crediting omitted; investment is the `CreatePortfoliosOrder`) | — | not used | — |

> In-memory-provider caveat: the natural-key unique indexes above are enforced by a relational provider; the EF in-memory provider enforces only the integer PK. Under the in-memory provider the duplicate guard is the explicit existing-record read before the claim, which is exact for the single-process sandbox but races under true multi-host concurrency. On SQL the unique index is the hard guard. The `UpvestException`/transport unknown-outcome tests are covered by the SDK `HttpClient`-seam approach in `dotnet-testing`; the live sandbox exercised the real 401/400/200 paths end-to-end.

---

## 6. Assumptions & Blockers

**Assumptions**
- **Operating model = TOL.** The task's full-KYC body (required email, address, FATCA) matches Upvest's
  TOL (Take-Our-Licence) user arm (`UserTolCreateRequest`), not BYOL. Implementation uses the
  `UserCreateRequest.UserTolCreateRequest(...)` arm.
- **Account shape.** "An account able to hold a managed portfolio" = an `AccountGroupCreateUserRequest`
  with `type = PERSONAL` plus an `AccountCreateUserRequest` with `type = PORTFOLIO` (the account type
  whose orders are portfolio-based). `investorId` = the Upvest user id.
- **`nationality` → `nationalities`.** The task's single `nationality` maps to a one-element
  `IReadOnlyList<Nationality>` (the field is required, min length 1).
- **FATCA is required but not in the task body.** `UserTolCreateRequest.Fatca` is required
  (`status: bool`, `confirmed_at: timestamp`). The app will send `status = false`,
  `confirmed_at = DateTimeOffset.UtcNow` unless told otherwise.
- **`modelId`/`paymentId`/`realignmentId` are app-facing ids** the app maps to Upvest UUIDs it stores;
  `PUT …/models/{modelId}` is create-or-replace keyed on the app id.
- **Status mapping.** Payment: `Status65` NEW/PROCESSING → `pending`; FILLED/SETTLED → `invested`;
  CANCELLED → `failed`. Realignment: derived from `Status71` across execution orders — all FILLED →
  `completed`; any CANCELLED (and none pending) → `failed`; any NEW/PROCESSING → `pending`. Investor:
  `pending` until the user is accepted and the account group is ready, then `active`; a failed check →
  `rejected` (acceptance/rejection is derived from `Users.RetrieveUser` status + account-group readiness,
  not a single field — see Blocker).
- **Admin/operator = the `ADMINISTRATORS` role** (`BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS`),
  applied via `[Authorize(Roles = …, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`,
  the convention in `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`. No "OPERATOR" role
  exists in this codebase; **DECIDED: use `ADMINISTRATORS`** on `UpsertModelEndpoint` and
  `TriggerRealignmentEndpoint`; verified live (normal user → 403, admin → allowed).
- **Persistence context.** **DECIDED: extended `CatalogContext`** (added `DbSet`s + four
  `IEntityTypeConfiguration`s in `src/Infrastructure/Data/Config/`) so the existing `IRepository<T>`/
  `EfRepository<T>` machinery serves the new entities unchanged — the dominant convention. No dedicated
  context. (In-memory ignores migrations per the environment note; a migration would be added for a SQL deployment.)
- **GetPortfolio holdings source. DECIDED:** holdings (instrumentId, quantity, value) and `totalValue`
  come from `Valuations.GetAccountValuation` (its `security_positions` already carries quantity **and**
  value); `Positions.ListPositions` is not called (it would be redundant). `UpvestPortfolioService.GetPortfolioAsync`.

**Blockers / gaps — all RESOLVED (decided and implemented; headless, no user to consult)**
1. **Identity-document `expiryDate` — RESOLVED (design decision, not a true gap).** The endpoint accepts the
   full `identityDocument` (`type`, `number`, `issuingCountry`, `expiryDate`). What Upvest's
   `IdentifierCreateRequest` models — `type` (coerced to `NATIONAL_ID`/`PASSPORT` by
   `ResolveDocumentType`), `issuingCountry`, and `identifier` (the number) — is forwarded; `expiryDate` is
   accepted and validated by the caller's form but not forwarded (the SDK has no field for it). The
   capability "sign the shopper up with their identity document" is fully exposed, so this is a field-level
   mapping decision, not a blocking gap. (`UpvestPortfolioService.BuildUserBody`/`CreateIdentifier` call.)
2. **Money-in — RESOLVED (design decision).** Pay-in = `CreatePortfoliosOrder` (BUY, `cash_amount`, EUR),
   which invests the amount across the followed model's weights — exactly the task's "pay an amount in … invested
   across the funds … in that model's weights"; its status drives the payment status. Explicit sandbox
   cash-crediting via `Tests.CreateBankTransaction` is **not** issued, because it requires the investor's
   virtual-bank-account IBAN (`Account29.Iban`) which no in-scope operation surfaces in this sandbox — a
   fabricated IBAN would not credit the right account. In a fully-scoped deployment cash reaches the account
   through Upvest's funding rails and auto-investment / the placed order invests it. (`UpvestPortfolioService.PayInAsync`.)
3. **Rebalancing status — RESOLVED (design decision).** `GET /api/portfolio/realignments/{id}` derives
   `pending/completed/failed` from `ListPortfolioRebalancingExecutionOrders` (`Status71`) in
   `DeriveRealignmentStatus`: any NEW/PROCESSING (or a truncated page) → `pending`; all FILLED →
   `completed`; otherwise → `failed`. (`UpvestPortfolioService.GetRealignmentAsync`.)
4. **Sandbox scope limit — CONFIRMED live, handled.** Verified against the live sandbox: `auth/token` for
   `users:admin` and the HTTP-signed `CreateUser` both return **200**; any operation whose scope set the
   client lacks (portfolios/accounts/tax, and even `users:read` via `RetrieveUser`) fails its token request
   with **401**, surfacing as `AuthSchemeException` → a `UpvestException` mapped to **502** with a
   scope-specific message, or is swallowed as a best-effort onboarding step (investor left `pending`). No
   crash, no PII in logs. Flows 2–5 and the account/tax steps of Flow 1 are coded in full and gated by
   sensible preconditions (409/400) ahead of the provider call.
5. **Instrument ISIN pre-validation — RESOLVED (design decision).** `ListInstruments` (`instruments:read`)
   is not available in this sandbox, so model-fund ISINs are **not** pre-validated against it; the provider's
   own 400 on `CreatePortfoliosAllocation` is the fallback (surfaced as a caller-fault 400). The
   cross-operation invariant for model selection/realignment is instead enforced against the app's own
   `ModelPortfolio` store, which is the authoritative set of models this shop offers.

---

## 7. Source labels
Every `source` cell in §2 cites a map page or a map-named SDK source file (one targeted open each). All
`YOUR CALL — not in the map` and `UNVERIFIED` items have been settled above and in §5: timeout budget
(READINESS #3), write-retry verbs (#4), persistence context, admin/operator role, holdings source, the
money-in path (Blocker 2), and the rebalancing-status derivation (Blocker 3) — each recorded with the code
member that implements it. Verified live against the sandbox on 2026-11-13 (CreateUser 200; scope-limited ops 401 handled).
