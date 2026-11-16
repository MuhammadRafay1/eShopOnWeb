# Upvest Investment API integration plan — eShopOnWeb managed investment portfolios

Scope: add a managed-investment-portfolio capability to eShopOnWeb, with Upvest as the provider, exposed on `src/PublicApi` (JWT, minimal-API `IEndpoint` convention). Every Upvest interaction goes through the **upvest-v4 .NET SDK** (`UpvestInvestmentApi`, root client `UpvestInvestmentApiClient`). This file is written by the planner; the main agent implements from it.

All SDK facts below were read this session from the SDK map (`sdk-map.md` + `map/operations/*`) and the source files it names, inside the plugin SDK at `plugins/upvest-v4/sdk/dotnet/`. Upvest's own orchestration order was cross-checked against `upvest-workflows/{user-onboarding,investment-solutions,payments}`.

---

## 0. The domain ↔ Upvest mapping (read this first)

| eShopOnWeb concept | Upvest concept | Upvest controller |
| --- | --- | --- |
| Investor (a shopper signed up) | **User** (TOL) + an **Account Group** + a **PORTFOLIO Account** | `Users`, `AccountGroups`, `AccountsApi` |
| Model portfolio (name + funds with target weights) | **Portfolio allocation** (`name` + `allocation[]` of `{instrument_id, instrument_id_type, weight}`, weights sum to 100) | `Portfolios` (allocations) |
| "Investor follows model M" | **Portfolio configuration** (links one `account_id` → one `allocation_id`) | `Portfolios` (configurations) |
| Pay-in → money credited, then invested across the model | **fund cash** (sandbox: vIBAN + simulated bank transfer, or top-up) **then** a **portfolio BUY order** (`cash_amount` distributed across the account's allocation weights) | `VirtualBankAccounts`+`Tests` (or `TopUps`), then `Portfolios` (orders) |
| Realignment of everyone on a model | **Portfolio rebalancing execution** targeting that **allocation** | `PortfoliosRebalancing` |
| "What the portfolio holds and is worth" | **Account valuation** (`total_security_value` + `security_positions[]`) | `Valuations` (fallback: `Positions`) |

Key consequence: Upvest has **no "model id" of its own** that matches the task's `modelId` path value, and **no first-class "payment"** object. The application owns the `modelId → allocation_id` map and owns the payment record (`paymentId = portfolio order id`). These are recorded as persisted application state below, not invented Upvest calls.

The application exposes **no webhook route** (task constraint). Upvest's async outcomes (order fill, rebalancing, account-group activation, user activation) are therefore learned by **re-reading** (`Retrieve…`/`List…`) on each GET — never by waiting on a callback.

---

## 1. Scope & sequence (implementation steps, each naming the operations it uses)

**Step 0 — Client, auth, config, persistence wiring.**
- Register `UpvestInvestmentApiClient` (singleton, built from `UpvestInvestmentApiClientOptions`) in `src/PublicApi` DI. Bind from the `Upvest:` config section: `Upvest:ClientId`, `Upvest:ClientSecret`, `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:SigningKeyPassphrase` (optional), `Upvest:BaseUrl`. Set `OauthClientCredentials` (ClientId/ClientSecret) and `options.Hooks = [new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(keyId, privateKeyPath, upvestClientId, passphrase))]`. `BaseUrl` is applied verbatim via `options.Server.Default.{env}.BaseUrl`. Fail-fast startup check (§5 row 1). `MUST load dotnet-client-initialization`, `MUST load dotnet-authentication`, `MUST load dotnet-configuration-resilience`.
- Add persisted application entities (EF Core, via the existing `IRepository<T>`/`EfRepository<T>` convention — exemplar `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`; entities live under `src/ApplicationCore/Entities`, a `DbContext` under `src/Infrastructure/Data`):
  - `InvestorRecord { Shopper (PK, = User.Identity.Name), UpvestUserId:Guid, UpvestAccountGroupId:Guid?, UpvestAccountId:Guid?, SignupStatus, ChosenModelId? }`
  - `ModelPortfolioRecord { ModelId (PK, path value), AllocationId:Guid, Name, FundsJson }`
  - `PaymentRecord { PaymentId (PK, = portfolio order Guid), Shopper, Amount:decimal, CreatedAt }`
  - `RealignmentRecord { RealignmentId (PK, = execution Guid), ModelId, CreatedAt }` (optional; GET realignment can also hit Upvest directly by id)

**Step 1 — Flow 1 Becoming an investor.** `POST /api/portfolio/investor`, `GET /api/portfolio/investor`.
Sequence (per Upvest user-onboarding workflow): `Users.CreateUser` (TOL body) → `UserChecks.CreateUserCheck` (KYC incl. the identity document) → `TaxResidencies.SetTaxResidencies` (taxId/taxCountry) → `AccountGroups.CreateAccountGroup` (user arm) → `AccountsApi.CreateAccount` (type **PORTFOLIO**). Status read: `Users.RetrieveUser` (user status) + `AccountGroups.RetrieveAccountGroup` / `AccountGroups.ListUserAccountGroups` (account-group `PENDING_APPROVAL → ACTIVE`). `MUST load dotnet-models`, `MUST load dotnet-error-handling`.

**Step 2 — Flow 2 Model portfolios.** `PUT /api/portfolio/models/{modelId}` (admin) → first time `Portfolios.CreatePortfoliosAllocation`, replace `Portfolios.UpdatePortfoliosAllocation` (full replace). `GET /api/portfolio/models` → `Portfolios.ListPortfoliosAllocations`. `PUT /api/portfolio/investor/model` → first time `Portfolios.CreatePortfoliosConfiguration`, switch `Portfolios.UpdatePortfoliosConfiguration`. ISIN validation → `Instruments.ListInstruments` / `Instruments.RetrieveInstrument` (cross-op invariant).

**Step 3 — Flow 3 Paying in.** `POST /api/portfolio/payments` → (fund cash in sandbox: `VirtualBankAccounts.CreateVirtualBankAccount` once + `Tests.CreateBankTransaction` to simulate the incoming SEPA credit; alternatively `TopUps.CreateTopup`) → `Portfolios.CreatePortfoliosOrder` (side BUY, cash_amount = amount, currency EUR). Refuse if the caller is not an accepted investor with a model chosen. `GET /api/portfolio/payments` → own `PaymentRecord`s (newest first) with status re-read via `Portfolios.RetrievePortfoliosOrder` / `Portfolios.ListPortfoliosOrders`.

**Step 4 — Flow 4 Realignment.** `POST /api/portfolio/models/{modelId}/realignments` (admin) → `PortfoliosRebalancing.TriggerPortfolioRebalancing` with the **Allocations** arm (`allocations:[allocation_id]`). `GET /api/portfolio/realignments/{realignmentId}` → `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` **plus** `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (status is derived — see Trap notes).

**Step 5 — Flow 5 The portfolio.** `GET /api/portfolio` → `Portfolios.RetrievePortfoliosConfiguration` (account → allocation_id → our modelId) + `Valuations.GetAccountValuation` (holdings + totalValue). `paidIn` = sum of own `PaymentRecord`s.

---

## 2. CONTRACT SHEET

> ⚠ **Signatures below are generated code, verbatim.** Each operation that takes input takes **ONE request record as its first parameter**, built with an object initializer whose property names are the record's own (never flat positional arguments). An operation with no inputs takes no request record.
> ⚠ **Every SDK type must be written fully-qualified with the namespace its source path implies**, taken from the path given for THAT type (`Models/` → `UpvestInvestmentApi.Models`; `Models/Enums/` → `UpvestInvestmentApi.Models.Enums`; `Models/AnyOf/` → `UpvestInvestmentApi.Models.AnyOf`; `Requests/<Ctrl>/` → `UpvestInvestmentApi.Requests.<Ctrl>`; `Errors/` → `UpvestInvestmentApi.Errors`). Never copy a neighbour's namespace.

**Every operation in scope is Case A (typed).** Each error type exposes `TryGetNoContent([NotNullWhen(true)] out RawError? value)` for its declared error statuses plus `TryGetRawError([NotNullWhen(true)] out RawError? error)` fallback. `RawError.StatusCode / ReadAsString() / ReadAsJson<T>()`. No operation in scope has a no-throw variant; none is paginated except where a **Pagination** note appears. All writes take a `required Guid IdempotencyKey` on their request record, and an optional `UpvestApiVersion UpvestApiVersion` (defaults `UpvestApiVersion._1`).

### Flow 1 — investor onboarding

| Operation (controller.Method) | Request record + required members | Body model + fields read/written | Response + inner fields read | Error (accessors) | Source |
| --- | --- | --- | --- | --- | --- |
| `Users.CreateUser` | `CreateUserRequest` { `IdempotencyKey`:Guid req; `UpvestApiVersion`; `Body`:`UserCreateRequest?` } | `UserCreateRequest` (AnyOf) → build **`UserTolCreateRequest`** arm: `FirstName`(first_name) req, `LastName`(last_name) req, `Email`(email) req, `BirthDate`(birth_date) req DateTimeOffset(date-only), `BirthCity`(birth_city) opt*, `BirthCountry`(birth_country) opt* enum, `Nationalities`(nationalities) req `IReadOnlyList<Nationality>` (≥1), `PhoneNumber`(phone_number) opt, `Address`(address) req `Address2`{AddressLine1,Postcode,City req; Country req enum}, `Fatca`(fatca) req, `TermsAndConditions` opt, `DataPrivacyAndSharingAgreement` opt | `UserCreateRequest1` (AnyOf) → `UserTol`: `Id`:Guid (→investorId), `Status`:`Status` enum (→signup status) | `ApiException<CreateUserError>` [400,401,403,406,429,5xx] | map/operations/Users.md; Models/AnyOf/UserCreateRequest.cs; Models/UserTolCreateRequest.cs; Models/Address2.cs; Models/UserTol.cs |
| `Users.RetrieveUser` | `RetrieveUserRequest` { `UserId`:Guid req } | — | `UserGetResponse` (AnyOf → UserTol) → `.Status` | `ApiException<RetrieveUserError>` | map/operations/Users.md; Models/AnyOf/UserGetResponse.cs |
| `UserChecks.CreateUserCheck` | `CreateUserCheckRequest` { `UserId`:Guid req; `UpvestApiVersion`; Body } | `UserCheckCreateRequest` (AnyOf) — KYC check carrying the **identityDocument** (type/number/issuingCountry/expiryDate) + KYC evidence/consent fields | `UserCheckCreateResponse` | `ApiException<CreateUserCheckError>` | map/operations/UserChecks.md; Models/AnyOf/UserCheckCreateRequest.cs |
| `TaxResidencies.SetTaxResidencies` | `SetTaxResidenciesRequest` { `UserId`:Guid req; `IdempotencyKey`:Guid req; Body } | `TaxResidenciesSetRequest` { `TaxResidencies`(tax_residencies) req `IReadOnlyList<TaxResidencyForCreateRequest>` (≥1) — carries taxId + taxCountry } | `TaxResidencyRecord` | `ApiException<SetTaxResidenciesError>` | map/operations/TaxResidencies.md; Models/TaxResidenciesSetRequest.cs; Models/TaxResidencyForCreateRequest.cs |
| `AccountGroups.CreateAccountGroup` | `CreateAccountGroupRequest` { `IdempotencyKey`:Guid req; Body } | `AccountGroupCreateRequest` (AnyOf) → `AccountGroupCreateUserRequest` arm (user_id + type, e.g. PERSONAL) | `AccountGroupCreateResponse` (AnyOf) → `.Id`:Guid (→ UpvestAccountGroupId), `.Status` | `ApiException<CreateAccountGroupError>` | map/operations/AccountGroups.md; Models/AnyOf/AccountGroupCreateRequest.cs; Models/AccountGroupCreateUserRequest.cs |
| `AccountGroups.RetrieveAccountGroup` | `RetrieveAccountGroupRequest` { `AccountGroupId`:Guid req } | — | `AccountGroupRetrieveResponse` (AnyOf) → `.Status` (`PENDING_APPROVAL`→pending, `ACTIVE`→ready) | `ApiException<RetrieveAccountGroupError>` | map/operations/AccountGroups.md; Models/AnyOf/AccountGroupRetrieveResponse.cs |
| `AccountGroups.ListUserAccountGroups` | `ListUserAccountGroupsRequest` { `UserId`:Guid req; Sort/Order/Offset/Limit opt } | — | `AccountGroupsListResponse` (data + meta) | `ApiException<ListUserAccountGroupsError>` | map/operations/AccountGroups.md |
| `AccountsApi.CreateAccount` | `CreateAccountRequest` { `IdempotencyKey`:Guid req; Body } | `AccountCreateRequest` (AnyOf) → **`AccountCreateUserRequest`**: `UserId`(user_id) req, `AccountGroupId`(account_group_id) req, `Type`(type) req `Type16` = **PORTFOLIO**, `Name` opt | `AccountCreateResponse` (AnyOf) → `.Id`:Guid (→ UpvestAccountId), `.Type` | `ApiException<CreateAccountError>` | map/operations/AccountsApi.md; Models/AnyOf/AccountCreateRequest.cs; Models/AccountCreateUserRequest.cs; Models/Enums/Type16.cs |
| `AccountsApi.ListUserAccounts` | `ListUserAccountsRequest` { `UserId`:Guid req; paging opt } | — | `AccountsListResponse` (data+meta) → account id/type | `ApiException<ListUserAccountsError>` | map/operations/AccountsApi.md |

\* `BirthCity`/`BirthCountry` are schema-optional but backend-enforced (onboarding workflow: omitting returns 400). Treat as **required by the endpoint** and always send.

### Flow 2 — models & model choice

| Operation | Request record + required members | Body model + fields | Response + inner fields read | Error | Source |
| --- | --- | --- | --- | --- | --- |
| `Portfolios.CreatePortfoliosAllocation` | `CreatePortfoliosAllocationRequest` { `IdempotencyKey`:Guid req; `Body`:`PortfoliosAllocationCreateRequest?` } | `PortfoliosAllocationCreateRequest` { `Name`(name) opt; `Allocation`(allocation) req `IReadOnlyList<Allocation>`; `RebalancingStrategyIds` opt } · `Allocation` { `InstrumentId`(instrument_id) req `InstrumentId` (AnyOf string ISIN/Guid — use `InstrumentId.String(isin)`); `InstrumentIdType`(instrument_id_type) `InstrumentIdType4`=Isin default; `Weight`(weight) req string, regex pct, must sum to 100 } | `PortfoliosAllocation` → `.Id`:Guid (store as AllocationId), `.Name`, `.Allocation[]` | `ApiException<CreatePortfoliosAllocationError>` [400,401,403,404,406,429,5xx] | map/operations/Portfolios.md; Models/PortfoliosAllocationCreateRequest.cs; Models/Allocation.cs; Models/AnyOf/InstrumentId.cs |
| `Portfolios.UpdatePortfoliosAllocation` | `UpdatePortfoliosAllocationRequest` { `AllocationId`:Guid req; Body } | `PortfoliosAllocationUpdateRequest` (full replace — send **all** funds) | `PortfoliosAllocation` | `ApiException<UpdatePortfoliosAllocationError>` | map/operations/Portfolios.md; Models/PortfoliosAllocationUpdateRequest.cs |
| `Portfolios.ListPortfoliosAllocations` | `ListPortfoliosAllocationsRequest` { Sort/Order/Offset/Limit/StartDate/EndDate/InstrumentIds opt } | — | `PortfoliosAllocationsListResponse` (data[]+meta) → each `.Id,.Name,.Allocation[]` | `ApiException<ListPortfoliosAllocationsError>` | map/operations/Portfolios.md; Models/PortfoliosAllocationsListResponse.cs |
| `Portfolios.CreatePortfoliosConfiguration` | `CreatePortfoliosConfigurationRequest` { `IdempotencyKey`:Guid req; Body } | `PortfoliosConfigurationCreateRequest` { `AccountId`(account_id) req Guid; `AllocationId`(allocation_id) req Guid; `RebalancingStrategyIds` opt } | `PortfoliosConfiguration` → `.AccountId,.AllocationId` | `ApiException<CreatePortfoliosConfigurationError>` [+422] | map/operations/Portfolios.md; Models/PortfoliosConfigurationCreateRequest.cs; Models/PortfoliosConfiguration.cs |
| `Portfolios.UpdatePortfoliosConfiguration` | `UpdatePortfoliosConfigurationRequest` { `AccountId`:Guid req; `IdempotencyKey`:Guid req; Body } | `PortfoliosConfigurationUpdateRequest` { `AllocationId`(allocation_id) opt Guid; `RebalancingStrategyIds` opt } | `PortfoliosConfiguration` | `ApiException<UpdatePortfoliosConfigurationError>` | map/operations/Portfolios.md; Models/PortfoliosConfigurationUpdateRequest.cs |
| `Portfolios.RetrievePortfoliosConfiguration` | `RetrievePortfoliosConfigurationRequest` { `AccountId`:Guid req } | — | `PortfoliosConfiguration` → `.AllocationId` (→ our modelId) | `ApiException<RetrievePortfoliosConfigurationError>` | map/operations/Portfolios.md |
| `Instruments.ListInstruments` | `ListInstrumentsRequest` { TradingStatus/Sort/Order/Offset/Limit opt } | — | `InstrumentsListResponse` (data[]+meta) → isin set | `ApiException<ListInstrumentsError>` | map/operations/Instruments.md |
| `Instruments.RetrieveInstrument` | `RetrieveInstrumentRequest` { `InstrumentId`:`InstrumentUniqueIdentifier` req } | — | `InstrumentsResponse` → confirms one ISIN exists | `ApiException<RetrieveInstrumentError>` | map/operations/Instruments.md |

### Flow 3 — paying in

| Operation | Request record + required members | Body model + fields | Response + inner fields read | Error | Source |
| --- | --- | --- | --- | --- | --- |
| `VirtualBankAccounts.CreateVirtualBankAccount` | `CreateVirtualBankAccountRequest` { `IdempotencyKey`:Guid req; Body } | `PaymentsVirtualBankAccountCreateRequest` (account_group_id) | `PaymentsVirtualBankAccountsResponse` → `.Id` (→ pass as `virtual_bank_account_id` to Tests) | `ApiException<CreateVirtualBankAccountError>` | map/operations/VirtualBankAccounts.md; Models/PaymentsVirtualBankAccountCreateRequest.cs |
| `Tests.CreateBankTransaction` | `CreateBankTransactionRequest` { `IdempotencyKey`:Guid req; Body } | `PaymentsBankTransactionCreateRequest` — sandbox-only; `account.iban` fixed `FR7617789000011051089906447`, `virtual_bank_account_id`, `counterparty_name` must match the account-group user's name, amount, currency | `PaymentsBankTransactionCreateResponse` | `ApiException<CreateBankTransactionError>` | map/operations/Tests.md; Models/PaymentsBankTransactionCreateRequest.cs |
| `TopUps.CreateTopup` (alt. funding) | `CreateTopupRequest` { `IdempotencyKey`:Guid req; Body } | `PaymentsTopUpCreateRequest` { `AccountGroupId`(account_group_id) req Guid; `CashAmount`(cash_amount) req string `^[0-9]{1,9}(\.[0-9]{2})?$`; `Currency`(currency) req `Currency` } | `PaymentsTopupsResponse` → `.Id`, `.Status`:`Status29?` (CONFIRMED/SETTLED/CANCELLED) | `ApiException<CreateTopupError>` | map/operations/TopUps.md; Models/PaymentsTopUpCreateRequest.cs; Models/PaymentsTopupsResponse.cs |
| `Portfolios.CreatePortfoliosOrder` | `CreatePortfoliosOrderRequest` { `IdempotencyKey`:Guid req; Body } | `PortfoliosOrderPlaceRequest` { `UserId`(user_id) req Guid; `AccountId`(account_id) req Guid; `CashAmount`(cash_amount) req string; `Currency`(currency) req `Currency`=Eur; `Side`(side) req `Side15`=Buy; `PostTax` opt bool } | `PortfoliosOrder` → `.Id`:Guid (→ **paymentId**), `.CashAmount` (→amount), `.Status`:`Status65` | `ApiException<CreatePortfoliosOrderError>` | map/operations/Portfolios.md; Models/PortfoliosOrderPlaceRequest.cs; Models/PortfoliosOrder.cs |
| `Portfolios.RetrievePortfoliosOrder` | `RetrievePortfoliosOrderRequest` { `PortfolioOrderId`:Guid req } | — | `PortfoliosOrder` → `.Status` (status re-read) | `ApiException<RetrievePortfoliosOrderError>` | map/operations/Portfolios.md |
| `Portfolios.ListPortfoliosOrders` | `ListPortfoliosOrdersRequest` { Sort/Order/Offset/Limit/StartDate/EndDate/AllocationId/AccountId opt } | — | `PortfoliosOrdersListResponse` (data[]+meta) | `ApiException<ListPortfoliosOrdersError>` | map/operations/Portfolios.md |

Status map (payment): `Status65` `New`/`Processing` → **pending**; `Filled`/`Settled` → **invested**; `Cancelled` → **failed**. (`Status65.cs`.)

### Flow 4 — realignment

| Operation | Request record + required members | Body model + fields | Response + inner fields read | Error | Source |
| --- | --- | --- | --- | --- | --- |
| `PortfoliosRebalancing.TriggerPortfolioRebalancing` | `TriggerPortfolioRebalancingOperationRequest` { `IdempotencyKey`:Guid req; Body } | `TriggerPortfolioRebalancingRequest` (AnyOf) → **`Allocations`** arm `{ AllocationsValue(allocations) req IReadOnlyList<Guid> }` (build `TriggerPortfolioRebalancingRequest.Allocations(new Allocations{...})`; use accounts-arm OR allocations-arm, never both) | `TriggerPortfolioRebalancingResponse` → `.Id`:Guid (→ **realignmentId**) — **no status field** | `ApiException<TriggerPortfolioRebalancingError>` | map/operations/PortfoliosRebalancing.md; Models/AnyOf/TriggerPortfolioRebalancingRequest.cs; Models/Allocations.cs; Models/TriggerPortfolioRebalancingResponse.cs |
| `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` | `RetrievePortfoliosRebalancingExecutionRequest` { `ExecutionId`:Guid req } | — | `PortfoliosRebalancingExecution` → `.Id,.Accounts,.Allocations` — **carries NO status field** | `ApiException<RetrievePortfoliosRebalancingExecutionError>` | map/operations/PortfoliosRebalancing.md; Models/PortfoliosRebalancingExecution.cs |
| `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` | `ListPortfolioRebalancingExecutionOrdersRequest` { `ExecutionId`:Guid req; Sort/Order/Offset/Limit/StartDate/EndDate opt } | — | `PortfoliosRebalancingExecutionOrderListResponse` (data[]+meta) → per-order status (derive realignment status) | `ApiException<ListPortfolioRebalancingExecutionOrdersError>` | map/operations/PortfoliosRebalancing.md; Models/PortfoliosRebalancingExecutionOrderListResponse.cs |

Status map (realignment): execution has no status, so derive from its orders — pending if any order `New`/`Processing`; completed if all `Filled`/`Settled`; failed if any `Cancelled`/failed. Exact order-status enum: read `PortfoliosRebalancingExecutionOrderListResponse.cs` and its item/status enum at implementation (`UNVERIFIED` which arm = "failed").

### Flow 5 — the portfolio

| Operation | Request record + required members | Body | Response + inner fields read | Error | Source |
| --- | --- | --- | --- | --- | --- |
| `Valuations.GetAccountValuation` | `GetAccountValuationRequest` { `AccountId`:Guid req; `PriceQuality`:`PriceQuality3` req (query `price_quality`) } | — | `AccountValuation` → `.TotalSecurityValue.Amount` (→ totalValue), `.SecurityPositions[]` → `.Instrument.Isin` (→instrumentId), `.Quantity` (→quantity), `.Value?.Amount` (→value; null when no price) | `ApiException<GetAccountValuationError>` | map/operations/Valuations.md; Models/AccountValuation.cs; Models/AccountValuationSecurityPosition.cs; Models/TotalSecurityValue.cs; Models/Instrument6.cs; Models/Value.cs |
| `Positions.ListPositions` (fallback) | `ListPositionsRequest` { `AccountId`:Guid req; Offset/Limit opt } | — | `PositionsListResponse` { `Data`:`IReadOnlyList<Datum1>`; `Meta` } → `Datum1.Instrument`, `.Quantity` (quantity only, no value) | `ApiException<ListPositionsError>` | map/operations/Positions.md; Models/PositionsListResponse.cs; Models/Datum1.cs |

`PriceQuality3`: `Eod` ("EOD") / `HighestAvailable` ("HIGHEST_AVAILABLE"). Use `HighestAvailable` for a current value (`YOUR CALL — not in the map`; `PriceQuality3.cs`).

### Enum value tables (members actually needed)

| Enum (file) | Members (C# → wire) |
| --- | --- |
| `Currency` (Models/Enums/Currency.cs) | `Eur`→"EUR", `Gbp`→"GBP" |
| `Side15` (Models/Enums/Side15.cs) | `Buy`→"BUY", `Sell`→"SELL" |
| `Status65` portfolio-order (Models/Enums/Status65.cs) | `New`,`Processing`,`Filled`,`Settled`,`Cancelled` |
| `Status29` top-up (Models/Enums/Status29.cs) | CONFIRMED/SETTLED/CANCELLED — confirm member spelling in file |
| `Status` user (Models/Enums/Status.cs) | `Active`,`Inactive`,`Offboarding`,`Offboarded` |
| `PriceQuality3` (Models/Enums/PriceQuality3.cs) | `Eod`,`HighestAvailable` |
| `InstrumentIdType4` (Models/Enums/InstrumentIdType4.cs) | `Isin` (default), `Upvest` — confirm in file |
| `Type16` account type (Models/Enums/Type16.cs) | `Trading`,`Portfolio` — confirm member spelling in file |

Open-enum note: these are `OpenStringEnum<T>` (static members, not C# enums); unknown wire values do not throw — resolve with `TryGetKnownValue`/`Match`. `MUST load dotnet-models`.

### Client construction / auth / servers

- Client: `new UpvestInvestmentApiClient(HttpClient, UpvestInvestmentApiClientOptions)` — only constructor. DI: `services.AddUpvestInvestmentApiClient(options => …)`. Source: sdk-map.md "Getting a client".
- Auth is **two schemes, both required** on every call: OAuth2 client credentials (`options.OauthClientCredentials = new OAuth2ClientCredentials { ClientId, ClientSecret }`) **and** HTTP message signing (`options.Hooks = [new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(keyId, privateKeyPath, upvestClientId, passphrase))]`). A missing signing hook/env makes every call throw before send (`InvalidOperationException`); a missing OAuth credential throws `AuthSchemeException`. Scopes are requested per-operation by the SDK — do not set scopes manually. Source: sdk-map.md "Request signing", "Servers & auth".
- Servers: one group `Default`. `ServerEnvironment.Production` (default) = `https://sandbox.upvest.co`; `ServerEnvironment.Environment2` = `https://api.upvest.co`. `Upvest:BaseUrl` is applied verbatim via `options.Server.Default.{Production|Environment2}.BaseUrl`. Source: sdk-map.md "Servers & auth".

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| Every fund `instrumentId` (ISIN) in a model must be a real tradable Upvest instrument | `Portfolios.CreatePortfoliosAllocation`/`UpdatePortfoliosAllocation` ← `Instruments.ListInstruments` (or `RetrieveInstrument` per ISIN) | In the `PUT /api/portfolio/models/{modelId}` handler, before the allocation write: validate each ISIN against the instruments the list/retrieve returns; reject unknown ISINs. |
| The `modelId` an investor chooses must be one the operator defined | `Portfolios.CreatePortfoliosConfiguration`/`UpdatePortfoliosConfiguration` ← the app's `ModelPortfolioRecord` store (populated from `Portfolios.CreatePortfoliosAllocation`) | In the `PUT /api/portfolio/investor/model` handler: look up `ModelPortfolioRecord` by `modelId` to get `AllocationId`; 404 if absent, before calling the configuration write. |
| The `modelId` a realignment targets must be one the operator defined | `PortfoliosRebalancing.TriggerPortfolioRebalancing` ← `ModelPortfolioRecord` store | In the `POST /api/portfolio/models/{modelId}/realignments` handler: resolve `modelId → AllocationId` before triggering; 404 if absent. |
| A pay-in is allowed only for an accepted investor with a model chosen | `Portfolios.CreatePortfoliosOrder` ← `InvestorRecord` (status ready + account id) and `PortfoliosConfiguration` (allocation linked) | In the `POST /api/portfolio/payments` handler: refuse (409/422) unless `InvestorRecord.SignupStatus` is ready and a configuration/`ChosenModelId` exists, before funding + order. |
| A caller may only read/pay into **their own** investor data | all per-investor reads/writes ← `InvestorRecord` keyed by `User.Identity.Name` | Every endpoint resolves the Upvest `userId`/`accountId`/`accountGroupId` from the caller's own `InvestorRecord` only — never from a request-supplied id. |

⚠ These rules are not in the map; derived from the task.

---

## 3. Trap notes (one per hazard, attached to the step where it bites)

- **Step 0 / all:** Upvest refuses any unsigned request *including the OAuth token request*; the two auth schemes are AND, not OR, and a missing signing config fails before the first byte is sent. State what each scheme contributes and how a missing one surfaces. `MUST load dotnet-authentication`.
- **Step 0 / all:** what `RetryOptions.Timeout` actually bounds versus the whole-call budget the endpoint caller experiences is not visible from the option name. `MUST load dotnet-configuration-resilience`.
- **Step 0 / all:** which HTTP verbs/statuses the SDK silently **resends** — the pay-in and onboarding writes are not all safe to auto-retry. `MUST load dotnet-configuration-resilience`.
- **Step 0 / Flow 1:** the `CreateUser` request body carries personal data; how the SDK logs request bodies (and the env var that can force body logging on with no code change) decides your logging posture. `MUST load dotnet-configuration-resilience`, `MUST load dotnet-getting-started` (Sensitive data).
- **All flows:** a drifted 2xx body or a non-2xx body that does not match the operation's `{Operation}Error` surfaces as `ResponseDeserializationException` (an `ApiException`, not `ApiException<TError>`) and escapes a catch ladder that only handles `ApiException<TError>`. `MUST load dotnet-error-handling`.
- **Flow 1/3/4 reads:** success returns only the deserialized body — HTTP status/headers are not on the return value; a re-read/poll cannot see `Retry-After` etc. without hooks. `MUST load dotnet-configuration-resilience`.
- **Flow 2 models:** `UpdatePortfoliosAllocation` is a **full replace** — omitted funds are removed; the weight regex and the sum-to-100 rule are enforced server-side. `MUST load dotnet-models`.
- **Flow 3 pay-in:** `InstrumentId` / `TriggerPortfolioRebalancingRequest` / `UserCreateRequest` are AnyOf unions — building the wrong arm serializes wrongly and is accepted as "unknown" on read. `MUST load dotnet-models`.
- **Flow 3 pay-in:** the portfolio order is placed **asynchronously** (Upvest tracks it by webhook, which this app does not expose) — the create response status is only the initial status; settle by re-reading. `MUST load dotnet-error-handling`.
- **Flow 4 realignment:** `PortfoliosRebalancingExecution` has **no status field** — do not fabricate one; status must be derived from the execution's orders list. `MUST load dotnet-models`.

---

## 4. REQUIRED READING (load every one before implementation starts)

| Skill | Governs |
| --- | --- |
| `upvest-v4:dotnet-client-initialization` | Step 0 — constructing/DI-registering `UpvestInvestmentApiClient`, HttpClient lifetime |
| `upvest-v4:dotnet-authentication` | Step 0 — OAuth2 + HTTP signing credentials, fail-fast on missing secret |
| `upvest-v4:dotnet-configuration-resilience` | Step 0/all — timeout budget, retry ownership, base-URL/env selection, pagination, logging/sensitive bodies |
| `upvest-v4:dotnet-models` | Flows 1–5 — AnyOf unions, open enums, dates, full-replace semantics, `AdditionalProperties` |
| `upvest-v4:dotnet-calling-endpoints` | Flows 1–5 — request-record + `requestOptions`/`cancellationToken` call shape, response envelopes |
| `upvest-v4:dotnet-error-handling` | all — Case A typed errors, `ResponseDeserializationException`, `SdkException` family (**always required**) |
| `upvest-v4:dotnet-testing` | tests — HttpClient stub seam, faking outcomes/clock, asserting outgoing requests |

Hazard row (verbatim): a body that does not match its declared type — a drifted or malformed **2xx** response (a missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch ladder that handles only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | At startup validate the bound `Upvest:` config: `ClientId`, `ClientSecret`, `SigningKeyId`, `SigningKeyPath`, `BaseUrl` must be present and non-blank (`SigningKeyPassphrase` optional — only if the key is encrypted); the signing key file must exist/load. Refuse to start otherwise, rather than discovering it as `AuthSchemeException`/`InvalidOperationException` on first call. Because both auth schemes are multi-part, **every** part is checked (a blank part ≠ a missing one). `YOUR CALL — not in the map` (which host startup hook). |
| 2 | Secret sourcing & rotation | Secrets come only from the `Upvest:` config section (IConfiguration/user-secrets/env/KeyVault — never source). DI builds `UpvestInvestmentApiClientOptions` **once** and captures it in the singleton client, so a rotated secret takes effect only on process restart. No hot-rotation requirement stated → restart is acceptable; record it. |
| 3 | Total timeout budget | The number the endpoint caller gets is the per-attempt `RetryOptions.Timeout` multiplied across `MaxRetries` + backoff + the SDK's token/signing round-trip; set a bounded `MaxRetries`/`Timeout` and a `CancellationToken` from the ASP.NET request. Exact meaning of `Timeout` and the whole-call bound: `dotnet-configuration-resilience`. `YOUR CALL — not in the map` (chosen numbers). |
| 4 | Write-retry ownership | Decide per write which may be auto-resent via `RetryOptions.HttpMethodsToRetry`/`StatusCodesToRetry`; the onboarding and pay-in POSTs carry a real idempotency key (row 5) so a resend is safe, but confirm the SDK's retried-verb set before relying on it. `dotnet-configuration-resilience`. `YOUR CALL — not in the map`. |
| 5 | Idempotency & ambiguous writes | **Every** write in scope has a **real** caller-supplied `required Guid IdempotencyKey` on its request record: `CreateUser`, `CreateUserCheck`, `SetTaxResidencies`, `CreateAccountGroup`, `CreateAccount`, `CreatePortfoliosAllocation`, `UpdatePortfoliosConfiguration`, `CreatePortfoliosOrder`, `TriggerPortfolioRebalancing`, `CreateVirtualBankAccount`, `CreateBankTransaction`, `CreateTopup`. Derive each key deterministically from the application claim (e.g. the `PaymentRecord`/`InvestorRecord` key) so a retry reuses it; reconciliation on ambiguous failure is the re-read in §UNKNOWN OUTCOMES. The injected `Idempotency-Key` header is not this key. Source: each `Requests/**` record read this session. |
| 6 | Observability | Log at Information: operation name + application claim id (shopper, modelId, paymentId) and the Upvest resource id returned. Log at Warning/Error: `ApiException` status + the provider correlation id from `RawError.ReadAsString()`/`ReadAsJson` body. Never log request bodies on Flow-1 paths. `YOUR CALL — not in the map` (log sink). |
| 7 | Sensitive data | **Yes — Flow 1 carries PII** (name, birth date/city/country, address, phone, taxId, identity document). `options.Logging.LogRequestBody` stays **off**, `options.Logging.LoggerFactory` is set explicitly in production (so the `UPVEST_INVESTMENT_API_LOG=trace` env var cannot force body logging), and the app's own diagnostics never echo the `CreateUser`/`CreateUserCheck` request bodies. `dotnet-configuration-resilience`. |
| 8 | Environment selection | One server group `Default`. Deployments set `Upvest:BaseUrl` verbatim: sandbox `https://sandbox.upvest.co` (= `ServerEnvironment.Production`) for test/dev, live `https://api.upvest.co` (= `ServerEnvironment.Environment2`) for prod. The sandbox IS a distinct environment, so test traffic is kept off live by `BaseUrl`/`Environment`. The sandbox-only `Tests.CreateBankTransaction` must be guarded by an environment check and never called against live. `YOUR CALL — not in the map` (which deployment sets which). |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. Claims stored in the application's own EF database (the store eShopOnWeb already uses) keyed by primary key. |
| 10 | Partial results | See PAGED READS. |
| 11 | Unknown outcomes | See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Investor sign-up (`Users.CreateUser` + onboarding chain) | `InvestorRecord` (PK = `Shopper` = `User.Identity.Name`) in the app EF DB | Primary-key insert of `InvestorRecord` for an already-signed-up shopper | the `DbUpdateException`/duplicate-key catch (same pattern as `DuplicateException` in `CreateCatalogItemEndpoint`) | TBD |
| Pay-in (`Portfolios.CreatePortfoliosOrder`) | `PaymentRecord` (PK = a pre-generated payment/claim id) in the app EF DB | Primary-key insert of the claim row before the SDK call | the duplicate-key catch on the claim insert | TBD |
| Model create/replace (`Portfolios.CreatePortfoliosAllocation`) | `ModelPortfolioRecord` (PK = `ModelId`) in the app EF DB | Primary-key presence of `ModelId` routes to replace (Update) instead of a second create | the existence check → branch, not a second insert | TBD |
| Realignment (`PortfoliosRebalancing.TriggerPortfolioRebalancing`) | `RealignmentRecord` / a per-model in-flight claim in the app EF DB | Primary-key insert of the realignment claim before trigger | the duplicate-key catch on the claim insert | TBD |

Order per row: claim write → SDK call → record result. The claim is written before the SDK call; release it if the provider refuses.

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `Portfolios.ListPortfoliosAllocations` (GET models) | `Limit`/`Offset` + `meta.total_count` | Loop pages until `data.Count < limit` / offset ≥ total; GET /models returns the full set (no truncation flag needed) | TBD |
| `Portfolios.ListPortfoliosOrders` (GET payments, if sourced from Upvest) | `Limit`/`Offset` + `meta` | Page to cover the shopper's orders; if a cap is applied, expose a `hasMore`/next-offset field on the response DTO | TBD |
| `Valuations.GetAccountValuation` `security_positions[]` | not paged (single response list) | N/A — returns all holdings in one object | TBD |
| `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` | `Limit`/`Offset` + `meta` | Page all orders to derive realignment status; derived status is complete only after the last page | TBD |

A log line is never the signal — the field/return type changes.

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `Users.CreateUser` | `Users.ListUsers` / `Users.RetrieveUser` | the deterministic idempotency key / email (list + match) | TBD | TBD |
| `AccountsApi.CreateAccount` | `AccountsApi.ListUserAccounts` | `user_id` + `type=PORTFOLIO` | TBD | TBD |
| `Portfolios.CreatePortfoliosOrder` (pay-in) | `Portfolios.ListPortfoliosOrders` | `account_id` + the idempotency key / cash_amount + created-at window | TBD | TBD |
| `TriggerPortfolioRebalancing` | `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (then RetrieveExecution) | the allocation id + created-at window (no caller reference is returned until the id is captured) | TBD | TBD |
| `TopUps.CreateTopup` / `Tests.CreateBankTransaction` | `TopUps.ListTopups` / `CreditFundings.ListCreditFundings` | `account_group_id` + amount + created-at window | TBD | TBD |

Each write settles its outcome in its own `catch` (re-read + match) before returning, or records the claim as unknown for a reconciliation pass; a reported failure or a caller retry is not a settlement.

---

## 6. Assumptions & Blockers

**Blockers**
- **None that stop planning.** The map exposes every capability the five flows need (users/onboarding, account groups, accounts, instruments, portfolio allocations, configurations, orders, rebalancing, payments/top-ups/vIBAN/sandbox bank transaction, positions, valuations). No invented data path is used.
- **Expected provider rejections in the sandbox (environment, not a gap — build every flow in full).** The task states the sandbox client is authorised **only for user operations**. Therefore, in the sandbox, calls needing any non-`users` scope will return **401 "This client is not authorised to use these scopes"**: `checks:admin` (`CreateUserCheck`), `taxes:admin` (`SetTaxResidencies`), `accounts:admin`/`accounts:read` (`CreateAccountGroup`, `CreateAccount`, `RetrieveAccountGroup`, `ListUserAccounts`), `portfolios:admin`/`portfolios:read` (all allocation/configuration/order + rebalancing ops), `instruments:read` (`ListInstruments`/`RetrieveInstrument`), `payments:admin`/`topups:admin`/`tests:admin` (vIBAN/top-up/bank-transaction), `positions:read`, `valuations:read`. Only `Users.CreateUser`/`RetrieveUser`/`ListUsers` are expected to succeed. Verification expectation: end-to-end sign-up past user creation, and Flows 2–5, cannot be observed against this sandbox client; assert behaviour with stubs/tests (`dotnet-testing`) and treat the 401 as the environment.

**Assumptions**
- `Upvest:ClientId` is used as **both** the OAuth2 `ClientId` and the signing hook's `upvestClientId` (the `upvest-client-id`), since the task's config keys provide a single client id. If Upvest issued a separate tenant client-id (UUID) distinct from the OAuth client-id, a second config key is needed — raise with the user.
- The signed-in shopper is identified by `User.Identity.Name` (the `ClaimTypes.Name` claim the JWT carries, per `IdentityTokenClaimService`), and is the key of `InvestorRecord`; no request ever supplies another shopper's Upvest ids.
- Flow 1's `POST /api/portfolio/investor` fields are a subset of what Upvest onboarding requires. The integration must also supply, from configuration/sandbox fixtures, the consent/evidence the SDK marks required or the backend enforces: `fatca`, `terms_and_conditions` + `data_privacy_and_sharing_agreement` (sandbox consent document ids), the KYC check evidence (`CreateUserCheck`), and an account-group type (e.g. `PERSONAL`). These are onboarding prerequisites, not task fields; treat missing values as configuration, not request input.
- "Money reaches an investor's account" in the sandbox is implemented via **vIBAN + `Tests.CreateBankTransaction`** (the documented sandbox credit mechanism) rather than `TopUps.CreateTopup` alone, because a top-up stays unsettled until a real SEPA transfer arrives; `CreateTopup` is retained as the explicit "amount paid in" record if preferred. Final choice is `YOUR CALL — not in the map`, settled at implementation.
- The task's `paidIn` (total amount paid in) is application-derived (sum of `PaymentRecord.Amount`), as Upvest exposes no single "total contributed" field for an account; recorded as application state, not an invented Upvest field.
- Money amounts: the API uses decimal **strings** (`cash_amount`, valuation `amount`); the task's JSON `number` with two decimals is a mapping the endpoints perform (parse/format to 2dp) — `YOUR CALL — not in the map`.

---

## 7. Source labels

Every contract-sheet row cites its map page (`map/operations/*.md`) and/or the exact SDK source file under `plugins/upvest-v4/sdk/dotnet/src/UpvestInvestmentApi/…` read this session. `UNVERIFIED` is used only for the realignment order-status "failed" arm (live-traffic-only). `YOUR CALL — not in the map` marks application decisions: funding mechanism choice, price-quality choice, money string↔number mapping, timeout/retry numbers, startup-hook placement, log sink, and the single-vs-dual client-id question. Items that could stop planning are in §6 (none do).
