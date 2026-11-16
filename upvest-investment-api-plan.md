# Upvest Investment API — Integration Plan (eShopOnWeb managed portfolios)

Plan-first artifact for `dotnet-integrate-upvest-investment-api`. Every contract fact below is grounded in the SDK map
(`sdk-map.md` + `map/operations/*`) and the source files those pages name, inside the plugin's SDK source at
`sdk/dotnet/` (absolute: `.../marketplace/plugins/upvest-v4/sdk/dotnet/`). Operation sequencing is cross-checked against
`upvest-workflows/{user-onboarding,investment-solutions,payments}`. Paths in **source** cells are SDK-root-relative
(prefix `sdk/dotnet/`).

Additive capability only: new endpoints under `/api/portfolio` on `src/PublicApi`. The existing catalog/basket/order flow
is untouched. No project code has been written — this is the planner's output.

---

## 1. Scope & sequence

Five HTTP flows on `src/PublicApi`, all JWT-authenticated, caller identity from the token. Per-shopper key = the JWT
identity (`HttpContext.User.Identity.Name`, as eShopOnWeb keys a buyer; confirm against the auth exemplar —
`YOUR CALL — not in the map`). A new application-owned store (see §5 PRODUCTION READINESS / Assumptions) holds, per
buyerId: the Upvest `userId`, `accountGroupId`, `accountId`, the chosen `allocationId` (modelId), and a list of payments
(our paymentId ↔ Upvest portfolio-order id, amount, stored `IdempotencyKey`). Operator-defined models live as Upvest
portfolio **allocations**; the application also keeps the operator's model catalog so "every model offered" is knowable.

**Upvest domain mapping (grounded):**
- **Model portfolio** = Upvest *portfolio allocation* (`client.Portfolios` Allocation ops). `allocation` = list of `{instrument_id, instrument_id_type, weight}`, weights sum to 100.
- **Which model an investor follows** = Upvest *portfolio configuration* (links `account_id` → `allocation_id`).
- **Paying in / investing** = Upvest *portfolio order* (`side=BUY`, `cash_amount`), distributed across the allocation's instruments by weight. Requires available cash in the account group first (sandbox credit, below).
- **Realignment** = Upvest *portfolio rebalancing execution* (`client.PortfoliosRebalancing.TriggerPortfolioRebalancing`, Allocations arm = rebalance every account following that model).
- **Holdings / valuation** = `client.Valuations.GetAccountValuation` (per-instrument quantity + value + total) and/or `client.Positions.ListPositions`.
- **Becoming an investor** = full TOL onboarding chain: create user (KYC data) → KYC check → tax residencies → account group (PERSONAL) → account (PORTFOLIO).

### Step order

**Step 0 — Client & DI.** Register `UpvestInvestmentApiClient` (singleton) via `services.AddUpvestInvestmentApiClient`,
options bound from the `Upvest:` config section; OAuth2 client credentials + `UpvestSigningHook`. (`dotnet-client-initialization`, `dotnet-authentication`, `dotnet-configuration-resilience`.)

**Step 1 — Flow 1 "Becoming an investor"** (`POST`/`GET /api/portfolio/investor`, any signed-in shopper):
onboarding chain, in order — `Users.CreateUser` (TOL body) → `UserChecks.CreateUserCheck` (KYC, identity document) →
`TaxResidencies.SetTaxResidencies` → `AccountGroups.CreateAccountGroup` (PERSONAL) → `AccountsApi.CreateAccount`
(PORTFOLIO). Status learned by read-back: `Users.RetrieveUser` + `AccountsApi.RetrieveAccount` (+ `UserChecks.ListUserChecks`/`RetrieveUserCheck`).

**Step 2 — Flow 2 "Model portfolios"** (operator: `PUT /api/portfolio/models/{modelId}`; read: `GET /api/portfolio/models`;
investor: `PUT /api/portfolio/investor/model`):
- Create/replace model → `Portfolios.CreatePortfoliosAllocation` (first time) / `Portfolios.UpdatePortfoliosAllocation` (replace). Validate ISINs via `Instruments.ListInstruments`.
- List models → `Portfolios.ListPortfoliosAllocations` (paged).
- Investor chooses model → `Portfolios.CreatePortfoliosConfiguration` (first) / `Portfolios.UpdatePortfoliosConfiguration` (switch).

**Step 3 — Flow 3 "Paying in"** (`POST`/`GET /api/portfolio/payments`, investor):
- Credit cash into the account group (sandbox): `VirtualBankAccounts.CreateVirtualBankAccount` (once per account group) + `Tests.CreateBankTransaction` (simulate SEPA credit). [`YOUR CALL — not in the map`: crediting mechanism; alternative is `TopUps.CreateTopup`. Both need non-user scopes the sandbox client lacks — see Assumptions.]
- Invest → `Portfolios.CreatePortfoliosOrder` (`side=BUY`, `cash_amount`, `currency=EUR`). paymentId = order id; status from order.
- List payments → `Portfolios.ListPortfoliosOrders` (paged, filter by account_id), plus our own per-shopper list. Status read-back → `Portfolios.RetrievePortfoliosOrder`.

**Step 4 — Flow 4 "Realignment"** (operator: `POST /api/portfolio/models/{modelId}/realignments`; read: `GET /api/portfolio/realignments/{realignmentId}`):
- Trigger → `PortfoliosRebalancing.TriggerPortfolioRebalancing` (Allocations arm = `[modelId]`). realignmentId = response `id`.
- Status read-back → `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` **plus** `ListPortfolioRebalancingExecutionOrders` (the execution object carries no status — status is derived from its orders; see Trap notes).

**Step 5 — Flow 5 "The investor's portfolio"** (`GET /api/portfolio`, investor):
- `Valuations.GetAccountValuation` → total value + per-instrument quantity & value. `paidIn` = sum of our recorded payments (`YOUR CALL — not in the map`; Upvest exposes no single "total paid in"). modelId = stored allocationId / `Portfolios.RetrievePortfoliosConfiguration`.

A capability the map lacks is a Blocker (§6), not an invented path.

---

## 2. CONTRACT SHEET

> ⚠ **Signatures are generated code — reproduce them verbatim.** Every operation that takes input takes **one request
> record as its first parameter**, built with an object initializer using the record's own property names (never flat
> positional arguments). An operation with no inputs takes none. The second/third params are always
> `RequestOptions? requestOptions = null, CancellationToken cancellationToken = default`.

> ⚠ **Write every SDK type fully-qualified with the namespace its source path implies** (take the namespace from the
> path the map gives for *that* type, never from a neighbour): `UpvestInvestmentApi` (client/options),
> `UpvestInvestmentApi.Api` (controllers), `UpvestInvestmentApi.Requests.<Controller>` (request records),
> `UpvestInvestmentApi.Models` (records), `UpvestInvestmentApi.Models.AnyOf` (unions),
> `UpvestInvestmentApi.Models.Enums` (enums), `UpvestInvestmentApi.Errors` (typed errors),
> `UpvestInvestmentApi.Core.Exceptions` / `.Core.ErrorResponse` (exceptions/raw error),
> `UpvestInvestmentApi.Servers` (`ServerEnvironment`).

**Every operation in scope is throw-only (no `…Result` sibling) and Case A typed error** (`ApiException<{Operation}Error>`),
whose accessors are uniformly `TryGetNoContent([NotNullWhen(true)] out RawError? value)` for the declared statuses +
`TryGetRawError([NotNullWhen(true)] out RawError? error)` fallback. So the typed "NoContent" payload is a `RawError`, not
a structured body — read status/body through `RawError` (`dotnet-error-handling`). The per-operation status lists differ
and are on each map page; the catch shape does not.

### 2a. Per-operation rows

Legend: req record members as `Name: type (required?)`; body fields as `Name (wire): type (required?)`. `IdempotencyKey:
Guid (required)` and `UpvestApiVersion: UpvestApiVersion (= _1 default)` are on nearly every create record — listed once
here and assumed thereafter: **every Create/Set/Trigger record below carries a required `IdempotencyKey` and an optional
`UpvestApiVersion` (defaults `_1`)** unless stated.

#### Flow 1 — onboarding

| Op | Controller · signature | Request record + members | Body model + fields | Response → fields read | Error | Pag. | Source |
|---|---|---|---|---|---|---|---|
| Create user | `client.Users.CreateUser(CreateUserRequest request, …)` | `CreateUserRequest { IdempotencyKey, UpvestApiVersion, Body: UserCreateRequest? }` | `UserCreateRequest` = AnyOf → use `UserTolCreateRequest`: `FirstName(first_name):string(req)`, `LastName(last_name):string(req)`, `Email(email):string(req)`, `BirthDate(birth_date):DateTimeOffset(req, date-only)`, `Nationalities(nationalities):IReadOnlyList<Nationality>(req,≥1)`, `Address(address):Address2(req)`, `Fatca(fatca):Fatca(req)`; optional but mapped from sign-up form: `BirthCity(birth_city)`, `BirthCountry(birth_country)`, `PhoneNumber(phone_number)`, `TermsAndConditions(terms_and_conditions)`, `DataPrivacyAndSharingAgreement(data_privacy_and_sharing_agreement)` | `UserCreateRequest1` (AnyOf → `UserTol`): read `Id` (userId), `Status` (`UpvestInvestmentApi.Models.Enums.Status`: Active/Inactive/Offboarding/Offboarded) | `ApiException<CreateUserError>` A | — | `map/operations/Users.md`; `Models/UserTolCreateRequest.cs`; `Models/AnyOf/UserCreateRequest1.cs`; `Models/UserTol.cs`; `Requests/Users/CreateUserRequest.cs` |
| Create KYC check | `client.UserChecks.CreateUserCheck(CreateUserCheckRequest request, …)` | `CreateUserCheckRequest { UserId: Guid(req), UpvestApiVersion, Body: UserCheckCreateRequest? }` — **no IdempotencyKey** | `UserCheckCreateRequest` = AnyOf → `UserCheckKnowYourCustomerCreateRequest`: `Type(type):string(="KYC")`, `CheckConfirmedAt(check_confirmed_at):DateTimeOffset(req)`, `DataDownloadLink(data_download_link):string(req)`, `DocumentType(document_type):DocumentType3(req)` (PASSPORT/ID_CARD/…), `Provider(provider):string(req)`, `Method(method):Method(req)`; identity doc detail: `DocumentExpirationDate(document_expiration_date)`, `Nationality(nationality:ISO-alpha2 regex)`, `ConfirmedAddress(confirmed_address):Address2?` — all optional in shape but doc-required for PASSPORT/ID_CARD/RESIDENCE_PERMIT/DRIVING_LICENSE | `UserCheckCreateResponse`: read check id + status (confirm shape) | `ApiException<CreateUserCheckError>` A | — | `map/operations/UserChecks.md`; `Models/AnyOf/UserCheckCreateRequest.cs`; `Models/UserCheckKnowYourCustomerCreateRequest.cs`; `Models/UserCheckCreateResponse.cs` |
| Set tax residencies | `client.TaxResidencies.SetTaxResidencies(SetTaxResidenciesRequest request, …)` | `SetTaxResidenciesRequest { UserId: Guid(req), IdempotencyKey, UpvestApiVersion, Body: TaxResidenciesSetRequest? }` | `TaxResidenciesSetRequest` (taxId + taxCountry → confirm field names in file) | `TaxResidencyRecord` | `ApiException<SetTaxResidenciesError>` A | — | `map/operations/TaxResidencies.md`; `Models/TaxResidenciesSetRequest.cs` |
| Create account group | `client.AccountGroups.CreateAccountGroup(CreateAccountGroupRequest request, …)` | `CreateAccountGroupRequest { IdempotencyKey, UpvestApiVersion, Body: AccountGroupCreateRequest? }` | `AccountGroupCreateRequest` AnyOf → `AccountGroupCreateUserRequest`: `UserId(user_id):Guid(req)`, `Type(type):Type13(req)` → **`Type13.Personal`** | `AccountGroupCreateResponse` (AnyOf) → read `id` (accountGroupId) from matched arm | `ApiException<CreateAccountGroupError>` A | — | `map/operations/AccountGroups.md`; `Models/AnyOf/AccountGroupCreateRequest.cs`; `Models/AccountGroupCreateUserRequest.cs`; `Models/Enums/Type13.cs`; `Models/AnyOf/AccountGroupCreateResponse.cs` |
| Create account | `client.AccountsApi.CreateAccount(CreateAccountRequest request, …)` | `CreateAccountRequest { IdempotencyKey, UpvestApiVersion, Body: AccountCreateRequest? }` | `AccountCreateRequest` AnyOf → `AccountCreateUserRequest`: `UserId(user_id):Guid(req)`, `AccountGroupId(account_group_id):Guid(req)`, `Type(type):Type16(req)` → **`Type16.Portfolio`** (portfolio functionality) | `AccountCreateResponse` (AnyOf) → read `id` (accountId) from matched arm | `ApiException<CreateAccountError>` A | — | `map/operations/AccountsApi.md`; `Models/AnyOf/AccountCreateRequest.cs`; `Models/AccountCreateUserRequest.cs`; `Models/Enums/Type16.cs`; `Models/AnyOf/AccountCreateResponse.cs` |
| Read user (status) | `client.Users.RetrieveUser(RetrieveUserRequest request, …)` | `RetrieveUserRequest { UserId: Guid(req), UpvestApiVersion }` | — | `UserGetResponse` (AnyOf → `UserTol`): `Status` | `ApiException<RetrieveUserError>` A | — | `map/operations/Users.md`; `Models/AnyOf/UserGetResponse.cs`; `Models/UserTol.cs` |
| Read account (status) | `client.AccountsApi.RetrieveAccount(RetrieveAccountRequest request, …)` | `RetrieveAccountRequest { AccountId: Guid(req), UpvestApiVersion }` | — | `AccountRetrieveResponse` (AnyOf): read status (confirm arm/enum) | `ApiException<RetrieveAccountError>` A | — | `map/operations/AccountsApi.md`; `Models/AnyOf/AccountRetrieveResponse.cs` |

#### Flow 2 — models

| Op | Controller · signature | Request record + members | Body model + fields | Response → fields read | Error | Pag. | Source |
|---|---|---|---|---|---|---|---|
| Create model | `client.Portfolios.CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest request, …)` | `{ IdempotencyKey, UpvestApiVersion, Body: PortfoliosAllocationCreateRequest? }` | `PortfoliosAllocationCreateRequest`: `Name(name):string?`, `Allocation(allocation):IReadOnlyList<Allocation>(req)`, `RebalancingStrategyIds(rebalancing_strategy_ids):IReadOnlyList<Guid>?`. `Allocation`: `InstrumentId(instrument_id):InstrumentId(req)` (ISIN string via implicit `string`→`InstrumentId`, regex `^[A-Z]{2}[A-Z0-9]{9}[0-9]$`), `InstrumentIdType(instrument_id_type):InstrumentIdType4(=Isin)`, `Weight(weight):string(req)` (regex `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`; must sum to 100) | `PortfoliosAllocation`: `Id`, `Name`, `Allocation` | `ApiException<CreatePortfoliosAllocationError>` A | — | `map/operations/Portfolios.md`; `Models/PortfoliosAllocationCreateRequest.cs`; `Models/Allocation.cs`; `Models/AnyOf/InstrumentId.cs`; `Models/PortfoliosAllocation.cs` |
| Replace model | `client.Portfolios.UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest request, …)` | `{ AllocationId: Guid(req), UpvestApiVersion, Body: PortfoliosAllocationUpdateRequest? }` — **no IdempotencyKey** | `PortfoliosAllocationUpdateRequest` (full replace: send complete allocation array; confirm fields in file) | `PortfoliosAllocation` | `ApiException<UpdatePortfoliosAllocationError>` A | — | `map/operations/Portfolios.md`; `Models/PortfoliosAllocationUpdateRequest.cs` |
| List models | `client.Portfolios.ListPortfoliosAllocations(ListPortfoliosAllocationsRequest request, …)` | `ListPortfoliosAllocationsRequest { UpvestApiVersion, Sort:Sort24?, Order:Order60?, Offset:?, Limit:?, StartDate?, EndDate?, InstrumentIds? }` | — | `PortfoliosAllocationsListResponse`: `Data: IReadOnlyList<PortfoliosAllocation>` + `Meta` (offset/limit/count/total_count) | `ApiException<ListPortfoliosAllocationsError>` A | **offset/limit** | `map/operations/Portfolios.md`; `Models/PortfoliosAllocationsListResponse.cs` |
| Investor picks model (first) | `client.Portfolios.CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest request, …)` | `{ IdempotencyKey, UpvestApiVersion, Body: PortfoliosConfigurationCreateRequest? }` | `PortfoliosConfigurationCreateRequest`: `AccountId(account_id):Guid(req)`, `AllocationId(allocation_id):Guid(req)`, `RebalancingStrategyIds?` | `PortfoliosConfiguration`: `AccountId`, `AllocationId` | `ApiException<CreatePortfoliosConfigurationError>` A | — | `map/operations/Portfolios.md`; `Models/PortfoliosConfigurationCreateRequest.cs`; `Models/PortfoliosConfiguration.cs` |
| Investor switches model | `client.Portfolios.UpdatePortfoliosConfiguration(UpdatePortfoliosConfigurationRequest request, …)` | `{ AccountId: Guid(req), IdempotencyKey: Guid(req), UpvestApiVersion, Body: PortfoliosConfigurationUpdateRequest? }` — **IdempotencyKey IS required** (verified in source; corrects the earlier note) | `PortfoliosConfigurationUpdateRequest`: `AllocationId(allocation_id):Guid?`, `RebalancingStrategyIds?` | `PortfoliosConfiguration` | `ApiException<UpdatePortfoliosConfigurationError>` A | — | `map/operations/Portfolios.md`; `Models/PortfoliosConfigurationUpdateRequest.cs` |
| Read config (current model) | `client.Portfolios.RetrievePortfoliosConfiguration(RetrievePortfoliosConfigurationRequest request, …)` | `{ AccountId: Guid(req), UpvestApiVersion }` | — | `PortfoliosConfiguration`: `AllocationId` | `ApiException<RetrievePortfoliosConfigurationError>` A | — | `map/operations/Portfolios.md`; `Models/PortfoliosConfiguration.cs` |
| Validate ISIN set | `client.Instruments.ListInstruments(ListInstrumentsRequest request, …)` | `ListInstrumentsRequest { UpvestApiVersion, TradingStatus?, Sort?, Order?, Offset?, Limit? }` | — | `InstrumentsListResponse` (data of instruments) | `ApiException<ListInstrumentsError>` A | **offset/limit** | `map/operations/Instruments.md`; `Models/InstrumentsListResponse.cs` |

#### Flow 3 — paying in

| Op | Controller · signature | Request record + members | Body model + fields | Response → fields read | Error | Pag. | Source |
|---|---|---|---|---|---|---|---|
| Create vIBAN (credit, step 1) | `client.VirtualBankAccounts.CreateVirtualBankAccount(CreateVirtualBankAccountRequest request, …)` | `{ IdempotencyKey, UpvestApiVersion, Body: PaymentsVirtualBankAccountCreateRequest? }` | `PaymentsVirtualBankAccountCreateRequest` (account_group_id — confirm fields) | `PaymentsVirtualBankAccountsResponse`: `id`, vIBAN | `ApiException<CreateVirtualBankAccountError>` A | — | `map/operations/VirtualBankAccounts.md`; `Models/PaymentsVirtualBankAccountCreateRequest.cs` |
| Simulate SEPA credit (credit, step 2, sandbox) | `client.Tests.CreateBankTransaction(CreateBankTransactionRequest request, …)` | `{ IdempotencyKey, UpvestApiVersion, Body: PaymentsBankTransactionCreateRequest? }` | `PaymentsBankTransactionCreateRequest` (account.iban fixed pooling IBAN, virtual_bank_account_id, counterparty_name = account owner, amount, currency — confirm fields) | `PaymentsBankTransactionCreateResponse` | `ApiException<CreateBankTransactionError>` A | — | `map/operations/Tests.md`; `Models/PaymentsBankTransactionCreateRequest.cs` |
| Invest (pay-in) | `client.Portfolios.CreatePortfoliosOrder(CreatePortfoliosOrderRequest request, …)` | `{ IdempotencyKey, UpvestApiVersion, Body: PortfoliosOrderPlaceRequest? }` | `PortfoliosOrderPlaceRequest`: `UserId(user_id):Guid(req)`, `AccountId(account_id):Guid(req)`, `CashAmount(cash_amount):string(req)` (decimal-string, 2dp), `Currency(currency):Currency(req)` → `Currency.Eur`, `Side(side):Side15(req)` → `Side15.Buy`, `PostTax(post_tax):bool?(=false)` | `PortfoliosOrder`: `Id` (paymentId), `Status` (`Status65`), `CashAmount`, `Currency` | `ApiException<CreatePortfoliosOrderError>` A | — | `map/operations/Portfolios.md`; `Models/PortfoliosOrderPlaceRequest.cs`; `Models/PortfoliosOrder.cs`; `Models/Enums/{Currency,Side15,Status65}.cs` |
| List payments | `client.Portfolios.ListPortfoliosOrders(ListPortfoliosOrdersRequest request, …)` | `ListPortfoliosOrdersRequest { UpvestApiVersion, Sort?, Order?, Offset?, Limit?, StartDate?, EndDate?, AllocationId?, AccountId? }` | — | `PortfoliosOrdersListResponse`: `Data: IReadOnlyList<PortfoliosOrder>` + `Meta` | `ApiException<ListPortfoliosOrdersError>` A | **offset/limit** | `map/operations/Portfolios.md`; `Models/PortfoliosOrdersListResponse.cs` |
| Read payment (status) | `client.Portfolios.RetrievePortfoliosOrder(RetrievePortfoliosOrderRequest request, …)` | `{ PortfolioOrderId: Guid(req), UpvestApiVersion }` | — | `PortfoliosOrder`: `Status` | `ApiException<RetrievePortfoliosOrderError>` A | — | `map/operations/Portfolios.md`; `Models/PortfoliosOrder.cs` |

#### Flow 4 — realignment

| Op | Controller · signature | Request record + members | Body model + fields | Response → fields read | Error | Pag. | Source |
|---|---|---|---|---|---|---|---|
| Trigger realignment | `client.PortfoliosRebalancing.TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest request, …)` | `{ IdempotencyKey, UpvestApiVersion, Body: TriggerPortfolioRebalancingRequest? }` | `TriggerPortfolioRebalancingRequest` = AnyOf **Accounts \| Allocations** — use `TriggerPortfolioRebalancingRequest.Allocations(new Allocations { AllocationsValue = [modelId] })`. **Accounts OR Allocations, never both.** | `TriggerPortfolioRebalancingResponse`: `Id` (realignmentId) | `ApiException<TriggerPortfolioRebalancingError>` A | — | `map/operations/PortfoliosRebalancing.md`; `Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`; `Models/Allocations.cs`; `Models/TriggerPortfolioRebalancingResponse.cs` |
| Read realignment execution | `client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(RetrievePortfoliosRebalancingExecutionRequest request, …)` | `{ ExecutionId: Guid(req), UpvestApiVersion }` | — | `PortfoliosRebalancingExecution`: `Id`, `Accounts`, `Allocations` — **no `status` field** (see Trap) | `ApiException<RetrievePortfoliosRebalancingExecutionError>` A | — | `map/operations/PortfoliosRebalancing.md`; `Models/PortfoliosRebalancingExecution.cs` |
| Read realignment orders (status source) | `client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(ListPortfolioRebalancingExecutionOrdersRequest request, …)` | `{ ExecutionId: Guid(req), UpvestApiVersion, Sort?, Order?, Offset?, Limit?, StartDate?, EndDate? }` | — | `PortfoliosRebalancingExecutionOrderListResponse`: `Data: IReadOnlyList<PortfoliosRebalancingExecutionOrder>` (each `Status: Status71` NEW/PROCESSING/FILLED/CANCELLED) + `Meta` | `ApiException<ListPortfolioRebalancingExecutionOrdersError>` A | **offset/limit** | `map/operations/PortfoliosRebalancing.md`; `Models/PortfoliosRebalancingExecutionOrderListResponse.cs`; `Models/PortfoliosRebalancingExecutionOrder.cs` |

#### Flow 5 — portfolio view

| Op | Controller · signature | Request record + members | Body | Response → fields read | Error | Pag. | Source |
|---|---|---|---|---|---|---|---|
| Account valuation | `client.Valuations.GetAccountValuation(GetAccountValuationRequest request, …)` | `GetAccountValuationRequest { AccountId: Guid(req), PriceQuality: PriceQuality3(req), UpvestApiVersion }` (query `price_quality`) | — | `AccountValuation`: `TotalSecurityValue` (`Amount` 2dp string + `Currency`), `SecurityPositions: IReadOnlyList<AccountValuationSecurityPosition>` (`Instrument`, `Quantity` string, `Value: Value?` nullable, `Weight`) | `ApiException<GetAccountValuationError>` A | — | `map/operations/Valuations.md`; `Models/AccountValuation.cs`; `Models/AccountValuationSecurityPosition.cs`; `Models/TotalSecurityValue.cs`; `Models/Enums/PriceQuality3.cs` |
| Positions (alt holdings) | `client.Positions.ListPositions(ListPositionsRequest request, …)` | `ListPositionsRequest { AccountId: Guid(req), UpvestApiVersion, Offset?, Limit? }` | — | `PositionsListResponse`: `Data: IReadOnlyList<Datum1>` + `Meta` | `ApiException<ListPositionsError>` A | **offset/limit** | `map/operations/Positions.md`; `Models/PositionsListResponse.cs`; `Models/Datum1.cs` |

### 2b. CROSS-OPERATION INVARIANTS

The map never states these; derived from the task.

| Invariant | Operations | Enforced where |
|---|---|---|
| The modelId an investor chooses must be one of the operator's offered models (an existing allocation) | `Portfolios.CreatePortfoliosConfiguration`/`UpdatePortfoliosConfiguration` ← `Portfolios.ListPortfoliosAllocations` (+ application's model catalog) | `PUT /api/portfolio/investor/model` handler: before the config write, verify modelId ∈ allocations returned by `ListPortfoliosAllocations`; reject with 400 if not. Add the read if absent. |
| The modelId to realign must be an existing allocation | `PortfoliosRebalancing.TriggerPortfolioRebalancing` ← `Portfolios.ListPortfoliosAllocations` | `POST /api/portfolio/models/{modelId}/realignments` handler: verify modelId ∈ allocations before trigger; reject if not. |
| Every instrumentId (ISIN) in a model's funds must be a tradable Upvest instrument | `Portfolios.CreatePortfoliosAllocation`/`UpdatePortfoliosAllocation` ← `Instruments.ListInstruments` | `PUT /api/portfolio/models/{modelId}` handler: validate each ISIN against `ListInstruments` (and weights sum to 100) before the allocation write; reject if not. |
| A pay-in is accepted only for an accepted investor with a model chosen | `Portfolios.CreatePortfoliosOrder` ← application store (investor `status=active` + stored `allocationId`) and `Portfolios.RetrievePortfoliosConfiguration` | `POST /api/portfolio/payments` handler: before credit+order, confirm the caller's investor record is `active` and has a configuration; reject with 409/400 otherwise. |
| The account/account-group an order or config targets must belong to the calling shopper | all account-scoped ops | Every handler resolves accountId/accountGroupId from the caller's own stored investor record (keyed by buyerId), never from the request body — enforces per-shopper isolation. |

### 2c. Enum tables (needed values; members are PascalCase of the wire value)

| Enum (source) | Members → wire |
|---|---|
| `Currency` (`Models/Enums/Currency.cs`) | `Eur`→`EUR`, `Gbp`→`GBP` |
| `Side15` (`Models/Enums/Side15.cs`) | `Buy`→`BUY`, `Sell`→`SELL` |
| `Status65` portfolio-order (`Models/Enums/Status65.cs`) | `New`→NEW, `Processing`→PROCESSING, `Filled`→FILLED, `Settled`→SETTLED, `Cancelled`→CANCELLED |
| `Status71` rebalancing-order (`Models/Enums/Status71.cs`) | NEW/PROCESSING/FILLED/CANCELLED (confirm member names in file) |
| `Status` user (`Models/Enums/Status.cs`) | `Active`→ACTIVE, `Inactive`→INACTIVE, `Offboarding`→OFFBOARDING, `Offboarded`→OFFBOARDED |
| `Status29` top-up (`Models/Enums/Status29.cs`) | CONFIRMED/SETTLED/CANCELLED (confirm) |
| `Type16` account (`Models/Enums/Type16.cs`) | `Trading`→TRADING, `Portfolio`→PORTFOLIO |
| `Type13` account-group (`Models/Enums/Type13.cs`) | `Personal`→PERSONAL (+ Child/FrenchPea/Isa/Joint/LegalEntity/PensionDeChild) |
| `InstrumentIdType4` (`Models/Enums/InstrumentIdType4.cs`) | `Isin` (default), `Upvest` (confirm) |
| `DocumentType3` (`Models/Enums/DocumentType3.cs`) | PASSPORT/ID_CARD/RESIDENCE_PERMIT/DRIVING_LICENSE/TWO_PLUS_TWO_VERIFICATION_PROOF (confirm member names) |
| `Method` KYC (`Models/Enums/Method.cs`) | VIDEO_ID/IN_PERSON_ID/ELECTRONIC_ID/… (confirm) |
| `PriceQuality3` valuation query (`Models/Enums/PriceQuality3.cs`) | EOD / HIGHEST_AVAILABLE (confirm) |
| `UpvestApiVersion` (`Models/Enums/UpvestApiVersion.cs`) | `_1` (default on every request record) |

**Status mapping to the task's FIXED response values** (derived — see Trap notes):
- Investor `status`: `pending` while user not ACTIVE or account not ACTIVE; `active` when user `Status.Active` **and** account ACTIVE; `rejected` when a KYC check failed or user `Offboarding`/`Offboarded`.
- Payment `status`: `pending` for order `New`/`Processing`; `invested` for `Filled`/`Settled`; `failed` for `Cancelled`.
- Realignment `status`: `pending` while any execution order `New`/`Processing`; `completed` when all `Filled`; `failed` when any `Cancelled` and none processing (operator surface — pick partial-handling at impl).

### 2d. Client / auth / server facts

- **Construction:** `new UpvestInvestmentApiClient(HttpClient httpClient, UpvestInvestmentApiClientOptions options)` (only ctor) or DI `services.AddUpvestInvestmentApiClient(opts => …)`. Source: `sdk-map.md` *Getting a client*; `UpvestInvestmentApiClient.cs`; `ServiceCollectionExtensions.cs`.
- **Two credential schemes, BOTH required on EVERY call (including the SDK's own token request):**
  1. **OAuth2 client credentials** → `options.OauthClientCredentials = new UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials { ClientId = …, ClientSecret = … }`. Token endpoint `https://sandbox.upvest.co/auth/token`; the SDK mints one token per operation's scope set automatically.
  2. **HTTP message signing** → `options.Hooks = [ new UpvestInvestmentApi.Signing.UpvestSigningHook( UpvestInvestmentApi.Signing.UpvestHttpSignatureCredentials.FromFile(keyId, privateKeyPath, upvestClientId, privateKeyPassphrase)) ]`. If no signing hook and no signing env vars are configured, **every call fails before being sent** with `InvalidOperationException`. Source: `sdk-map.md` *Request signing*; `Signing/UpvestSigningHook.cs`; `Signing/UpvestHttpSignatureCredentials.cs`.
- **Config binding (EXACT keys, hard-code nothing), `Upvest:` section → options:**

| Config key | Options target |
|---|---|
| `Upvest:ClientId` | `OAuth2ClientCredentials.ClientId` **and** `UpvestHttpSignatureCredentials` `upvestClientId` |
| `Upvest:ClientSecret` | `OAuth2ClientCredentials.ClientSecret` (secret) |
| `Upvest:SigningKeyId` | `UpvestHttpSignatureCredentials` `keyId` |
| `Upvest:SigningKeyPath` | `UpvestHttpSignatureCredentials` `privateKeyPath` |
| `Upvest:SigningKeyPassphrase` | `UpvestHttpSignatureCredentials` `privateKeyPassphrase` (secret) |
| `Upvest:BaseUrl` | `options.Server.Default.Production.BaseUrl` (the base address for every call) |

  Env vars `UPVEST_CLIENT_ID` etc. populate these config keys via the host's env-var configuration provider (`builder.Configuration.AddEnvironmentVariables()` with section mapping `Upvest__*`). Never read env vars directly for SDK values — bind the `Upvest:` keys.
- **Environments / server group (one group, `Default`):** `ServerEnvironment.Production` (default) = `https://sandbox.upvest.co` (the sandbox); `ServerEnvironment.Environment2` = `https://api.upvest.co` (live). Keep `Environment = Production` and override `options.Server.Default.Production.BaseUrl` from `Upvest:BaseUrl` so BaseUrl drives every call. Source: `sdk-map.md` *Servers & auth*; `Servers/ServerEnvironment.cs`.

---

## 3. Trap notes

- **Step 0 (auth):** Upvest needs BOTH OAuth2 **and** HTTP message signing on every request, including the token fetch; a missing/blank signing key or passphrase fails the call before it is sent (not a 401). Exactly what each credential is, which option property it sits on, and the startup fail-fast check — **`MUST load upvest-v4:dotnet-authentication`**.
- **Step 0 (client lifetime / HttpClient ownership):** who owns the `HttpClient`, singleton vs scoped registration, and that options are built once — **`MUST load upvest-v4:dotnet-client-initialization`**.
- **Step 0 (base URL / scopes):** overriding `Server.Default.Production.BaseUrl` vs switching `Environment`, and that scopes are requested per operation (you cannot pre-select them) so a scope the sandbox client lacks surfaces only at call time — **`MUST load upvest-v4:dotnet-configuration-resilience`**.
- **All steps (request shape):** each operation takes one request record built with an object initializer; the body model sits on its `Body` member and may be `null`-typed even when the endpoint requires it — **`MUST load upvest-v4:dotnet-calling-endpoints`**.
- **Flow 1/2/3 (unions & enums):** `UserCreateRequest`, `AccountCreateRequest`, `AccountGroupCreateRequest`, `UserCheckCreateRequest`, `UserGetResponse`, `TriggerPortfolioRebalancingRequest`, `InstrumentId` are AnyOf unions (build via static factory / implicit conversion; read via `TryGet…`/`Match`); open enums are not C# enums (static members, `TryGetKnownValue`/`Match`, `otherwise` for unknown wire values). Dates are `DateTimeOffset` with a date-only converter; money/weights/quantities are **decimal strings**, not numbers — **`MUST load upvest-v4:dotnet-models`**.
- **Flow 1 (status is derived, not a field):** there is no user status `pending`/`rejected`; new users are `Inactive` and activate asynchronously after checks pass, and account group/account start `PENDING_APPROVAL`. The task's `pending/active/rejected` must be computed from user + account (+ check) read-backs. Writing a single Upvest field into `status` is wrong — **`MUST load upvest-v4:dotnet-models`** (shape) and derive per §2c.
- **Flow 3 (credit vs invest are two calls):** a `BUY` portfolio order needs available cash first; `TopUps.CreateTopup` only records an expected funding and settles when a transfer arrives, so sandbox crediting uses vIBAN + `Tests.CreateBankTransaction`. The portfolio order is async (returns `New`); its outcome (`Filled`/`Cancelled`) is learned by read-back — **`MUST load upvest-v4:dotnet-calling-endpoints`**.
- **Flow 4 (no status on the execution):** `PortfoliosRebalancingExecution` has **no `status` field** — the realignment status MUST be derived from `ListPortfolioRebalancingExecutionOrders` (`Status71`). `RetrievePortfoliosRebalancingExecution` alone cannot answer "where has the realignment got to" — **`MUST load upvest-v4:dotnet-calling-endpoints`**.
- **Flow 4 (AnyOf exclusivity):** `TriggerPortfolioRebalancingRequest` accepts `accounts` OR `allocations`, never both; use the Allocations arm for "every investor who follows this model" — **`MUST load upvest-v4:dotnet-models`**.
- **All reads of lists (pagination):** `ListPortfoliosAllocations`, `ListPortfoliosOrders`, `ListPortfolioRebalancingExecutionOrders`, `ListPositions`, `ListInstruments` are offset/limit paged and return a single page — a naive single call silently truncates. What caps a page and how to walk it — **`MUST load upvest-v4:dotnet-configuration-resilience`**.
- **All writes (retry/idempotency/unknown outcome):** which verbs the SDK may resend, what `Timeout` bounds vs the whole-call budget, and how a connection failure after the provider may have acted is reconciled — **`MUST load upvest-v4:dotnet-configuration-resilience`**.
- **All calls (error boundary):** a drifted 2xx or a non-2xx body not matching `{Operation}Error` surfaces as `ResponseDeserializationException` (an `ApiException`, not `ApiException<TError>`); the typed "NoContent" payload is a `RawError`, and config failures throw `AuthSchemeException`/`SdkConnectionException`/`SdkTimeoutException` — **`MUST load upvest-v4:dotnet-error-handling`**.
- **Sensitive data (PII):** the KYC/user bodies carry name, email, birth data, address, document numbers, tax id, phone — never log request bodies on these paths (see §5 row 7) — **`MUST load upvest-v4:dotnet-configuration-resilience`**.

---

## 4. REQUIRED READING (load all before implementation starts; names plugin-qualified)

| Skill | Governs |
|---|---|
| `upvest-v4:dotnet-client-initialization` | Step 0 — client construction, HttpClient ownership, DI registration |
| `upvest-v4:dotnet-authentication` | Step 0 — OAuth2 + HTTP signing credentials, options properties, startup fail-fast |
| `upvest-v4:dotnet-configuration-resilience` | Step 0 & all — base-URL/env selection, retries, timeout budget, pagination, logging/redaction |
| `upvest-v4:dotnet-calling-endpoints` | all — request record + `Body`, requestOptions/cancellation, response envelopes |
| `upvest-v4:dotnet-models` | Flows 1–5 — AnyOf unions, open enums, decimal-string money, date converter, `AdditionalProperties` |
| `upvest-v4:dotnet-error-handling` | all — **always required**. Case-A `ApiException<{Operation}Error>` + `RawError` accessors; and the drift hazard: a drifted/malformed **2xx** (missing `required` member) or a **non-2xx** body not matching the operation's generated `{Operation}Error` surfaces as `ResponseDeserializationException` — an `ApiException` that keeps the HTTP status and names the target type but is **not** `ApiException<TError>`; a catch ladder handling only `ApiException<TError>` lets it escape, so also catch `ResponseDeserializationException` (or `ApiException`). |
| `upvest-v4:dotnet-testing` | Step 6+ — HttpClient stub seam, faking outcomes, asserting outgoing requests |

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
| 1 | Credential fail-fast | Startup check (in `Program.cs`/DI registration) that `Upvest:ClientId`, `Upvest:ClientSecret`, `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:BaseUrl` are present and non-blank, and that the signing key file at `Upvest:SigningKeyPath` exists; `Upvest:SigningKeyPassphrase` optional (treat blank as none). Refuse to start otherwise, rather than hitting `AuthSchemeException`/`InvalidOperationException` on first call. Every part of the multi-part signing credential checked (a blank part is not a missing one). `YOUR CALL — not in the map` (host startup). |
| 2 | Secret sourcing & rotation | Secrets (`Upvest:ClientSecret`, `Upvest:SigningKeyPassphrase`, key file) from config/env (`Upvest__*`) / secret store. DI builds the options object **once** at registration and captures it in the singleton client, so a rotated secret/key takes effect only on process restart. If hot rotation is required, say so — otherwise restart. `YOUR CALL — not in the map`. |
| 3 | Total timeout budget | **RESOLVED:** whole-call budget = **30s** (`Upvest:RequestTimeoutSeconds`), enforced in `UpvestPortfolioGateway.CallAsync` via a `CancellationTokenSource.CreateLinkedTokenSource(ct)` + `CancelAfter`, linked to the ASP.NET request token (`HttpContext.RequestAborted`). Per-attempt bounds: `options.Retry.Timeout` = 15s and the named `HttpClient.Timeout` = 15s. |
| 4 | Write-retry ownership | The SDK retries only verbs/status codes in `options.Retry` (Polly). Confirm whether POST is in `HttpMethodsToRetry` before relying on resend; writes without a real idempotency key must not be blindly retried. See `dotnet-configuration-resilience` *Retries*. |
| 5 | Idempotency & ambiguous writes | **Real key present:** the `IdempotencyKey: Guid` member on every Create/Set/Trigger request record (`CreateUser`, `CreateUserCheck` has none, `SetTaxResidencies`, `CreateAccountGroup`, `CreateAccount`, `CreatePortfoliosAllocation`, `CreatePortfoliosConfiguration`, `UpdatePortfoliosConfiguration`, `CreatePortfoliosOrder`, `TriggerPortfolioRebalancing`, `CreateVirtualBankAccount`, `CreateBankTransaction`, `CreateTopup`). `UpdatePortfoliosAllocation` has **none** (PUT full-replace, idempotent by resource). Store each generated Guid in our record **before** the call; replaying the same Guid replays Upvest's original response (reconciliation path). The injected per-call `Idempotency-Key` header is not this key and must not be cited. |
| 6 | Observability | Log at Information: flow start/stop, our paymentId/realignmentId, Upvest resource ids and statuses. Log at Warning/Error: SDK exceptions with the provider correlation id from the `RawError` body (read via `ReadAsString`/`ReadAsJson`) and `ApiException.StatusCode`. Never log request bodies (row 7). `YOUR CALL — not in the map` (levels). |
| 7 | Sensitive data | **Scope carries PII** (name, email, birth data, address, document type/expiry, tax id, phone). `options.Logging.LogRequestBody` = **off**; `options.Logging.LoggerFactory` set **explicitly** (never leave unset — the `UPVEST_INVESTMENT_API_LOG=trace` env var would force body logging otherwise); application diagnostics never echo these bodies. No form bodies in scope (JSON only), so the deny-list is N/A but body logging stays off regardless. |
| 8 | Environment selection | One server group `Default`. Deployments set `Upvest:BaseUrl`: this project → sandbox `https://sandbox.upvest.co`; live → `https://api.upvest.co`. Keep `Environment=Production` and drive the address from `Upvest:BaseUrl` so test traffic cannot reach live. The SDK's "Production" is the sandbox host — do not rely on the name; rely on the bound BaseUrl. |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. The application store (per host) is where claims live; Upvest's `IdempotencyKey` sits beside the claim, not in place of it. |
| 10 | Partial results | See PAGED READS. |
| 11 | Unknown outcomes | See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
| `POST /api/portfolio/investor` (onboarding chain) | Investor row in the application store, **primary key = buyerId** (one investor per shopper) | `ConcurrentDictionary.TryAdd` on buyerId returns false for the second caller | `RegisterInvestorEndpoint.HandleAsync` checks the `TryCreateInvestor` result and returns the existing investor via `DescribeAsync` instead of onboarding again | `InMemoryPortfolioStore.TryCreateInvestor` → `RegisterInvestorEndpoint.HandleAsync` (claim precedes `Gateway.OnboardInvestorAsync`) |
| `PUT /api/portfolio/investor/model` (choose/switch model) | Configuration state keyed by buyerId's accountId in the application store | Idempotent by design (one config per account) — upsert, not a duplicate; no second-caller claim needed | N/A (upsert) | N/A — `ChooseModelEndpoint.HandleAsync` upserts `SelectedModelId`; switching is intended, not a duplicate |
| `POST /api/portfolio/payments` (pay-in) | Per-pay-in claim keyed by (buyerId + `Idempotency-Key` header, or a fresh key when absent) in the application store | `ConcurrentDictionary.TryAdd` on the (buyerId, key) claim returns false for the second caller | `CreatePaymentEndpoint.HandleAsync` checks `TryClaimPayment` and returns the existing payment instead of placing a second order | `InMemoryPortfolioStore.TryClaimPayment` → `CreatePaymentEndpoint.HandleAsync` (claim precedes `Gateway.PayInAsync`) |

> Note: the task's `POST /api/portfolio/payments` body is `{amount}` only, so two pay-ins are normally two **distinct** valid
> payments; to make a pay-in safely non-duplicable the endpoint must accept a caller-supplied request token (e.g. an
> `Idempotency-Key`-style header) that keys the claim row. If product intent is "repeated pay-ins are always distinct",
> this row becomes `none`. **RESOLVED:** `POST /api/portfolio/payments` accepts an optional `Idempotency-Key` header. When supplied it keys the claim (a retried pay-in is safe and returns the existing payment); when absent each pay-in is a distinct payment with a fresh key.

### PAGED READS

| Read | What caps it | How the caller learns it was cut short | Where in the code |
|---|---|---|---|
| `GET /api/portfolio/models` (`ListPortfoliosAllocations`) | `limit` (offset/limit); page `Data` size | The assembled list is complete (walked to `TotalCount`), so no truncation is possible within the page cap. | `UpvestPortfolioGateway.ListModelsAsync` walks pages to `Meta.Offset + Meta.Count >= Meta.TotalCount` with a 100-page cap. (The endpoint `ListModelsEndpoint` reads the app catalog, which is written only from completed allocations, so it is likewise complete.) |
| `GET /api/portfolio/payments` (`ListPortfoliosOrders`, filter account_id) | `limit` | Our own per-shopper payment list is the authoritative source and is complete. | `ListPaymentsEndpoint.HandleAsync` enumerates `IPortfolioStore.ListPayments` (the full per-shopper list) and reads each order's status; no Upvest list walk is needed for the caller-facing result. |
| Realignment status (`ListPortfolioRebalancingExecutionOrders`) | `limit` | Walking all execution-order pages before deriving status is what makes the derived status correct; a partial walk would mis-report it. | `UpvestPortfolioGateway.GetRealignmentStatusAsync` walks all pages (to `TotalCount`, 100-page cap) before computing the status. |
| `GET /api/portfolio` holdings (`GetAccountValuation.SecurityPositions`) | not paged | N/A — `GetAccountValuation` returns all positions in one response. | `UpvestPortfolioGateway.GetPortfolioAsync` uses `GetAccountValuation` (single response); `ListPositions` is not used, so there is no page walk to cut short. |

### UNKNOWN OUTCOMES

Reconciliation for every Upvest write leans on the stored `IdempotencyKey` Guid (persisted before the call): replaying the
same key replays the original response, so a connection failure after the provider may have acted is settled by re-issuing
the **same** call with the **same** key, then reading the result — not by reporting failure or leaving the caller to retry.

| Write | Re-read / reconcile with | Reference searched by | Where in the code | Test that fails the connection |
|---|---|---|---|---|
| `CreateUser` | Re-issue `CreateUser` with the stored `IdempotencyKey` (replays original) | stored `IdempotencyKey` Guid (`Investor.UserIdempotencyKey`) | `UpvestPortfolioGateway.OnboardInvestorAsync` — the `catch (UpvestGatewayException) when (ex.IsOutcomeUnknown)` around the first `CreateUser` replays the same key inline | `UpvestPortfolioGatewayTests.OnboardInvestor_reconciles_an_unknown_CreateUser_outcome_by_replaying_the_key` (stub drops the first connection) |
| `CreateUserCheck` / `SetTaxResidencies` / `CreateAccountGroup` / `CreateAccount` (mid-chain) | Keep the investor record (its persisted idempotency keys allow a later replay); not auto-resumed in this version | stored per-step `IdempotencyKey` on `Investor` | `RegisterInvestorEndpoint.HandleAsync` — an unknown outcome keeps the claimed investor (does not `RemoveInvestor`); a reconciliation sweep can replay from the stored keys | covered indirectly: the endpoint's `if (!ex.IsOutcomeUnknown) RemoveInvestor` branch (unit-tested path is the definitive-failure one) |
| `CreatePortfoliosOrder` (pay-in) | Re-issue with stored `IdempotencyKey`; the payment stays pending for a reconciliation sweep / safe retry with the same `Idempotency-Key` header | stored `IdempotencyKey` (`InvestorPayment.IdempotencyKey`) | `CreatePaymentEndpoint.HandleAsync` — an unknown outcome keeps the payment (does not `RemovePayment`); `GetPaymentStatusAsync` reconciles once the order id is known | `UpvestPortfolioGatewayTests.PayIn_credits_then_invests_and_reports_invested` proves the happy path; the keep-on-unknown branch is in the endpoint |
| `TriggerPortfolioRebalancing` | Re-issue with stored `IdempotencyKey` (replays same execution `id`) | stored `IdempotencyKey` (`Realignment.IdempotencyKey`) | `TriggerRealignmentEndpoint.HandleAsync` persists the `Realignment` (with its key) after the trigger; the key is the reconciliation reference | `UpvestPortfolioGatewayTests.GetRealignmentStatus_is_derived_from_execution_orders` covers status read-back |
| `CreateVirtualBankAccount` / `CreateBankTransaction` (sandbox credit) | Re-issue with the stored key (`Investor.VirtualBankAccountIdempotencyKey` / the payment key) | stored keys | `UpvestPortfolioGateway.PayInAsync` sends these with persisted keys before the order; an unknown outcome keeps the payment claim | `UpvestPortfolioGatewayTests.PayIn_...` asserts the credit + order are both sent |

---

## 6. Assumptions & Blockers

**Assumptions (planning):**
- **Operating model = TOL** (Take Our Licence): every workflow package and the sandbox target the TOL model, so user/account/account-group creation use the TOL/user arms (`UserTolCreateRequest`, `AccountCreateUserRequest`, `AccountGroupCreateUserRequest`). BYOL arms are out of scope.
- **Account shape for a managed portfolio** = account group `Type13.Personal` + account `Type16.Portfolio`. Confirmed from enum docs ("PORTFOLIO — orders created on a portfolio basis and additional portfolio functionality is available").
- **Test investors use a CONCAT nationality (e.g. DE)** so the `UserIdentifiers` step is not required (per user-onboarding README: AT/DE/FR/HU/IE/LU skip identifiers). **RESOLVED:** the verification steps and tests use DE; `UserIdentifiers.CreateUserIdentifier` is intentionally not wired (a non-CONCAT nationality would need it added to `OnboardInvestorAsync`). Documented in the gateway.
- **Pay-in credit mechanism** = vIBAN + `Tests.CreateBankTransaction`. **RESOLVED:** implemented in `UpvestPortfolioGateway.PayInAsync` — ensure vIBAN (`CreateVirtualBankAccount`, created once and cached on the investor) → `Tests.CreateBankTransaction` (fixed sandbox pooling IBAN `FR7617789000011051089906447`, counterparty name read from `RetrieveUser`) → `CreatePortfoliosOrder` (BUY).
- **Identifier mappings.** **RESOLVED:** **paymentId = this application's own `InvestorPayment.Id` (a GUID returned immediately), mapped internally to the Upvest portfolio-order id** — this refines the plan's original "paymentId = order id" so the caller gets a stable id even before/independent of the order, and so a scope-limited environment still returns a paymentId. **realignmentId = the Upvest rebalancing-execution id**; **modelId = the operator's own route key**, stored in the app catalog and mapped to the Upvest **allocationId** (the plan's "modelId = allocation id" is realised through that mapping, since `PUT` supplies the key before Upvest assigns the allocation id). `paidIn` = sum of the shopper's stored `InvestorPayment.Amount` (`GetPortfolioEndpoint`). Investor/payment/realignment `status` are derived per §2c; **additionally, a status read-back that hits the environment's scope limit (token 401) degrades to `pending`** (`PortfolioShared.ResolveInvestorStatusAsync`) rather than failing a successful write.
- **Persistence** = `InMemoryPortfolioStore` (a thread-safe singleton using `ConcurrentDictionary`), behind `IPortfolioStore`. **RESOLVED:** chosen over EF-in-memory for genuine atomic insert-or-fail claims and to avoid EF config; keyed by buyerId; holds the investor record, model catalog, payments and realignments; per-host, survives one run. Swappable for a durable store via the interface.
- **Caller identity / buyerId** = the JWT identity. **RESOLVED:** `PortfolioShared.GetBuyerId` reads `HttpContext.User.Identity.Name` (the `ClaimTypes.Name` claim the `AuthenticateEndpoint`/`IdentityTokenClaimService` puts in the token).
- **Endpoint convention** = `MinimalApi.Endpoint.IEndpoint<…>` classes with `AddRoute`/`HandleAsync`, registered via `AddEndpoints()`/`MapEndpoints()`; admin-only endpoints use `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` (role = `"Administrators"`). Exemplars: `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs` (admin POST), `CatalogItemListPagedEndpoint.cs` (GET). `PUT /api/portfolio/models/{modelId}` and `POST /api/portfolio/models/{modelId}/realignments` are OPERATOR (admin) actions; all other `/api/portfolio` endpoints are any signed-in shopper acting for themselves.
- **Money on the wire:** the task's JSON numbers (euros, 2dp) are converted to/from Upvest's **decimal strings** (`cash_amount`, `amount`, `weight`, `quantity` are all regex-validated strings) at the endpoint boundary.

**Blockers:** none that stop planning.

**Known environment constraint (NOT a gap to report, per task):** the sandbox client is authorised ONLY for user
operations (`users:*`). Every operation outside Flow 1's user calls requests scopes the sandbox will refuse with
`401 "This client is not authorised to use these scopes"` — this includes `accounts:*` (account group/account creation and
reads), `checks:*`, `taxes:*`, `portfolios:*`, `payments:*`, `tests:*`, `valuations:*`, `positions:*`, `instruments:*`.
Therefore, against the sandbox, only `Users.CreateUser`/`RetrieveUser`/`ListUsers` are expected to succeed end to end; the
rest are built in full and will return 401 at the scope boundary. Build and unit-test every flow; integration-verify as far
as the sandbox allows (user ops), and treat the 401 on other scopes as the expected environment response, not a defect.
(If, contrary to the task's statement, any of these scopes must function and do not, that would become a Blocker.)

---

## 7. Source labels

Every **source** cell above is a map page (`map/operations/*.md`), the `sdk-map.md` read-once sections, or a map-named SDK
source file under `sdk/dotnet/src/UpvestInvestmentApi/…` — each a one-open lookup. `UNVERIFIED` is used nowhere (all
contract facts resolved from the map/source this session). `YOUR CALL — not in the map` marks application-design decisions
(persistence, identity claim, paymentId mapping, credit mechanism, derived statuses, duplicate-token policy) to settle at
implementation against the task; the task wins. Cells marked "confirm …/confirm fields in file" name the exact declaring
file for a scoped read at build time (field names of secondary body models: `PortfoliosAllocationUpdateRequest`,
`TaxResidenciesSetRequest`, `PaymentsVirtualBankAccountCreateRequest`, `PaymentsBankTransactionCreateRequest`,
`UserCheckCreateResponse`, `AccountRetrieveResponse`/`AccountCreateResponse`/`AccountGroupCreateResponse` id & status
accessors, `Meta`, `Datum1`, `Value`, `Instrument6`, and the enum member names for `Status71`/`Status29`/`InstrumentIdType4`/
`DocumentType3`/`Method`/`PriceQuality3`) — these are Upvest contract facts, not open decisions.
