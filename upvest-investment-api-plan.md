# Upvest Investment API integration plan — eShopOnWeb managed portfolios

Additive feature on `src/PublicApi`: shoppers hold an Upvest-managed investment portfolio. JWT-authenticated, routed under `/api/`. Does not touch the existing catalog/basket/order flow.

**SDK source (build reference + map):** `marketplace/plugins/upvest-v4/sdk/dotnet/` (plugin-relative `sdk/dotnet/`). API spec `1.172.0`. Reference `src/UpvestInvestmentApi/UpvestInvestmentApi.csproj`.

**Caller identity = buyerId.** Every eShopOnWeb JWT carries `ClaimTypes.Name` = username/email (see `src/Infrastructure/Identity/IdentityTokenClaimService.cs`; `Order.BuyerId` is that string). All persisted rows are keyed by this `buyerId`; a caller may read/write only its own rows. Admin endpoints use `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` (exemplar `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`).

---

## Domain mapping (task concept → Upvest resource)

| Task concept | Upvest resource | Notes |
| --- | --- | --- |
| Shopper becomes an investor | **User** (`CreateUser`, TOL body) + **Account** (`CreateAccount`, type `PORTFOLIO`) inside an **Account group** (`CreateAccountGroup`/`ListUserAccountGroups`) | Account needs an `account_group_id`; cash funding is keyed by `account_group_id`, portfolio/positions by `account_id`. |
| Sign-up accepted | KYC check **PASSED** (`UserChecks`) **and** account status **ACTIVE** (`Account.Status` = `Status21.Active`) | App learns both by re-reading (no webhook route). |
| Model portfolio (name + funds + weights) | **Portfolio allocation** (`CreatePortfoliosAllocation` / `UpdatePortfoliosAllocation`; `PortfoliosAllocation.allocation[] = {instrument_id(ISIN), weight}`) | Weights are decimal **strings**; must sum to 100 (app-validated). |
| Investor follows a model | **Portfolio configuration** (`CreatePortfoliosConfiguration` first, `UpdatePortfoliosConfiguration` to switch) links `account_id → allocation_id` | |
| Pay money in | Sandbox credit **`CreateVirtualCashIncrease`** (account-group cash) → invest **`CreatePortfoliosOrder`** (`side = BUY`, `cash_amount`) across the account's configured allocation | Order spreads cash across the allocation's weights server-side. |
| Realignment | **`TriggerPortfolioRebalancing`** (`Allocations` arm = list of allocation ids) | Rebalances every account configured to that allocation. |
| Portfolio holdings & value | `RetrievePortfoliosConfiguration` (model) + `ListPositions` (qty per instrument) + `GetAccountValuation` (per-instrument + total value) | `paidIn` is an app-owned sum. |

**Account group provisioning is part of sign-up** — `CreateAccount` requires `account_group_id`, which only `CreateAccountGroup`/`ListUserAccountGroups` supply. See Assumptions (A1).

---

## 1. Scope & sequence

**Config & DI (once):** register `UpvestInvestmentApiClient` as a singleton (shared `HttpClient`), bind `Upvest:` options, wire OAuth2 + signing hook, fail-fast on missing credentials. Add EF entities + `IEntityTypeConfiguration` for the in-memory store (`UseOnlyInMemoryDatabase`).

**Flow 1 — Becoming an investor** (`POST/GET /api/portfolio/investor`)
1. Guard one investor row per `buyerId` (claim), persist `pending`.
2. `AccountGroups.CreateAccountGroup` (or reuse via `ListUserAccountGroups`) → `Users.CreateUser` (TOL body with KYC personal data) → `AccountsApi.CreateAccount` (type `PORTFOLIO`, the user's account group).
3. `UserChecks.CreateUserCheck` (KYC) to drive acceptance; status is re-read, never pushed.
4. `GET` re-reads `UserChecks.ListUserChecks` (KYC `PASSED`/`FAILED`) + `AccountsApi.RetrieveAccount` (`ACTIVE`), maps to `pending`/`active`/`rejected`, persists, returns `{investorId, status}`.

**Flow 2 — Model portfolios** (`PUT/GET /api/portfolio/models`, `PUT /api/portfolio/investor/model`)
1. (operator) validate weights sum to 100 and each `instrumentId` is a real fund (`Instruments.ListInstruments`/`RetrieveInstrument`) → `Portfolios.CreatePortfoliosAllocation` (new modelId) / `UpdatePortfoliosAllocation` (existing). Persist modelId → allocationId.
2. `GET models` returns persisted models (or `Portfolios.ListPortfoliosAllocations`).
3. (investor) choose model → validate modelId is one offered → `Portfolios.CreatePortfoliosConfiguration` (first) / `UpdatePortfoliosConfiguration` (switch). Persist selected modelId.

**Flow 3 — Paying in** (`POST/GET /api/portfolio/payments`)
1. Refuse unless investor is `active` with a model chosen. Persist payment `pending` (claim).
2. `VirtualCashBalances.CreateVirtualCashIncrease` (credit account group) → `Portfolios.CreatePortfoliosOrder` (`side = BUY`, `cash_amount`, `currency = EUR`).
3. `GET` re-reads `Portfolios.RetrievePortfoliosOrder` → map `Status65` to `pending`/`invested`/`failed`; return newest-first `{paymentId, amount, status}`.

**Flow 4 — Realignment** (`POST /api/portfolio/models/{modelId}/realignments`, `GET …/realignments/{id}`)
1. (operator) `PortfoliosRebalancing.TriggerPortfolioRebalancing` (`Allocations` arm, the model's allocationId). Persist realignment `pending`.
2. `GET` re-reads `RetrievePortfoliosRebalancingExecution` + `ListPortfolioRebalancingExecutionOrders` → derive `completed`/`failed` (see B1).

**Flow 5 — The investor's portfolio** (`GET /api/portfolio`)
- `RetrievePortfoliosConfiguration` (modelId) + `ListPositions` (holdings qty) + `GetAccountValuation` (per-instrument value + total). `paidIn` from persisted payments. Returns `{modelId, holdings[{instrumentId, quantity, value}], totalValue, paidIn}`.

> **Sandbox scope note (applies to every SDK call below):** the sandbox client is authorised for *user operations only*. Every operation here requires an admin/read scope (`users:admin`, `accounts:admin`, `checks:admin`, `portfolios:admin`/`:read`, `virtual_cash_balances:admin`, `positions:read`, `valuations:read`, `instruments:read`), so each call is refused with **401 "This client is not authorised to use these scopes."** Per the task this is the environment, not a blocker — flows are built in full; verify as far as the sandbox allows.

---

## 2. CONTRACT SHEET

> ⚠ **Signatures below are generated code, reproduced verbatim.** Each operation that takes input takes **ONE request record as its first parameter**, built with an object initializer whose property names are the record's own — never flat positional arguments. An operation with no inputs takes none.
> ⚠ **Every SDK type must be written fully-qualified** with the namespace its source path implies, taken from the path given for *that* type — never inferred from a neighbouring type. Namespaces: client/options `UpvestInvestmentApi`; records `UpvestInvestmentApi.Models`; enums `UpvestInvestmentApi.Models.Enums`; unions `UpvestInvestmentApi.Models.AnyOf`; request records `UpvestInvestmentApi.Requests.<Controller>`; errors `UpvestInvestmentApi.Errors`; `RawError` `UpvestInvestmentApi.Core.ErrorResponse`; `ApiException<T>` `UpvestInvestmentApi.Core.Exceptions`; signing `UpvestInvestmentApi.Signing`.

**Every operation below is throw-only, Case A (typed), server group `Default`.** Case A accessor on all of them is `TryGetNoContent([NotNullWhen(true)] out RawError? value)` for the declared error statuses + inherited `TryGetRawError([NotNullWhen(true)] out RawError? error)` fallback — the typed payload is `RawError` in both arms (no field-bearing error body). Error statuses per op are in §2 error-status column. All request records also carry `UpvestApiVersion UpvestApiVersion { get; init; } = UpvestApiVersion._1` (default; leave unset) — source `src/UpvestInvestmentApi/Models/Enums/UpvestApiVersion.cs`.

### Per-operation rows

| Controller · Op | Signature (first param = request record) | Request record members (`Name: type, required?`) | Body model + fields (`Name (wire): type, required?`) | Response envelope → fields read | Err statuses | source |
| --- | --- | --- | --- | --- | --- | --- |
| `Users.CreateUser` | `CreateUser(CreateUserRequest request, …)` | `IdempotencyKey: Guid, REQUIRED` · `Body: UserCreateRequest?` | `UserCreateRequest` (union; use `.UserTolCreateRequest(UserTolCreateRequest)` arm). `UserTolCreateRequest`: `FirstName (first_name): string, REQ` · `LastName (last_name): string, REQ` · `Email (email): string, REQ` · `BirthDate (birth_date): DateTimeOffset, REQ` · `Nationalities (nationalities): IReadOnlyList<Nationality>, REQ (min 1)` · `Address (address): Address2, REQ` · `Fatca (fatca): Fatca, REQ` · opt: `BirthCity`,`BirthCountry`,`PhoneNumber`,`TermsAndConditions`,`DataPrivacyAndSharingAgreement`. `Address2`: `AddressLine1 (address_line1), Postcode (postcode), Country (country: Country), City (city)` all REQ. `Fatca`: `Status (status): bool, REQ` · `ConfirmedAt (confirmed_at): DateTimeOffset, REQ` | `UserCreateRequest1` (union) → `.TryGetUserTol(out UserTol)` → `UserTol.Id: Guid` (investorId), `UserTol.Status: Status` | 400,401,403,406,429,500,503,504 | `map/operations/Users.md`; `Requests/Users/CreateUserRequest.cs`; `Models/UserTolCreateRequest.cs`; `Models/Address2.cs`; `Models/Fatca.cs`; `Models/AnyOf/UserCreateRequest1.cs`; `Models/UserTol.cs` |
| `Users.RetrieveUser` | `RetrieveUser(RetrieveUserRequest request, …)` | `UserId: Guid, REQUIRED` | — | `UserGetResponse` (union, `Models/AnyOf/UserGetResponse.cs`) → user `Status` | 401,403,404,406,429,500,503,504 | `map/operations/Users.md` |
| `AccountGroups.CreateAccountGroup` | `CreateAccountGroup(CreateAccountGroupRequest request, …)` | `IdempotencyKey: Guid, REQUIRED` · `Body: AccountGroupCreateRequest?` | `AccountGroupCreateRequest` (union, `Models/AnyOf/AccountGroupCreateRequest.cs`) — personal/`PERSONAL` group for the user | `AccountGroupCreateResponse` (union) → group `Id` | 400,401,403,404,406,429,500,503,504 | `map/operations/AccountGroups.md` |
| `AccountGroups.ListUserAccountGroups` | `ListUserAccountGroups(ListUserAccountGroupsRequest request, …)` | `UserId: Guid, REQUIRED` · query `Sort/Order/Offset/Limit` | — | `AccountGroupsListResponse` → existing group ids | 400,401,403,404,406,429,500,503,504 | `map/operations/AccountGroups.md` |
| `AccountsApi.CreateAccount` | `CreateAccount(CreateAccountRequest request, …)` | `IdempotencyKey: Guid, REQUIRED` · `Body: AccountCreateRequest?` | `AccountCreateRequest` (union; `.AccountCreateUserRequest(AccountCreateUserRequest)`). `AccountCreateUserRequest`: `UserId (user_id): Guid, REQ` · `AccountGroupId (account_group_id): Guid, REQ` · `Type (type): Type16, REQ (= PORTFOLIO)` · opt `Name` | `AccountCreateResponse` (union) → `.TryGetAccount(out Account)` → `Account.Id: Guid`, `Account.Status: Status21` | 400,401,403,404,406,429,500,503,504 | `map/operations/AccountsApi.md`; `Models/AnyOf/AccountCreateRequest.cs`; `Models/AccountCreateUserRequest.cs`; `Models/AnyOf/AccountCreateResponse.cs`; `Models/Account.cs` |
| `AccountsApi.RetrieveAccount` | `RetrieveAccount(RetrieveAccountRequest request, …)` | `AccountId: Guid, REQUIRED` | — | `AccountRetrieveResponse` (union, `Models/AnyOf/AccountRetrieveResponse.cs`) → `Account.Status: Status21` | 401,403,404,406,429,500,503,504 | `map/operations/AccountsApi.md` |
| `UserChecks.CreateUserCheck` | `CreateUserCheck(CreateUserCheckRequest request, …)` | `UserId: Guid, REQUIRED` · `Body: UserCheckCreateRequest?` | `UserCheckCreateRequest` (union, `Models/AnyOf/UserCheckCreateRequest.cs`) — KYC arm | `UserCheckCreateResponse` → `Id: Guid` | 400,401,403,404,406,429,500,503,504 | `map/operations/UserChecks.md`; `Models/UserCheckCreateResponse.cs` |
| `UserChecks.ListUserChecks` | `ListUserChecks(ListUserChecksRequest request, …)` | `UserId: Guid, REQUIRED` · query `ListHistory`,`Type` | — | `UserCheckListResponse` (`Models/UserCheckListResponse.cs`) → KYC entries; each `UserCheck` → `.TryGetUserCheckKnowYourCustomer(out UserCheckKnowYourCustomer)` → `.Status: Status6` (IN_PROGRESS/PASSED/FAILED) | 400,401,403,404,406,429,500,503,504 | `map/operations/UserChecks.md`; `Models/AnyOf/UserCheck.cs`; `Models/UserCheckKnowYourCustomer.cs` |
| `Instruments.ListInstruments` | `ListInstruments(ListInstrumentsRequest request, …)` | (no required) · query `TradingStatus`,`Sort`,`Order`,`Offset`,`Limit` | — | `InstrumentsListResponse` → offered funds (ISIN) | 400,401,403,406,429,500,503,504 | `map/operations/Instruments.md` |
| `Instruments.RetrieveInstrument` | `RetrieveInstrument(RetrieveInstrumentRequest request, …)` | `InstrumentId: InstrumentUniqueIdentifier, REQUIRED` | — | `InstrumentsResponse` → confirm fund exists | 401,403,404,406,429,500,503,504 | `map/operations/Instruments.md` |
| `Portfolios.CreatePortfoliosAllocation` | `CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest request, …)` | `IdempotencyKey: Guid, REQUIRED` · `Body: PortfoliosAllocationCreateRequest?` | `PortfoliosAllocationCreateRequest`: `Allocation (allocation): IReadOnlyList<Allocation>, REQ` · opt `Name`,`RebalancingStrategyIds`. `Allocation`: `InstrumentId (instrument_id): InstrumentId, REQ` · `InstrumentIdType (instrument_id_type): InstrumentIdType4 = Isin` · `Weight (weight): string, REQ` (regex `^(100\|[0-9]{1,2}(\.[0-9]{1,5})?)$`) | `PortfoliosAllocation` → `Id: Guid` (allocationId), `Name`, `Allocation[]` | 400,401,403,404,406,429,500,503,504 | `map/operations/Portfolios.md`; `Models/PortfoliosAllocationCreateRequest.cs`; `Models/Allocation.cs`; `Models/AnyOf/InstrumentId.cs`; `Models/PortfoliosAllocation.cs` |
| `Portfolios.UpdatePortfoliosAllocation` | `UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest request, …)` | `AllocationId: Guid, REQUIRED` · `Body: PortfoliosAllocationUpdateRequest?` (**no IdempotencyKey**) | `PortfoliosAllocationUpdateRequest` (`Models/PortfoliosAllocationUpdateRequest.cs`) | `PortfoliosAllocation` | 400,401,403,404,406,429,500,503,504 | `map/operations/Portfolios.md` |
| `Portfolios.ListPortfoliosAllocations` | `ListPortfoliosAllocations(ListPortfoliosAllocationsRequest request, …)` | (no required) · query `Sort/Order/Offset/Limit/StartDate/EndDate/InstrumentIds` | — | `PortfoliosAllocationsListResponse` (`Models/PortfoliosAllocationsListResponse.cs`) → models + funds | 400,401,403,404,405,406,429,500,503,504 | `map/operations/Portfolios.md` |
| `Portfolios.CreatePortfoliosConfiguration` | `CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest request, …)` | `IdempotencyKey: Guid, REQUIRED` · `Body: PortfoliosConfigurationCreateRequest?` | `PortfoliosConfigurationCreateRequest`: `AccountId (account_id): Guid, REQ` · `AllocationId (allocation_id): Guid, REQ` · opt `RebalancingStrategyIds` | `PortfoliosConfiguration` → `AccountId`,`AllocationId` | 400,401,403,404,406,422,429,500,503,504 | `map/operations/Portfolios.md`; `Models/PortfoliosConfigurationCreateRequest.cs`; `Models/PortfoliosConfiguration.cs` |
| `Portfolios.UpdatePortfoliosConfiguration` | `UpdatePortfoliosConfiguration(UpdatePortfoliosConfigurationRequest request, …)` | `AccountId: Guid, REQUIRED` · `IdempotencyKey: Guid, REQUIRED` · `Body: PortfoliosConfigurationUpdateRequest?` | `PortfoliosConfigurationUpdateRequest`: opt `AllocationId (allocation_id): Guid?` · opt `RebalancingStrategyIds` (set `AllocationId` to switch) | `PortfoliosConfiguration` | 400,401,403,404,406,429,500,503,504 | `map/operations/Portfolios.md`; `Models/PortfoliosConfigurationUpdateRequest.cs` |
| `Portfolios.RetrievePortfoliosConfiguration` | `RetrievePortfoliosConfiguration(RetrievePortfoliosConfigurationRequest request, …)` | `AccountId: Guid, REQUIRED` | — | `PortfoliosConfiguration` → `AllocationId` (model the account follows) | 401,403,404,405,406,429,500,503,504 | `map/operations/Portfolios.md` |
| `VirtualCashBalances.CreateVirtualCashIncrease` | `CreateVirtualCashIncrease(CreateVirtualCashIncreaseRequest request, …)` | `IdempotencyKey: Guid, REQUIRED` · `Body: VirtualCashBalanceVirtualCashIncreaseCreateRequest?` | body: `AccountGroupId (account_group_id): Guid, REQ` · `Amount (amount): string, REQ` (regex `^[0-9]{1,9}(\.[0-9]{2})?$`) · `Currency (currency): Currency1, REQ (= EUR)` | `VirtualCashBalanceVirtualCashIncrease` (`Models/VirtualCashBalanceVirtualCashIncrease.cs`) → increase id/status | 400,401,403,404,406,429,500,503,504 | `map/operations/VirtualCashBalances.md`; `Models/VirtualCashBalanceVirtualCashIncreaseCreateRequest.cs` |
| `Portfolios.CreatePortfoliosOrder` | `CreatePortfoliosOrder(CreatePortfoliosOrderRequest request, …)` | `IdempotencyKey: Guid, REQUIRED` · `Body: PortfoliosOrderPlaceRequest?` | `PortfoliosOrderPlaceRequest`: `UserId (user_id): Guid, REQ` · `AccountId (account_id): Guid, REQ` · `CashAmount (cash_amount): string, REQ` · `Currency (currency): Currency, REQ (= EUR)` · `Side (side): Side15, REQ (= BUY)` · opt `PostTax (post_tax): bool? = false` | `PortfoliosOrder` → `Id: Guid` (upvest order id), `Status: Status65`, `CashAmount`, `ClientReference` | 400,401,403,404,406,429,500,503,504 | `map/operations/Portfolios.md`; `Models/PortfoliosOrderPlaceRequest.cs`; `Models/PortfoliosOrder.cs` |
| `Portfolios.RetrievePortfoliosOrder` | `RetrievePortfoliosOrder(RetrievePortfoliosOrderRequest request, …)` | `PortfolioOrderId: Guid, REQUIRED` | — | `PortfoliosOrder` → `Status: Status65` | 401,403,404,406,429,500,503,504 | `map/operations/Portfolios.md` |
| `Portfolios.ListPortfoliosOrders` | `ListPortfoliosOrders(ListPortfoliosOrdersRequest request, …)` | (no required) · query incl. `AccountId`,`Offset`,`Limit`,`Sort`,`Order` | — | `PortfoliosOrdersListResponse` → caller's orders | 400,401,403,404,405,406,429,500,503,504 | `map/operations/Portfolios.md` |
| `PortfoliosRebalancing.TriggerPortfolioRebalancing` | `TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest request, …)` | `IdempotencyKey: Guid, REQUIRED` · `Body: TriggerPortfolioRebalancingRequest?` | `TriggerPortfolioRebalancingRequest` (union; use `.Allocations(Allocations)`). `Allocations`: `AllocationsValue (allocations): IReadOnlyList<Guid>, REQ` | `TriggerPortfolioRebalancingResponse` → `Id: Guid` (execution id) | 400,401,403,404,406,429,500,503,504 | `map/operations/PortfoliosRebalancing.md`; `Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`; `Models/Allocations.cs`; `Models/TriggerPortfolioRebalancingResponse.cs` |
| `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` | `RetrievePortfoliosRebalancingExecution(RetrievePortfoliosRebalancingExecutionRequest request, …)` | `ExecutionId: Guid, REQUIRED` | — | `PortfoliosRebalancingExecution` → `Id`,`CreatedAt`,`UpdatedAt`,`Accounts?`,`Allocations?` — **NO status field** (see B1) | 401,403,404,405,406,429,500,503,504 | `map/operations/PortfoliosRebalancing.md`; `Models/PortfoliosRebalancingExecution.cs` |
| `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` | `ListPortfolioRebalancingExecutionOrders(ListPortfolioRebalancingExecutionOrdersRequest request, …)` | `ExecutionId: Guid, REQUIRED` · query `Offset`,`Limit`,`Sort`,`Order`,`StartDate`,`EndDate` | — | `PortfoliosRebalancingExecutionOrderListResponse` → per-order statuses (derive completed/failed) | 400,401,403,404,405,406,429,500,503,504 | `map/operations/PortfoliosRebalancing.md` |
| `Positions.ListPositions` | `ListPositions(ListPositionsRequest request, …)` | `AccountId: Guid, REQUIRED` · query `Offset`,`Limit` | — | `PositionsListResponse` → `Data: IReadOnlyList<Datum1>` (`Instrument.Isin`, `Quantity: string`), `Meta` | 400,401,403,404,406,429,500,503,504 | `map/operations/Positions.md`; `Models/PositionsListResponse.cs`; `Models/Datum1.cs`; `Models/Instrument.cs`; `Models/Meta.cs` |
| `Valuations.GetAccountValuation` | `GetAccountValuation(GetAccountValuationRequest request, …)` | `AccountId: Guid, REQUIRED` · `PriceQuality: PriceQuality3, REQUIRED` (query `price_quality`) | — | `AccountValuation` → `TotalSecurityValue {Amount: string, Currency}`, `SecurityPositions: IReadOnlyList<AccountValuationSecurityPosition>` (`Instrument6`, `Quantity`, `Value?`) | 400,401,403,404,406,429,500,503,504 | `map/operations/Valuations.md`; `Models/AccountValuation.cs`; `Models/TotalSecurityValue.cs`; `Models/AccountValuationSecurityPosition.cs` |

**Idempotency keys are REAL** on every `Create*`/`Trigger*` write above (`IdempotencyKey: Guid` is a member of the request record — `Idempotency` in `dotnet-getting-started`). `UpdatePortfoliosAllocation` and `CreateUserCheck` carry **no** idempotency key. The injected `Idempotency-Key` header is not one.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| A model's fund `instrumentId` (ISIN) must be an instrument Upvest offers | `Portfolios.CreatePortfoliosAllocation` ← `Instruments.ListInstruments` (or `RetrieveInstrument`) | In the `PUT /api/portfolio/models/{modelId}` handler, before the allocation write — look up each ISIN; the app does not currently read the instrument set, so this read is added as part of the work. |
| The investor's chosen `modelId` must be one the operator defined | `Portfolios.CreatePortfoliosConfiguration`/`UpdatePortfoliosConfiguration` ← app's `ModelPortfolio` store (populated from `CreatePortfoliosAllocation`) / `Portfolios.ListPortfoliosAllocations` | In the `PUT /api/portfolio/investor/model` handler, before the configuration write — reject a modelId not in the `ModelPortfolio` repository. |
| A payment requires an accepted investor **with a model chosen** | `Portfolios.CreatePortfoliosOrder` ← app's `PortfolioInvestor` (status `active`) + selected model | In the `POST /api/portfolio/payments` handler, before crediting/ordering. |
| Realignment targets an existing model's allocation | `PortfoliosRebalancing.TriggerPortfolioRebalancing` ← app's `ModelPortfolio` store (modelId → allocationId) | In the `POST …/{modelId}/realignments` handler, before the trigger. |

### Enum value tables (only those the integration branches on)

| Enum (source) | Values (C# member = wire) | Used for |
| --- | --- | --- |
| `Status21` (`Models/Enums/Status21.cs`) | `PendingApproval`=PENDING_APPROVAL · `Active`=ACTIVE · `Closing`=CLOSING · `Closed`=CLOSED · `Locked`=LOCKED | account-ready → sign-up `active` |
| `Status6` (`Models/Enums/Status6.cs`) | IN_PROGRESS · PASSED · FAILED (KYC) | KYC accepted/rejected |
| `Status` (user) (`Models/Enums/Status.cs`) | ACTIVE · INACTIVE · OFFBOARDING · OFFBOARDED | user lifecycle |
| `Status65` (`Models/Enums/Status65.cs`) | `New`=NEW · `Processing`=PROCESSING · `Filled`=FILLED · `Settled`=SETTLED · `Cancelled`=CANCELLED | payment: `pending`(NEW/PROCESSING) → `invested`(FILLED/SETTLED) → `failed`(CANCELLED) |
| `Status29` (`Models/Enums/Status29.cs`) | CONFIRMED · SETTLED · CANCELLED | top-up status (only if TopUps funding is used instead of VirtualCashIncrease) |
| `Currency` (`Models/Enums/Currency.cs`) | `Eur`=EUR · `Gbp`=GBP | order currency (EUR) |
| `Currency1` (`Models/Enums/Currency1.cs`) | EUR · GBP · USD | cash-increase currency (EUR) |
| `Type16` (`Models/Enums/Type16.cs`) | TRADING · **PORTFOLIO** | account type (PORTFOLIO) |
| `Side15` (`Models/Enums/Side15.cs`) | BUY · SELL | order side (BUY) |
| `InstrumentIdType4` (`Models/Enums/InstrumentIdType4.cs`) | `Isin`=ISIN · UPVEST (default Isin) | allocation instrument id type |
| `PriceQuality3` (`Models/Enums/PriceQuality3.cs`) | EOD · HIGHEST_AVAILABLE | valuation price quality |

Open-enum note: these are `OpenStringEnum<T>` (not C# enums) — use static members (`Status65.Filled`), compare with `==`, and keep an `otherwise` branch for unknown wire values (`MUST load dotnet-models`). `Weight`/`Amount`/`CashAmount`/`Quantity` are **decimal strings**, not numbers — format euros to the body regex and parse them back for the app's JSON-number responses.

### Client construction / auth / server node (source: `sdk-map.md` *Getting a client*, *Servers & auth*; `Signing/*`)

- Construct `new UpvestInvestmentApi.UpvestInvestmentApiClient(httpClient, options)` — the only constructor; one shared `HttpClient`; register as **singleton** (`MUST load dotnet-client-initialization`).
- `options.OauthClientCredentials = new OAuth2ClientCredentials { ClientId = Upvest:ClientId, ClientSecret = Upvest:ClientSecret }` (`UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials`). Token minted at `{BaseUrl}/auth/token`; one token per scope set (`MUST load dotnet-authentication`).
- **Signing is required on every call.** `options.Hooks = [ new UpvestInvestmentApi.Signing.UpvestSigningHook( UpvestInvestmentApi.Signing.UpvestHttpSignatureCredentials.FromFile(keyId: Upvest:SigningKeyId, privateKeyPath: Upvest:SigningKeyPath, upvestClientId: Upvest:ClientId, privateKeyPassphrase: Upvest:SigningKeyPassphrase)) ]`. Without a signing hook or the signing env vars, every call throws `InvalidOperationException` before send.
- `options.Environment = ServerEnvironment.Production` (= sandbox, `https://sandbox.upvest.co`). **BaseUrl verbatim:** set `options.Server.Default.Production.BaseUrl = Upvest:BaseUrl` so every call uses the configured base address. One server group (`Default`).

---

## 3. Trap notes

- **Client/HttpClient lifetime & DI singleton** (config/DI step) — getting ownership or lifetime wrong leaks sockets or rebuilds options per request. `MUST load dotnet-client-initialization`.
- **OAuth2 + signing wiring; multi-part credential** (auth step) — how the two schemes combine and what a missing/blank part costs before first call. `MUST load dotnet-authentication`.
- **Union (AnyOf) build + read** on `UserCreateRequest`, `AccountCreateRequest`, `UserCheckCreateRequest`, `AccountCreateResponse`, `UserCreateRequest1`, `UserCheck`, `TriggerPortfolioRebalancingRequest`, `InstrumentId` (all flows) — picking/reading the wrong arm silently serializes an empty/`Unknown` body. `MUST load dotnet-models`.
- **Open enums & decimal-string money fields** (Flows 2/3/5) — enums are not C# enums; amounts/weights/quantities are regex-constrained strings. `MUST load dotnet-models`.
- **Error boundary: Case A accessors + `ResponseDeserializationException`** (every call) — see REQUIRED READING row. `MUST load dotnet-error-handling`.
- **Total timeout budget / per-attempt vs whole-call; write-retry set; pagination; logging of PII bodies** (resilience + Flow 1) — what `Timeout` bounds, which verbs retry, how offset/limit paging terminates, and that `UPVEST_INVESTMENT_API_LOG=trace` forces body logging. `MUST load dotnet-configuration-resilience`.
- **Stubbing the SDK's `HttpClient` seam** (tests) — `MUST load dotnet-testing`.

---

## 4. REQUIRED READING (load every one before implementation starts)

- `upvest-v4:dotnet-client-initialization` — client construction + DI singleton (config/DI step).
- `upvest-v4:dotnet-authentication` — OAuth2 + signing hook, fail-fast (auth step).
- `upvest-v4:dotnet-models` — unions/open enums/decimal-string money (Flows 1–5 bodies).
- `upvest-v4:dotnet-calling-endpoints` — request-record call shape, requestOptions/cancellation (all calls).
- `upvest-v4:dotnet-configuration-resilience` — timeouts, retries, pagination, logging/PII (resilience).
- `upvest-v4:dotnet-error-handling` — **always** — error boundary.
- `upvest-v4:dotnet-testing` — SDK stub seam (tests).

> **Hazard row (verbatim):** a body that does not match its declared type — a drifted or malformed **2xx** response (a missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch ladder that handles only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | **Credential fail-fast** | Bind `Upvest:` via `IConfiguration`/`IOptions<UpvestSettings>`. At DI registration, throw (host refuses to start) if any of `ClientId`, `ClientSecret`, `SigningKeyId`, `SigningKeyPath`, `BaseUrl` is missing/blank; verify the `SigningKeyPath` file exists. `SigningKeyPassphrase` is optional (null when the PEM is unencrypted) — checked only if the key is encrypted. Do **not** rely on the SDK's own env vars (`UPVEST_API_*`) in production; supply every credential in code so a blank part is caught at startup, not as `AuthSchemeException`/`InvalidOperationException` on first call. **DECIDED:** settings class `UpvestSettings` (section `Upvest:`); guard in `AddUpvestPortfolio` via `AddOptions().Validate(...).ValidateOnStart()` with one check per credential (ClientId, ClientSecret, SigningKeyId, SigningKeyPath, BaseUrl) naming the config key and never the value; the signing-key **file-existence** check runs in the singleton client factory (fails on first construction, naming the key). `SigningKeyPassphrase` is optional (unencrypted PEM). |
| 2 | **Secret sourcing & rotation** | Secrets from configuration (user-secrets/env/keyvault via `IConfiguration`), never source/logs/responses. Options object is built **once** at singleton registration and captured — a rotated `ClientSecret`/passphrase takes effect only on process restart. No hot-rotation required for this feature. |
| 3 | **Total timeout budget** | Per-request budget enforced via `RequestOptions`/`RetryOptions.Timeout` + a `CancellationToken` threaded from the endpoint; the caller-visible budget is the whole-call bound, not a single attempt. **DECIDED:** `RetryOptions.Timeout = 15s` (per attempt) + named `HttpClient.Timeout = 30s` (per-attempt backstop) in `AddUpvestPortfolio`; the whole-call budget is the request's `CancellationToken` threaded from each endpoint through `IPortfolioService`/`IUpvestPortfolioGateway` to every SDK call. |
| 4 | **Write-retry ownership** | Default retry set (idempotent verbs) from `dotnet-configuration-resilience`. POST writes here each carry a **real** `IdempotencyKey`, so provider-side replay makes a resend safe. **DECIDED:** keep the SDK default `HttpMethodsToRetry` (POST **not** retried) — so each write reaches Upvest at most once per call on transport failures; the persisted idempotency keys + reconciliation (UNKNOWN OUTCOMES) settle the rest. Not added to the retry set. |
| 5 | **Idempotency & ambiguous writes** | Real key present on `CreateUser`, `CreateAccountGroup`, `CreateAccount`, `CreateUserCheck`? (CreateUserCheck has none — reconcile by `ListUserChecks`), `CreatePortfoliosAllocation`, `CreatePortfoliosConfiguration`, `UpdatePortfoliosConfiguration`, `CreateVirtualCashIncrease`, `CreatePortfoliosOrder`, `TriggerPortfolioRebalancing`. The app **generates the `IdempotencyKey` once, persists it on the claim row, and reuses the same value on every resend** so a replay returns the original result. `UpdatePortfoliosAllocation` has no key (last-write-wins, acceptable for operator model edits). |
| 6 | **Observability** | Log at Information: operation name + app ids (buyerId, paymentId, investorId) + resulting status; at Warning/Error: `ApiException` status + `RawError.ReadAsString()` (provider correlation id) via the error boundary. **Never** log request bodies or personal data. |
| 7 | **Sensitive data** | **Yes** — Flow 1 `CreateUser` carries names, birth date/city/country, nationality, address, phone, and `CreateUserCheck` carries document/tax data. Posture: `options.Logging.LogRequestBody` **off**; `options.Logging.LoggerFactory` set explicitly (never leave it unset — `UPVEST_INVESTMENT_API_LOG=trace` would force body logging); app code never echoes a request body on these paths. KYC personal data is sent to Upvest but **not persisted** beyond identifiers + status. |
| 8 | **Environment selection** | One server group `Default`. `ServerEnvironment.Production` (`https://sandbox.upvest.co`) for sandbox, `Environment2` (`https://api.upvest.co`) for live — but `options.Server.Default.Production.BaseUrl` is set to `Upvest:BaseUrl` verbatim. Each deployment sets `Upvest:BaseUrl`; sandbox is the SDK default, so test traffic cannot reach live unless `BaseUrl` is pointed there. |
| 9 | **Duplicate prevention under concurrency** | See DUPLICATE CLAIMS. The eShopOnWeb store is EF Core over the in-memory provider (`UseOnlyInMemoryDatabase`, per-host); a primary-key insert conflict is the refusal (enforced even in-memory). |
| 10 | **Partial results** | See PAGED READS. |
| 11 | **Unknown outcomes** | See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Sign up as investor (`CreateUser`+`CreateAccount`) | `PortfolioInvestor` row, PK = `BuyerId`, in `CatalogContext` | PK insert conflict on `BuyerId` (one investor per shopper) | `catch` on `AddAsync` conflict in `PortfolioService.SignUpInvestorAsync` → return existing record | `PortfolioService.SignUpInvestorAsync`: `_investors.AddAsync(investor)` (claim) → `ReconcileInvestorAsync` → `_upvest.CreateUserAsync`/`CreateAccountAsync`. The claim is caught by `catch (Exception) when (IsUniquenessConflict(ex))`. |
| Pay in (`CreateVirtualCashIncrease`+`CreatePortfoliosOrder`) | `PortfolioPayment` row, PK = app `PaymentId` generated **before** the SDK calls, with persisted idempotency keys | PK insert conflict on the pre-generated `PaymentId` | `PortfolioService.PayInAsync` (the `AddAsync` precedes the SDK calls) | `PortfolioService.PayInAsync`: `new PortfolioPayment(buyerId, amount)` + `_payments.AddAsync(payment)` (claim) → `_upvest.CreditCashAsync` → `_upvest.PlaceOrderAsync`. |
| Realignment (`TriggerPortfolioRebalancing`) | `PortfolioRealignment` row, PK = app `RealignmentId` generated before the SDK call, with persisted `IdempotencyKey` | PK insert conflict on the pre-generated `RealignmentId` | `PortfolioService.TriggerRealignmentAsync` (the `AddAsync` precedes the trigger) | `PortfolioService.TriggerRealignmentAsync`: `new PortfolioRealignment(modelId)` + `_realignments.AddAsync(realignment)` (claim) → `_upvest.TriggerRealignmentAsync`. |

(Order per row: claim write → SDK call → record result. `PUT /models/{modelId}` and `PUT /investor/model` are keyed upserts by `modelId`/`buyerId` — last-write-wins, no duplicate hazard.)

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `ListPositions` (Flow 5 holdings) | offset/limit; `Meta.TotalCount` vs returned `Count`; `MaxPages` (100) cap | Full read — `GetPortfolioAsync` loops until `offset+Meta.Count >= Meta.TotalCount`; all holdings returned to the caller, no truncation | `UpvestPortfolioGateway.GetPortfolioAsync` — the positions `for` loop guarded by `MaxPages`, breaks on `offset + positions.Meta.Count >= positions.Meta.TotalCount` |
| `ListPortfoliosOrders` (Flow 3 payments) | n/a — not used | Payments are read from the app's `PortfolioPayment` store (`PortfolioPaymentsByBuyerSpecification`), not from Upvest; status comes from re-reading each order by id | `PortfolioService.GetPaymentsAsync` (no Upvest list call) |
| `ListPortfoliosAllocations` (Flow 2 models) | n/a — not used | Models are served from the app's `ModelPortfolio` store | `PortfolioService.GetModelsAsync` (no Upvest list call) |
| `ListPortfolioRebalancingExecutionOrders` (status derivation) | offset/limit; `Meta.TotalCount`; `MaxPages` (100) cap | Full read — `GetRealignmentOutcomeAsync` pages every order so the derived status sees all of them; the derived status is what the caller reads, never a log | `UpvestPortfolioGateway.GetRealignmentOutcomeAsync` — the orders `for` loop guarded by `MaxPages`, breaks on `offset + meta.Count >= meta.TotalCount` |

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `CreateUser` | resend `CreateUser` with the same persisted `CreateUserIdempotencyKey` (replay returns the same user); `Users.RetrieveUser` once `UpvestUserId` is stored | persisted `PortfolioInvestor.CreateUserIdempotencyKey` / stored `UpvestUserId` | `PortfolioService.ReconcileInvestorAsync` `catch (UpvestGatewayException)` leaves the investor `pending`; the row is resumed on the next `POST`/`GET` because each step is gated on its id being null | `PortfolioService.ReconcileInvestorAsync` (`CreateUserAsync` guarded by `investor.UpvestUserId is null`; translated `UpvestGatewayException.OutcomeUnknown` marks transport failures) |
| `CreateAccount` | resume via the same `CreateAccountIdempotencyKey` (gated on `UpvestAccountId is null`); re-read with `GetAcceptanceAsync` | persisted `CreateAccountIdempotencyKey` / stored `UpvestAccountId` | `PortfolioService.ReconcileInvestorAsync` — `CreateAccountAsync` gated on `UpvestAccountId is null`; `GET /investor` re-runs reconcile and settles status | `PortfolioService.ReconcileInvestorAsync` |
| `CreateVirtualCashIncrease` + `CreatePortfoliosOrder` | `Portfolios.RetrievePortfoliosOrder` by stored `UpvestOrderId`; resend with the same persisted keys | persisted `CashCreditIdempotencyKey`/`PlaceOrderIdempotencyKey`; stored `UpvestOrderId` | `PortfolioService.PayInAsync` `catch` leaves payment `pending`; `GetPaymentsAsync` re-reads the order and settles it | `PortfolioService.PayInAsync` (`CreditCashAsync` gated on `!payment.CashCredited`) + `PortfolioService.GetPaymentsAsync` (reconcile loop over `UpvestOrderId`) |
| `TriggerPortfolioRebalancing` | `GetRealignmentOutcomeAsync` by stored `UpvestExecutionId`; resend with the same persisted `TriggerIdempotencyKey` | persisted `TriggerIdempotencyKey`; stored `UpvestExecutionId` | `PortfolioService.TriggerRealignmentAsync` `catch` leaves it `pending`; `GetRealignmentAsync` re-reads and settles | `PortfolioService.TriggerRealignmentAsync` + `PortfolioService.GetRealignmentAsync` |

---

## 6. Assumptions & Blockers

**Assumptions**

- **A1 — Account group provisioning.** `CreateAccount` requires an `account_group_id`. **DECIDED:** a **personal account group per investor** (`Type13.Personal`) is created during sign-up via `CreateAccountGroupAsync` and its id persisted on `PortfolioInvestor.UpvestAccountGroupId`; cash funding keys on it. Implemented in `UpvestPortfolioGateway.CreateAccountGroupAsync` + `PortfolioService.ReconcileInvestorAsync`. (List-and-reuse was dropped as unnecessary — the app owns the id, created once and persisted.)
- **A2 — Paying money in (sandbox).** **DECIDED:** credit via `VirtualCashBalances.CreateVirtualCashIncrease` (account-group virtual cash, `Currency1.Eur`), then invest via `Portfolios.CreatePortfoliosOrder` (`Side15.Buy`, `Currency.Eur`, `cash_amount`). Implemented in `UpvestPortfolioGateway.CreditCashAsync` + `PlaceOrderAsync`, orchestrated in `PortfolioService.PayInAsync`.
- **A3 — modelId mapping.** The task's `{modelId}` path value is a client-chosen id mapped to an Upvest `allocation_id` in the app's `ModelPortfolio` store. Investors follow a model via `PortfoliosConfiguration` (account→allocation). `GET /models` serves the app store (populated from `CreatePortfoliosAllocation`).
- **A4 — Sign-up status source.** `active` = KYC `Status6.PASSED` (via `ListUserChecks`) **and** `Account.Status21.Active`; `rejected` = KYC `FAILED` / user offboarded; otherwise `pending`. Derived by re-read on `GET /investor` (no webhook route exists in this app, per the task).
- **A5 — User onboarding model.** **DECIDED:** `CreateUser` uses the **TOL** arm (`UserCreateRequest.UserTolCreateRequest`) — see `UpvestPortfolioGateway.CreateUserAsync`. Field placement: `firstName/lastName/email/birthDate/birthCity/birthCountry/nationality/address/phoneNumber` map to `UserTolCreateRequest`; `nationality`/country codes are resolved against the SDK's `OpenStringEnum` ISO tables via `TypedEnum.TryGetKnownValue` (a bad code → 400). `Fatca` (required) is sent with `Status=false, ConfirmedAt=now`. The `identityDocument` maps to the **KYC check** (`UserCheckKnowYourCustomerCreateRequest` in `CreateKycCheckAsync`): `type`→`DocumentType3`, `expiryDate`→`DocumentExpirationDate`, `nationality`→`Nationality`; `Method=ElectronicId`, `Provider="eShopOnWeb"`. **The KYC body has no field for the document `number` or `issuingCountry`** (Upvest's KYC-confirmation model records that an external check passed, not the raw document fields), so those two are accepted and validated by the app but not forwarded — this is an SDK shape fact, not a gap. `taxId`/`taxCountry` are validated and accepted; Upvest's tax-residency/FATCA sub-resources are out of the core five flows, so they are not forwarded in this build.
- **A6 — Persistence.** New EF aggregates in `CatalogContext` with `IEntityTypeConfiguration`: `PortfolioInvestor` (PK `BuyerId`), `ModelPortfolio` (PK `ModelId`), `PortfolioPayment` (PK `PaymentId`), `PortfolioRealignment` (PK `RealignmentId`). In-memory provider, per-host store, keyed by `buyerId` for isolation.

**Blockers**

- **B1 — Realignment status has no provider status field.** `PortfoliosRebalancingExecution` (the `GET …/realignments/{id}` re-read target) carries only `id`, `created_at`, `updated_at`, `accounts?`, `allocations?` — **no status**. So `completed`/`failed` cannot be read from the execution directly; it must be **derived** by paging `ListPortfolioRebalancingExecutionOrders` and aggregating the per-order `Status65` values (e.g. all `FILLED`/`SETTLED` → `completed`; any `CANCELLED` → `failed`; otherwise `pending`). This is a design workaround, not a true blocker. **DECIDED aggregation** (`UpvestPortfolioGateway.DeriveRealignmentOutcome`, over the execution orders' `Status71`): empty → `pending`; any `Cancelled` → `failed` (`completed`? no); all `Filled` → `completed`; otherwise (`New`/`Processing` present) → `pending`. Unknown/undeclared statuses fall through to `pending`, so an unrecognised value is never reported as a definite outcome. Still `UNVERIFIED` against live order statuses (sandbox refuses the read for scope), hence the defensive default.

No planning-stopping blockers. **Every SDK call in this plan requires an admin/read scope the sandbox's user-only client lacks, so each is expected to return 401 "not authorised to use these scopes."** Per the task this is the environment, not a blocker — the flows are planned in full and verified as far as the sandbox allows.

---

## 7. Source labels

Every per-operation row cites its map page and the SDK source file(s) read this session (all under `sdk/dotnet/`). `UNVERIFIED` appears only on B1's aggregation rule. `YOUR CALL — not in the map` markers: A1 (account-group strategy), A2 (funding primitive), A5 (tax/document field placement), B1 (aggregation thresholds), and PR rows 1/3/4 as noted — all settled above in place with the decision + implementing member.

---

## 8. Verification results (against the live sandbox)

Built, host starts, all 10 endpoints behave as specified; 15/15 existing PublicApi integration tests still green, plus new `PortfolioServiceTests` unit tests.

**What the sandbox confirmed for real (the client is authorised for user operations only):**

- **`POST /users` → 200** with an EEA nationality (e.g. `DE`): a real Upvest user is created end-to-end — so `CreateUserAsync` (TOL body, FATCA, ISO-code resolution, signing + OAuth) is wire-verified.
- **`POST /users` → 400** `"user must have at least one accepted EEA nationality"` with a non-EEA nationality (e.g. `GB`): this is Upvest refusing the shopper's details, which the app maps to sign-up **`rejected`** (a definite client-data rejection at the user step → `PortfolioService.ReconcileInvestorAsync`).
- **`POST /account_groups`, `/instruments`, `/portfolios/allocations`, etc. → 401** `"not authorised to use these scopes"`: the documented environment wall (the sandbox client lacks these scopes). The app catches each as `UpvestGatewayException`, keeps application state, and leaves the sign-up/payment/realignment **`pending`** (never a false failure). This is the environment, not a code gap.

So `pending` and `rejected` are demonstrable against the live sandbox today; `active` requires account/KYC scopes the sandbox client is not granted, and the full invest/realign/holdings paths are built and wait behind the same 401 wall. No personal data appears in logs (verified by grep over the running host's output; `LogRequestBody` off, `LoggerFactory` explicit).
