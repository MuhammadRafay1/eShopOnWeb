# Upvest Investment API — Integration Plan (eShopOnWeb managed portfolios)

Plan-first artifact for `dotnet-integrate-upvest-investment-api`. Every contract fact below is grounded in the SDK map
(`sdk-map.md` + `map/operations/*.md`) and the named source files inside the plugin SDK at
`sdk/dotnet/` (plugin-relative; this machine: `…/marketplace/plugins/upvest-v4/sdk/dotnet/`). The repo is referenced read-only
except for this file. Target project: `src/PublicApi` (eShopOnWeb, .NET 8, minimal-API endpoints via `MinimalApi.Endpoint`).

---

## 1. Scope & sequence

Additive feature: managed investment portfolios on top of Upvest. All capabilities are JWT-authenticated HTTP endpoints on
`src/PublicApi` under `/api/`, caller identity taken from the token (buyer id / email claim). Catalog, basket, order are untouched.

The shopper↔Upvest link, the model catalog, each payment, and each realignment are stored in a **store this app owns**
(the existing `IRepository<T>` / EF layer, running on the in-memory DB with `UseOnlyInMemoryDatabase=true`; data lives for one run).
The HTTP contract shapes (request/response DTOs) are the app's own; this sheet records only the **Upvest** call surface those
endpoints sit on.

Implementation steps, in order (each names the Upvest operations it uses):

1. **Config + client wiring** — bind `Upvest:` options; register one `UpvestInvestmentApiClient` singleton with OAuth client
   credentials **and** the HTTP message-signing hook; fail fast on any missing credential part. (No operation; see §2 client block.)
2. **Flow 1 — Becoming an investor.**
   - `POST /api/portfolio/investor` → `Users.CreateUser` (body arm `UserTolCreateRequest`), then
     `AccountGroups.CreateAccountGroup` (arm `AccountGroupCreateUserRequest`, type `PERSONAL`), then
     `AccountsApi.CreateAccount` (arm `AccountCreateUserRequest`, **type `PORTFOLIO`** = managed-portfolio-capable account).
     Supporting onboarding calls that move the user toward `ACTIVE` and consume the remaining request fields:
     `UserChecks.CreateUserCheck` (KYC), `TaxResidencies.SetTaxResidencies` (taxId/taxCountry),
     `UserIdentifiers.CreateIdentifier` (identityDocument).
   - `GET /api/portfolio/investor` → `Users.RetrieveUser` (re-read user status) + the app's stored account-ready flag.
3. **Flow 2 — Model portfolios.**
   - `PUT /api/portfolio/models/{modelId}` (OPERATOR) → `Portfolios.CreatePortfoliosAllocation` on first write;
     `Portfolios.UpdatePortfoliosAllocation` on replace (full-replace PUT semantics).
   - `GET /api/portfolio/models` → `Portfolios.ListPortfoliosAllocations`.
   - `PUT /api/portfolio/investor/model` (investor follows a model) → `Portfolios.CreatePortfoliosConfiguration`
     (or `UpdatePortfoliosConfiguration` when switching).
4. **Flow 3 — Paying in.**
   - `POST /api/portfolio/payments` → sandbox money-in: `VirtualBankAccounts.CreateVirtualBankAccount` (once per account group) +
     `Tests.CreateBankTransaction` (credits the account-group cash balance in sandbox); then
     `Portfolios.CreatePortfoliosOrder` (`side = BUY`) which invests the cash across the followed model's weights.
   - `GET /api/portfolio/payments` → app's stored payment rows, each reconciled against
     `Portfolios.RetrievePortfoliosOrder` (and optionally `CreditFundings.ListCreditFundings`).
5. **Flow 4 — Realignment (OPERATOR).**
   - `POST /api/portfolio/models/{modelId}/realignments` → `PortfoliosRebalancing.TriggerPortfolioRebalancing`
     (union arm `Allocations` — realign every account following that allocation/model).
   - `GET /api/portfolio/realignments/{realignmentId}` → `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution`
     (+ `ListPortfolioRebalancingExecutionOrders` to synthesize status — see §6 Blocker B1).
6. **Flow 5 — The portfolio.**
   - `GET /api/portfolio` → `Portfolios.RetrievePortfoliosConfiguration` (modelId = `allocation_id`) +
     `Valuations.GetAccountValuation` (`total_security_value` → totalValue; `security_positions[]` → holdings
     `{instrumentId, quantity, value}`). `paidIn` = app-stored sum of accepted payments.

**Status discipline:** no webhook route is exposed. Every status (sign-up, payment, realignment) is learned by the app's own
re-read calls (`RetrieveUser`, `RetrievePortfoliosOrder`, `RetrievePortfoliosRebalancingExecution`), on demand when a `GET` is served.

**Sandbox reality (not a defect, not a gap to fix):** the sandbox client is authorised **only for user operations**. Every call
outside `users:*` returns `401 "This client is not authorised to use these scopes"`. That is the environment. Build each flow in
full; expect live 401s on everything except the `Users.*` calls (see §6, Blocker B0). Contracts below are fully fleshed regardless.

---

## 2. CONTRACT SHEET

> ⚠ **Signatures below are generated code, copied verbatim from the map.** Each operation that takes input takes **exactly one
> request record** as its first parameter, built with an object initializer using **that record's own property names** — never flat
> positional arguments. An operation with no inputs takes no record.
> ⚠ **Every SDK type is fully-qualified using the namespace its source path implies** (`Models/` → `UpvestInvestmentApi.Models`,
> `Models/AnyOf/` → `UpvestInvestmentApi.Models.AnyOf`, `Models/Enums/` → `UpvestInvestmentApi.Models.Enums`,
> `Requests/<Ctrl>/` → `UpvestInvestmentApi.Requests.<Ctrl>`, `Errors/` → `UpvestInvestmentApi.Errors`). Take each type's namespace
> from the path the map gives for **that** type, not a neighbour.

**Request-record shape is uniform across every write here** (verified on `CreateUserRequest`, `CreateAccountRequest`,
`CreatePortfoliosAllocationRequest`): `IdempotencyKey: Guid` (**required**), `UpvestApiVersion: UpvestApiVersion` (defaulted to
`_1`, leave unset), and a nullable `Body: <PayloadModel>?`. The map lists only `IdempotencyKey` as required, but **the endpoint is
rejected without `Body`** — so `Body` is "optional in the type, required by the call" (the §2 ⚠ case). All writes: set `IdempotencyKey`
= a fresh `Guid` per logical attempt, set `Body`.

### Flow 1 — Becoming an investor

| Operation | Controller · Method signature | Request record + members | Body model + fields (wire) | Response envelope → fields read | Error case + accessors | Pag. | Source |
|---|---|---|---|---|---|---|---|
| CreateUser | `client.Users.CreateUser(CreateUserRequest request, RequestOptions?, CancellationToken)` | `UpvestInvestmentApi.Requests.Users.CreateUserRequest`: `IdempotencyKey: Guid` (req) · `UpvestApiVersion` (def) · `Body: UserCreateRequest?` | `UpvestInvestmentApi.Models.AnyOf.UserCreateRequest` union → build arm `UpvestInvestmentApi.Models.UserTolCreateRequest`: `first_name`(req), `last_name`(req), `email`(req), `birth_date`(req, DateOnly→DateTimeOffset), `birth_city`(opt), `birth_country`(opt, enum `BirthCountry`), `nationalities`(req, `IReadOnlyList<Nationality>`, ISO-3166-α2), `phone_number`(opt), `address`(req, `Address2`), `fatca`(req, `Fatca`), `terms_and_conditions`(opt), `data_privacy_and_sharing_agreement`(opt) | `UpvestInvestmentApi.Models.AnyOf.UserCreateRequest1` → read user `id` (→ store as `investorId`/upvest userId) and `status` | `ApiException<UpvestInvestmentApi.Errors.CreateUserError>` — Case A; `TryGetNoContent(out RawError?)` [400,401,403,406,429,500,503,504]; `TryGetRawError(out RawError?)` | none | map Users.md; `Models/UserTolCreateRequest.cs`; `Models/AnyOf/UserCreateRequest.cs`, `UserCreateRequest1.cs` |
| RetrieveUser | `client.Users.RetrieveUser(RetrieveUserRequest request, …)` | `UpvestInvestmentApi.Requests.Users.RetrieveUserRequest`: `UserId: Guid` (req) | — (GET) | `UpvestInvestmentApi.Models.AnyOf.UserGetResponse` → arm `UpvestInvestmentApi.Models.UserTol`; read `status` (open `Models/UserTol.cs` at impl for exact field + enum) | `ApiException<RetrieveUserError>` — Case A; `TryGetNoContent` [401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map Users.md; `Models/AnyOf/UserGetResponse.cs`; `UserTol.cs` `TBD-open` |
| CreateUserCheck | `client.UserChecks.CreateUserCheck(CreateUserCheckRequest request, …)` | `UpvestInvestmentApi.Requests.UserChecks.CreateUserCheckRequest`: `UserId: Guid` (req) · `Body: UserCheckCreateRequest?` | `UpvestInvestmentApi.Models.AnyOf.UserCheckCreateRequest` (open file at impl for arm/fields) | `UpvestInvestmentApi.Models.UserCheckCreateResponse` → read check `id` | `ApiException<CreateUserCheckError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map UserChecks.md; `Models/AnyOf/UserCheckCreateRequest.cs` `TBD-open` |
| SetTaxResidencies | `client.TaxResidencies.SetTaxResidencies(SetTaxResidenciesRequest request, …)` | `UpvestInvestmentApi.Requests.TaxResidencies.SetTaxResidenciesRequest`: `UserId: Guid` (req) · `IdempotencyKey: Guid` (req) · `Body: TaxResidenciesSetRequest?` | `UpvestInvestmentApi.Models.TaxResidenciesSetRequest` (taxId/taxCountry → open at impl) | `UpvestInvestmentApi.Models.TaxResidencyRecord` | `ApiException<SetTaxResidenciesError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map TaxResidencies.md; `Models/TaxResidenciesSetRequest.cs` `TBD-open` |
| CreateIdentifier | `client.UserIdentifiers.CreateIdentifier(CreateIdentifierRequest request, …)` | `UpvestInvestmentApi.Requests.UserIdentifiers.CreateIdentifierRequest`: `UserId: Guid` (req) · `Body: IdentifierCreateRequest?` | `UpvestInvestmentApi.Models.IdentifierCreateRequest` (identityDocument → open at impl) | `UpvestInvestmentApi.Models.Identifier` → read `id` | `ApiException<CreateIdentifierError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map UserIdentifiers.md; `Models/IdentifierCreateRequest.cs` `TBD-open` |
| CreateAccountGroup | `client.AccountGroups.CreateAccountGroup(CreateAccountGroupRequest request, …)` | `UpvestInvestmentApi.Requests.AccountGroups.CreateAccountGroupRequest`: `IdempotencyKey: Guid` (req) · `Body: AccountGroupCreateRequest?` | `UpvestInvestmentApi.Models.AnyOf.AccountGroupCreateRequest` → arm `UpvestInvestmentApi.Models.AccountGroupCreateUserRequest`: `user_id`(req, Guid), `type`(req, enum `Type13` — use `PERSONAL`), `securities_account_number`(opt) | `UpvestInvestmentApi.Models.AnyOf.AccountGroupCreateResponse` → read account-group `id`, `status` (open at impl) | `ApiException<CreateAccountGroupError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map AccountGroups.md; `Models/AccountGroupCreateUserRequest.cs`; `Models/AnyOf/AccountGroupCreateResponse.cs` `TBD-open` |
| CreateAccount | `client.AccountsApi.CreateAccount(CreateAccountRequest request, …)` | `UpvestInvestmentApi.Requests.AccountsApi.CreateAccountRequest`: `IdempotencyKey: Guid` (req) · `Body: AccountCreateRequest?` | `UpvestInvestmentApi.Models.AnyOf.AccountCreateRequest` → arm `UpvestInvestmentApi.Models.AccountCreateUserRequest`: `user_id`(req, Guid), `account_group_id`(req, Guid), `type`(req, enum `Type16` — use `PORTFOLIO`), `name`(opt) | `UpvestInvestmentApi.Models.AnyOf.AccountCreateResponse` → read account `id` (→ store), `status` (open at impl) | `ApiException<CreateAccountError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map AccountsApi.md; `Models/AccountCreateUserRequest.cs`; `Models/AnyOf/AccountCreateResponse.cs` `TBD-open` |

### Flow 2 — Model portfolios

| Operation | Controller · Method signature | Request record + members | Body model + fields (wire) | Response envelope → fields read | Error case + accessors | Pag. | Source |
|---|---|---|---|---|---|---|---|
| CreatePortfoliosAllocation | `client.Portfolios.CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest request, …)` | `UpvestInvestmentApi.Requests.Portfolios.CreatePortfoliosAllocationRequest`: `IdempotencyKey: Guid` (req) · `Body: PortfoliosAllocationCreateRequest?` | `UpvestInvestmentApi.Models.PortfoliosAllocationCreateRequest`: `name`(opt), `allocation`(**req**, `IReadOnlyList<Allocation>`), `rebalancing_strategy_ids`(opt). `Allocation`: `instrument_id`(req, union `InstrumentId` — ISIN string arm), `instrument_id_type`(def `Isin`, enum `InstrumentIdType4`), `weight`(req, decimal string, regex `100|[0-9]{1,2}(.[0-9]{1,5})?`, **sum = 100**) | `UpvestInvestmentApi.Models.PortfoliosAllocation`: read `id`(→ modelId), `name`, `allocation[]` | `ApiException<CreatePortfoliosAllocationError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map Portfolios.md; `Models/PortfoliosAllocationCreateRequest.cs`; `Models/Allocation.cs`; `Models/AnyOf/InstrumentId.cs`; `Models/PortfoliosAllocation.cs` |
| UpdatePortfoliosAllocation | `client.Portfolios.UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest request, …)` | `UpvestInvestmentApi.Requests.Portfolios.UpdatePortfoliosAllocationRequest`: `AllocationId: Guid` (req) · `Body: PortfoliosAllocationUpdateRequest?` | `UpvestInvestmentApi.Models.PortfoliosAllocationUpdateRequest` — **full replace**: send the complete `allocation[]` (weights sum 100), not a delta (open file at impl for exact fields) | `UpvestInvestmentApi.Models.PortfoliosAllocation`: read `id`, `allocation[]` | `ApiException<UpdatePortfoliosAllocationError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map Portfolios.md; `Models/PortfoliosAllocationUpdateRequest.cs` `TBD-open` |
| ListPortfoliosAllocations | `client.Portfolios.ListPortfoliosAllocations(ListPortfoliosAllocationsRequest request, …)` | `UpvestInvestmentApi.Requests.Portfolios.ListPortfoliosAllocationsRequest`: (all optional) query `Sort`,`Order`,`Offset`,`Limit`,`StartDate`,`EndDate`,`InstrumentIds` | — (GET) | `UpvestInvestmentApi.Models.PortfoliosAllocationsListResponse`: `data: IReadOnlyList<PortfoliosAllocation>` (each `id`,`name`,`allocation[]`), `meta: Meta` (`offset`,`limit`,`count`,`total_count`) | `ApiException<ListPortfoliosAllocationsError>` — Case A; `TryGetNoContent` [400,401,403,404,405,406,429,500,503,504]; `TryGetRawError` | **offset/limit** (query `offset`,`limit`; `meta.total_count`) | map Portfolios.md; `Models/PortfoliosAllocationsListResponse.cs`; `Models/Meta.cs` |
| CreatePortfoliosConfiguration | `client.Portfolios.CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest request, …)` | `UpvestInvestmentApi.Requests.Portfolios.CreatePortfoliosConfigurationRequest`: `IdempotencyKey: Guid` (req) · `Body: PortfoliosConfigurationCreateRequest?` | `UpvestInvestmentApi.Models.PortfoliosConfigurationCreateRequest`: `account_id`(req, Guid), `allocation_id`(req, Guid = chosen modelId), `rebalancing_strategy_ids`(opt) | `UpvestInvestmentApi.Models.PortfoliosConfiguration`: read `account_id`, `allocation_id` | `ApiException<CreatePortfoliosConfigurationError>` — Case A; `TryGetNoContent` [400,401,403,404,406,422,429,500,503,504]; `TryGetRawError` | none | map Portfolios.md; `Models/PortfoliosConfigurationCreateRequest.cs`; `Models/PortfoliosConfiguration.cs` |
| UpdatePortfoliosConfiguration (model switch) | `client.Portfolios.UpdatePortfoliosConfiguration(UpdatePortfoliosConfigurationRequest request, …)` | `UpvestInvestmentApi.Requests.Portfolios.UpdatePortfoliosConfigurationRequest`: `AccountId: Guid` (req) · `IdempotencyKey: Guid` (req) · `Body: PortfoliosConfigurationUpdateRequest?` | `UpvestInvestmentApi.Models.PortfoliosConfigurationUpdateRequest` (optional `allocation_id`; open at impl) | `UpvestInvestmentApi.Models.PortfoliosConfiguration` | `ApiException<UpdatePortfoliosConfigurationError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map Portfolios.md; `Models/PortfoliosConfigurationUpdateRequest.cs` `TBD-open` |

### Flow 3 — Paying in

| Operation | Controller · Method signature | Request record + members | Body model + fields (wire) | Response envelope → fields read | Error case + accessors | Pag. | Source |
|---|---|---|---|---|---|---|---|
| CreateVirtualBankAccount | `client.VirtualBankAccounts.CreateVirtualBankAccount(CreateVirtualBankAccountRequest request, …)` | `UpvestInvestmentApi.Requests.VirtualBankAccounts.CreateVirtualBankAccountRequest`: `IdempotencyKey: Guid` (req) · `Body: PaymentsVirtualBankAccountCreateRequest?` | `UpvestInvestmentApi.Models.PaymentsVirtualBankAccountCreateRequest` (account_group_id; open at impl) | `UpvestInvestmentApi.Models.PaymentsVirtualBankAccountsResponse` → read vIBAN `id` (→ pass to CreateBankTransaction) | `ApiException<CreateVirtualBankAccountError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map VirtualBankAccounts.md; `Models/PaymentsVirtualBankAccountCreateRequest.cs` `TBD-open` |
| CreateBankTransaction (sandbox money-in) | `client.Tests.CreateBankTransaction(CreateBankTransactionRequest request, …)` | `UpvestInvestmentApi.Requests.Tests.CreateBankTransactionRequest`: `IdempotencyKey: Guid` (req) · `Body: PaymentsBankTransactionCreateRequest?` | `UpvestInvestmentApi.Models.PaymentsBankTransactionCreateRequest`: `account`(req, `Account29` — sandbox pooling IBAN `FR7617789000011051089906447`), `amount`(req, decimal string, regex `[0-9]{1,9}(.[0-9]{2})?` — **2dp euros**), `currency`(req, enum `BankTransactionCurrency`), `client_reference`(req, ≤35), `counterparty_name`(req — must equal account-group owner name), `counterparty_account`(req, `CounterpartyAccount`), `counterparty_agent`(opt), `virtual_bank_account_id`(opt, Guid), `remittance_information`(opt), `purpose_code`(opt) | `UpvestInvestmentApi.Models.PaymentsBankTransactionCreateResponse` (open at impl) | `ApiException<CreateBankTransactionError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map Tests.md; `Models/PaymentsBankTransactionCreateRequest.cs` |
| CreatePortfoliosOrder (invest) | `client.Portfolios.CreatePortfoliosOrder(CreatePortfoliosOrderRequest request, …)` | `UpvestInvestmentApi.Requests.Portfolios.CreatePortfoliosOrderRequest`: `IdempotencyKey: Guid` (req) · `Body: PortfoliosOrderPlaceRequest?` | `UpvestInvestmentApi.Models.PortfoliosOrderPlaceRequest`: `user_id`(req, Guid), `account_id`(req, Guid), `cash_amount`(req, decimal string), `currency`(req, enum `Currency` — `EUR`), `side`(req, enum `Side15` — `BUY`), `post_tax`(opt, def false) | `UpvestInvestmentApi.Models.PortfoliosOrder`: read `id`(→ paymentId link), `status`(enum `Status65`: NEW/PROCESSING/FILLED/SETTLED/CANCELLED), `cash_amount`, `orders[]` | `ApiException<CreatePortfoliosOrderError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map Portfolios.md; `Models/PortfoliosOrderPlaceRequest.cs`; `Models/PortfoliosOrder.cs`; `Models/Enums/Status65.cs` |
| RetrievePortfoliosOrder (status re-read) | `client.Portfolios.RetrievePortfoliosOrder(RetrievePortfoliosOrderRequest request, …)` | `UpvestInvestmentApi.Requests.Portfolios.RetrievePortfoliosOrderRequest`: `PortfolioOrderId: Guid` (req) | — (GET) | `UpvestInvestmentApi.Models.PortfoliosOrder`: read `status` (`Status65`) | `ApiException<RetrievePortfoliosOrderError>` — Case A; `TryGetNoContent` [401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map Portfolios.md; `Models/PortfoliosOrder.cs` |
| ListCreditFundings (money-in reconcile) | `client.CreditFundings.ListCreditFundings(ListCreditFundingsRequest request, …)` | `UpvestInvestmentApi.Requests.CreditFundings.ListCreditFundingsRequest`: `AccountGroupId: Guid` (req) · query `Sort`,`Order`,`Limit`,`Offset` | — (GET) | `UpvestInvestmentApi.Models.PaymentsCreditFundingsListResponse`: `data: IReadOnlyList<Datum5>`, `meta: Meta` | `ApiException<ListCreditFundingsError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | **offset/limit** (`meta.total_count`) | map CreditFundings.md; `Models/PaymentsCreditFundingsListResponse.cs`; `Models/Meta.cs` |

### Flow 4 — Realignment

| Operation | Controller · Method signature | Request record + members | Body model + fields (wire) | Response envelope → fields read | Error case + accessors | Pag. | Source |
|---|---|---|---|---|---|---|---|
| TriggerPortfolioRebalancing | `client.PortfoliosRebalancing.TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest request, …)` | `UpvestInvestmentApi.Requests.PortfoliosRebalancing.TriggerPortfolioRebalancingOperationRequest`: `IdempotencyKey: Guid` (req) · `Body: TriggerPortfolioRebalancingRequest?` | `UpvestInvestmentApi.Models.AnyOf.TriggerPortfolioRebalancingRequest` union — **exactly one arm**: `Allocations(Allocations)` (realign all accounts following this model) **OR** `Accounts(Accounts)`; never both. Build via `TriggerPortfolioRebalancingRequest.Allocations(new Allocations{…})` (open `Models/Allocations.cs` at impl for field) | `UpvestInvestmentApi.Models.TriggerPortfolioRebalancingResponse`: read `id`(→ realignmentId) | `ApiException<TriggerPortfolioRebalancingError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map PortfoliosRebalancing.md; `Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`; `Models/TriggerPortfolioRebalancingResponse.cs`; `Models/Allocations.cs` `TBD-open` |
| RetrievePortfoliosRebalancingExecution | `client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution(RetrievePortfoliosRebalancingExecutionRequest request, …)` | `UpvestInvestmentApi.Requests.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecutionRequest`: `ExecutionId: Guid` (req) | — (GET) | `UpvestInvestmentApi.Models.PortfoliosRebalancingExecution`: `id`, `created_at`, `updated_at`, `accounts[]?`, `allocations[]?` — **NO `status` field** (see Blocker B1) | `ApiException<RetrievePortfoliosRebalancingExecutionError>` — Case A; `TryGetNoContent` [401,403,404,405,406,429,500,503,504]; `TryGetRawError` | none | map PortfoliosRebalancing.md; `Models/PortfoliosRebalancingExecution.cs` |
| ListPortfolioRebalancingExecutionOrders (status synth) | `client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(ListPortfolioRebalancingExecutionOrdersRequest request, …)` | `UpvestInvestmentApi.Requests.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrdersRequest`: `ExecutionId: Guid` (req) · query `Sort`,`Order`,`Offset`,`Limit`,`StartDate`,`EndDate` | — (GET) | `UpvestInvestmentApi.Models.PortfoliosRebalancingExecutionOrderListResponse` → per-order status to aggregate (open at impl) | `ApiException<ListPortfolioRebalancingExecutionOrdersError>` — Case A; `TryGetNoContent` [400,401,403,404,405,406,429,500,503,504]; `TryGetRawError` | **offset/limit** | map PortfoliosRebalancing.md; `Models/PortfoliosRebalancingExecutionOrderListResponse.cs` `TBD-open` |

### Flow 5 — The portfolio

| Operation | Controller · Method signature | Request record + members | Body model | Response envelope → fields read | Error case + accessors | Pag. | Source |
|---|---|---|---|---|---|---|---|
| RetrievePortfoliosConfiguration | `client.Portfolios.RetrievePortfoliosConfiguration(RetrievePortfoliosConfigurationRequest request, …)` | `UpvestInvestmentApi.Requests.Portfolios.RetrievePortfoliosConfigurationRequest`: `AccountId: Guid` (req) | — (GET) | `UpvestInvestmentApi.Models.PortfoliosConfiguration`: read `allocation_id`(→ modelId) | `ApiException<RetrievePortfoliosConfigurationError>` — Case A; `TryGetNoContent` [401,403,404,405,406,429,500,503,504]; `TryGetRawError` | none | map Portfolios.md; `Models/PortfoliosConfiguration.cs` |
| GetAccountValuation | `client.Valuations.GetAccountValuation(GetAccountValuationRequest request, …)` | `UpvestInvestmentApi.Requests.Valuations.GetAccountValuationRequest`: `AccountId: Guid` (req) · `PriceQuality` (req, query `price_quality`, enum `PriceQuality3`) | — (GET) | `UpvestInvestmentApi.Models.AccountValuation`: `total_security_value`(`TotalSecurityValue`: `amount`(2dp string), `currency`) → **totalValue**; `security_positions[]`(`AccountValuationSecurityPosition`: `instrument`(`Instrument6`: uuid/isin), `quantity`(string), `value`(`Value?`, nullable) ) → **holdings {instrumentId, quantity, value}** | `ApiException<GetAccountValuationError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | none | map Valuations.md; `Models/AccountValuation.cs`; `Models/TotalSecurityValue.cs`; `Models/AccountValuationSecurityPosition.cs`; `Models/Instrument6.cs`/`Value.cs` `TBD-open` |
| ListPositions (alt. holdings/quantity) | `client.Positions.ListPositions(ListPositionsRequest request, …)` | `UpvestInvestmentApi.Requests.Positions.ListPositionsRequest`: `AccountId: Guid` (req) · query `Offset`,`Limit` | — (GET) | `UpvestInvestmentApi.Models.PositionsListResponse`: `data: IReadOnlyList<Datum1>` (`instrument`(`Instrument`: `uuid`,`isin`), `quantity` — **no per-position value**), `meta: Meta` | `ApiException<ListPositionsError>` — Case A; `TryGetNoContent` [400,401,403,404,406,429,500,503,504]; `TryGetRawError` | **offset/limit** | map Positions.md; `Models/PositionsListResponse.cs`; `Models/Datum1.cs`; `Models/Instrument.cs`; `Models/Meta.cs` |
| ListInstruments (model-fund validation set) | `client.Instruments.ListInstruments(ListInstrumentsRequest request, …)` | `UpvestInvestmentApi.Requests.Instruments.ListInstrumentsRequest`: query `TradingStatus`,`Sort`,`Order`,`Offset`,`Limit` | — (GET) | `UpvestInvestmentApi.Models.InstrumentsListResponse` → set of valid instrument ISINs (open at impl) | `ApiException<ListInstrumentsError>` — Case A; `TryGetNoContent` [400,401,403,406,429,500,503,504]; `TryGetRawError` | **offset/limit** | map Instruments.md; `Models/InstrumentsListResponse.cs` `TBD-open` |

### Client construction / auth / server node

- Constructor (only one): `new UpvestInvestmentApiClient(HttpClient httpClient, UpvestInvestmentApiClientOptions options)`. Register as a
  **singleton** with a dedicated `HttpClient`. DI helper also exists: `services.AddUpvestInvestmentApiClient(options => …)`.
- **OAuth 2.0 client credentials** (one of two required credentials): `options.OauthClientCredentials =
  new UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials { ClientId = Upvest:ClientId,
  ClientSecret = Upvest:ClientSecret }`. Tokens minted from `<BaseUrl>/auth/token`, one per scope set.
- **HTTP message signing** (the SECOND required credential — Upvest refuses every request, incl. the token call, without it):
  `options.Hooks = [ new UpvestInvestmentApi.Signing.UpvestSigningHook(
  UpvestInvestmentApi.Signing.UpvestHttpSignatureCredentials.FromFile(keyId: Upvest:SigningKeyId,
  privateKeyPath: Upvest:SigningKeyPath, upvestClientId: Upvest:ClientId, privateKeyPassphrase: Upvest:SigningKeyPassphrase)) ]`.
  (Do **not** rely on `FromEnvironment()`; bind from the `Upvest:` section in code.) — `dotnet-authentication` governs exact wiring.
- **Base URL**: set `options.Environment = ServerEnvironment.Production` (default) and override the node with
  `options.Server.Default.Production.BaseUrl = Upvest:BaseUrl` so the configured base address is used verbatim for every call.
  (Which environment node to pin is a `YOUR CALL` — see §5 row 8.)
- No operation accepts `signature`, `signature-input`, `authorization`, or `upvest-client-id` members — the hook and OAuth scheme set them.

### Enum value tables (only those the code sets)

| Enum (source) | Value used | Wire |
|---|---|---|
| `Type16` (`Models/Enums/Type16.cs`) — account type | `PORTFOLIO` (managed-portfolio-capable) | `PORTFOLIO` |
| `Type13` (`Models/Enums/Type13.cs`) — account-group type | `PERSONAL` | `PERSONAL` |
| `Side15` (`Models/Enums/Side15.cs`) — order side | `BUY` | `BUY` |
| `Currency` (`Models/Enums/Currency.cs`) | `EUR` | `EUR` |
| `BankTransactionCurrency` (`Models/Enums/BankTransactionCurrency.cs`) | `EUR` | `EUR` |
| `InstrumentIdType4` (`Models/Enums/InstrumentIdType4.cs`) | `Isin` (default) | `ISIN` |
| `Status65` (`Models/Enums/Status65.cs`) — order status | read: New/Processing/Filled/Settled/Cancelled | — |
| `PriceQuality3` (`Models/Enums/PriceQuality3.cs`) — valuation request | open at impl (e.g. `HIGHEST_AVAILABLE`) `TBD-open` | — |

> Enums are **open string enums** (`OpenStringEnum<T>`): use the static PascalCase members (`Side15.Buy`), resolve unknown wire
> values via `TryGetKnownValue`/`Match` `otherwise`. Exact member names per enum = open that enum file at impl (`TBD-open`).

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
|---|---|---|
| The `modelId` an investor chooses to follow must be one the operator actually defined (an allocation this app offers) | `Portfolios.CreatePortfoliosConfiguration` / `UpdatePortfoliosConfiguration` ← `Portfolios.ListPortfoliosAllocations` (and the app's own stored model catalog) | In the `PUT /api/portfolio/investor/model` handler: reject the `modelId` unless it is present in the app's model-catalog store (populated by `PUT /api/portfolio/models/{modelId}` → `CreatePortfoliosAllocation`), before the configuration call. The app **owns** that store; read it in the handler. |
| The `modelId` path param on realignment must be an existing allocation | `PortfoliosRebalancing.TriggerPortfolioRebalancing` ← `Portfolios.ListPortfoliosAllocations` / app model-catalog store | In the `POST /api/portfolio/models/{modelId}/realignments` handler: reject unless `modelId` is a known model before triggering. |
| Each fund `instrumentId` (ISIN) in a model's weights must be a real tradable instrument | `Portfolios.CreatePortfoliosAllocation` / `UpdatePortfoliosAllocation` ← `Instruments.ListInstruments` | In `PUT /api/portfolio/models/{modelId}`: validate each `instrumentId` against the instrument set from `ListInstruments` before writing the allocation. (NB: `instruments:read` is out of the sandbox client's scope → this read 401s in sandbox; keep the check, let it degrade — see B0.) |
| The `account_id` used to follow a model / place an order / value a portfolio must be the caller's own PORTFOLIO account | `CreatePortfoliosConfiguration`, `CreatePortfoliosOrder`, `GetAccountValuation` ← app-stored investor→account mapping (from `CreateAccount`) | Every investor endpoint resolves `account_id` from the caller's stored investor record (keyed by JWT buyer id), never from the request body. |

> These invariants are **not** in the map — derived from the task. The legal set is defined by the operator's model catalog
> (app-owned, seeded by `CreatePortfoliosAllocation`) and by `ListInstruments`; a per-id lookup is not a substitute.

---

## 3. Trap notes

- **Auth is two credentials, not one** (client wiring). OAuth client-credentials AND an HTTP message-signing hook are both required
  on every request, including the token fetch; a missing signing key fails *before* the request is sent with
  `InvalidOperationException`/`AuthSchemeException`, not a 401. What each credential binds to and how the hook is constructed —
  **MUST load `upvest-v4:dotnet-authentication`**.
- **`401 "not authorised to use these scopes"` is the sandbox, not a bug** (every non-`users:*` flow). Distinguishing a scope-denial
  401 from a genuine auth failure, and where `AuthSchemeException` sits vs a server 401 — **MUST load `upvest-v4:dotnet-error-handling`**.
- **Request record vs body** (every write). The real payload rides on `Body`, which the type marks optional though the call needs it;
  the injected `Idempotency-Key` header is **not** a dedup key. What the record's members mean and how the request is built —
  **MUST load `upvest-v4:dotnet-calling-endpoints`**.
- **AnyOf union bodies** (CreateUser, CreateAccount, CreateAccountGroup, TriggerPortfolioRebalancing, InstrumentId). Picking/serializing
  the right arm, and how an unknown arm round-trips — **MUST load `upvest-v4:dotnet-models`**.
- **Open string enums** (`Side15`, `Type16`, `Status65`, `Currency`, …) are not C# enums; wrong member access won't compile, and an
  unexpected wire value lands in `otherwise`. Correct member access and unknown-value handling — **MUST load `upvest-v4:dotnet-models`**.
- **Response envelopes wrap their payload** — list reads go one level into `data`/`meta`; a success returns only the body (status/headers
  are not on the return value). Reading status/headers needs hooks — **MUST load `upvest-v4:dotnet-configuration-resilience`**.
- **What `Timeout` bounds vs the whole-call budget, and which verbs auto-retry** (every write; the money-in + order writes especially).
  What `Timeout` actually bounds and which methods the SDK may resend — **MUST load `upvest-v4:dotnet-configuration-resilience`**.
- **Pagination is offset/limit, capped** (ListPortfoliosAllocations, ListCreditFundings, ListPositions, ListInstruments,
  ListPortfolioRebalancingExecutionOrders). How to walk pages and what signals a cut-short result —
  **MUST load `upvest-v4:dotnet-configuration-resilience`**.
- **Personal data in request bodies** (CreateUser, CreateIdentifier, SetTaxResidencies, CreateBankTransaction). Verbatim body logging
  when `LogRequestBody`/`UPVEST_INVESTMENT_API_LOG=trace` is on — **MUST load `upvest-v4:dotnet-configuration-resilience`**.
- **Stubbing the SDK for tests** (all flows). The `HttpClient` seam and constructing SDK exceptions —
  **MUST load `upvest-v4:dotnet-testing`**.

---

## 4. REQUIRED READING (load all before implementation starts)

| Skill | Governs |
|---|---|
| `upvest-v4:dotnet-authentication` | Client credentials + HTTP message-signing hook wiring (step 1). |
| `upvest-v4:dotnet-client-initialization` | `UpvestInvestmentApiClient` construction, `HttpClient` lifetime, singleton DI registration (step 1). |
| `upvest-v4:dotnet-calling-endpoints` | First call on each controller; request record + `Body`; idempotency key reality (all flows). |
| `upvest-v4:dotnet-models` | AnyOf union bodies, open enums, dates, decimal-string amounts, wire names (all flows). |
| `upvest-v4:dotnet-error-handling` | try/catch design; Case A `ApiException<{Op}Error>` + `TryGetNoContent`/`TryGetRawError`; scope-denial 401s (all flows). |
| `upvest-v4:dotnet-configuration-resilience` | Retries, timeout budget, base-URL node, offset/limit pagination, logging/sensitive-data posture (all flows). |
| `upvest-v4:dotnet-testing` | SDK `HttpClient` stub seam, fake clock, asserting outgoing requests (tests). |

**Mandatory hazard (verbatim):** A body that does not match its declared type — a drifted or malformed **2xx** response (a missing
`required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as
`ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an
`ApiException<TError>`. A catch ladder that handles only `ApiException<TError>` lets it escape, so it must also catch
`ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
|---|---|---|
| 1 | **Credential fail-fast** | At startup (DI registration in `src/Infrastructure/Dependencies.cs` or `Program.cs`), bind `Upvest:` and refuse to start when any of `ClientId`, `ClientSecret`, `SigningKeyId`, `SigningKeyPath`, `BaseUrl` is missing/blank (and verify the key file at `SigningKeyPath` is readable). `SigningKeyPassphrase` may be legitimately empty (unencrypted key) — treat blank as "no passphrase", not missing. Each of the 2-part OAuth credential and the 3-part signing credential is checked separately. **Settled:** `UpvestSettingsValidator : IValidateOptions<UpvestSettings>` + `.ValidateOnStart()`. |
| 2 | **Secret sourcing & rotation** | Secrets come from the `Upvest:` config section (env vars `UPVEST_CLIENT_SECRET`, `UPVEST_SIGNING_KEY_PASSPHRASE`, etc. via `AddEnvironmentVariables`, already called in `Program.cs`). The options object is built **once at singleton registration** and captured; a rotated secret/key takes effect only on process restart. No hot-rotation is required for this feature — documented, not implemented. |
| 3 | **Total timeout budget** | A caller's budget = per-attempt `RetryOptions.Timeout` × (1 + `MaxRetries`) + backoff. Decide one overall budget for the investor-facing GETs and a separate one for the money-in/order writes, enforced on `options.Retry` + an outer `CancellationToken` from the ASP.NET request. Exact meaning of `Timeout` confirmed via `dotnet-configuration-resilience` before setting numbers. `TBD` final numbers at impl. |
| 4 | **Write-retry ownership** | The SDK auto-retries only the verbs/status codes in `RetryOptions.HttpMethodsToRetry`/`StatusCodesToRetry` (default excludes POST). Confirm which of the POSTs (CreateUser, CreateAccount(Group), CreatePortfoliosOrder, CreateBankTransaction, TriggerPortfolioRebalancing) the SDK may resend via `dotnet-configuration-resilience`; keep non-idempotent writes off the resend list. Reconcile via §UNKNOWN OUTCOMES. `TBD` confirm at impl. |
| 5 | **Idempotency & ambiguous writes** | Each write's request record carries a **real** caller-supplied `IdempotencyKey: Guid`. Store the generated key on the app's record **before** the call, reuse it verbatim on a retry of the *same* logical action (so Upvest replays rather than duplicates); use a fresh key for a genuinely new action. On an ambiguous outcome, reconcile via the re-read in §UNKNOWN OUTCOMES. The injected `Idempotency-Key` header on records lacking the member is **not** this key. |
| 6 | **Observability** | Log at Information: endpoint entry, Upvest operation name, resulting resource id (userId/accountId/orderId/executionId), and the status read. Log at Warning/Error: `ApiException` status + the `RawError`/`TryGetNoContent` body text and any provider request/correlation id it carries. Never log request bodies (row 7). Correlation id from the error body flows into the app's `IAppLogger` scope. |
| 7 | **Sensitive data** | **Yes** — CreateUser/CreateIdentifier/SetTaxResidencies/CreateBankTransaction bodies carry name, email, birth data, tax id, identity-document number, IBANs. Posture: `options.Logging.LogRequestBody` stays **off**; `options.Logging.LoggerFactory` set explicitly in production (never leave `UPVEST_INVESTMENT_API_LOG` to arm `trace` body logging); the app's own endpoint logs echo **no** request body on these paths. Personal details are never logged anywhere (task rule). |
| 8 | **Environment selection** | One server group `Default`. `ServerEnvironment.Production` → `https://sandbox.upvest.co`; `Environment2` → `https://api.upvest.co`. This feature runs against **sandbox**; `Upvest:BaseUrl` is set verbatim via `options.Server.Default.Production.BaseUrl`. **Settled:** keep `Environment=Production` and override only its node's `BaseUrl` (BaseUrl is used verbatim). `Tests.CreateBankTransaction` is reached only on the money-in path, which requires an account the sandbox cannot provision, so it never runs against live here. |
| 9 | **Duplicate prevention under concurrency** | See **DUPLICATE CLAIMS**. The app owns an EF/in-memory store; a unique/primary key on (buyer id) for the investor record and on the app-side payment id prevents a second concurrent claim. |
| 10 | **Partial results** | See **PAGED READS**. |
| 11 | **Unknown outcomes** | See **UNKNOWN OUTCOMES**. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
|---|---|---|---|---|
| Investor sign-up (`CreateUser`+`CreateAccountGroup`+`CreateAccount`) | App EF/in-memory store: investor row keyed by **buyer id (JWT)** as primary key (`InvestorConfiguration.HasKey(BuyerId)`) | Store refuses the second insert of the same buyer-id primary key | `catch (ArgumentException/InvalidOperationException)` in `BecomeInvestorEndpoint.HandleAsync` | `BecomeInvestorEndpoint.HandleAsync`: `_investors.AddAsync(investor)` (claim) → `_service.ProvisionInvestorAsync(...)` (CreateUser) |
| Pay-in (`CreatePortfoliosOrder` + sandbox money-in) | App store: payment row keyed by app-generated **PaymentId** PK (`PaymentConfiguration.HasKey(PaymentId)`), written before the SDK calls | Primary-key insert of the same PaymentId is refused | `catch` in `PayInEndpoint.HandleAsync` | `PayInEndpoint.HandleAsync`: `_payments.AddAsync(payment)` (claim) → `_service.PayInAsync(payIn)` |
| Model write (`CreatePortfoliosAllocation`/`Update`) | App store: model row keyed by **ModelId** (path param) PK (`ModelPortfolioConfiguration.HasKey(ModelId)`) | PUT is upsert by ModelId — `GetByIdAsync` chooses create vs. Replace; a concurrent duplicate create is refused by the PK | `AddAsync`/`UpdateAsync` branch in `UpsertModelEndpoint.HandleAsync` | `UpsertModelEndpoint.HandleAsync`: `_service.UpsertModelAllocationAsync(...)` then `_models.AddAsync`/`UpdateAsync(existing)` |
| Follow-model (`CreatePortfoliosConfiguration`/`Update`) | App store: the single investor row (keyed by buyer id) carries `FollowedModelId`/`PortfolioConfigurationExists` | Create vs. Update chosen from `PortfolioConfigurationExists` on the one investor row | `ChooseModelEndpoint.HandleAsync` | `ChooseModelEndpoint.HandleAsync`: `_service.FollowModelAsync(...)` then `investor.FollowModel(...)`/`_investors.UpdateAsync` |
| Realignment trigger (`TriggerPortfolioRebalancing`) | App store: realignment row keyed by app-generated **RealignmentId** PK (`RealignmentConfiguration.HasKey(RealignmentId)`) before the call | Primary-key insert refused | `catch` in `TriggerRealignmentEndpoint.HandleAsync` | `TriggerRealignmentEndpoint.HandleAsync`: `_realignments.AddAsync(realignment)` (claim) → `_service.TriggerRealignmentAsync(...)` |

> Claim → SDK call → record result, in that order, in each handler. Store = the app's existing repository (in-memory DB this run);
> rely on a **primary key** (refused even by in-memory stores), not a read-before-write.

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
|---|---|---|---|
| `ListInstruments` (fund validation) | `limit` (100) + a `maxPages` cap (100) | Not surfaced to the HTTP caller — a read used only internally to validate ISINs; the loop walks until `offset >= meta.total_count` or the cap, then compares. The caller sees a `400` listing any unknown ISIN, never a truncated list. | `UpvestInvestmentService.FindUnknownInstrumentsAsync` (offset/`meta.TotalCount` loop, `maxPages` bound) |
| `ListPortfolioRebalancingExecutionOrders` (status synth) | `limit` (100) + `maxPages` cap (100) | Not surfaced as a list — aggregated into the realignment `status` the caller reads; the loop walks to `meta.total_count` or the cap. | `UpvestInvestmentService.GetRealignmentStatusAsync` (offset/`meta.TotalCount` loop) |
| `ListPortfoliosAllocations` | n/a — **not used**. The model catalog is app-owned (`GET /api/portfolio/models` reads the app store), so no Upvest list is paged for it. | n/a | n/a |
| `ListCreditFundings` / `ListPositions` | n/a — **not used** (money-in reconcile is via the order re-read; holdings come from `GetAccountValuation`, which is not paged). | n/a | n/a |

> A log line is not an answer — the portfolio/models DTO exposes the total/`truncated` flag so the HTTP caller sees it.

### UNKNOWN OUTCOMES

| Write | Re-read operation | Reference searched by | Where in the code | Test that fails the connection |
|---|---|---|---|---|
| `CreateUser` | `Users.RetrieveUser` | the stored `UpvestUserId`, or replay with the stored `SignupIdempotencyKey` | On a connection/timeout failure the SDK call's own `catch` in `UpvestInvestmentService.CallAsync` throws `UpvestFailureKind.Unknown`; the investor **claim row stays** with no `UpvestUserId`, so the next `POST /api/portfolio/investor` takes the reconcile branch in `BecomeInvestorEndpoint.HandleAsync` (existing row, `UpvestUserId == null`) and re-runs provisioning with the **same stored idempotency key** → Upvest replays rather than duplicates. | `BecomeInvestorEndpoint.HandleAsync` reconcile branch + `Investor.SignupIdempotencyKey` | not automated (requires injecting a transport fault into the signed SDK client; verified by design + live happy-path) |
| `CreatePortfoliosOrder` (+ money-in) | `Portfolios.RetrievePortfoliosOrder` | stored `UpvestOrderId` (set after the order returns); the payment row otherwise stays pending | `PayInEndpoint.HandleAsync` `catch (UpvestIntegrationException Unknown)` leaves the payment **pending** and returns `202`; `ListPaymentsEndpoint.HandleAsync` re-reads any pending order's status (`GetOrderStatusAsync`) and settles it | `PayInEndpoint.HandleAsync` (Unknown branch) + `ListPaymentsEndpoint.HandleAsync` (reconcile) | not automated (same reason) |
| `TriggerPortfolioRebalancing` | `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` + `ListPortfolioRebalancingExecutionOrders` | stored `UpvestExecutionId` | `TriggerRealignmentEndpoint.HandleAsync` `catch (Unknown)` leaves the realignment **pending** and returns `202`; `GetRealignmentEndpoint.HandleAsync` re-reads and settles via `GetRealignmentStatusAsync` | `TriggerRealignmentEndpoint.HandleAsync` (Unknown branch) + `GetRealignmentEndpoint.HandleAsync` | not automated (same reason) |
| `CreateAccountGroup` / `CreateAccount` / `CreatePortfoliosConfiguration` / `CreateBankTransaction` | re-attempted on next call | these are reached only once an account is provisioned (not possible under the sandbox client's scopes); their reconcile is the same claim-row + idempotency-key pattern as above | handled by the enclosing endpoint's claim row | not reachable in this environment |

> Each write's own `catch (SdkConnectionException/SdkTimeoutException)` re-reads by the reference above and settles the record
> (or marks it unknown for a later settle), rather than reporting a flat failure or leaving the caller to retry blindly.

---

## 6. Assumptions & Blockers

**Assumptions (intent):**
- **A1 — TOL operating model.** CreateUser uses the `UserTolCreateRequest` arm (Upvest holds the licence; onboarding README confirms
  the TOL flow). If eShop is BYOL, swap to `UserByolCreateRequest` — different required fields.
- **A2 — "Model portfolio" = Upvest portfolio allocation; "following a model" = portfolio configuration** linking the account to the
  allocation. Grounded in the investment-solutions workflow + Portfolios operations. The operator's `modelId` is stored in the app and
  mapped to the Upvest `allocation_id` returned by `CreatePortfoliosAllocation` (they need not be equal; the app keeps the mapping).
- **A3 — Investing a payment per model weights = a `CreatePortfoliosOrder` BUY** after cash is credited; Upvest distributes the cash
  across the followed allocation's weights asynchronously (`status` NEW→PROCESSING→FILLED→SETTLED). The app records `paymentId` and
  reconciles via `RetrievePortfoliosOrder`.
- **A4 — Sandbox money-in = vIBAN + `Tests.CreateBankTransaction`.** The payments workflow states that in sandbox money reaches an
  account group by simulating a bank transfer to a virtual bank account (fixed pooling IBAN `FR7617789000011051089906447`,
  `counterparty_name` = account-group owner). No real money moves.
- **A5 — "status: pending→active, or rejected" maps onto Upvest's user lifecycle** (onboarding README: user starts `INACTIVE`, becomes
  `ACTIVE` once checks pass). The app translates Upvest's user/account status into the task's `{pending, active, rejected}`; the exact
  Upvest status enum is read from `UserTol.cs`/`AccountRetrieveResponse` at impl. "account ready" = the PORTFOLIO account reaching
  Upvest's active status.
- **A6 — `paidIn` is app-maintained** (sum of accepted payments), as no single Upvest read returns it for a managed portfolio;
  `GET /api/portfolio` composes modelId (config), holdings + totalValue (valuation), paidIn (app store).
- **A7 — amounts are 2-dp euro JSON numbers at the app boundary**, converted to Upvest's **decimal-string** amount fields
  (`cash_amount`, `amount`) and `currency = EUR`; responses' decimal-string amounts are converted back to 2-dp numbers.
- **A8 — persistence is the existing `IRepository<T>`/EF layer on the in-memory DB** (`UseOnlyInMemoryDatabase=true`), per-host,
  single-run lifetime; new portfolio entities are added there. (Exemplar reviewed: endpoints via `IEndpoint<…>` as in
  `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`; JWT `[Authorize(Roles=…)]` with `ADMINISTRATORS` for OPERATOR.)

**Blockers (both resolved during implementation — headless, decided and proceeded):**
- **B0 — RESOLVED (stated environment, not a defect).** Live verification confirmed the sandbox client is granted **`users:admin`
  only** — not even `users:read` (so `RetrieveUser`, which requests `users:read users:admin`, failed to mint a token until the fix
  below), and every account/tax/portfolio/payment/valuation scope returns `401 "This client is not authorised to use these scopes"`.
  Decisions: (1) every flow is built in full; (2) a scope-denial `401`/`403` or `AuthSchemeException` is classed `ProviderRejectedUs`
  and the calling endpoint **degrades** — the investor/model/payment/realignment record persists app-side and the live-only parts are
  skipped (logged), so Flows 2–5 are drivable end-to-end through PublicApi even though their Upvest writes 401 live; (3) added an
  **optional** `Upvest:Scope` setting (bound in `UpvestServiceCollectionExtensions`, set to `users:admin` in sandbox user-secrets) that,
  when present, requests every token with that single scope — this makes `RetrieveUser` succeed live (admin token reads the user) while a
  fully-scoped production deployment leaves it unset so each operation requests its own scopes. **Verified live:** `CreateUser` +
  `RetrieveUser` round-trip against `https://sandbox.upvest.co`.
- **B1 — RESOLVED by the planned synthesis.** Realignment status is synthesised from `ListPortfolioRebalancingExecutionOrders`:
  the order status field is `Status71` (`NEW/PROCESSING/FILLED/CANCELLED`, confirmed in source). Any `CANCELLED` ⇒ `failed`; orders
  present and all `FILLED` ⇒ `completed`; otherwise `pending`. Implemented in `UpvestInvestmentService.GetRealignmentStatusAsync`.

**Decisions taken during implementation (YOUR CALL — settled; none left open):**
- **CreateUserCheck dropped from provisioning.** The KYC check body (`UserCheckKnowYourCustomerCreateRequest`) requires a KYC evidence
  download link, provider and method that the shop's sign-up form does not carry; fabricating KYC evidence would be wrong. The identity
  document maps cleanly to `UserIdentifiers.CreateIdentifier` (which also runs live under `users:admin`), and taxId/taxCountry to
  `TaxResidencies.SetTaxResidencies`. So the form is fully consumed without a synthetic KYC check.
- **Consents omitted from `CreateUser`.** `terms_and_conditions`/`data_privacy_and_sharing_agreement` are optional (api.json has a
  "TOL user without consents" example); the reference's sandbox consent-document UUIDs are rejected by this tenant
  (`invalid data privacy document uuid`). Omitting them is valid; in production the shop supplies its own consent document IDs.
- **Supplementary onboarding steps (identifier, tax) are best-effort.** `CreateUser` is the critical path; a failure of the identifier
  or tax submission (a refused scope, or provider validation such as DE's CONCAT identifier-standard requiring an empty value) is logged
  and does not fail the sign-up (`UpvestInvestmentService.TrySupportingStepAsync`).
- **Fail-fast (§5 row 1):** implemented as `UpvestSettingsValidator : IValidateOptions<UpvestSettings>` + `.ValidateOnStart()` in
  `UpvestServiceCollectionExtensions`; checks each OAuth and signing part separately and that the key file exists; passphrase may be blank.
- **Environment (§5 row 8):** `Environment = ServerEnvironment.Production` with `Server.Default.Production.BaseUrl = Upvest:BaseUrl`
  (verbatim); `Tests.CreateBankTransaction` is reached only on the money-in path, which requires an account the sandbox cannot provision.
- **Test fixtures:** made-up EEA (DE) investor — the sandbox rejects a non-EEA (e.g. GB) nationality and validates DE postcodes, so tests
  use a Berlin address (`10115`). No real person's details.

---

## 7. Source labels

- Map pages: `map/operations/{Users,UserChecks,TaxResidencies,UserIdentifiers,AccountGroups,AccountsApi,Portfolios,`
  `PortfoliosRebalancing,VirtualBankAccounts,Tests,CreditFundings,Positions,Valuations,Instruments}.md`; index `sdk-map.md`.
- Source files read this session (shapes verbatim): `Models/PortfoliosAllocationCreateRequest.cs`, `PortfoliosAllocation.cs`,
  `Allocation.cs`, `AnyOf/InstrumentId.cs`, `PortfoliosConfigurationCreateRequest.cs`, `PortfoliosConfiguration.cs`,
  `PortfoliosAllocationsListResponse.cs`, `Meta.cs`, `PortfoliosOrderPlaceRequest.cs`, `PortfoliosOrder.cs`, `Enums/Status65.cs`,
  `AnyOf/TriggerPortfolioRebalancingRequest.cs`, `TriggerPortfolioRebalancingResponse.cs`, `PortfoliosRebalancingExecution.cs`,
  `PositionsListResponse.cs`, `Datum1.cs`, `Instrument.cs`, `AccountValuation.cs`, `TotalSecurityValue.cs`,
  `AccountValuationSecurityPosition.cs`, `PaymentsBankTransactionCreateRequest.cs`, `PaymentsCreditFundingsListResponse.cs`,
  `AnyOf/UserCreateRequest.cs`, `UserTolCreateRequest.cs`, `AnyOf/UserGetResponse.cs`, `AnyOf/AccountCreateRequest.cs`,
  `AccountCreateUserRequest.cs`, `AnyOf/AccountGroupCreateRequest.cs`, `AccountGroupCreateUserRequest.cs`, and request records
  `Requests/Portfolios/CreatePortfoliosAllocationRequest.cs`, `Requests/Users/CreateUserRequest.cs`,
  `Requests/AccountsApi/CreateAccountRequest.cs`.
- `TBD-open`: cells marked so name the exact source file to open at implementation for the remaining field/enum/arm detail
  (response AnyOf arms `UserTol.cs`/`AccountCreateResponse.cs`/`AccountGroupCreateResponse.cs`, bodies
  `TaxResidenciesSetRequest.cs`/`IdentifierCreateRequest.cs`/`UserCheckCreateRequest.cs`/`PaymentsVirtualBankAccountCreateRequest.cs`/
  `PortfoliosAllocationUpdateRequest.cs`/`PortfoliosConfigurationUpdateRequest.cs`/`Allocations.cs`, valuation `Instrument6.cs`/`Value.cs`,
  `InstrumentsListResponse.cs`, `PortfoliosRebalancingExecutionOrderListResponse.cs`, enum members). These are lookups, not open
  contract questions — each is a one-file targeted open.
- `YOUR CALL — not in the map`: §5 row 1 (where fail-fast throws), row 8 (environment-node pinning + keeping `Tests.*` off live),
  and all `TBD` cells in DUPLICATE CLAIMS / PAGED READS / UNKNOWN OUTCOMES (settled once code compiles).
- `UNVERIFIED` (only live traffic settles): the exact Upvest user/account **status enum values** that mark "accepted"/"rejected"
  (A5) — defensively map via open-enum `Match` `otherwise`; and whether `Tests.CreateBankTransaction` alone credits the cash balance
  usable by `CreatePortfoliosOrder` in sandbox (A4) — both unverifiable here because the sandbox client cannot exercise those scopes (B0).
