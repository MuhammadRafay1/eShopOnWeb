# Upvest Investment API integration plan — managed investment portfolios in eShopOnWeb

Grounded against the Upvest Investment API .NET SDK that ships in this plugin at `sdk/dotnet/` (API spec `1.172.0`). Every contract fact below cites the SDK map page or the SDK source file it came from. SDK source paths are relative to that plugin SDK root. This plan is additive to eShopOnWeb; it does not touch the catalog/basket/order flow.

The integration lives in `src/PublicApi`, following the existing ardalis/MinimalApi endpoint convention (exemplar: `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`), registered by `builder.Services.AddEndpoints()` + `app.MapEndpoints()` in `src/PublicApi/Program.cs`. App data is persisted through `IRepository<T>`/`EfRepository<T>` over new aggregate entities added to `CatalogContext` (`src/Infrastructure/Data/CatalogContext.cs`), which runs on the EF in-memory provider when `UseOnlyInMemoryDatabase=true` (`src/Infrastructure/Dependencies.cs`).

---

## 1. Scope & sequence

Caller identity comes from the JWT `ClaimTypes.Name` claim (the username/email), set in `src/Infrastructure/Identity/IdentityTokenClaimService.cs`. Every per-shopper endpoint keys all app records by that username and only ever reads/writes the caller's own records. Admin endpoints use `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` exactly as `CreateCatalogItemEndpoint` does.

**App persistence (new aggregates in `CatalogContext`)**
- `Investor` — PK `ShopperUserName` (natural key, one per shopper); fields: Upvest `UserId`, `AccountGroupId`, `AccountId`, `VirtualBankAccountIban`, `ChosenAllocationId?`, cached `Status`, and the per-write idempotency keys (persisted so retries replay the same key).
- `ModelPortfolio` — PK app `ModelId` (route id); maps to Upvest `AllocationId`, holds `Name` and funds. Lets PUT be create-or-replace and lets the realignment route resolve `{modelId}` → `AllocationId`.
- `PortfolioPayment` — per shopper; fields: `Amount`, Upvest `PortfolioOrderId`, `Currency`, cached `Status`, `CreatedAt`, idempotency keys. Drives GET payments (newest-first) and the `paidIn` total.

**Flow 1 — Becoming an investor** (any signed-in shopper, self)
- `POST /api/portfolio/investor`: (1) `client.Users.CreateUser` (TOL arm) → Upvest userId; (2) `client.AccountGroups.CreateAccountGroup` (`AccountGroupCreateUserRequest`, type `PERSONAL`) → accountGroupId; (3) `client.AccountsApi.CreateAccount` (`AccountCreateUserRequest`, type `PORTFOLIO` — the account type that "can hold a managed portfolio", per `Type16` docs). Persist `Investor`. Respond `investorId` (Upvest userId) + `status`.
- `GET /api/portfolio/investor`: read-back via `client.Users.RetrieveUser` (`UserTol.Status`) and `client.AccountGroups.RetrieveAccountGroup` (account-group readiness); derive `status` (§ YOUR CALL below). No inbound webhook; status is learned purely by these reads.

**Flow 2 — Model portfolios**
- `PUT /api/portfolio/models/{modelId}` (ADMIN): first PUT for a `modelId` → `client.Portfolios.CreatePortfoliosAllocation`; subsequent PUT → `client.Portfolios.UpdatePortfoliosAllocation` (by stored `AllocationId`). Funds → `Allocation` list (`instrument_id` = ISIN, `weight` = string). App validates weights sum to exactly 100 before the call.
- `GET /api/portfolio/models` (any signed-in shopper): `client.Portfolios.ListPortfoliosAllocations` → map to `{modelId, name, funds[{instrumentId, weight}]}` (resolve `AllocationId` → app `ModelId` via stored map).
- `PUT /api/portfolio/investor/model` (self): `client.Portfolios.CreatePortfoliosConfiguration` first time (links `account_id`→`allocation_id`), `client.Portfolios.UpdatePortfoliosConfiguration` to switch later.

**Flow 3 — Paying in** (self)
- `POST /api/portfolio/payments`: precondition — caller is an `active` investor with a chosen model, else refuse (HTTP 409/422, no SDK call). Then (1) ensure a virtual bank account IBAN for the account group (`client.VirtualBankAccounts.ListVirtualBankAccounts`, or `CreateVirtualBankAccount` if none); (2) `client.Tests.CreateBankTransaction` credits that IBAN with the euro amount — this is "whatever Upvest provides" for money-in in the sandbox; (3) `client.Portfolios.CreatePortfoliosOrder` side `BUY`, `cash_amount` = amount, which distributes the cash across the account's current allocation in the model's weights (per `PortfoliosOrder` summary). Persist `PortfolioPayment`. Respond `paymentId` + `status`.
- `GET /api/portfolio/payments`: return the caller's `PortfolioPayment` rows newest-first; refresh each `status` by `client.Portfolios.RetrievePortfoliosOrder` read-back.

**Flow 4 — Realignment** (ADMIN)
- `POST /api/portfolio/models/{modelId}/realignments`: `client.PortfoliosRebalancing.TriggerPortfolioRebalancing` with the `Allocations` arm carrying the model's `AllocationId` (realigns every account following that allocation). Respond `realignmentId` (execution id).
- `GET /api/portfolio/realignments/{realignmentId}`: `client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` confirms existence; status is **derived** by paging `client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` and aggregating each order's `Status71` (the execution resource itself carries no status field — see §6).

**Flow 5 — The portfolio** (self)
- `GET /api/portfolio`: model followed via `client.Portfolios.RetrievePortfoliosConfiguration` (`allocation_id` → app `modelId`); holdings + total via `client.Valuations.GetAccountValuation` (`security_positions` → `{instrumentId=instrument.isin, quantity, value=value.amount}`, `total_security_value.amount` → `totalValue`); `paidIn` = sum of the caller's accepted `PortfolioPayment.Amount`.

---

## 2. CONTRACT SHEET

⚠ **Signatures below are generated code, verbatim.** Each operation that takes input takes **one** request record as its first parameter, built with an object initializer whose property names are the record's own (never flat arguments); an operation with no inputs takes none. `requestOptions` and `cancellationToken` are optional trailing params.

⚠ **Every SDK type must be written fully-qualified with the namespace its source path implies** (`src/UpvestInvestmentApi/Models/…` → `UpvestInvestmentApi.Models`; `…/Models/AnyOf/…` → `UpvestInvestmentApi.Models.AnyOf`; `…/Models/Enums/…` → `UpvestInvestmentApi.Models.Enums`; `…/Requests/<Controller>/…` → `UpvestInvestmentApi.Requests.<Controller>`; `…/Errors/…` → `UpvestInvestmentApi.Errors`). Take each type's namespace from the path the map gives for **that** type, never from a neighbour.

**All operations in scope are Case A (typed).** Every error cell is identical: `catch (ApiException<{Operation}Error>)` exposing `ex.Error.TryGetNoContent([NotNullWhen(true)] out RawError? value)` (covers the listed 4xx/5xx statuses) plus the inherited `ex.Error.TryGetRawError(out RawError? error)` fallback. `ApiException<TError>` is `UpvestInvestmentApi.Core.Exceptions`; `RawError` is `UpvestInvestmentApi.Core.ErrorResponse`. No operation in scope is paginated via a `Pageable`; the list operations return a single response with a `UpvestInvestmentApi.Models.Meta` block (`offset/limit/count/total_count`) — page manually. All operations are server group `Default`. (Source: `sdk-map.md` Error-handling model + Operations defaults.)

| # | Op (`client.X.Y`) | Method signature (first param = request record) · required members | Body model + key fields (`C# (wire): type, required?`) | Response envelope → fields read | Source |
| --- | --- | --- | --- | --- | --- |
| 1 | `Users.CreateUser` | `CreateUser(CreateUserRequest request, …)` · req: `IdempotencyKey: Guid`. Also `Body: UserCreateRequest?`, `UpvestApiVersion` (default `_1`) | `UserCreateRequest` (AnyOf, `Models.AnyOf`) → **`UserTolCreateRequest`** arm (`Models`): `FirstName (first_name): string, req` · `LastName (last_name): string, req` · `Email (email): string, req` · `BirthDate (birth_date): DateTimeOffset, req` · `Nationalities (nationalities): IReadOnlyList<Nationality>, req` · `Address (address): Address2, req` · `Fatca (fatca): Fatca, req` · optional: `BirthCity`, `BirthCountry`, `PhoneNumber`, `TermsAndConditions`, `DataPrivacyAndSharingAgreement` | `UserCreateRequest1` (AnyOf, `Models.AnyOf`) → `UserTol` arm: `Id: Guid` (→ investorId), `Status: Status` | Users.md; `Models/UserTolCreateRequest.cs`; `Models/AnyOf/UserCreateRequest.cs`, `UserCreateRequest1.cs`; `Models/UserTol.cs` |
| 2 | `Users.RetrieveUser` | `RetrieveUser(RetrieveUserRequest request, …)` · req: `UserId: Guid` | — | `UserGetResponse` (AnyOf, `Models.AnyOf`) → `UserTol`/`UserByol` arm: `Status: Status` (ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED) | Users.md; `Models/AnyOf/UserGetResponse.cs`; `Models/Enums/Status.cs` |
| 3 | `AccountGroups.CreateAccountGroup` | `CreateAccountGroup(CreateAccountGroupRequest request, …)` · req: `IdempotencyKey: Guid`. `Body: AccountGroupCreateRequest?` | `AccountGroupCreateRequest` (AnyOf) → `AccountGroupCreateUserRequest` (`Models`): `UserId (user_id): Guid, req` · `Type (type): Type13, req` (use `Type13.Personal`) · opt `Name`?,`SecuritiesAccountNumber?` | `AccountGroupCreateResponse` (AnyOf, `Models.AnyOf`) → group `id` (read declaring file at impl) | AccountGroups.md; `Models/AnyOf/AccountGroupCreateRequest.cs`; `Models/AccountGroupCreateUserRequest.cs`; `Models/Enums/Type13.cs` |
| 4 | `AccountGroups.RetrieveAccountGroup` | `RetrieveAccountGroup(RetrieveAccountGroupRequest request, …)` · req: `AccountGroupId: Guid` | — | `AccountGroupRetrieveResponse` (AnyOf) → group status (PENDING_APPROVAL/ACTIVE — read declaring file at impl) | AccountGroups.md; `Models/AnyOf/AccountGroupRetrieveResponse.cs` |
| 5 | `AccountsApi.CreateAccount` | `CreateAccount(CreateAccountRequest request, …)` · req: `IdempotencyKey: Guid`. `Body: AccountCreateRequest?` | `AccountCreateRequest` (AnyOf) → `AccountCreateUserRequest` (`Models`): `UserId (user_id): Guid, req` · `AccountGroupId (account_group_id): Guid, req` · `Type (type): Type16, req` (use `Type16.Portfolio`) · opt `Name?` | `AccountCreateResponse` (AnyOf) → account `id` (read declaring file at impl) | AccountsApi.md; `Models/AnyOf/AccountCreateRequest.cs`; `Models/AccountCreateUserRequest.cs`; `Models/Enums/Type16.cs` |
| 6 | `AccountsApi.RetrieveAccount` | `RetrieveAccount(RetrieveAccountRequest request, …)` · req: `AccountId: Guid` | — | `AccountRetrieveResponse` (AnyOf) → account readiness (read declaring file at impl) | AccountsApi.md; `Models/AnyOf/AccountRetrieveResponse.cs` |
| 7 | `Portfolios.CreatePortfoliosAllocation` | `CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest request, …)` · req: `IdempotencyKey: Guid`. `Body: PortfoliosAllocationCreateRequest?` | `PortfoliosAllocationCreateRequest` (`Models`): `Allocation (allocation): IReadOnlyList<Allocation>, req` · opt `Name?`, `RebalancingStrategyIds?`. `Allocation` item: `InstrumentId (instrument_id): InstrumentId(AnyOf string|Guid), req` · `InstrumentIdType (instrument_id_type): InstrumentIdType4` (default `Isin`) · `Weight (weight): string, req` (regex `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`) | `PortfoliosAllocation` (`Models`): `Id: Guid` (→ store as AllocationId), `Name: string`, `Allocation: IReadOnlyList<Allocation>` | Portfolios.md; `Models/PortfoliosAllocationCreateRequest.cs`; `Models/Allocation.cs`; `Models/AnyOf/InstrumentId.cs`; `Models/PortfoliosAllocation.cs` |
| 8 | `Portfolios.UpdatePortfoliosAllocation` | `UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest request, …)` · req: `AllocationId: Guid`. `Body: PortfoliosAllocationUpdateRequest?` | `PortfoliosAllocationUpdateRequest`: `Allocation (allocation): IReadOnlyList<Allocation>, req` · opt `Name?` (weights still sum to 100) | `PortfoliosAllocation` | Portfolios.md; `Models/PortfoliosAllocationUpdateRequest.cs` |
| 9 | `Portfolios.ListPortfoliosAllocations` | `ListPortfoliosAllocations(ListPortfoliosAllocationsRequest request, …)` · no required | query: `sort,order,offset,limit,start_date,end_date,instrument_ids` | `PortfoliosAllocationsListResponse` → `data: IReadOnlyList<PortfoliosAllocation>` + `meta: Meta` | Portfolios.md; `Models/PortfoliosAllocationsListResponse.cs` |
| 10 | `Portfolios.CreatePortfoliosConfiguration` | `CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest request, …)` · req: `IdempotencyKey: Guid`. `Body: PortfoliosConfigurationCreateRequest?` | `PortfoliosConfigurationCreateRequest`: `AccountId (account_id): Guid, req` · `AllocationId (allocation_id): Guid, req` · opt `RebalancingStrategyIds?` | `PortfoliosConfiguration`: `AccountId`, `AllocationId` | Portfolios.md; `Models/PortfoliosConfigurationCreateRequest.cs`; `Models/PortfoliosConfiguration.cs` |
| 11 | `Portfolios.UpdatePortfoliosConfiguration` | `UpdatePortfoliosConfiguration(UpdatePortfoliosConfigurationRequest request, …)` · req: `AccountId: Guid`, `IdempotencyKey: Guid`. `Body: PortfoliosConfigurationUpdateRequest?` | `PortfoliosConfigurationUpdateRequest`: opt `AllocationId (allocation_id): Guid?` · opt `RebalancingStrategyIds?` (nothing marked required — send `AllocationId` to switch model) | `PortfoliosConfiguration` | Portfolios.md; `Models/PortfoliosConfigurationUpdateRequest.cs`; `Requests/Portfolios/UpdatePortfoliosConfigurationRequest.cs` |
| 12 | `Portfolios.RetrievePortfoliosConfiguration` | `RetrievePortfoliosConfiguration(RetrievePortfoliosConfigurationRequest request, …)` · req: `AccountId: Guid` | — | `PortfoliosConfiguration`: `AllocationId` (→ model followed) | Portfolios.md; `Models/PortfoliosConfiguration.cs` |
| 13 | `Portfolios.CreatePortfoliosOrder` | `CreatePortfoliosOrder(CreatePortfoliosOrderRequest request, …)` · req: `IdempotencyKey: Guid`. `Body: PortfoliosOrderPlaceRequest?` | `PortfoliosOrderPlaceRequest`: `UserId (user_id): Guid, req` · `AccountId (account_id): Guid, req` · `CashAmount (cash_amount): string, req` · `Currency (currency): Currency, req` (use `EUR`) · `Side (side): Side15, req` (use `BUY`) · opt `PostTax?` | `PortfoliosOrder`: `Id: Guid` (→ store), `Status: Status65` (NEW/PROCESSING/FILLED/SETTLED/CANCELLED) | Portfolios.md; `Models/PortfoliosOrderPlaceRequest.cs`; `Models/PortfoliosOrder.cs`; `Models/Enums/Status65.cs` |
| 14 | `Portfolios.RetrievePortfoliosOrder` | `RetrievePortfoliosOrder(RetrievePortfoliosOrderRequest request, …)` · req: `PortfolioOrderId: Guid` | — | `PortfoliosOrder`: `Status: Status65` (→ payment status read-back) | Portfolios.md; `Models/PortfoliosOrder.cs` |
| 15 | `Tests.CreateBankTransaction` | `CreateBankTransaction(CreateBankTransactionRequest request, …)` · req: `IdempotencyKey: Guid`. `Body: PaymentsBankTransactionCreateRequest?` | `PaymentsBankTransactionCreateRequest`: `Account (account): Account29{Iban: string}, req` · `Amount (amount): string, req` (regex `^[0-9]{1,9}(\.[0-9]{2})?$`) · `Currency (currency): BankTransactionCurrency, req` · `ClientReference (client_reference): string, req` · `CounterpartyName (counterparty_name): string, req` · `CounterpartyAccount (counterparty_account): CounterpartyAccount, req` · opt `RemittanceInformation?`, `VirtualBankAccountId?` | `PaymentsBankTransactionCreateResponse`: `Id: Guid`, `BankReference: string` | Tests.md; `Models/PaymentsBankTransactionCreateRequest.cs`; `Models/Account29.cs`; `Models/PaymentsBankTransactionCreateResponse.cs` |
| 16 | `VirtualBankAccounts.ListVirtualBankAccounts` | `ListVirtualBankAccounts(ListVirtualBankAccountsRequest request, …)` · req: `AccountGroupId: Guid` | query: `sort,order,limit,offset` | `PaymentsVirtualBankAccountsListResponse` → data items carrying IBAN (read declaring file at impl) + `Meta` | VirtualBankAccounts.md; `Models/PaymentsVirtualBankAccountsListResponse.cs` |
| 17 | `VirtualBankAccounts.CreateVirtualBankAccount` | `CreateVirtualBankAccount(CreateVirtualBankAccountRequest request, …)` · req: `IdempotencyKey: Guid`. `Body: PaymentsVirtualBankAccountCreateRequest?` | `PaymentsVirtualBankAccountCreateRequest` (read declaring file at impl — ties VBA to account group) | `PaymentsVirtualBankAccountsResponse` → IBAN | VirtualBankAccounts.md; `Models/PaymentsVirtualBankAccountCreateRequest.cs`; `Models/PaymentsVirtualBankAccountsResponse.cs` |
| 18 | `PortfoliosRebalancing.TriggerPortfolioRebalancing` | `TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest request, …)` · req: `IdempotencyKey: Guid`. `Body: TriggerPortfolioRebalancingRequest?` | `TriggerPortfolioRebalancingRequest` (AnyOf) → **`Allocations`** arm (`Models`): `AllocationsValue (allocations): IReadOnlyList<Guid>, req` (the model's AllocationId) | `TriggerPortfolioRebalancingResponse`: `Id: Guid` (→ realignmentId = execution id) | PortfoliosRebalancing.md; `Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`; `Models/Allocations.cs`; `Models/TriggerPortfolioRebalancingResponse.cs` |
| 19 | `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` | `RetrievePortfoliosRebalancingExecution(RetrievePortfoliosRebalancingExecutionRequest request, …)` · req: `ExecutionId: Guid` | — | `PortfoliosRebalancingExecution`: `Id`, `CreatedAt`, `UpdatedAt`, `Accounts?`, `Allocations?` — **no status field** | PortfoliosRebalancing.md; `Models/PortfoliosRebalancingExecution.cs` |
| 20 | `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` | `ListPortfolioRebalancingExecutionOrders(ListPortfolioRebalancingExecutionOrdersRequest request, …)` · req: `ExecutionId: Guid` | query: `sort,order,offset,limit,start_date,end_date` | `PortfoliosRebalancingExecutionOrderListResponse` → `data: IReadOnlyList<PortfoliosRebalancingExecutionOrder>` (each `Status: Status71` NEW/PROCESSING/FILLED/CANCELLED) + `Meta` | PortfoliosRebalancing.md; `Models/PortfoliosRebalancingExecutionOrder.cs`; `Models/Enums/Status71.cs` |
| 21 | `Valuations.GetAccountValuation` | `GetAccountValuation(GetAccountValuationRequest request, …)` · req: `AccountId: Guid`, `PriceQuality: PriceQuality3` | query: `price_quality` | `AccountValuation`: `TotalSecurityValue{Amount: string, Currency}` (→ totalValue) · `SecurityPositions: IReadOnlyList<AccountValuationSecurityPosition>` each `Instrument: Instrument6{Uuid, Isin?}` + `Quantity: string` + `Value: Value?{Amount: string}` (→ holdings) | Valuations.md; `Models/AccountValuation.cs`; `Models/AccountValuationSecurityPosition.cs`; `Models/TotalSecurityValue.cs`; `Models/Value.cs`; `Models/Instrument6.cs`; `Models/Enums/PriceQuality3.cs` |
| 22 | `Instruments.ListInstruments` | `ListInstruments(ListInstrumentsRequest request, …)` · no required | query: `trading_status,sort,order,offset,limit` | `InstrumentsListResponse` → instrument data (ISINs) + `Meta` | Instruments.md; `Models/InstrumentsListResponse.cs` |

### Enum value tables (needed)

| Enum (`Models.Enums`) | Members (C# → wire) | Used by |
| --- | --- | --- |
| `Status` | `Active→ACTIVE`, `Inactive→INACTIVE`, `Offboarding→OFFBOARDING`, `Offboarded→OFFBOARDED` (open enum) | investor status read-back (ops 1,2) |
| `Status65` | `New→NEW`, `Processing→PROCESSING`, `Filled→FILLED`, `Settled→SETTLED`, `Cancelled→CANCELLED` (open) | payment status (ops 13,14) |
| `Status71` | `New→NEW`, `Processing→PROCESSING`, `Filled→FILLED`, `Cancelled→CANCELLED` (open — confirm member names in `Models/Enums/Status71.cs` at impl) | realignment status (op 20) |
| `Type13` | `Personal→PERSONAL` (+ CHILD/FRENCH_PEA/ISA/JOINT/LEGAL_ENTITY/PENSION_DE_CHILD) | account group (op 3) |
| `Type16` | `Trading→TRADING`, `Portfolio→PORTFOLIO` | account type (op 5) — PORTFOLIO holds a managed portfolio |
| `PriceQuality3` | `Eod→EOD`, `HighestAvailable→HIGHEST_AVAILABLE` | valuation (op 21) |
| `InstrumentIdType4` | `Isin→ISIN` (default on `Allocation`) | model funds (op 7/8) — confirm at impl |
| `Currency` | `EUR`, `GBP` — confirm member names in `Models/Enums/Currency.cs` at impl | order (op 13) |
| `Side15` | `BUY`, `SELL` — confirm member names in `Models/Enums/Side15.cs` at impl | order (op 13) |

All enums are `OpenStringEnum<T>` (not C# enums): use the static members (`Status65.Filled`), resolve raw values with `TryGetKnownValue`, branch with the generated `Match` whose `otherwise` arm receives undeclared values. Enum bodies are open — treat an unrecognised wire value as "unknown/keep-polling", never crash.

### Client construction / auth / server node (Source: `sdk-map.md` "Getting a client", "Request signing", "Servers & auth")

- Client: `UpvestInvestmentApi.UpvestInvestmentApiClient(HttpClient httpClient, UpvestInvestmentApiClientOptions options)`; DI helper `services.AddUpvestInvestmentApiClient(options => …)`. Every API group is a property (`client.Users`, `client.Portfolios`, …).
- OAuth: `options.OauthClientCredentials = new UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials { ClientId = <Upvest:ClientId>, ClientSecret = <Upvest:ClientSecret> }`.
- **Request signing is required on EVERY call** (incl. the token request). Set `options.Hooks = [ new UpvestInvestmentApi.Signing.UpvestSigningHook(UpvestInvestmentApi.Signing.UpvestHttpSignatureCredentials.FromFile(keyId: <Upvest:SigningKeyId>, privateKeyPath: <Upvest:SigningKeyPath>, upvestClientId: <Upvest:ClientId>, privateKeyPassphrase: <Upvest:SigningKeyPassphrase>)) ]`. If no hook and no signing env vars are set, every call throws `InvalidOperationException` before being sent.
- Base URL: bind `Upvest:BaseUrl` verbatim and set it via `options.Server.Default.Production.BaseUrl` (default `ServerEnvironment.Production` = `https://sandbox.upvest.co`). Use the configured value directly rather than relying on the environment default.
- Config section `Upvest` keys (exactly): `ClientId`, `ClientSecret`, `SigningKeyId`, `SigningKeyPath`, `SigningKeyPassphrase`, `BaseUrl`.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| The `modelId` an investor chooses must be a known model (→ its Upvest `AllocationId`) | `Portfolios.CreatePortfoliosConfiguration`/`UpdatePortfoliosConfiguration` (10/11) ← app `ModelPortfolio` store, itself populated from `Portfolios.CreatePortfoliosAllocation` (7) | In `PUT /api/portfolio/investor/model`: look up `ModelPortfolio` by route `modelId`; 404/400 if absent, before any SDK call |
| The `{modelId}` realigned must be a known model | `PortfoliosRebalancing.TriggerPortfolioRebalancing` (18) ← app `ModelPortfolio` store (op 7) | In `POST /api/portfolio/models/{modelId}/realignments`: resolve `modelId`→`AllocationId`; 404 if absent, before op 18 |
| Each fund `instrumentId` (ISIN) in a model must be a real Upvest instrument | `Portfolios.CreatePortfoliosAllocation`/`UpdatePortfoliosAllocation` (7/8) ← Upvest's own validation at op 7/8 | **DECIDED.** Op 22 (`ListInstruments`, `instruments:read`) is out of this sandbox client's scope, so a pre-flight instrument read would 401 and block every model. Instead `PortfolioService.ValidateModel` validates ISIN **format** locally (`^[A-Z]{2}[A-Z0-9]{9}[0-9]$`) + weights-sum-to-exactly-100 + no-duplicate-fund, and relies on Upvest's own 400 at `CreatePortfoliosAllocation` to reject an ISIN that is validly-formed but unknown (surfaced as 400 to the operator). Op 22 is therefore dropped from scope. |
| A payer must be an `active` investor with a chosen model | app precondition ← `Investor` store (ops 1–6) + `PortfoliosConfiguration` (10/12) | In `POST /api/portfolio/payments`: refuse (409/422) before any SDK call if the caller has no `active` Investor or no `ChosenAllocationId` |
| A realignment id / order id / account id used in a read must belong to the caller's own records | reads (12,14,19,20,21) ← `Investor`/`PortfolioPayment`/`ModelPortfolio` stores | Every self endpoint resolves ids from the caller's own stored records only — never accepts a raw Upvest id from the client for another shopper |

---

## 3. Trap notes

- **Client build & DI**: the `UpvestInvestmentApiClient` owns no `HttpClient` lifetime decisions you can guess — registration lifetime and `HttpClient` ownership change behaviour under concurrency. **MUST load upvest-v4:dotnet-client-initialization.**
- **Auth + signing wiring**: a missing/blank signing credential or OAuth secret fails in a scheme-specific way, and the signing hook vs. env-var precedence is not obvious from the options surface. **MUST load upvest-v4:dotnet-authentication.**
- **Building AnyOf request bodies** (`UserCreateRequest`, `AccountCreateRequest`, `AccountGroupCreateRequest`, `TriggerPortfolioRebalancingRequest`) and reading AnyOf responses (`UserCreateRequest1`, `UserGetResponse`, `AccountCreateResponse`, `AccountGroupCreateResponse`): the factory/implicit-conversion and `TryGet…`/`Match` mechanics, open-enum `Match` arms, decimal-string amounts, and `DateTimeOffset` date-only fields all have traps a signature can't show. **MUST load upvest-v4:dotnet-models.**
- **Calling an operation**: the per-operation request record, `requestOptions`/`cancellationToken`, and response-envelope unwrapping. **MUST load upvest-v4:dotnet-calling-endpoints.**
- **Error boundary**: which exception types actually reach your catch, Case A `TryGet…` vs `RawError`, and reading status/correlation from error bodies. **MUST load upvest-v4:dotnet-error-handling.**
- **Timeouts/retries/pagination/logging**: what `Timeout` bounds vs a whole-call budget, which verbs retry, how manual offset/limit paging terminates, and how logging can leak bodies. **MUST load upvest-v4:dotnet-configuration-resilience.**
- **Testing the SDK seam**: the `HttpClient` stub seam and faking the clock/poll loop for status read-back. **MUST load upvest-v4:dotnet-testing.**

---

## 4. REQUIRED READING (load all before implementation starts)

- `upvest-v4:dotnet-client-initialization` — client construction & DI (Flow setup).
- `upvest-v4:dotnet-authentication` — OAuth + required request signing (all flows).
- `upvest-v4:dotnet-models` — AnyOf unions, open enums, decimal-string amounts, dates (all write/read bodies).
- `upvest-v4:dotnet-calling-endpoints` — request records, requestOptions, response envelopes (all ops).
- `upvest-v4:dotnet-error-handling` — error boundary (all ops). **Always required.** Hazard row: a body that does not match its declared type — a drifted/malformed **2xx** (missing `required` member) or a **non-2xx** body that does not match the operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** `ApiException<TError>`; a catch ladder handling only `ApiException<{Operation}Error>` lets it escape, so also catch `ResponseDeserializationException` (or `ApiException`).
- `upvest-v4:dotnet-configuration-resilience` — timeouts, retries, manual paging, logging posture (payments/realignment/valuation).
- `upvest-v4:dotnet-testing` — SDK stub seam and clock/poll faking (status read-back tests).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | At DI registration, bind the `Upvest` section and assert non-blank `ClientId`, `ClientSecret`, `SigningKeyId`, `SigningKeyPath`, `BaseUrl` (and `SigningKeyPassphrase` if the key is encrypted) — throw at startup so the host refuses to boot, rather than discovering it as `InvalidOperationException` (signing) or `AuthSchemeException` (OAuth) on the first call. Every multi-part part is checked: a blank part is not a missing one. |
| 2 | Secret sourcing & rotation | Secrets come from the `Upvest` config section (env vars / user-secrets; never source). The DI registration builds `UpvestInvestmentApiClientOptions` once and captures it in the client singleton, so a rotated `ClientSecret`/passphrase takes effect only on process restart. No hot-rotation required for this sandbox task; state this limitation. |
| 3 | Total timeout budget | **DECIDED.** Per-attempt bound = 30s, set on both `options.Retry.Timeout` and the named `HttpClient.Timeout` in `UpvestServiceCollectionExtensions`. Whole-call budget = the caller's `CancellationToken` (ASP.NET `RequestAborted`), threaded through every endpoint → service → gateway call, so a disconnected client cancels outbound work. No extra per-call `CancellationTokenSource` layer was added; the request-scoped token is the single budget. |
| 4 | Write-retry ownership | Every write in scope carries a caller-supplied `IdempotencyKey`, so an idempotent resend is safe. Which verbs the SDK auto-resends is under *Retries* in `dotnet-configuration-resilience` (loaded); confirm POST/PUT retry posture there before enabling retries on writes. |
| 5 | Idempotency & ambiguous writes | **Real key present**: every write (ops 1,3,5,7,10,11,13,15,17,18) has a required `IdempotencyKey: Guid` on its request record — the genuine dedup key. Persist each generated key on the owning app record (`Investor`/`ModelPortfolio`/`PortfolioPayment`) so a retry replays the **same** key. The injected `Idempotency-Key` header is not this key. Reconciliation path for an ambiguous write: replay the stored key, or re-read by stored reference (see UNKNOWN OUTCOMES). |
| 6 | Observability | **DECIDED.** Log at Information the app operation + the Upvest resource id returned (userId/accountId/allocationId/orderId/executionId) in `PortfolioService`/`UpvestPortfolioGateway`; **never** request bodies. On error, `UpvestPortfolioGateway.TranslateApiException` logs the operation + provider HTTP status at Warning. The provider `request_id` lives in the typed `{Operation}Error` body (`TryGetNoContent`→`RawError`), but that body can echo the request's personal data, so it is deliberately **not** logged — the strict "personal details never in logs" rule wins over richer correlation. Scope failures surface as `AuthSchemeException` (the token request is refused) and are logged at Warning with the operation name only. |
| 7 | Sensitive data | **Scope carries personal data** (name, email, birth info, address, nationality, and task-level taxId/identityDocument). Keep `options.Logging.LogRequestBody` OFF; set `options.Logging.LoggerFactory` explicitly in production (do not leave it unset — that arms `UPVEST_INVESTMENT_API_LOG=trace` body logging); never echo request bodies in app diagnostics on these paths; personal details are never written to logs (task rule). Add any form-body sensitive keys to the deny-list per `dotnet-configuration-resilience`. |
| 8 | Environment selection | One server group `Default`. `ServerEnvironment.Production`→`https://sandbox.upvest.co` (sandbox), `Environment2`→`https://api.upvest.co` (live). This task binds `Upvest:BaseUrl` verbatim onto `options.Server.Default.Production.BaseUrl` and runs against the sandbox; production deployment would point BaseUrl at the live host. Test traffic is kept off live by the BaseUrl binding + the sandbox being the default. |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. |
| 10 | Partial results | See PAGED READS. |
| 11 | Unknown outcomes | See UNKNOWN OUTCOMES. |

**DUPLICATE CLAIMS**

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `POST /api/portfolio/investor` (ops 1,3,5 — must not create two Upvest users for one shopper) | `Investor` row, PK = caller `ShopperUserName`, in `CatalogContext` | PK insert conflict on `AddAsync`+`SaveChanges` (a PK is refused even by the EF in-memory provider; a read-first check is not a refusal) | `catch (Exception)` around the claim save → reload by PK and return the existing investor | `PortfolioService.GetOrCreateInvestorClaimAsync` (claim: `_investors.AddAsync`), then the onboarding writes in `RegisterInvestorAsync` |
| `POST /api/portfolio/payments` (ops 15,13 — a double-submit must not place two orders) | `PortfolioPayment` row, PK = `PaymentId` = the caller's idempotency key when supplied, in `CatalogContext` | PK insert conflict on the claim save before the SDK calls | `catch (Exception)` → reload by PK and return the already-recorded payment | `PortfolioService.ClaimPaymentAsync` (claim: `_payments.AddAsync`), then `CreditCashAsync`/`PlaceBuyOrderAsync` in `PayInAsync` |

Order per row: **claim write → SDK call(s) → record result**. Claim is written before any SDK call so a second caller is stopped before reaching Upvest; released if the provider refuses the call.

**PAGED READS**

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `Portfolios.ListPortfoliosAllocations` (op 9) | `limit` query param; response `Meta` | Full set accumulated across pages (never a single truncated page). NOTE: `GET /api/portfolio/models` is served from the **app store** (`ListModelsAsync`), the authoritative record of the model-id→funds the operator set; this gateway method is implemented but not on the GET-models path. | `UpvestPortfolioGateway.ListModelAllocationsAsync` (while-loop on `page.Meta.Offset+Count >= TotalCount`) |
| `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (op 20, realignment status) | `limit`; `Meta` | All orders accumulated before deriving status; an incomplete page would wrongly report "completed" | `UpvestPortfolioGateway.GetRealignmentStatusAsync` (paging loop, then `AggregateRealignmentStatus`) |
| `VirtualBankAccounts.ListVirtualBankAccounts` (op 16) | `limit`; `Meta` | Page until the matching IBAN is found or `Meta` exhausted | `UpvestPortfolioGateway.EnsureVirtualBankAccountIbanAsync` (paging loop) |

(`Valuations.GetAccountValuation` op 21 returns the full `security_positions` set in one response — not paged.)

**UNKNOWN OUTCOMES**

**DECIDED — uniform reconciliation by idempotency-key replay.** Every write carries a required, caller-supplied `IdempotencyKey` (the real dedup key), persisted on the owning app record *before* the call. On a connection failure (`SdkConnectionException`/`SdkTimeoutException`) `UpvestPortfolioGateway.ExecuteAsync` **replays the same call once with the same idempotency key**: if the first attempt reached Upvest, the replay returns the original resource (deduplicated); if it did not, the replay performs the write. If the replay also cannot reach the provider, the outcome is marked unknown (`UpvestIntegrationException.OutcomeUnknown = true`) and the API boundary returns 502 "did not confirm — check status before retrying", never a bare failure that would prompt a double-write. This replaces the per-write re-read helpers originally sketched; it uses only grounded in-scope writes and needs no extra list operations.

| Write | How the outcome is settled | Where in the code |
| --- | --- | --- |
| `Users.CreateUser` (1), `CreateAccountGroup` (3), `CreateAccount` (5) | replay same `IdempotencyKey` once; a cross-request resubmit is stopped earlier by the `Investor` PK claim and resumes from persisted ids | `ExecuteAsync(..., isWrite:true)` replay; resume in `RegisterInvestorAsync` |
| `CreatePortfoliosAllocation` (7) / `Update` (8) | replay same `IdempotencyKey`; app `ModelPortfolio` persisted before the call so GET models reflects intent regardless | `ExecuteAsync` replay; `UpsertModelAsync` |
| `CreatePortfoliosConfiguration` (10) / `Update` (11) | replay same `IdempotencyKey` | `ExecuteAsync` replay; `SetFollowedModelAsync` |
| `Tests.CreateBankTransaction` (15) | replay same `IdempotencyKey` (idempotent; does not re-credit) | `ExecuteAsync` replay; `CreditCashAsync` |
| `CreatePortfoliosOrder` (13) | replay same `IdempotencyKey`; status later reconciled by read-back (op 14) in `GetPaymentsAsync` | `ExecuteAsync` replay; `PlaceBuyOrderAsync` |
| `TriggerPortfolioRebalancing` (18) | replay same `IdempotencyKey`; status by read-back (ops 19/20) | `ExecuteAsync` replay; `TriggerRealignmentAsync` |

NOTE on the connection-failure test: the replay path is covered by design in `ExecuteAsync`; a dedicated stubbed-`HttpClient` connection-failure test was not added because exercising the gateway at the HTTP seam requires valid request-signing material (the SDK signs every call, including in tests). The mapping/aggregation and service-orchestration paths are unit-tested instead; the live sandbox exercised the real error-translation path (CreateUser 400, scope `AuthSchemeException`→502).

---

## 6. Assumptions & Blockers

**Assumptions (intent) — all YOUR CALL items settled in code**
- **Operating model = TOL** — RESOLVED. `UpvestPortfolioGateway.BuildUserBody` builds `UserTolCreateRequest` and sends it via `UserCreateRequest.UserTolCreateRequest(...)`. Verified live: CreateUser succeeds against the sandbox (a real user id is returned).
- **taxId, taxCountry, identityDocument not forwarded** — RESOLVED. They are accepted in the app's `RegisterInvestorRequest`/`InvestorRegistration` (honoring the task's sign-up form) but `UserTolCreateRequest` has no such fields, so they are not sent to `CreateUser`. **`taxCountry` is used meaningfully**: `Fatca.Status` = (taxCountry == "US"). `taxId` and `identityDocument` are collected-but-not-forwarded (the shop's own KYC record). This is a design decision, **not a reportable gap**: the core capability — become an investor with an account able to hold a managed portfolio — is fully covered by the map; the SDK's TOL onboarding simply does not take these fields.
- **Investor status mapping** — RESOLVED in `GetInvestorStatusAsync`: `rejected` if the user is `OFFBOARDED`/`OFFBOARDING` or the account group/account is `CLOSED`/`LOCKED`; `active` once both the account group and the PORTFOLIO account are `ACTIVE`; otherwise `pending`. `GET /api/portfolio/investor` falls back to the last-known persisted status if the read-back is blocked (so a provider blip does not 5xx a status read).
- **Realignment status** — RESOLVED in `AggregateRealignmentStatus` (across all op-20 pages): `pending` if any order is `NEW`/`PROCESSING`; `completed` if all terminal orders are `FILLED` or benign-`CANCELLED` (`PORTFOLIO_IS_BALANCED`); `failed` if any order is `CANCELLED` for another reason; empty → `pending`.
- **Money-in path** — credit via `Tests.CreateBankTransaction` to the account group's virtual-bank-account IBAN, then invest with a `BUY` `CreatePortfoliosOrder`. **UNVERIFIED at runtime** (the sandbox client lacks the `payments`/`portfolios` scopes, so these calls return a scope `AuthSchemeException` — see Operating condition). The explicit BUY is kept; if a deployment with `AUTO_INVESTMENT` auto-invests incoming cash, the explicit order would be redundant and should be removed then.
- **Currency = EUR**; amounts are serialized as two-decimal strings (`FormatAmount`) matching the wire regexes.
- **Model identity** — the app owns `{modelId}` and maps it to Upvest's `AllocationId`; PUT is create-or-replace keyed on the app `modelId` (`UpsertModelAsync`).

**Scope-401 manifestation (learned live).** The sandbox client is authorised for *some* user operations only. A call outside its scope fails when the SDK requests an **OAuth token for that operation's scope** — the token endpoint returns `401`, surfaced as `AuthSchemeException` (not `ApiException<TError>`). `ExecuteAsync` catches it and maps it to a 502. Notably even `RetrieveUser` (read) is outside scope here, while `CreateUser` (write) is inside it — so onboarding creates the user live, then stops at `CreateAccountGroup`.

**Operating condition (not a blocker — per task's sandbox note)**
- This sandbox client is authorised **only for user operations** (`users:*`). Every call needing another scope — `accounts:admin/read`, `portfolios:admin/read`, `positions:read`, `valuations:read`, `payments:admin/read`, `tests:admin`, `instruments:read` — will be refused with 401 ("This client is not authorised to use these scopes"). That covers ops 3–22 here. Per the task this is the environment, not a code defect and not a gap to report: build every flow in full and verify against the sandbox as far as the user scope allows (ops 1,2 end-to-end; the rest wired and unit-tested with a stubbed `HttpClient`). The error boundary must surface this 401 cleanly rather than treating it as an app bug.

**Blockers**
- None. Every capability the task needs has a corresponding SDK operation grounded above; no invented data path was required.
