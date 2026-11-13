# Upvest Investment API integration plan — managed investment portfolios in eShopOnWeb

Target SDK: APIMatic-generated **Upvest Investment API .NET SDK**, root namespace `UpvestInvestmentApi`, shipped in this plugin at `sdk/dotnet/` (API spec `1.172.0`). All `Source` paths below are relative to that SDK root (`sdk/dotnet/`), e.g. `src/UpvestInvestmentApi/...` and `map/operations/...`.

Project: `C:\claude-runs\t10rafay-task10-plugin-opus48medium-003\repo` (eShopOnWeb, .NET 8). New endpoints go on `src/PublicApi` (JWT-authenticated, routed under `api/`). The feature is **additive** — it does not touch catalog/basket/order.

---

## 0. Repo conventions to imitate (pattern + one exemplar each)

- **Endpoint style**: `MinimalApi.Endpoint` `IEndpoint<...>` classes, one per file, `AddRoute` maps the route and `HandleAsync` runs it. Exemplar: `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`. Endpoints auto-registered by `builder.Services.AddEndpoints()` + `app.MapEndpoints()` in `src/PublicApi/Program.cs`.
- **Admin-only authorization**: `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` on the delegate — this is the OPERATOR/admin guard. Exemplar: `CreateCatalogItemEndpoint.AddRoute`. Non-admin authenticated routes use `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`; caller identity comes from the JWT (`ClaimTypes.Name`/email in the token issued by `src/PublicApi/AuthEndpoints/AuthenticateEndpoint.cs`).
- **Persistence**: EF Core via `IRepository<T>` / `IReadRepository<T>` (`Microsoft.eShopWeb.ApplicationCore.Interfaces`), backed by `EfRepository` over `CatalogContext` (`src/Infrastructure/Data/CatalogContext.cs`). Entities live in `src/ApplicationCore/Entities/*`, derive `BaseEntity`, and aggregate roots implement `IAggregateRoot`. **This `CatalogContext` is the store this codebase keeps its records in** — the new investor / payment / model-mapping records belong here as new entities with an EF config under `src/Infrastructure/Data/Config/`.
- **In-memory store**: `Dependencies.ConfigureServices` (`src/Infrastructure/Dependencies.cs`) switches `CatalogContext`/`AppIdentityDbContext` to `UseInMemoryDatabase` when config `UseOnlyInMemoryDatabase == "true"`. Per-host isolated store — every flow must be verifiable through PublicApi alone.
- **Config binding**: options bound in `Program.cs` from `IConfiguration` (`builder.Services.Configure<T>(section)`). Settings files: `src/PublicApi/appsettings.json` (+ `appsettings.Development.json`, `appsettings.test.json`).
- **DI of a client**: register in `Program.cs` after `Infrastructure.Dependencies.ConfigureServices`.

---

## 1. Scope & sequence

Operating model: **TOL (Take Our Licence)** — Upvest holds the regulatory licence and manages portfolios, which is what "an account able to hold a managed portfolio" requires. All user/account/portfolio types below are the TOL arms.

**Step A — Client wiring (once).** Register `HttpClient` + `UpvestInvestmentApiClient` as a singleton via the SDK options object, bind all six `Upvest:*` settings, install the signing hook, and fail fast on missing secrets. (§CONTRACT SHEET → client construction; PRODUCTION READINESS 1, 2, 8.)

**Step B — New EF entities + repository** in `CatalogContext`: `PortfolioInvestor` (one per shopper: shopper key, Upvest `user_id`, `account_group_id`, `account_id`, derived sign-up status), `PortfolioPayment` (shopper key, Upvest portfolio-order id, amount, derived status), `PortfolioModel` (operator model: app `modelId` → Upvest `allocation_id`, name, funds). These hold the per-shopper isolation, the `modelId`↔`allocation_id` map, and `paidIn`.

**Step C — Flow 1 (investor onboarding), POST/GET `/api/portfolio/investor`.**
- POST: `Users.CreateUser` (TOL body) → `UserIdentifiers.CreateIdentifier` (identity document) → `TaxResidencies.SetTaxResidencies` (tax id/country) → `AccountGroups.CreateAccountGroup` (PERSONAL) → `AccountsApi.CreateAccount` (type **PORTFOLIO**). Persist all returned ids against the shopper. Return `investorId` (= Upvest `user_id`) + derived `status`.
- GET: re-read via `Users.RetrieveUser` (+ `AccountsApi.RetrieveAccount` / `AccountGroups.RetrieveAccountGroup` for readiness) and recompute `status`.

**Step D — Flow 2 (model portfolios).**
- PUT `/api/portfolio/models/{modelId}` (admin): `Portfolios.CreatePortfoliosAllocation` on first use (store `modelId`→`allocation_id`), `Portfolios.UpdatePortfoliosAllocation` on replace. Validate weights sum to exactly 100 before calling.
- GET `/api/portfolio/models`: `Portfolios.ListPortfoliosAllocations` (or project from the stored `PortfolioModel` rows joined to a re-read).
- PUT `/api/portfolio/investor/model`: `Portfolios.CreatePortfoliosConfiguration` (first choice) / `Portfolios.UpdatePortfoliosConfiguration` (switch), linking the shopper's `account_id` to the chosen model's `allocation_id`.

**Step E — Flow 3 (paying in), POST/GET `/api/portfolio/payments`.**
- Guard: refuse unless the shopper is an accepted investor (`status == active`) **and** has chosen a model.
- POST: `Tests.CreateBankTransaction` (credit cash into the sandbox account — Upvest's sandbox funding) → `Portfolios.CreatePortfoliosOrder` (side **BUY**, `cash_amount`, EUR) which invests across the model allocation's weights. Persist the order id + amount; return `paymentId` + derived `status`.
- GET: list the shopper's `PortfolioPayment` rows newest-first, each re-read via `Portfolios.RetrievePortfoliosOrder` to refresh `status`.

**Step F — Flow 4 (realignment).**
- POST `/api/portfolio/models/{modelId}/realignments` (admin): `PortfoliosRebalancing.TriggerPortfolioRebalancing` with the **Allocations** arm carrying the model's `allocation_id`. Return `realignmentId` (= execution id) + `status` (pending).
- GET `/api/portfolio/realignments/{realignmentId}`: `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` **plus** `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` to derive `status` (the execution object carries no status — see Blocker B2).

**Step G — Flow 5 (portfolio view), GET `/api/portfolio`.**
- `Valuations.GetAccountValuation` (holdings with quantity + value, and total value) + `Portfolios.RetrievePortfoliosConfiguration` (→ `modelId`) + `paidIn` summed from the shopper's `PortfolioPayment` rows.

**Status at Upvest / no inbound route.** The app learns every status by its own re-reads (`RetrieveUser`, `RetrievePortfoliosOrder`, `RetrievePortfoliosRebalancingExecution` + execution-orders list). No webhook route is exposed, matching the task.

> **Authorization reality (see Blocker B1):** the client is authorised for `users:*` only. Operations needing `accounts:*`, `portfolios:*`, `tests:admin`, `taxes:*`, `valuations:read`, `positions:read`, `instruments:read` will return **401 "This client is not authorised to use these scopes"** in this sandbox. This is the environment, not a code defect — every flow is built in full regardless, and the 401 is surfaced as a clean error.

---

## 2. CONTRACT SHEET

> ⚠ **Signatures below are generated code, reproduced verbatim.** Every operation that takes input takes **ONE request record as its first parameter** (an operation with no inputs takes none), built with an **object initializer using the record's own property names** — never flat positional arguments. `requestOptions` and `cancellationToken` are optional trailing args.
>
> ⚠ **Every SDK type must be written fully-qualified with the namespace its source path implies** — taken from the path given for THAT type, not from a neighbour. Namespaces in play: client/options `UpvestInvestmentApi`; controllers `UpvestInvestmentApi.Api`; records `UpvestInvestmentApi.Models`; unions `UpvestInvestmentApi.Models.AnyOf`; enums `UpvestInvestmentApi.Models.Enums`; request records `UpvestInvestmentApi.Requests.<Controller>`; errors `UpvestInvestmentApi.Errors`; exceptions `UpvestInvestmentApi.Core.Exceptions`; raw error `UpvestInvestmentApi.Core.ErrorResponse`; signing `UpvestInvestmentApi.Signing`.

All 160 operations are **Case A (typed error)** and **throw-only** (no `…Result` variant). All in-scope operations carry the same accessor shape: `TryGetNoContent([NotNullWhen(true)] out RawError? value)` (covering the listed non-2xx statuses) + inherited `TryGetRawError([NotNullWhen(true)] out RawError? error)` fallback. The typed payload for every `TryGetNoContent` here is `RawError` (not a structured body). Source for every operation row: its `map/operations/<Controller>.md` block.

### Flow 1 — Becoming an investor

| Op (controller.method) | Signature (first param = request record) · required members | Body model + fields (`Name (wire): type, required?`) | Response envelope → fields read | Source |
| --- | --- | --- | --- | --- |
| `Users.CreateUser` | `CreateUser(CreateUserRequest request, …)` — required `IdempotencyKey: Guid`; `UpvestApiVersion` (default `_1`); `Body: UserCreateRequest?` | `UserCreateRequest` (AnyOf, `Models/AnyOf/UserCreateRequest.cs`) → build the **TOL** arm `UserCreateRequest.UserTolCreateRequest(new UserTolCreateRequest{…})`. `UserTolCreateRequest` (`Models/UserTolCreateRequest.cs`) required: `FirstName (first_name): string`, `LastName (last_name): string`, `Email (email): string`, `BirthDate (birth_date): DateTimeOffset` (date-only converter), `Nationalities (nationalities): IReadOnlyList<Nationality>` (min 1), `Address (address): Address2`, `Fatca (fatca): Fatca`. Optional but task-relevant: `BirthCity (birth_city)`, `BirthCountry (birth_country): BirthCountry`, `PhoneNumber (phone_number)`. `Address2` (`Models/Address2.cs`) required: `AddressLine1 (address_line1)`, `Postcode (postcode)`, `Country (country): Country`, `City (city)`. `Fatca` (`Models/Fatca.cs`) required: `Status (status): bool`, `ConfirmedAt (confirmed_at): DateTimeOffset`. | `UserCreateRequest1` (AnyOf, `Models/AnyOf/UserCreateRequest1.cs`) → `TryGetUserTol(out UserTol)`; read `UserTol.Id (id): Guid` = `investorId`, `UserTol.Status (status): Status`. | `map/operations/Users.md` · `Models/UserTolCreateRequest.cs` · `Models/Address2.cs` · `Models/Fatca.cs` · `Models/UserTol.cs` |
| `Users.RetrieveUser` | `RetrieveUser(RetrieveUserRequest request, …)` — required `UserId: Guid` | — (no body) | `UserGetResponse` (AnyOf, `Models/AnyOf/UserGetResponse.cs`) → `TryGetUserTol(out UserTol)`; read `UserTol.Status`. | `map/operations/Users.md` · `Models/UserTol.cs` |
| `UserIdentifiers.CreateIdentifier` | `CreateIdentifier(CreateIdentifierRequest request, …)` — required `UserId: Guid` | `IdentifierCreateRequest` (`Models/IdentifierCreateRequest.cs`): `Type (type): Type7` (default `NationalId`), required `IssuingCountry (issuing_country): IssuingCountry`, `Identifier (identifier): string?` (= document number), `IdentifierStandard (identifier_standard): string?`. **No expiry-date field exists** (see Blocker B3). | `Identifier` (`Models/Identifier.cs`) → id of the created identifier. | `map/operations/UserIdentifiers.md` · `Models/IdentifierCreateRequest.cs` |
| `TaxResidencies.SetTaxResidencies` | `SetTaxResidencies(SetTaxResidenciesRequest request, …)` — required `UserId: Guid`, `IdempotencyKey: Guid` | `TaxResidenciesSetRequest` (`Models/TaxResidenciesSetRequest.cs`): required `TaxResidencies (tax_residencies): IReadOnlyList<TaxResidencyForCreateRequest>` (min 1). `TaxResidencyForCreateRequest` (AnyOf, `Models/AnyOf/TaxResidencyForCreateRequest.cs`) → `WithTaxIdentifierNumber` arm (`Models/WithTaxIdentifierNumber.cs`): required `Country (country): Country` (= taxCountry), `TaxIdentifierNumber (tax_identifier_number): string` (= taxId). | `TaxResidencyRecord` (`Models/TaxResidencyRecord.cs`). | `map/operations/TaxResidencies.md` · `Models/TaxResidenciesSetRequest.cs` · `Models/WithTaxIdentifierNumber.cs` |
| `AccountGroups.CreateAccountGroup` | `CreateAccountGroup(CreateAccountGroupRequest request, …)` — required `IdempotencyKey: Guid`; `Body: AccountGroupCreateRequest?` | `AccountGroupCreateRequest` (AnyOf, `Models/AnyOf/AccountGroupCreateRequest.cs`) → `AccountGroupCreateUserRequest` arm (`Models/AccountGroupCreateUserRequest.cs`): required `UserId (user_id): Guid`, `Type (type): Type13` = `Type13.Personal`. | `AccountGroupCreateResponse` (AnyOf, `Models/AnyOf/AccountGroupCreateResponse.cs`) → account-group id. | `map/operations/AccountGroups.md` · `Models/AccountGroupCreateUserRequest.cs` |
| `AccountsApi.CreateAccount` | `CreateAccount(CreateAccountRequest request, …)` — required `IdempotencyKey: Guid`; `Body: AccountCreateRequest?` | `AccountCreateRequest` (AnyOf, `Models/AnyOf/AccountCreateRequest.cs`) → `AccountCreateUserRequest` arm (`Models/AccountCreateUserRequest.cs`): required `UserId (user_id): Guid`, `AccountGroupId (account_group_id): Guid`, `Type (type): Type16` = **`Type16.Portfolio`** (the type that "can hold a managed portfolio"). Optional `Name (name)`. | `AccountCreateResponse` (AnyOf, `Models/AnyOf/AccountCreateResponse.cs`) → `TryGetAccount(out Account)`; read `Account.Id` = `account_id`. | `map/operations/AccountsApi.md` · `Models/AccountCreateUserRequest.cs` |
| `AccountsApi.RetrieveAccount` | `RetrieveAccount(RetrieveAccountRequest request, …)` — required `AccountId: Guid` | — | `AccountRetrieveResponse` (AnyOf, `Models/AnyOf/AccountRetrieveResponse.cs`) → account status for readiness. | `map/operations/AccountsApi.md` |
| `AccountGroups.RetrieveAccountGroup` | `RetrieveAccountGroup(RetrieveAccountGroupRequest request, …)` — required `AccountGroupId: Guid` | — | `AccountGroupRetrieveResponse` (AnyOf) → account-group status for readiness. | `map/operations/AccountGroups.md` |

### Flow 2 — Model portfolios

| Op | Signature · required | Body model + fields | Response → fields read | Source |
| --- | --- | --- | --- | --- |
| `Portfolios.CreatePortfoliosAllocation` | `CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest request, …)` — required `IdempotencyKey: Guid`; `Body: PortfoliosAllocationCreateRequest?` | `PortfoliosAllocationCreateRequest` (`Models/PortfoliosAllocationCreateRequest.cs`): `Name (name): string?`, required `Allocation (allocation): IReadOnlyList<Allocation>`. `Allocation` (`Models/Allocation.cs`): required `InstrumentId (instrument_id): InstrumentId` (AnyOf string/Guid — pass ISIN as `InstrumentId.String("…")`), `InstrumentIdType (instrument_id_type): InstrumentIdType4` (default `Isin`), required `Weight (weight): string` (percentage string, regex `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`). | `PortfoliosAllocation` (`Models/PortfoliosAllocation.cs`): `Id (id): Guid` = the model's `allocation_id`, `Name`, `Allocation`. | `map/operations/Portfolios.md` · `Models/PortfoliosAllocationCreateRequest.cs` · `Models/Allocation.cs` · `Models/PortfoliosAllocation.cs` |
| `Portfolios.UpdatePortfoliosAllocation` | `UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest request, …)` — required `AllocationId: Guid`; `Body: PortfoliosAllocationUpdateRequest?` | `PortfoliosAllocationUpdateRequest` (`Models/PortfoliosAllocationUpdateRequest.cs`) — mirrors the create body (name + allocation list); open its file at build to confirm required members. | `PortfoliosAllocation`. | `map/operations/Portfolios.md` · `Models/PortfoliosAllocationUpdateRequest.cs` |
| `Portfolios.ListPortfoliosAllocations` | `ListPortfoliosAllocations(ListPortfoliosAllocationsRequest request, …)` — no required members; query `sort/order/offset/limit/start_date/end_date/instrument_ids` | — | `PortfoliosAllocationsListResponse` (`Models/PortfoliosAllocationsListResponse.cs`) → `data` list of `PortfoliosAllocation` + offset/limit `meta`. | `map/operations/Portfolios.md` |
| `Portfolios.RetrievePortfoliosAllocation` | `RetrievePortfoliosAllocation(RetrievePortfoliosAllocationRequest request, …)` — required `AllocationId: Guid` | — | `PortfoliosAllocation`. | `map/operations/Portfolios.md` |
| `Portfolios.CreatePortfoliosConfiguration` | `CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest request, …)` — required `IdempotencyKey: Guid`; `Body: PortfoliosConfigurationCreateRequest?` | `PortfoliosConfigurationCreateRequest` (`Models/PortfoliosConfigurationCreateRequest.cs`): required `AccountId (account_id): Guid`, `AllocationId (allocation_id): Guid`; `RebalancingStrategyIds?`. | `PortfoliosConfiguration` (`Models/PortfoliosConfiguration.cs`): `AccountId`, `AllocationId`. | `map/operations/Portfolios.md` · `Models/PortfoliosConfigurationCreateRequest.cs` |
| `Portfolios.UpdatePortfoliosConfiguration` | `UpdatePortfoliosConfiguration(UpdatePortfoliosConfigurationRequest request, …)` — required `AccountId: Guid`, `IdempotencyKey: Guid`; `Body: PortfoliosConfigurationUpdateRequest?` | `PortfoliosConfigurationUpdateRequest` (`Models/PortfoliosConfigurationUpdateRequest.cs`): `AllocationId (allocation_id): Guid?` (switch target), `RebalancingStrategyIds?`. | `PortfoliosConfiguration`. | `map/operations/Portfolios.md` · `Models/PortfoliosConfigurationUpdateRequest.cs` |
| `Portfolios.RetrievePortfoliosConfiguration` | `RetrievePortfoliosConfiguration(RetrievePortfoliosConfigurationRequest request, …)` — required `AccountId: Guid` | — | `PortfoliosConfiguration` → `AllocationId` (map back to `modelId`). | `map/operations/Portfolios.md` |

### Flow 3 — Paying in

| Op | Signature · required | Body model + fields | Response → fields read | Source |
| --- | --- | --- | --- | --- |
| `Tests.CreateBankTransaction` | `CreateBankTransaction(CreateBankTransactionRequest request, …)` — required `IdempotencyKey: Guid`; `Body: PaymentsBankTransactionCreateRequest?` | `PaymentsBankTransactionCreateRequest` (`Models/PaymentsBankTransactionCreateRequest.cs`): required `Account (account): Account29`, `Amount (amount): string` (regex 2 dp), `Currency (currency): BankTransactionCurrency`, `ClientReference (client_reference): string`, `CounterpartyName (counterparty_name): string`, `CounterpartyAccount (counterparty_account): CounterpartyAccount`. Optional `RemittanceInformation`, `CounterpartyAgent`, `VirtualBankAccountId`, `PurposeCode`. Open `Models/Account29.cs` + `Models/CounterpartyAccount.cs` at build for their required fields. | `PaymentsBankTransactionCreateResponse` (`Models/PaymentsBankTransactionCreateResponse.cs`): `Id (id): Guid`, `BankReference (bank_reference): string`. | `map/operations/Tests.md` · `Models/PaymentsBankTransactionCreateRequest.cs` |
| `Portfolios.CreatePortfoliosOrder` | `CreatePortfoliosOrder(CreatePortfoliosOrderRequest request, …)` — required `IdempotencyKey: Guid`; `Body: PortfoliosOrderPlaceRequest?` | `PortfoliosOrderPlaceRequest` (`Models/PortfoliosOrderPlaceRequest.cs`): required `UserId (user_id): Guid`, `AccountId (account_id): Guid`, `CashAmount (cash_amount): string` (positive decimal string), `Currency (currency): Currency` = `Currency.Eur`, `Side (side): Side15` = `Side15.Buy`. Optional `PostTax` (default false). | `PortfoliosOrder` (`Models/PortfoliosOrder.cs`): `Id (id): Guid` = `paymentId`, `Status (status): Status65`, `CashAmount`, `Orders`. | `map/operations/Portfolios.md` · `Models/PortfoliosOrderPlaceRequest.cs` · `Models/PortfoliosOrder.cs` |
| `Portfolios.RetrievePortfoliosOrder` | `RetrievePortfoliosOrder(RetrievePortfoliosOrderRequest request, …)` — required `PortfolioOrderId: Guid` | — | `PortfoliosOrder` → `Status` (refresh payment status). | `map/operations/Portfolios.md` · `Models/PortfoliosOrder.cs` |
| `Portfolios.ListPortfoliosOrders` | `ListPortfoliosOrders(ListPortfoliosOrdersRequest request, …)` — no required; query incl. `account_id`, `offset`, `limit` | — | `PortfoliosOrdersListResponse` → `data` + offset/limit `meta` (alternative to per-row re-read). | `map/operations/Portfolios.md` |

### Flow 4 — Realignment

| Op | Signature · required | Body model + fields | Response → fields read | Source |
| --- | --- | --- | --- | --- |
| `PortfoliosRebalancing.TriggerPortfolioRebalancing` | `TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest request, …)` — required `IdempotencyKey: Guid`; body member on the request record | `TriggerPortfolioRebalancingRequest` (AnyOf, `Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`) → **Allocations** arm `Models/Allocations.cs`: required `AllocationsValue (allocations): IReadOnlyList<Guid>` (= `[model's allocation_id]`). (Accounts arm available if targeting specific accounts.) | `TriggerPortfolioRebalancingResponse` (`Models/TriggerPortfolioRebalancingResponse.cs`): `Id (id): Guid` = `realignmentId` (execution id). | `map/operations/PortfoliosRebalancing.md` · `Models/AnyOf/TriggerPortfolioRebalancingRequest.cs` · `Models/Allocations.cs` |
| `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` | `RetrievePortfoliosRebalancingExecution(RetrievePortfoliosRebalancingExecutionRequest request, …)` — required `ExecutionId: Guid` | — | `PortfoliosRebalancingExecution` (`Models/PortfoliosRebalancingExecution.cs`): `Id`, `CreatedAt`, `UpdatedAt`, `Accounts`, `Allocations`. **⚠ No `status` field.** | `map/operations/PortfoliosRebalancing.md` · `Models/PortfoliosRebalancingExecution.cs` |
| `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` | `ListPortfolioRebalancingExecutionOrders(ListPortfolioRebalancingExecutionOrdersRequest request, …)` — required `ExecutionId: Guid`; query `sort/order/offset/limit/start_date/end_date` | — | `PortfoliosRebalancingExecutionOrderListResponse` (`Models/...ListResponse.cs`): `data: IReadOnlyList<PortfoliosRebalancingExecutionOrder>` + offset/limit `meta`. Each `PortfoliosRebalancingExecutionOrder` (`Models/PortfoliosRebalancingExecutionOrder.cs`): `Status (status): Status71`. **Derive realignment status from these.** | `map/operations/PortfoliosRebalancing.md` · `Models/PortfoliosRebalancingExecutionOrder.cs` |

### Flow 5 — The investor's portfolio

| Op | Signature · required | Response → fields read | Source |
| --- | --- | --- | --- |
| `Valuations.GetAccountValuation` | `GetAccountValuation(GetAccountValuationRequest request, …)` — required `AccountId: Guid`, `PriceQuality: PriceQuality3` (query `price_quality`, e.g. `PriceQuality3.HighestAvailable`) | `AccountValuation` (`Models/AccountValuation.cs`): `TotalSecurityValue (total_security_value): TotalSecurityValue {Amount, Currency}` = `totalValue`; `SecurityPositions (security_positions): IReadOnlyList<AccountValuationSecurityPosition>` — each `Instrument (instrument): Instrument6`, `Quantity (quantity): string`, `Value (value): Value? {Amount, Currency}` (**nullable** when no price) → `holdings[{instrumentId, quantity, value}]`. | `map/operations/Valuations.md` · `Models/AccountValuation.cs` · `Models/AccountValuationSecurityPosition.cs` · `Models/TotalSecurityValue.cs` · `Models/Value.cs` |
| `Positions.ListPositions` | `ListPositions(ListPositionsRequest request, …)` — required `AccountId: Guid`; query `offset/limit` | `PositionsListResponse` (`Models/PositionsListResponse.cs`): `Data: IReadOnlyList<Datum1>` + offset/limit `Meta`. `Datum1` (`Models/Datum1.cs`): `Instrument`, `Quantity (quantity): string` (no monetary value — valuation is the source for value). Alternative holdings source. | `map/operations/Positions.md` · `Models/Datum1.cs` |
| `Instruments.ListInstruments` | `ListInstruments(ListInstrumentsRequest request, …)` — no required; query `trading_status/sort/order/offset/limit` | `InstrumentsListResponse` → the instrument universe (for fund-ISIN validation, see invariants). | `map/operations/Instruments.md` |

### Enum values needed (verbatim from each enum's file; `OpenStringEnum` static members, PascalCase)

| Enum (file) | Members used (C# → wire) |
| --- | --- |
| `Side15` (`Models/Enums/Side15.cs`) | `Buy`→`BUY`, `Sell`→`SELL` |
| `Currency` (`Models/Enums/Currency.cs`) | `Eur`→`EUR`, `Gbp`→`GBP` |
| `Type13` acct-group (`Models/Enums/Type13.cs`) | `Personal`→`PERSONAL` (others: Child, FrenchPea, Isa, Joint, LegalEntity, PensionDeChild) |
| `Type16` acct type (`Models/Enums/Type16.cs`) | `Portfolio`→`PORTFOLIO`, `Trading`→`TRADING` |
| `Type7` identifier (`Models/Enums/Type7.cs`) | `NationalId`→`NATIONAL_ID`, `Passport`→`PASSPORT` |
| `InstrumentIdType4` (`Models/Enums/InstrumentIdType4.cs`) | `Isin` (default); `Upvest` — confirm second member name at build |
| `Status` user (`Models/Enums/Status.cs`) | `Active`→`ACTIVE`, `Inactive`→`INACTIVE`, `Offboarding`→`OFFBOARDING`, `Offboarded`→`OFFBOARDED` |
| `Status65` order (`Models/Enums/Status65.cs`) | `New`, `Processing`, `Filled`, `Settled`, `Cancelled` |
| `Status71` rebal order (`Models/Enums/Status71.cs`) | `New`, `Processing`, `Filled`, `Cancelled` |
| `PriceQuality3` (`Models/Enums/PriceQuality3.cs`) | `Eod`→`EOD`, `HighestAvailable`→`HIGHEST_AVAILABLE` |
| `UpvestApiVersion` (`Models/Enums/UpvestApiVersion.cs`) | `_1` (default on every request record) |
| `Nationality` / `Country` / `IssuingCountry` / `BirthCountry` / `BankTransactionCurrency` | ISO-code `OpenStringEnum`s with one static member per code (e.g. `Nationality.De`). **No public factory** — see trap T4 for building from a runtime string. |

### Status mapping (app-facing status derived from Upvest status — `YOUR CALL — not in the map`)

- **Investor `status`** (pending/active/rejected): `UserTol.Status` has `ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED` — **no `REJECTED`**. Decision to settle at build: treat account-ready (account group ACTIVE + PORTFOLIO account created) as `active`; `INACTIVE`/not-yet-ready as `pending`; map an onboarding failure / offboarded to `rejected`. The map gives no single "rejected" signal (Blocker B4). Source: `Models/UserTol.cs`, `Models/Enums/Status.cs`.
- **Payment `status`** (pending→invested/failed): `Status65` `NEW`/`PROCESSING` → `pending`; `FILLED`/`SETTLED` → `invested`; `CANCELLED` → `failed`. Source: `Models/Enums/Status65.cs`.
- **Realignment `status`** (pending→completed/failed): aggregate `Status71` across the execution's orders — all `FILLED` → `completed`; any `CANCELLED` with none pending → `failed`; otherwise `pending`. An execution with zero orders returned yet → `pending`. Source: `Models/Enums/Status71.cs`.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| The `modelId` an investor chooses must be a model the operator defined | `Portfolios.Create/UpdatePortfoliosConfiguration` ← app `PortfolioModel` set (seeded by `Portfolios.Create/UpdatePortfoliosAllocation`; list via `Portfolios.ListPortfoliosAllocations`) | `PutInvestorModelEndpoint` — look up the app `PortfolioModel` row by `modelId` (→ `allocation_id`); 404/400 if absent, before calling the SDK |
| The `modelId` to realign must be a model the operator defined | `PortfoliosRebalancing.TriggerPortfolioRebalancing` ← app `PortfolioModel` set | realignment POST endpoint — resolve `modelId`→`allocation_id` from `PortfolioModel`, before the SDK call |
| Each fund `instrumentId` (ISIN) in a model must be an instrument Upvest offers | `Portfolios.Create/UpdatePortfoliosAllocation` ← `Instruments.ListInstruments` | PUT model endpoint — validate each ISIN against the instrument list before the write. **Note:** `instruments:read` is out of the authorised scope set (Blocker B1), so this check will 401 in this sandbox; the plan still issues it and the operator-supplied weights-sum-to-100 rule is enforced locally regardless |
| A payment is accepted only from an accepted investor who has chosen a model | `Tests.CreateBankTransaction` + `Portfolios.CreatePortfoliosOrder` ← app `PortfolioInvestor` (status `active`) + `PortfoliosConfiguration`/stored model choice | payments POST endpoint — guard on the shopper's `PortfolioInvestor.Status == active` and a recorded model choice, before any SDK call |

### Client construction / auth / server node (Source: `sdk-map.md` "Getting a client", "Request signing", "Servers & auth")

- **Constructor (only one):** `new UpvestInvestmentApiClient(HttpClient httpClient, UpvestInvestmentApiClientOptions options)`. Groups are properties: `client.Users`, `client.AccountGroups`, `client.AccountsApi`, `client.Portfolios`, `client.PortfoliosRebalancing`, `client.Valuations`, `client.Positions`, `client.Instruments`, `client.Tests`, `client.TaxResidencies`, `client.UserIdentifiers`.
- **Options:** `OauthClientCredentials = new OAuth2ClientCredentials { ClientId = <Upvest:ClientId>, ClientSecret = <Upvest:ClientSecret> }` (namespace `UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials`); `Environment = ServerEnvironment.Production` (= `https://sandbox.upvest.co`, the sandbox); base URL override point `options.Server.Default.Production.BaseUrl` — set it from `Upvest:BaseUrl` verbatim (see trap T1).
- **Request signing (required on EVERY call, incl. the OAuth token request):** set `options.Hooks = [ new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(keyId: <Upvest:SigningKeyId>, privateKeyPath: <Upvest:SigningKeyPath>, upvestClientId: <Upvest:ClientId>, privateKeyPassphrase: <Upvest:SigningKeyPassphrase>)) ]` (namespace `UpvestInvestmentApi.Signing`). Without a signing hook (or the signing env vars), every call throws `InvalidOperationException` before being sent. See trap T2.
- **Mapping of the six `Upvest:*` settings onto the client:** `Upvest:ClientId`→`OAuth2ClientCredentials.ClientId` **and** `UpvestHttpSignatureCredentials.upvestClientId`; `Upvest:ClientSecret`→`OAuth2ClientCredentials.ClientSecret`; `Upvest:SigningKeyId`→`FromFile.keyId`; `Upvest:SigningKeyPath`→`FromFile.privateKeyPath`; `Upvest:SigningKeyPassphrase`→`FromFile.privateKeyPassphrase`; `Upvest:BaseUrl`→`options.Server.Default.Production.BaseUrl`.

---

## 3. Trap notes (hazard + skill pointer; answers intentionally omitted)

- **T1 (Step A, base URL):** the task fixes `Upvest:BaseUrl` as the base address for every call, verbatim, but `ServerEnvironment` also encodes a base URL and the two can disagree; getting which one actually wins wrong points traffic at the default host. **MUST load `upvest-v4:dotnet-configuration-resilience`.**
- **T2 (Step A, auth + signing):** Upvest needs BOTH OAuth2 client-credentials AND HTTP message signing; which credential goes on which option, which env var fills an unset one, and how a missing/blank part surfaces (`AuthSchemeException` vs `InvalidOperationException`) decides the fail-fast design — the task calls this out explicitly. **MUST load `upvest-v4:dotnet-authentication`.**
- **T3 (Step A, DI + HttpClient lifetime):** how the client, its `HttpClient`, and the options singleton are owned/registered determines secret-capture-at-registration and rotation behaviour. **MUST load `upvest-v4:dotnet-client-initialization`.**
- **T4 (request bodies, every write):** request fields typed as `OpenStringEnum` (`Nationality`, `Country`, `IssuingCountry`, `Side15`, `Currency`, `Type13/16/7`) and AnyOf unions (`UserCreateRequest`, `InstrumentId`, `TriggerPortfolioRebalancingRequest`, `TaxResidencyForCreateRequest`) have no public constructor/flat setter — building them from the shopper's runtime strings the wrong way fails at compile or silently lands in the Unknown arm. **MUST load `upvest-v4:dotnet-models`.**
- **T5 (every call site):** which exceptions actually reach the catch, and reading the typed error body vs `RawError` from `ApiException<TError>` — needed to translate the 401-scope error the sandbox returns into a clean API response. **MUST load `upvest-v4:dotnet-error-handling`.**
- **T6 (lists: models, payments, positions, execution orders):** these reads are offset/limit paginated; what caps a page and how a truncated result is signalled governs the "newest first / full set" guarantees. **MUST load `upvest-v4:dotnet-configuration-resilience`.**
- **T7 (status re-reads, Steps C/E/F):** since there is no inbound webhook, status freshness depends on retry/timeout/polling semantics — what `Timeout` bounds and which verbs auto-retry. **MUST load `upvest-v4:dotnet-configuration-resilience`.**
- **T8 (tests):** stubbing the SDK's `HttpClient` seam to verify each flow through PublicApi (incl. the 401-scope path) without live Upvest. **MUST load `upvest-v4:dotnet-testing`.**

---

## 4. REQUIRED READING (load all before implementation)

- `upvest-v4:dotnet-authentication` — Step A OAuth2 + HTTP signing credential wiring and fail-fast.
- `upvest-v4:dotnet-client-initialization` — client/HttpClient/options DI registration.
- `upvest-v4:dotnet-configuration-resilience` — base-URL/server selection, retries, timeouts, pagination, logging.
- `upvest-v4:dotnet-models` — open enums, AnyOf unions, dates, decimal-string amounts, nullable `Value`.
- `upvest-v4:dotnet-error-handling` — **always** — Case A typed errors, `TryGet…`, status/body access. **Hazard row (verbatim):** a body that does not match its declared type — a drifted or malformed **2xx** response (a missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch ladder that handles only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).
- `upvest-v4:dotnet-calling-endpoints` — per-operation request record, `requestOptions`/`cancellationToken`, response envelopes.
- `upvest-v4:dotnet-testing` — SDK stub seam for the flow tests.

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | **Credential fail-fast** | A startup check in `Program.cs` (or an options `IValidateOptions`) reads the bound `Upvest:*` section and **refuses to start** if any of `Upvest:ClientId`, `Upvest:ClientSecret`, `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:SigningKeyPassphrase`, `Upvest:BaseUrl` is missing **or blank** — all six checked individually (a blank part is not a missing one). This pre-empts the `AuthSchemeException`/`InvalidOperationException` the SDK would otherwise throw on first call. Exact binding mechanism: `YOUR CALL — not in the map` (settle at build; confirm whether the signing hook reads code-supplied values vs env vars per `dotnet-authentication`). |
| 2 | **Secret sourcing & rotation** | Secrets from `IConfiguration` (`Upvest:ClientSecret`, `Upvest:SigningKeyPassphrase`) — user-secrets/env in dev, a secret store in prod. The options object + signing hook are built **once at singleton registration**; a rotated secret takes effect only on process restart. No hot-rotation required by the task; documented as restart-to-rotate. |
| 3 | **Total timeout budget** | The caller-visible budget is the per-attempt `Timeout` × retries + backoff; enforced via `options.Retry` and request `CancellationToken`. Exact numbers `YOUR CALL — not in the map`; what `Timeout` bounds vs a whole call → `dotnet-configuration-resilience` (trap T7). |
| 4 | **Write-retry ownership** | Which in-scope writes (`CreateUser`, `CreateAccountGroup`, `CreateAccount`, `CreateIdentifier`, `SetTaxResidencies`, `CreatePortfoliosAllocation/Configuration`, `CreatePortfoliosOrder`, `CreateBankTransaction`, `TriggerPortfolioRebalancing`) the SDK may auto-resend is governed by `RetryOptions.HttpMethodsToRetry`; confirm POST/PUT inclusion → `dotnet-configuration-resilience`. Mitigated by the idempotency keys (#5). |
| 5 | **Idempotency & ambiguous writes** | Every write in scope takes a **real** caller-supplied `IdempotencyKey: Guid` on its request record (confirmed on all create/update/trigger request records). Reuse a stable per-logical-operation key (not a fresh GUID per attempt) so a resend deduplicates. The auto-injected `Idempotency-Key` header is NOT this key. Reconciliation path for unknown outcomes: §UNKNOWN OUTCOMES. |
| 6 | **Observability** | Log operation name + Upvest correlation id read from the error body (`RawError.ReadAsString()`/`ReadAsJson`) at Warning/Error; Info for state transitions (investor active, payment invested, realignment completed). Never log request bodies (#7). Correlation-id extraction mechanics → `dotnet-error-handling`. |
| 7 | **Sensitive data** | **Yes — scope carries personal data** (name, email, birth, address, nationality, tax id, identity-document number, phone). Posture: `options.Logging.LogRequestBody` **off**; `options.Logging.LoggerFactory` set explicitly (never leave unset — the `UPVEST_INVESTMENT_API_LOG=trace` env var would otherwise force body logging); app diagnostics never echo request bodies or the `Upvest:ClientSecret`/`Upvest:SigningKeyPassphrase`. Deny-list any sensitive form keys if form bodies are ever used. |
| 8 | **Environment selection** | One server group `Default`. `Production`→`https://sandbox.upvest.co` (the sandbox — this deployment), `Environment2`→`https://api.upvest.co` (live). This integration targets the sandbox; `Upvest:BaseUrl` overrides `options.Server.Default.Production.BaseUrl` verbatim (trap T1). Test traffic stays on the sandbox host; the live host is never configured here. |
| 9 | **Duplicate prevention under concurrency** | See DUPLICATE CLAIMS. |
| 10 | **Partial results** | See PAGED READS. |
| 11 | **Unknown outcomes** | See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Investor onboarding (`CreateUser` + chain) | `PortfolioInvestor` row in `CatalogContext`, keyed by shopper identity (unique/PK on shopper key) | EF insert of a second row with the same shopper key violates the primary/unique key | `catch` around the claim `SaveChanges`/`AddAsync` → return the existing investor | TBD |
| Pay in (`CreateBankTransaction` + `CreatePortfoliosOrder`) | `PortfolioPayment` row in `CatalogContext`, keyed by shopper key + client-supplied payment idempotency key (unique) | EF insert of a second row with the same (shopper, idempotency key) violates the unique key | `catch` around the claim save → return the existing payment | TBD |
| Choose/switch model (`Create/UpdatePortfoliosConfiguration`) | model-choice field on the `PortfolioInvestor` row (single row per shopper) | update of the single shopper row serialises; no duplicate row can exist (PK on shopper key) | `catch`/re-read on the shopper row update | TBD |
| Create/replace model (`Create/UpdatePortfoliosAllocation`) | `PortfolioModel` row keyed by `modelId` (PK) | second create for the same `modelId` hits the existing PK → routed to update instead | lookup-then-branch in the PUT handler (idempotent by design) | TBD |
| Trigger realignment (`TriggerPortfolioRebalancing`) | optional `PortfolioRealignment` row keyed by (modelId, idempotency key) | EF unique key rejects a duplicate trigger | `catch` around the claim save | TBD |

> Claim order per row: **write the claim first → SDK call → record result**; release the claim if the provider refuses. Stores are PK/unique constraints the in-memory EF provider enforces.

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `Portfolios.ListPortfoliosAllocations` (GET /models) | offset/limit; `meta.total_count` vs returned count | page through until returned count reaches `meta.total_count`, or expose `total`/`hasMore` on the response DTO | TBD |
| `Portfolios.ListPortfoliosOrders` (GET /payments, if used) | offset/limit; `meta.total_count` | same — loop to total, or surface a `hasMore` flag on the payments DTO | TBD |
| `Positions.ListPositions` / `Valuations` security_positions (GET /portfolio) | offset/limit (positions); valuation positions inline | page positions to `meta.total_count` so holdings are complete; valuation returns all `security_positions` in one body | TBD |
| `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (realignment status) | offset/limit; `meta.total_count` | page all execution orders before deriving status, else status is computed on a partial set | TBD |

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `CreateUser` (+ chain) | `Users.ListUsers` / `Users.RetrieveUser` | stable idempotency key / stored `user_id`; if id not yet persisted, match by the deterministic onboarding idempotency key | onboarding endpoint `catch (SdkConnectionException/SdkTimeoutException)` → reconcile before returning | TBD |
| `CreatePortfoliosOrder` | `Portfolios.RetrievePortfoliosOrder` / `ListPortfoliosOrders` | order id if returned, else `account_id` + idempotency key via list | payments endpoint `catch` → re-read, settle payment status | TBD |
| `CreateBankTransaction` | (sandbox) re-issue is safe via idempotency key; reconcile via credit-funding/transaction reads | client_reference / idempotency key | payments endpoint `catch` before the order step | TBD |
| `TriggerPortfolioRebalancing` | `RetrievePortfoliosRebalancingExecution` + `ListPortfolioRebalancingExecutionOrders` | execution id if returned, else the realignment claim's idempotency key | realignment endpoint `catch` → reconcile | TBD |
| `Create/UpdatePortfoliosConfiguration` | `Portfolios.RetrievePortfoliosConfiguration` | `account_id` | model-choice endpoint `catch` → re-read config | TBD |

---

## 6. Assumptions & Blockers

**Assumptions (intent):**
- **A1 — TOL operating model.** "Managed portfolio" + "Upvest as the investment provider" is read as the TOL arm throughout (`UserTolCreateRequest`, `AccountCreateUserRequest` type `PORTFOLIO`, `AccountGroupCreateUserRequest` type `PERSONAL`). If the deployment is BYOL, the user/account bodies change.
- **A2 — FATCA + consents.** `UserTolCreateRequest.Fatca` is **required** by the SDK but not in the task's field list; onboarding will supply a default FATCA record (`status=false`, `confirmed_at=now`). Terms/data-privacy consent objects are optional and omitted unless Upvest rejects without them.
- **A3 — `nationality` (single, task) → `Nationalities` (list, SDK).** Mapped to a one-element list.
- **A4 — Funding then explicit BUY.** Flow 3 both credits cash (`CreateBankTransaction`) and places an explicit `CreatePortfoliosOrder` (BUY). Upvest also has an `AUTO_INVESTMENT` initiation flow that invests incoming cash automatically; if auto-investment is enabled for the account, the explicit order may be redundant or conflict — settle at build. `YOUR CALL — not in the map`.
- **A5 — Money representation.** SDK amounts are decimal **strings** (`cash_amount`, `amount`, valuation `amount`); the API surface uses JSON numbers with two decimals. The app converts decimal euros ↔ string at the boundary and formats responses to 2 dp. `paidIn` is summed from the app's own `PortfolioPayment` rows (not an Upvest field).
- **A6 — `modelId` is app-defined.** The route `{modelId}` is the operator's identifier; the app stores `modelId`→Upvest `allocation_id`. GET /models projects from stored rows (names/weights) reconciled with `ListPortfoliosAllocations`.
- **A7 — New entities live in `CatalogContext`.** Consistent with the repo's single business `DbContext` and `IRepository<T>`; in-memory under `UseOnlyInMemoryDatabase=true`.

**Blockers:**
- **B1 — Scope restriction (environment, build anyway).** The sandbox client is authorised for `users:*` only. Operations requiring `accounts:admin/read`, `portfolios:admin/read`, `tests:admin`, `taxes:admin`, `valuations:read`, `positions:read`, `instruments:read` will return **401 "This client is not authorised to use these scopes"**. Per the task this is the environment, not a code gap — Flows 2–5 and the account chain in Flow 1 are built in full and the 401 is surfaced cleanly. Only `CreateUser`/`RetrieveUser` (`users:read/admin`) and `CreateIdentifier` (`users:admin`) are expected to succeed end-to-end. `SetTaxResidencies` needs `taxes:admin` → expected 401.
- **B2 — Realignment has no status field.** `PortfoliosRebalancingExecution` (returned by `RetrievePortfoliosRebalancingExecution`) exposes **no `status`**. The task's realignment `status` (pending→completed/failed) is **derived** by paging `ListPortfolioRebalancingExecutionOrders` and aggregating each order's `Status71`. Recorded as a derivation, not a direct read. If the operator requires a single authoritative execution status, the SDK does not provide one.
- **B3 — Identity-document expiry has no home.** The task's `identityDocument.expiryDate` has **no corresponding field** in `IdentifierCreateRequest` (which carries only `type`, `issuing_country`, `identifier`/number, `identifier_standard`). `expiryDate` will be accepted by the API endpoint but **dropped** before the Upvest call (no SDK capability). No other operation accepts an identity-document expiry. Confirm acceptability with the product owner.
- **B4 — No "rejected" user status.** `UserTol.Status` has no `REJECTED` value (only `ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED`). The task's investor `status=rejected` cannot be read from a single Upvest field; it must be inferred (e.g. onboarding/role failure or offboarded), which the map does not define. Mapping settled as `YOUR CALL — not in the map` (see Status mapping); a precise rejected-signal is unavailable.

---

## 7. Source labels

Every contract-sheet row cites its `map/operations/*.md` page and/or the single `src/UpvestInvestmentApi/...` file it was read from (all relative to the plugin SDK root `sdk/dotnet/`). Items marked `YOUR CALL — not in the map` (status mappings, timeout numbers, auto-invest vs explicit order, fail-fast binding mechanism) are application decisions to settle at implementation against the task. `UNVERIFIED`: none required — all cited facts were read from the map or source this session. Planning blockers are in §6.
