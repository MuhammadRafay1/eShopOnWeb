# Upvest Investment API — Integration Plan (eShopOnWeb · managed investment portfolios)

Plan for the additive "managed investment portfolio" capability on `src/PublicApi`, with Upvest as
the investment provider, driven by `dotnet-integrate-upvest-investment-api`. Every SDK fact below is
grounded in the SDK map (`sdk-map.md` + `map/operations/`) and the source files it names, all under
the plugin-relative SDK root `sdk/dotnet/` (absolute on this machine:
`C:\claude-runs\t10rafay-task10-plugin-arazzo-opus48medium-002\marketplace\plugins\upvest-v4\sdk\dotnet\`),
cross-checked against the Upvest Arazzo workflow packages in `repo/upvest-workflows/`.

> **SANDBOX SCOPE WALL (applies to the whole sheet).** The task states the sandbox client is
> authorised ONLY for **user** operations; any call needing another scope returns `401 "This client
> is not authorised to use these scopes"`. Operations keyed to `users:admin` / `users:read` are the
> only ones the sandbox will accept (CreateUser, RetrieveUser, ListUsers, CreateIdentifier,
> ListUserIdentifiers). Every operation keyed to `checks:*`, `taxes:*`, `accounts:*`, `portfolios:*`,
> `payments:*`, `topups:*`, `positions:read`, `valuations:read`, `tests:admin` is planned in full but
> is **UNVERIFIED** against live traffic — code them defensively per the trap notes and the
> error-handling boundary; do not design them away.

---

## 1. Scope & sequence

The capability maps onto the Upvest **Take-Our-Licence (TOL)** portfolio model: a *model portfolio* =
a Upvest **portfolio allocation** (named set of instrument weights summing to 100); an investor's
portfolio *follows* a model through a **portfolio configuration** (links an account to an allocation);
paying in = credit cash then place a **portfolio order** (BUY, cash distributed across the account's
configured allocation); realignment = **trigger portfolio rebalancing**; the portfolio view = the
**configuration** (model) + **positions** (holdings) + **account valuation** (total value).

Implementation steps, each naming the Upvest operations it drives. All app-side routes are new
`IEndpoint<>` minimal-API endpoints on `src/PublicApi` (exemplar:
`src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`), JWT-authenticated, caller identity
from the token's `ClaimsPrincipal`. A new EF store owned by PublicApi (in-memory provider,
`UseOnlyInMemoryDatabase=true`) persists the app's own records: shopper→Upvest-user/account mapping
(`investorId`, `accountGroupId`, `accountId`, idempotency keys), model→allocation-id mapping, chosen
model per investor, payments (with app `paymentId`), and realignments (app `realignmentId`).

- **Step 0 — Client, signing & DI.** Register a singleton `UpvestInvestmentApiClient` bound from the
  `Upvest:` config section (§2 client facts). Fail-fast on missing credential parts. `MUST load
  dotnet-client-initialization`, `dotnet-authentication`, `dotnet-configuration-resilience`.
- **Step 1 — Become an investor** (`POST/GET /api/portfolio/investor`). Onboard the shopper as a TOL
  user and open a PORTFOLIO account. Upvest sequence (per `user-onboarding` workflow):
  `Users.CreateUser` → `UserIdentifiers.CreateIdentifier` (identity document) →
  `TaxResidencies.SetTaxResidencies` (taxId/taxCountry) → `UserChecks.CreateUserCheck` (KYC, async) →
  `AccountGroups.CreateAccountGroup` (PERSONAL) → `AccountsApi.CreateAccount` (type `PORTFOLIO`).
  Status (`GET`) is observed by **reads**, no webhook: `Users.RetrieveUser` (user status) +
  `AccountsApi.RetrieveAccount` (account status). *Sandbox: only CreateUser/CreateIdentifier accepted;
  checks/tax/account-group/account are UNVERIFIED (scope wall).* 
- **Step 2 — Model portfolios** (`PUT /api/portfolio/models/{modelId}`, `GET /api/portfolio/models`,
  `PUT /api/portfolio/investor/model`). Operator create-or-replace a model →
  `Portfolios.CreatePortfoliosAllocation` (first PUT, store Upvest allocation id) /
  `Portfolios.UpdatePortfoliosAllocation` (replace, full allocation array). List →
  `Portfolios.ListPortfoliosAllocations`. Investor chooses model →
  `Portfolios.CreatePortfoliosConfiguration` (first choice) / `Portfolios.UpdatePortfoliosConfiguration`
  (switch). Instrument ISINs validated against `Instruments.ListInstruments` (CROSS-OP invariant).
  *Sandbox: all `portfolios:*` UNVERIFIED.*
- **Step 3 — Pay in** (`POST/GET /api/portfolio/payments`). Credit cash to the account group, then
  invest: `TopUps.CreateTopup` (sandbox cash-in mechanism; account-group cash credit) →
  `Portfolios.CreatePortfoliosOrder` (side BUY, `cash_amount`, distributed across the account's
  configured allocation). Status observed by reads: `TopUps.RetrieveTopup` +
  `Portfolios.RetrievePortfoliosOrder`. *Sandbox: `topups:*` and `portfolios:*` UNVERIFIED.*
- **Step 4 — Realignment** (`POST /api/portfolio/models/{modelId}/realignments`,
  `GET /api/portfolio/realignments/{realignmentId}`). Operator realigns a model's followers →
  `PortfoliosRebalancing.TriggerPortfolioRebalancing` with the **Allocations** arm
  (`allocations: [upvest_allocation_id]`). Status observed by reads:
  `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` (identity only — carries NO status
  field, see Blocker B-1) + `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (order
  statuses, from which realignment status is derived). *Sandbox: `portfolios:*` UNVERIFIED.*
- **Step 5 — The investor's portfolio** (`GET /api/portfolio`). modelId ←
  `Portfolios.RetrievePortfoliosConfiguration` (`allocation_id`); holdings ← `Positions.ListPositions`
  (instrument + quantity; per-holding value derived from valuation security positions); totalValue ←
  `Valuations.GetAccountValuation` (`total_security_value`); paidIn ← app's own invested-payment sum.
  *Sandbox: `positions:read`/`valuations:read` UNVERIFIED.*

A capability the map lacks is a Blocker (§6), never an invented path.

---

## 2. CONTRACT SHEET

> ⚠ **Signatures below are generated code, verbatim.** Every operation that takes input takes **ONE
> request record as its first parameter** (an operation with no inputs takes none), built with an
> object initializer whose property names are the record's own — never flat positional arguments.
> `RequestOptions? requestOptions = null` and `CancellationToken cancellationToken = default` follow.
>
> ⚠ **Every SDK type is written fully-qualified with the namespace its source path implies**, taken
> from the path the map gives for THAT type (`Models/` → `UpvestInvestmentApi.Models`; `Models/Enums/`
> → `UpvestInvestmentApi.Models.Enums`; `Models/AnyOf/` → `UpvestInvestmentApi.Models.AnyOf`;
> `Requests/<Ctrl>/` → `UpvestInvestmentApi.Requests.<Ctrl>`; `Errors/` → `UpvestInvestmentApi.Errors`),
> never from where a neighbouring type sits. Child namespaces do not import transitively — a separate
> `using` per type kind.
>
> ⚠ **All 160 operations are Case A (typed error), throw-based, no pagination unless a row says
> otherwise, server group `Default`.** Every row's error is `ApiException<{Operation}Error>` whose only
> accessors are `TryGetNoContent([NotNullWhen(true)] out RawError? value)` (the listed statuses) and
> `TryGetRawError([NotNullWhen(true)] out RawError? error)` (fallback) — i.e. the typed body carries no
> structured fields beyond `RawError`; read status/message via the `RawError` the accessor yields.
> `ResponseDeserializationException` / `ApiException` must also be caught (see REQUIRED READING).

### 2a. Per-operation rows

Each write's request record carries `UpvestApiVersion UpvestApiVersion` (defaults `UpvestApiVersion._1`,
`Models/Enums/UpvestApiVersion.cs`) plus, where noted, `required Guid IdempotencyKey`. The `Body`
member on each create record is **optional (`? = null`)** but is what the endpoint actually sends —
you MUST set it (see the ⚠ note after the table).

| # | Controller · method | Request record + members | Body model + fields (`name (wire): type, required?`) | Response envelope + fields read | Error | Source |
|---|---|---|---|---|---|---|
| 1 | `client.Users.CreateUser(CreateUserRequest r)` | `IdempotencyKey: Guid, required` · `Body: UserCreateRequest?` | `UserCreateRequest` is AnyOf → build `UserTolCreateRequest` arm: `FirstName (first_name): string, req` · `LastName (last_name): string, req` · `Email (email): string, req` · `BirthDate (birth_date): DateTimeOffset, req` (DateOnly wire) · `Nationalities (nationalities): IReadOnlyList<Nationality>, req` · `Address (address): Address2, req` · `Fatca (fatca): Fatca, req` · optional: `BirthCity`, `BirthCountry`, `PhoneNumber`, `TermsAndConditions`, `DataPrivacyAndSharingAgreement` | `UserCreateRequest1` (AnyOf) → `TryGetUserTol(out UserTol)`; read `UserTol.Id (id): Guid`, `UserTol.Status (status): Status` | `ApiException<CreateUserError>` · NoContent[400,401,403,406,429,500,503,504] | map `Users.md`; `Models/UserTolCreateRequest.cs`; `Models/AnyOf/UserCreateRequest.cs`; `Models/UserTol.cs` |
| 2 | `client.Users.RetrieveUser(RetrieveUserRequest r)` | `UserId: Guid, required` | — (no body) | `UserGetResponse` (AnyOf) → `TryGetUserTol(out UserTol)`; read `UserTol.Status` (ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED) | `ApiException<RetrieveUserError>` · NoContent[401,403,404,406,429,500,503,504] | map `Users.md`; `Models/AnyOf/UserGetResponse.cs`; `Models/UserTol.cs` |
| 3 | `client.UserIdentifiers.CreateIdentifier(CreateIdentifierRequest r)` | `UserId: Guid, required` · `Body: IdentifierCreateRequest?` | `IdentifierCreateRequest` — carries the identity-document fields (passport/ID number, issuing country, standard). Read field names/wire from source at implement time. | `Identifier`; read `Id` | `ApiException<CreateIdentifierError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `UserIdentifiers.md`; `Models/IdentifierCreateRequest.cs` |
| 4 | `client.TaxResidencies.SetTaxResidencies(SetTaxResidenciesRequest r)` | `UserId: Guid, required` · `IdempotencyKey: Guid, required` · `Body: TaxResidenciesSetRequest?` | `TaxResidenciesSetRequest` — taxId (TIN) + taxCountry list. Read shape from source at implement time. | `TaxResidencyRecord` | `ApiException<SetTaxResidenciesError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `TaxResidencies.md`; `Models/TaxResidenciesSetRequest.cs` |
| 5 | `client.UserChecks.CreateUserCheck(CreateUserCheckRequest r)` | `UserId: Guid, required` · `Body: UserCheckCreateRequest?` | `UserCheckCreateRequest` (AnyOf). KYC check; **async (202)** — do not poll synchronously. | `UserCheckCreateResponse`; read check id/status | `ApiException<CreateUserCheckError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `UserChecks.md`; `Models/AnyOf/UserCheckCreateRequest.cs` |
| 6 | `client.AccountGroups.CreateAccountGroup(CreateAccountGroupRequest r)` | `IdempotencyKey: Guid, required` · `Body: AccountGroupCreateRequest?` | `AccountGroupCreateRequest` (AnyOf) — PERSONAL group for the user. Read arm from source. | `AccountGroupCreateResponse` (AnyOf); read account-group id + status (initial `PENDING_APPROVAL`) | `ApiException<CreateAccountGroupError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `AccountGroups.md`; `Models/AnyOf/AccountGroupCreateRequest.cs` |
| 7 | `client.AccountsApi.CreateAccount(CreateAccountRequest r)` | `IdempotencyKey: Guid, required` · `Body: AccountCreateRequest?` | `AccountCreateRequest` AnyOf → `AccountCreateUserRequest` arm: `UserId (user_id): Guid, req` · `AccountGroupId (account_group_id): Guid, req` · `Type (type): Type16, req` → set **`PORTFOLIO`** · opt `Name` | `AccountCreateResponse` (AnyOf) → `TryGetAccount(out Account)`; read `Account.Id`, `Account` status | `ApiException<CreateAccountError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `AccountsApi.md`; `Models/AnyOf/AccountCreateRequest.cs`; `Models/AccountCreateUserRequest.cs`; `Models/AnyOf/AccountCreateResponse.cs` |
| 8 | `client.AccountsApi.RetrieveAccount(RetrieveAccountRequest r)` | `AccountId: Guid, required` | — | `AccountRetrieveResponse` (AnyOf) → `TryGetAccount(out Account)`; read `Account` status (observe "account ready") | `ApiException<RetrieveAccountError>` · NoContent[401,403,404,406,429,500,503,504] | map `AccountsApi.md`; `Models/AnyOf/AccountRetrieveResponse.cs` |
| 9 | `client.Portfolios.CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest r)` | `IdempotencyKey: Guid, required` · `Body: PortfoliosAllocationCreateRequest?` | `PortfoliosAllocationCreateRequest`: `Allocation (allocation): IReadOnlyList<Allocation>, req` · `Name (name): string?, opt` · `RebalancingStrategyIds?`. `Allocation`: `InstrumentId (instrument_id): InstrumentId, req` (AnyOf; ISIN via `.String("…")` — converter matches `^[A-Z]{2}[A-Z0-9]{9}[0-9]$`) · `InstrumentIdType (instrument_id_type): InstrumentIdType4 = Isin` · `Weight (weight): string, req` (regex `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`; weights sum to **100**) | `PortfoliosAllocation`; read `Id (id): Guid`, `Name`, `Allocation[]` | `ApiException<CreatePortfoliosAllocationError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosAllocationCreateRequest.cs`; `Models/Allocation.cs`; `Models/AnyOf/InstrumentId.cs`; `Models/PortfoliosAllocation.cs` |
| 10 | `client.Portfolios.UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest r)` | `AllocationId: Guid, required` · `Body: PortfoliosAllocationUpdateRequest?` | `PortfoliosAllocationUpdateRequest`: `Allocation (allocation): IReadOnlyList<Allocation>, req` (FULL replace — send all funds) · `Name (name): string?, opt` | `PortfoliosAllocation`; read `Id`, `Allocation[]` | `ApiException<UpdatePortfoliosAllocationError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosAllocationUpdateRequest.cs` |
| 11 | `client.Portfolios.ListPortfoliosAllocations(ListPortfoliosAllocationsRequest r)` | (none required) · query: `Sort`,`Order`,`Offset`,`Limit`,`StartDate`,`EndDate`,`InstrumentIds` | — | `PortfoliosAllocationsListResponse`; read items + pagination meta | `ApiException<ListPortfoliosAllocationsError>` · NoContent[400,401,403,404,405,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosAllocationsListResponse.cs` |
| 12 | `client.Portfolios.CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest r)` | `IdempotencyKey: Guid, required` · `Body: PortfoliosConfigurationCreateRequest?` | `PortfoliosConfigurationCreateRequest`: `AccountId (account_id): Guid, req` · `AllocationId (allocation_id): Guid, req` · `RebalancingStrategyIds?` | `PortfoliosConfiguration`; read `AccountId`, `AllocationId` | `ApiException<CreatePortfoliosConfigurationError>` · NoContent[400,401,403,404,406,422,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosConfigurationCreateRequest.cs`; `Models/PortfoliosConfiguration.cs` |
| 13 | `client.Portfolios.UpdatePortfoliosConfiguration(UpdatePortfoliosConfigurationRequest r)` | `AccountId: Guid, required` · `IdempotencyKey: Guid, required` · `Body: PortfoliosConfigurationUpdateRequest?` | `PortfoliosConfigurationUpdateRequest`: `AllocationId (allocation_id): Guid?, opt` (switch model) · `RebalancingStrategyIds?` | `PortfoliosConfiguration`; read `AllocationId` | `ApiException<UpdatePortfoliosConfigurationError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosConfigurationUpdateRequest.cs` |
| 14 | `client.Portfolios.RetrievePortfoliosConfiguration(RetrievePortfoliosConfigurationRequest r)` | `AccountId: Guid, required` | — | `PortfoliosConfiguration`; read `AllocationId` (→ app modelId) | `ApiException<RetrievePortfoliosConfigurationError>` · NoContent[401,403,404,405,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosConfiguration.cs` |
| 15 | `client.TopUps.CreateTopup(CreateTopupRequest r)` | `IdempotencyKey: Guid, required` · `Body: PaymentsTopUpCreateRequest?` | `PaymentsTopUpCreateRequest`: `AccountGroupId (account_group_id): Guid, req` · `CashAmount (cash_amount): string, req` (regex `^[0-9]{1,9}(\.[0-9]{2})?$` — **two decimals**, euros) · `Currency (currency): Currency, req` (`EUR`) | `PaymentsTopupsResponse`; read `Id (id): Guid`, `Status (status): Status29?` (CONFIRMED/SETTLED/CANCELLED), `SettlementReference` | `ApiException<CreateTopupError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `TopUps.md`; `Models/PaymentsTopUpCreateRequest.cs`; `Models/PaymentsTopupsResponse.cs` |
| 16 | `client.TopUps.RetrieveTopup(RetrieveTopupRequest r)` | `TopupId: Guid, required` | — | `PaymentsTopupsResponse`; read `Status` | `ApiException<RetrieveTopupError>` · NoContent[401,403,404,406,429,500,503,504] | map `TopUps.md` |
| 17 | `client.Portfolios.CreatePortfoliosOrder(CreatePortfoliosOrderRequest r)` | `IdempotencyKey: Guid, required` · `Body: PortfoliosOrderPlaceRequest?` | `PortfoliosOrderPlaceRequest`: `UserId (user_id): Guid, req` · `AccountId (account_id): Guid, req` · `CashAmount (cash_amount): string, req` (regex `^[0-9]{0,63}(\.[0-9]{1,27})?$`) · `Currency (currency): Currency, req` (`EUR`) · `Side (side): Side15, req` → **`BUY`** · `PostTax (post_tax): bool? = false`. **No allocation_id** — the order distributes across the account's CONFIGURED allocation (row 12/13), so the configuration MUST exist first. | `PortfoliosOrder`; read `Id (id): Guid`, `Status (status): Status65` (NEW/PROCESSING/FILLED/SETTLED/CANCELLED), `CancellationReason`, `Orders[]` | `ApiException<CreatePortfoliosOrderError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosOrderPlaceRequest.cs`; `Models/PortfoliosOrder.cs` |
| 18 | `client.Portfolios.RetrievePortfoliosOrder(RetrievePortfoliosOrderRequest r)` | `PortfolioOrderId: Guid, required` | — | `PortfoliosOrder`; read `Status`, `CancellationReason` | `ApiException<RetrievePortfoliosOrderError>` · NoContent[401,403,404,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosOrder.cs` |
| 19 | `client.Portfolios.ListPortfoliosOrders(ListPortfoliosOrdersRequest r)` | (none req) · query incl. `AccountId`,`Offset`,`Limit` | — | `PortfoliosOrdersListResponse`; items + meta | `ApiException<ListPortfoliosOrdersError>` · NoContent[400,401,403,404,405,406,429,500,503,504] | map `Portfolios.md`; `Models/PortfoliosOrdersListResponse.cs` |
| 20 | `client.PortfoliosRebalancing.TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest r)` | `IdempotencyKey: Guid, required` · `Body: TriggerPortfolioRebalancingRequest?` | `TriggerPortfolioRebalancingRequest` AnyOf → **`Allocations` arm**: `Allocations.AllocationsValue (allocations): IReadOnlyList<Guid>, req` = `[upvest_allocation_id]`. (`Accounts` arm also exists — send accounts OR allocations, never both.) Build via `TriggerPortfolioRebalancingRequest.Allocations(new Allocations{ AllocationsValue = [..] })`. | `TriggerPortfolioRebalancingResponse`; read `Id (id): Guid` (execution id) | `ApiException<TriggerPortfolioRebalancingError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `PortfoliosRebalancing.md`; `Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`; `Models/Allocations.cs`; `Models/TriggerPortfolioRebalancingResponse.cs` |
| 21 | `client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(RetrievePortfoliosRebalancingExecutionRequest r)` | `ExecutionId: Guid, required` | — | `PortfoliosRebalancingExecution`; read `Id`, `Accounts?`, `Allocations?` — **NO status field** (see Blocker B-1) | `ApiException<RetrievePortfoliosRebalancingExecutionError>` · NoContent[401,403,404,405,406,429,500,503,504] | map `PortfoliosRebalancing.md`; `Models/PortfoliosRebalancingExecution.cs` |
| 22 | `client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(ListPortfolioRebalancingExecutionOrdersRequest r)` | `ExecutionId: Guid, required` · query `Offset`,`Limit`,… | — | `PortfoliosRebalancingExecutionOrderListResponse`; order statuses → derive realignment status | `ApiException<ListPortfolioRebalancingExecutionOrdersError>` · NoContent[400,401,403,404,405,406,429,500,503,504] | map `PortfoliosRebalancing.md`; `Models/PortfoliosRebalancingExecutionOrderListResponse.cs` |
| 23 | `client.Positions.ListPositions(ListPositionsRequest r)` | `AccountId: Guid, required` · query `Offset`,`Limit` | — | `PositionsListResponse`: `Data: IReadOnlyList<Datum1>, req` (`Datum1.Instrument` {uuid, isin}, `Quantity (quantity): string`, `SettledQuantity`, `AvailableForTrading`…) · `Meta: Meta, req` (offset/limit/count/total_count) | `ApiException<ListPositionsError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `Positions.md`; `Models/PositionsListResponse.cs`; `Models/Datum1.cs` |
| 24 | `client.Valuations.GetAccountValuation(GetAccountValuationRequest r)` | `AccountId: Guid, required` · `PriceQuality: PriceQuality3, required` · query `price_quality` | — | `AccountValuation`: `TotalSecurityValue (total_security_value): TotalSecurityValue, req` (amount+currency → totalValue) · `SecurityPositions (security_positions): IReadOnlyList<AccountValuationSecurityPosition>, req` (per-instrument value) · `ValuationTime` | `ApiException<GetAccountValuationError>` · NoContent[400,401,403,404,406,429,500,503,504] | map `Valuations.md`; `Models/AccountValuation.cs` |
| 25 | `client.Instruments.ListInstruments(…)` | (ISIN validation — read `map/operations/Instruments.md` at implement time for exact signature/query) | — | instrument list — confirms a fund ISIN is a real Upvest instrument | Case A typed | map `Instruments.md` |

⚠ **The `Body` member is optional on every create record but is the actual payload — always set it.**
`required?` on the request records marks only `IdempotencyKey`/path ids as `required`; the `Body` is
`T? = null` and selects nothing for you. Doc-named fields deliberately left out of the TOL user body
(row 1): `MiddleName`, `Salutation`, `Title`, `BirthName`, `PostalAddress`, `BranchId`, `Gender`,
`SocialSecurityNumber`, `Tags` — all optional; include `TermsAndConditions` +
`DataPrivacyAndSharingAgreement` if the shop collects consent (the onboarding workflow treats them as
needed for a non-child user — confirm against `UserTolCreateRequest.cs` before shipping). `Fatca`,
`Nationalities`, `Address` are **required** and have no task field — the app must supply them
(YOUR CALL — not in the map; e.g. FATCA=non-US default, nationality from the task `nationality` field).

### 2b. CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
|---|---|---|
| A fund ISIN written into a model must be a real Upvest instrument | `Portfolios.CreatePortfoliosAllocation`/`UpdatePortfoliosAllocation` (write) ← `Instruments.ListInstruments` (defines legal set) | In the `PUT /api/portfolio/models/{modelId}` handler, validate every `funds[].instrumentId` against `ListInstruments` **before** the allocation write; reject unknown ISINs. The app does not currently read instruments — add that read; it is part of the work. |
| The `modelId` an investor chooses must be one the operator defined | `PUT /api/portfolio/investor/model` → `Portfolios.CreatePortfoliosConfiguration`/`UpdatePortfoliosConfiguration` (write) ← app model store (populated by row 9/10) + `Portfolios.ListPortfoliosAllocations` | In the choose-model handler, look up the app model record by `modelId`, resolve its stored Upvest `allocation_id`; reject if absent. Enforced before the configuration write. |
| A payment's account/allocation must belong to an accepted investor with a chosen model | `POST /api/portfolio/payments` → `TopUps.CreateTopup` + `Portfolios.CreatePortfoliosOrder` ← app investor store (row 7 account id, account ACTIVE) + app chosen-model (row 12) | In the payments handler, require the caller's investor record to be `active` AND have a `PortfoliosConfiguration` (chosen model); refuse (`409/422`) otherwise, before any SDK call. |
| A realignment targets a model that exists | `POST /api/portfolio/models/{modelId}/realignments` → `TriggerPortfolioRebalancing` ← app model store | Resolve `modelId`→Upvest `allocation_id` from the app store before triggering; reject unknown modelId. |

⚠ The map never states these — derived from the task. **Operations** names the call/store that
*defines* the legal set (a list/search or the app's own stored write result), not a single-value
existence probe.

### 2c. Enum tables (only those the scope needs)

Enums are `OpenStringEnum<T>` — use the static member (PascalCase even when the wire value isn't), not
a C# enum literal; resolve unknown wire values with `TryGetKnownValue` + `Match`. Read exact member
lists from each file at implement time; the values the scope branches on:

| Enum (file) | Members the scope uses (wire) |
|---|---|
| `Side15` (`Models/Enums/Side15.cs`) | `Buy` (BUY) — payments always BUY |
| `Currency` (`Models/Enums/Currency.cs`) | `Eur` (EUR) |
| `Type16` (`Models/Enums/Type16.cs`) | `Portfolio` (PORTFOLIO) — account type |
| `InstrumentIdType4` (`Models/Enums/InstrumentIdType4.cs`) | `Isin` (ISIN) default |
| `Status65` (`Models/Enums/Status65.cs`) | NEW, PROCESSING, FILLED, SETTLED, CANCELLED — order status → payment `pending`/`invested`/`failed` |
| `Status29` (`Models/Enums/Status29.cs`) | CONFIRMED, SETTLED, CANCELLED — top-up status |
| `Status` (`Models/Enums/Status.cs`) | ACTIVE, INACTIVE, OFFBOARDING, OFFBOARDED — user status |
| `PriceQuality3` (`Models/Enums/PriceQuality3.cs`) | EOD / HIGHEST_AVAILABLE — valuation query (pick one) |

App status mapping (YOUR CALL — not in the map; `GET` endpoints must translate Upvest → task vocab):
- investor `pending`/`active`/`rejected` ← user `Status` + account status (ACTIVE = ready; a FAILED
  KYC check / closed account = rejected). No single Upvest "rejected" field — derive it.
- payment `pending`/`invested`/`failed` ← order `Status65` (FILLED/SETTLED→invested, CANCELLED→failed,
  else pending) — plus top-up `Status29` for the cash-in leg.
- realignment `pending`/`completed`/`failed` ← execution orders' statuses (B-1).

### 2d. Client construction / auth / server facts

- **Client type / ctor**: `UpvestInvestmentApiClient(HttpClient httpClient,
  UpvestInvestmentApiClientOptions options)` (root ns `UpvestInvestmentApi`). One HttpClient,
  singleton client via `services.AddUpvestInvestmentApiClient(...)` (DI extension) or manual singleton.
  `MUST load dotnet-client-initialization`.
- **OAuth**: `options.OauthClientCredentials = new OAuth2ClientCredentials { ClientId = <Upvest:ClientId>,
  ClientSecret = <Upvest:ClientSecret> }` (ns `UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials`).
  Tokens fetched from `<base>/auth/token`, one per scope set. `MUST load dotnet-authentication`.
- **HTTP message signing — required on EVERY call** (incl. the token request). Set
  `options.Hooks = [ new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(
  keyId: <Upvest:SigningKeyId>, privateKeyPath: <Upvest:SigningKeyPath>, upvestClientId: <Upvest:ClientId>,
  privateKeyPassphrase: <Upvest:SigningKeyPassphrase>)) ]` (ns `UpvestInvestmentApi.Signing`). If no
  hook and no `UPVEST_API_*` env vars, **every call throws `InvalidOperationException` before being
  sent**. No operation takes signature/authorization/upvest-client-id members — the hook sets them.
- **Base URL used verbatim**: `Upvest:BaseUrl` is the base for every call. Keep
  `options.Environment = ServerEnvironment.Production` (its default host is `https://sandbox.upvest.co`)
  and override `options.Server.Default.Production.BaseUrl = <Upvest:BaseUrl>` so the configured value is
  used verbatim. (`ServerEnvironment.Environment2` = `https://api.upvest.co`, live — not used unless
  `Upvest:BaseUrl` points there.) `MUST load dotnet-configuration-resilience`.
- **Config binding** (`Upvest:` section, EXACT keys): `Upvest:ClientId`, `Upvest:ClientSecret`,
  `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:SigningKeyPassphrase`, `Upvest:BaseUrl`.
  `ClientSecret` and `SigningKeyPassphrase` are secrets — never logged, returned, or committed.

---

## 3. Trap notes

- **Step 0 signing**: Upvest refuses any unsigned request (even `/auth/token`); a missing signing key
  surfaces as an `InvalidOperationException` at call time, not an obvious 401 — what makes the hook
  "configured" vs fall through to env vars is non-obvious. `MUST load dotnet-authentication`.
- **Step 0 base URL**: the server-group override point and how `Environment` interacts with a verbatim
  `BaseUrl` is not obvious from the property name. `MUST load dotnet-configuration-resilience`.
- **Step 0 timeouts/retries**: what `RetryOptions.Timeout` bounds vs a whole multi-attempt call, and
  which HTTP methods/status codes the SDK auto-retries (bears on write idempotency, §5 PR-4). `MUST
  load dotnet-configuration-resilience`.
- **All steps — union bodies**: `UserCreateRequest`, `UserCreateRequest1`, `UserGetResponse`,
  `AccountCreateRequest`, `AccountCreateResponse`, `AccountRetrieveResponse`,
  `TriggerPortfolioRebalancingRequest`, `InstrumentId`, `AccountGroupCreateRequest/Response`,
  `UserCheckCreateRequest` are AnyOf unions: build with the arm factory / implicit conversion, read the
  arm with `TryGet…`, and handle the `otherwise`/`TryGetUnknown` arm. Getting the arm wrong compiles
  but fails at runtime. `MUST load dotnet-models`.
- **All steps — reading responses**: response envelopes wrap their payload (`PositionsListResponse.Data`,
  AnyOf `TryGetUserTol`); reads go one level down, and success returns only the body — status/headers
  need a hook. `MUST load dotnet-calling-endpoints`.
- **All writes — error boundary**: a drifted 2xx or a non-2xx body that doesn't match
  `{Operation}Error` throws `ResponseDeserializationException` (an `ApiException`, not
  `ApiException<TError>`); the scope-wall 401s arrive as `ApiException<{Op}Error>` →
  `TryGetNoContent(out RawError)`. `MUST load dotnet-error-handling`.
- **Step 3/4 — async settlement**: top-ups, portfolio orders and rebalancings settle asynchronously at
  Upvest; this app has NO webhook route, so status is learned only by the app's own `Retrieve…`/`List…`
  reads (polling). Do not expect a terminal status in the create response. `MUST load
  dotnet-calling-endpoints`.
- **Step 3 — amount formats differ**: top-up `cash_amount` is `^[0-9]{1,9}(\.[0-9]{2})?$` (two
  decimals) but order `cash_amount` is `^[0-9]{0,63}(\.[0-9]{1,27})?$`; the task's "euros, two
  decimals" must be formatted to each regex. `MUST load dotnet-models`.
- **Step 5 — per-holding value**: `Positions.ListPositions` gives quantity, not monetary value;
  per-holding value comes from `AccountValuation.SecurityPositions`. Joining the two by instrument is
  the app's job. `MUST load dotnet-calling-endpoints`.
- **Testing** the SDK seam (HttpClient stub, faking the clock, constructing SDK exceptions). `MUST
  load dotnet-testing`.

---

## 4. REQUIRED READING (load every one before implementation starts)

| Skill (plugin-qualified) | Step it governs |
|---|---|
| `upvest-v4:dotnet-client-initialization` | Step 0 — client construction, HttpClient lifetime, DI singleton |
| `upvest-v4:dotnet-authentication` | Step 0 — OAuth client-credentials + mandatory HTTP message signing hook |
| `upvest-v4:dotnet-configuration-resilience` | Step 0 — base-URL/server override, retries, timeouts, pagination, logging |
| `upvest-v4:dotnet-models` | Steps 1–5 — AnyOf union bodies, open enums, dates, decimal-string regexes |
| `upvest-v4:dotnet-calling-endpoints` | Steps 1–5 — per-operation request records, response envelopes, polling reads |
| `upvest-v4:dotnet-error-handling` | All writes — Case A typed errors, the `RawError` accessor, the boundary |
| `upvest-v4:dotnet-testing` | Tests — HttpClient stub, clock, constructing SDK exceptions |

**Error-boundary hazard (verbatim):** a body that does not match its declared type — a drifted or
malformed **2xx** response (a missing `required` member) or a **non-2xx** body that does not match its
operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an
`ApiException` that keeps the HTTP status and names the target type but is **not** an
`ApiException<TError>`; a catch ladder that handles only `ApiException<TError>` lets it escape, so it
must also catch `ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
| 1 | **Credential fail-fast** | At DI registration, bind `Upvest:ClientId/ClientSecret/SigningKeyId/SigningKeyPath/SigningKeyPassphrase/BaseUrl` and **throw at startup** if any required part is null/blank (SigningKeyPassphrase MAY be legitimately absent — treat empty as "no passphrase", not an error; the other five are mandatory). Each part checked individually — a blank part is not a missing one. This front-runs the signing hook's `InvalidOperationException` and OAuth `AuthSchemeException` that would otherwise appear on the first call. The check is in app code (config binding), since config — not the SDK's own env vars — supplies the credential. |
| 2 | **Secret sourcing & rotation** | Secrets come from the `Upvest:` config section (secret store / env in prod; never source). DI builds the options object **once** and captures it in the singleton client → a rotated `ClientSecret`/`SigningKeyPassphrase`/key file takes effect only on process restart. Acceptable for this sample; if hot rotation is needed, resolve credentials per-request via an `IOptionsMonitor`-backed hook (not planned). |
| 3 | **Total timeout budget** | `RetryOptions.Timeout` bounds one attempt, not the whole call; the caller's budget = (attempts × per-attempt) + backoff. Decide a concrete `MaxRetries`/`Delay`/`Timeout` in `options.Retry` and additionally pass a request-level `CancellationToken` from the ASP.NET request to bound the whole call. Exact knobs set at Step 0 per `dotnet-configuration-resilience`. |
| 4 | **Write-retry ownership** | The SDK auto-retries only the methods/status codes in `RetryOptions` (default set — confirm in `dotnet-configuration-resilience`). POST writes (CreateUser, CreateTopup, CreatePortfoliosOrder, TriggerPortfolioRebalancing, CreateAccount/Group, SetTaxResidencies) must be safe to resend: each carries a real `IdempotencyKey` (§PR-5), so a resend replays rather than duplicates. Ensure POSTs are retried ONLY when that key is held constant across attempts (same request record instance). |
| 5 | **Idempotency & ambiguous writes** | Real keys ARE present: `IdempotencyKey: Guid` is a required member of CreateUser, SetTaxResidencies, CreateAccountGroup, CreateAccount, CreatePortfoliosAllocation, CreatePortfoliosConfiguration, UpdatePortfoliosConfiguration, CreatePortfoliosOrder, CreateTopup, TriggerPortfolioRebalancing. The app **persists the generated Guid with its own record** and reuses it on retry (replay = original response). CreateIdentifier and CreateUserCheck have NO key member → reconcile via `ListUserIdentifiers` / `ListUserChecks`. The auto-injected `Idempotency-Key` header is NOT a key and is not relied on. |
| 6 | **Observability** | Log at Information: app-level operation + app ids (`investorId`, `paymentId`, `realignmentId`, Upvest resource ids). Log at Warning/Error: the `RawError` status/body from `TryGetNoContent(out RawError)` (`ReadAsString()`), plus any `x-request-id`-style correlation id Upvest returns (reachable via the error `RawError` / a response hook — see PR-7). NEVER log request bodies on user-onboarding/identifier/tax paths (PII). |
| 7 | **Sensitive data** | YES — Flow 1 carries PII (name, birthDate, birthCity, address, nationality, taxId, identity-document number, phone, email). Therefore: `options.Logging.LogRequestBody` stays **off**; `options.Logging.LoggerFactory` is set **explicitly** (leaving it unset arms `UPVEST_INVESTMENT_API_LOG=trace` which forces body logging with no deploy); app diagnostics never echo request bodies on these paths; "Personal details never logged" (task) enforced. Any form-body deny-list keys added per `dotnet-configuration-resilience`. |
| 8 | **Environment selection** | One server group `Default`, two environments: `Production`→`https://sandbox.upvest.co` (sandbox, default), `Environment2`→`https://api.upvest.co` (live). Each deployment sets `Upvest:BaseUrl`, applied verbatim via `options.Server.Default.Production.BaseUrl`, with `Environment=Production`. The sandbox IS a distinct host from live, so test traffic is kept off the live system by `Upvest:BaseUrl`. No other server group is touched. |
| 9 | **Duplicate prevention under concurrency** | See DUPLICATE CLAIMS below — the app store (EF, PublicApi-owned) holds a claim keyed so a second concurrent caller is refused before reaching Upvest. |
| 10 | **Partial results** | See PAGED READS below — `ListPortfoliosAllocations`, `ListPositions`, `ListPortfoliosOrders`, `ListPortfolioRebalancingExecutionOrders` are offset/limit paged with `meta.total_count`. |
| 11 | **Unknown outcomes** | See UNKNOWN OUTCOMES below — each write reconciled via its persisted `IdempotencyKey` replay or a `List…` read by account; connection failures after the provider may have acted are settled, not reported as failure. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
| `POST /api/portfolio/investor` (CreateUser+account chain) | App EF store: `Investor` row keyed by shopper id (ApplicationUser id from JWT) as primary key | Primary-key insert conflict (PK enforced even by in-memory provider) when a second request for the same shopper inserts | `catch` on the `SaveChanges`/`AddAsync` PK violation in the investor-signup handler | TBD |
| `POST /api/portfolio/payments` | App EF store: `Payment` row keyed by app `paymentId` (generated) with a unique `(investorId, clientRequestId)` — clientRequestId supplied/derived per submission | PK/unique-key insert conflict on the claim row written **before** the SDK top-up/order calls | `catch` on the claim-insert conflict in the payments handler | TBD |
| `POST /api/portfolio/models/{modelId}/realignments` | App EF store: `Realignment` row keyed by app `realignmentId`; claim written before TriggerPortfolioRebalancing | PK insert conflict on the claim row | `catch` on claim-insert conflict in the realignment handler | TBD |

`PUT /api/portfolio/models/{modelId}` and `PUT /api/portfolio/investor/model` are idempotent PUT/upserts
keyed by `{modelId}` / caller — not duplicate-sensitive, so no claim row. Order for each row above:
**claim → SDK call → record result**; release the claim if the SDK call is refused.

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
|---|---|---|---|
| `GET /api/portfolio/models` → `ListPortfoliosAllocations` | offset/limit (`Limit`) | Loop pages until `meta.offset+meta.count >= meta.total_count`, or expose `totalCount` + `hasMore` on the response DTO | TBD |
| `GET /api/portfolio` holdings → `ListPositions` | offset/limit | Loop until `meta` exhausted, or surface `totalCount`/`hasMore` in the portfolio DTO's holdings | TBD |
| `GET /api/portfolio/payments` → (if sourced from `ListPortfoliosOrders`/`ListTopups`) | offset/limit | Surface `totalCount`/`hasMore` on the payments DTO | TBD |
| realignment status → `ListPortfolioRebalancingExecutionOrders` | offset/limit | Page all orders before computing the derived status so the status is not computed from a truncated page | TBD |

Primary payment list is the app's own `Payment` store (newest first), enriched with Upvest status via
per-item reads; the Upvest list reads above back the enrichment where needed.

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
|---|---|---|---|---|
| `CreateUser` (investor signup) | replay `CreateUser` with the persisted `IdempotencyKey` (replays original response), else `ListUsers` | persisted `IdempotencyKey` Guid | `catch (SdkConnectionException/SdkTimeoutException)` in signup handler → mark investor `pending`, settle on next `GET` via replay | TBD |
| `SetTaxResidencies` / `CreateAccountGroup` / `CreateAccount` | replay with persisted `IdempotencyKey` | persisted `IdempotencyKey`; account via `ListUserAccounts` by `UserId` | `catch` in signup chain → leave step unsettled, re-drive from persisted key on next poll | TBD |
| `CreateTopup` | replay `CreateTopup` w/ key, else `ListTopups` by `AccountGroupId` | persisted `IdempotencyKey` / `AccountGroupId` | `catch` in payments handler → payment stays `pending`, settled by `RetrieveTopup`/replay | TBD |
| `CreatePortfoliosOrder` | replay `CreatePortfoliosOrder` w/ key, else `ListPortfoliosOrders` by `AccountId` | persisted `IdempotencyKey` / `AccountId` | `catch` in payments handler → payment `pending`, settled by `RetrievePortfoliosOrder`/replay | TBD |
| `TriggerPortfolioRebalancing` | replay w/ key (returns same execution id) | persisted `IdempotencyKey` | `catch` in realignment handler → realignment `pending`, settled by execution-orders read | TBD |
| `CreateIdentifier` | `ListUserIdentifiers` by `UserId` (no key member) | `UserId` + identifier fields | `catch` in signup chain → check list before re-posting | TBD |

Each settles the outcome in the failing write's own `catch` (or records an unknown state the `GET`
poll settles) — never reports a bare failure, never leaves the caller to blind-retry.

---

## 6. Assumptions & Blockers

**Assumptions (intent):**
- **A-1.** "Managed investment portfolio" = Upvest TOL **portfolio allocation + configuration + portfolio
  orders** (not single-instrument trading, savings plans, or pensions). The investor's account is
  `type: PORTFOLIO`.
- **A-2.** Sandbox cash-in uses `TopUps.CreateTopup` (account-group cash credit) as the "whatever Upvest
  provides" mechanism, then `CreatePortfoliosOrder` BUY invests it. The alternative vIBAN +
  `Tests.CreateBankTransaction` simulate path exists (`tests:admin`) but TopUp is the simpler single
  credit; both are scope-walled in this sandbox regardless. If the operator requires the vIBAN
  simulate path specifically, this is a one-line swap in Step 3 — flag for confirmation.
- **A-3.** The task's `paidIn` is the app's own sum of the investor's `invested` payments (euros), not a
  Upvest field; tracked in the app's `Payment` store.
- **A-4.** Required Upvest onboarding fields with no task counterpart (`fatca`, `terms_and_conditions`,
  `data_privacy_and_sharing_agreement`, account-group type) are supplied by the app with sensible
  defaults (FATCA non-US; consent timestamps captured at signup; group `PERSONAL`). Confirm the shop's
  consent-capture story. (YOUR CALL — not in the map.)
- **A-5.** Caller identity (which shopper, admin role) comes from the JWT `ClaimsPrincipal`;
  one-investor-per-shopper keyed by the ApplicationUser id. One shopper never sees another's data
  because every read/write is scoped to the caller's own `Investor` record. (YOUR CALL — not in the
  map; app design, no PublicApi exemplar read for ClaimsPrincipal access — use `HttpContext.User`.)
- **A-6.** Operator (`PUT /models`, `POST …/realignments`) endpoints carry
  `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes
  = JwtBearerDefaults.AuthenticationScheme)]`, matching the CatalogItem admin endpoints.

**Blockers:**
- **B-1 (planning-level, has a workaround — NOT fatal).** `RetrievePortfoliosRebalancingExecution`
  returns `PortfoliosRebalancingExecution`, which carries **no status field** (only `id`, timestamps,
  `accounts?`, `allocations?`). The task's `GET /api/portfolio/realignments/{id}` requires
  `pending/completed/failed`. Resolution: derive the realignment status from
  `ListPortfolioRebalancingExecutionOrders` (the per-order statuses) — pending until all orders
  terminal, completed if all filled/settled, failed if any cancelled/failed. This is a derivation
  decision (recorded in §2c), not a missing capability, so planning proceeds; confirm the exact order
  status enum and terminal set by reading `PortfoliosRebalancingExecutionOrderListResponse.cs` at
  implement time. **UNVERIFIED** against live traffic (scope wall).
- **B-2 (environment, expected — NOT a capability gap).** Per the task, the sandbox client is
  authorised ONLY for user operations. Therefore every call outside `users:*` (all of Flow 2–5, plus
  checks/tax/account creation in Flow 1) WILL be refused `401 "not authorised to use these scopes"` in
  the sandbox. The plan implements them in full and marks them UNVERIFIED; this is the environment, not
  a code defect, and does not block planning or implementation. Operator must grant the remaining scopes
  (`checks:admin`, `taxes:admin`, `accounts:admin`, `portfolios:admin/read`, `topups:admin/read`,
  `payments:admin`, `positions:read`, `valuations:read`) for live verification.

---

## 7. Source labels

Every per-operation row cites its map page (`map/operations/<Controller>.md`) and the declaring source
file(s) under `sdk/dotnet/src/UpvestInvestmentApi/…` — a later lookup is one targeted open. Rows marked
**UNVERIFIED** (scope wall B-2, B-1) can be settled only by live traffic once scopes are granted;
until then, code them defensively per the trap notes and error boundary. Rows marked **YOUR CALL — not
in the map** (status vocab mapping §2c, required-onboarding defaults §2a note / A-4, caller-identity
A-5) are application decisions settled at implement time against the task. Nothing is left for a "later
source lookup": the three request-model shapes referenced by prose-only (`IdentifierCreateRequest`,
`TaxResidenciesSetRequest`, `UserCheckCreateRequest`, `AccountGroupCreateRequest`) are named with their
exact source paths and are opened when their step is implemented.
