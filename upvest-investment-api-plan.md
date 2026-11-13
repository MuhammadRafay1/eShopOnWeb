# Upvest Investment API — Integration Plan (eShopOnWeb managed investment portfolio)

Plan for an **additive** "managed investment portfolio" capability on `src/PublicApi`, with Upvest as the
investment provider. All facts below were taken this session from the SDK map
(`marketplace/plugins/upvest-v4/sdk/dotnet/sdk-map.md` + `map/operations/*`) and the source files those pages
name, plus the Arazzo workflows under `repo/upvest-workflows/`. SDK source root (plugin-relative):
`<plugin-root>/sdk/dotnet/` — written below as `sdk/dotnet/`.

The app exposes **no webhook route**. Every status (sign-up, payment, realignment) is learned by the app's **own
re-reads** (polling `Retrieve…`/`List…`), never by a callback.

---

## 1. Scope & sequence

eShop stores (its own records; EF `IRepository<T>` over `CatalogContext`, `UseOnlyInMemoryDatabase=true`) the
mapping from a signed-in shopper (JWT `ClaimTypes.NameIdentifier`) to the Upvest ids it creates, plus payment and
realignment records. Upvest never learns the shopper's eShop identity; the app never shows one shopper another's row.

**Flow 1 — Becoming an investor** (`POST/GET /api/portfolio/investor`)
1. `Users.CreateUser` (TOL body) → Upvest `user_id`, status `INACTIVE`.
2. `UserChecks.CreateUserCheck` ×2 — `KYC` (the identity doc the shop checked) and `INSTRUMENT_FIT`. 202/async.
3. `TaxResidencies.SetTaxResidencies` — `taxId` + `taxCountry` (full-replace; always send the complete set).
4. `UserIdentifiers.CreateIdentifier` — reporting identifier; **skip** when nationality ∈ {AT, DE, FR, HU, IE, LU}
   (Upvest auto-generates). Optional per workflow `user-identifiers`.
5. `AccountGroups.CreateAccountGroup` — `AccountGroupCreateUserRequest` `type = PERSONAL`.
6. `AccountsApi.CreateAccount` — `AccountCreateUserRequest` `type = PORTFOLIO` (portfolio functionality requires it).
7. Status read = `Users.RetrieveUser` → `UserTol.Status` (`INACTIVE`→`ACTIVE`; see §6 for `rejected` mapping).
Persist `{shopperId → userId, accountGroupId, accountId, createUserIdempotencyKey}` as one `InvestorRecord`.

**Flow 2 — Model portfolios** (`PUT/GET /api/portfolio/models/{modelId}`, `PUT /api/portfolio/investor/model`)
- PUT model → `Portfolios.CreatePortfoliosAllocation` (first write) / `Portfolios.UpdatePortfoliosAllocation` (replace
  by `allocation_id`). Body `allocation` = list of `Allocation{instrument_id, instrument_id_type=ISIN, weight}`;
  weights must sum to 100. OPERATOR/admin-only.
- GET models → `Portfolios.ListPortfoliosAllocations` (paged); each entry read via its `Allocation` list.
- Investor chooses model → `Portfolios.CreatePortfoliosConfiguration` (links `account_id`→`allocation_id`);
  switch later → `Portfolios.UpdatePortfoliosConfiguration` (`account_id` + `IdempotencyKey`, PATCH).
- App stores `{modelId → allocation_id}` so the shop's `{modelId}` path value maps to Upvest's UUID.

**Flow 3 — Paying in** (`POST/GET /api/portfolio/payments`)
- Refuse unless caller is an accepted investor (`InvestorRecord` with `accountId`) **and** a configuration exists.
- `TopUps.CreateTopup` — credits cash to the `account_group_id` (sandbox money-in: top-up, lifecycle
  `CONFIRMED`→`SETTLED`). Then `Portfolios.CreatePortfoliosOrder` — `side=BUY`, `cash_amount`, distributed across
  the model's funds by their weights.
- Status re-reads: `TopUps.RetrieveTopup` + `Portfolios.RetrievePortfoliosOrder`
  (`NEW`/`PROCESSING`→pending, `FILLED`/`SETTLED`→invested, `CANCELLED`→failed).
- `GET payments` newest-first from the app's own `PaymentRecord` store (guarantees isolation + topup/order pairing).

**Flow 4 — Realignment** (`POST /api/portfolio/models/{modelId}/realignments`, `GET …/realignments/{id}`)
- `PortfoliosRebalancing.TriggerPortfolioRebalancing` — `Body = Allocations{allocations:[allocation_id]}` (realign
  every account that follows that model). OPERATOR/admin-only. Returns `id` only.
- Status read: **the execution object carries no status field** (`PortfoliosRebalancingExecution` has none). Derive
  status from `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (the orders it created) aggregated to
  pending/completed/failed; confirm existence with `RetrievePortfoliosRebalancingExecution`. See §6.

**Flow 5 — The portfolio** (`GET /api/portfolio`)
- `modelId` ← `Portfolios.RetrievePortfoliosConfiguration(account_id).AllocationId` (mapped back to shop `modelId`).
- holdings + per-instrument value + `totalValue` ← `Valuations.GetAccountValuation` (`security_positions[]`:
  `instrument`, `quantity`, `value`; `total_security_value`). **`Positions.ListPositions` has no monetary value**
  (`Datum1` carries only quantities), so valuation is the source for value/total.
- `paidIn` ← sum of the app's `PaymentRecord` amounts for the caller.

A capability the map lacks would be a Blocker (§6); none invented.

---

## 2. CONTRACT SHEET

⚠ **Signatures below are generated code, verbatim.** Each operation that takes input takes **one request record as
its first parameter** (an operation with no inputs takes none), built with an **object initializer whose property
names are the record's own** — never flat positional arguments. The create/replace operations carry the request
*body* on a `Body` property of that record (e.g. `CreatePortfoliosAllocationRequest.Body`), which is optional on the
record even though the endpoint needs it.

⚠ **Every SDK type is written fully-qualified with the namespace its source path implies** (per the map's
*Namespaces by content type*): `sdk/dotnet/src/UpvestInvestmentApi/Models/…` → `UpvestInvestmentApi.Models`;
`…/Models/AnyOf/…` → `UpvestInvestmentApi.Models.AnyOf`; `…/Models/Enums/…` → `UpvestInvestmentApi.Models.Enums`;
`…/Requests/<Controller>/…` → `UpvestInvestmentApi.Requests.<Controller>`; `…/Errors/…` → `UpvestInvestmentApi.Errors`.
Take each type's namespace from the path the map gives for **that** type, never from a neighbour.

Every operation below is **Case A (typed)**, **throw-only**, **no pagination helper** (list ops page by `offset`/`limit`
query members; the response `Meta` carries `total_count`), **server group `Default`**, and its error accessor set is
`TryGetNoContent([NotNullWhen(true)] out RawError? value)` [the declared 4xx/5xx statuses] · `TryGetRawError(out RawError?)`
[fallback] unless noted. Auth on every row: `options.OauthClientCredentials` with the listed scopes (+ the required
`UpvestSigningHook`; see client facts). `UpvestApiVersion` defaults to `_1` on every request record — leave it.

### Flow 1 — onboarding

| Op (`client.X`) | Signature · request record (required members) | Body model + fields read/sent | Returns (inner fields read) | Scopes | Source |
| --- | --- | --- | --- | --- | --- |
| `Users.CreateUser` | `CreateUser(CreateUserRequest req, …)` — req: `IdempotencyKey: Guid, required`; `Body: UserCreateRequest?` | `UserCreateRequest` (AnyOf) → `UserTolCreateRequest`: `FirstName (first_name) req`, `LastName (last_name) req`, `Email (email) req`, `BirthDate (birth_date) req` (DateTimeOffset, date-only converter), `Nationalities (nationalities) req` (`IReadOnlyList<Nationality>`), `Address (address) req` (`Address2`: line1/postcode/city/country), `Fatca (fatca) req`, `BirthCity (birth_city)` + `BirthCountry (birth_country)` **send both — backend enforces despite optional schema**, `PhoneNumber (phone_number)`, `TermsAndConditions`, `DataPrivacyAndSharingAgreement` | `UserCreateRequest1` (AnyOf) → `UserTol`: `Id`, `Status` | `users:admin` | `map/operations/Users.md`; `sdk/.../Requests/Users/CreateUserRequest.cs`; `sdk/.../Models/UserTolCreateRequest.cs`; `sdk/.../Models/AnyOf/UserCreateRequest.cs`, `UserCreateRequest1.cs`; `sdk/.../Models/UserTol.cs` |
| `UserChecks.CreateUserCheck` | `CreateUserCheck(CreateUserCheckRequest req, …)` — req: `UserId: Guid, required` (+ `Body: UserCheckCreateRequest?`) | `UserCheckCreateRequest` (AnyOf) — KYC arm: `type=KYC`, `check_confirmed_at`, `data_download_link`, `document_type`, `provider`, `method`, identity-doc fields (`document_expiration_date`, `nationality`); INSTRUMENT_FIT arm: `type=INSTRUMENT_FIT`, `instrument_suitability`. 202/async | `UserCheckCreateResponse`: `id` | `checks:admin` | `map/operations/UserChecks.md`; `sdk/.../Models/AnyOf/UserCheckCreateRequest.cs` (read arms at build time) |
| `TaxResidencies.SetTaxResidencies` | `SetTaxResidencies(SetTaxResidenciesRequest req, …)` — req: `UserId: Guid`, `IdempotencyKey: Guid`, both required | `TaxResidenciesSetRequest`: `tax_residencies` (each: `country` + `tax_identifier_number` **xor** `missing_tin_reason`). **Full-replace** — always send the whole set | `TaxResidencyRecord` | `taxes:admin` | `map/operations/TaxResidencies.md`; `sdk/.../Models/TaxResidenciesSetRequest.cs` |
| `UserIdentifiers.CreateIdentifier` | `CreateIdentifier(CreateIdentifierRequest req, …)` — req: `UserId: Guid, required` | `IdentifierCreateRequest`: `type` (NATIONAL_ID/PASSPORT), `issuing_country`, `identifier`, `identifier_standard?`. Skip for CONCAT nationalities | `Identifier`: `Id` | `users:admin` | `map/operations/UserIdentifiers.md`; `sdk/.../Models/IdentifierCreateRequest.cs` |
| `AccountGroups.CreateAccountGroup` | `CreateAccountGroup(CreateAccountGroupRequest req, …)` — req: `IdempotencyKey: Guid, required` (+ `Body: AccountGroupCreateRequest?`) | `AccountGroupCreateRequest` (AnyOf) → `AccountGroupCreateUserRequest`: `UserId (user_id) req`, `Type (type) req = Type13.Personal` | `AccountGroupCreateResponse` (AnyOf): `id`, `status` | `accounts:admin` | `map/operations/AccountGroups.md`; `sdk/.../Models/AccountGroupCreateUserRequest.cs`; `sdk/.../Models/AnyOf/AccountGroupCreateRequest.cs` |
| `AccountsApi.CreateAccount` | `CreateAccount(CreateAccountRequest req, …)` — req: `IdempotencyKey: Guid, required` (+ `Body: AccountCreateRequest?`) | `AccountCreateRequest` (AnyOf) → `AccountCreateUserRequest`: `UserId (user_id) req`, `AccountGroupId (account_group_id) req`, `Type (type) req = Type16.Portfolio`, `Name?` | `AccountCreateResponse` (AnyOf): `id` | `accounts:admin` | `map/operations/AccountsApi.md`; `sdk/.../Models/AccountCreateUserRequest.cs`; `sdk/.../Models/AnyOf/AccountCreateRequest.cs` |
| `Users.RetrieveUser` | `RetrieveUser(RetrieveUserRequest req, …)` — req: `UserId: Guid, required` | — | `UserGetResponse` (AnyOf) → `UserTol.Status` (`Status` enum) | `users:read`/`users:admin` | `map/operations/Users.md`; `sdk/.../Models/AnyOf/UserGetResponse.cs`; `sdk/.../Models/UserTol.cs` |

### Flow 2 — models

| Op (`client.X`) | Signature · request record (required members) | Body model + fields | Returns (inner fields read) | Scopes | Source |
| --- | --- | --- | --- | --- | --- |
| `Portfolios.CreatePortfoliosAllocation` | `CreatePortfoliosAllocation(CreatePortfoliosAllocationRequest req, …)` — req: `IdempotencyKey: Guid, required`; `Body: PortfoliosAllocationCreateRequest?` | `PortfoliosAllocationCreateRequest`: `Allocation (allocation) req` = `IReadOnlyList<Allocation>`; `Name (name)?`. `Allocation`: `InstrumentId (instrument_id) req` (`InstrumentId` union — pass ISIN via `InstrumentId.String("…")`/implicit string), `InstrumentIdType (instrument_id_type) = Isin`, `Weight (weight) req` (string, regex `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`) | `PortfoliosAllocation`: `Id`, `Name`, `Allocation` | `portfolios:admin` | `map/operations/Portfolios.md`; `sdk/.../Requests/Portfolios/CreatePortfoliosAllocationRequest.cs`; `sdk/.../Models/PortfoliosAllocationCreateRequest.cs`, `Allocation.cs`, `AnyOf/InstrumentId.cs`, `PortfoliosAllocation.cs` |
| `Portfolios.UpdatePortfoliosAllocation` | `UpdatePortfoliosAllocation(UpdatePortfoliosAllocationRequest req, …)` — req: `AllocationId: Guid, required` (+ `Body: PortfoliosAllocationUpdateRequest?`) | `PortfoliosAllocationUpdateRequest` — **full replace**, send all instruments | `PortfoliosAllocation` | `portfolios:admin` | `map/operations/Portfolios.md`; `sdk/.../Models/PortfoliosAllocationUpdateRequest.cs` |
| `Portfolios.ListPortfoliosAllocations` | `ListPortfoliosAllocations(ListPortfoliosAllocationsRequest req, …)` | query: `Offset`, `Limit`, `Sort`, `Order`, … | `PortfoliosAllocationsListResponse`: `data[]` (each `PortfoliosAllocation`), `meta.total_count` | `portfolios:read`/`admin` | `map/operations/Portfolios.md`; `sdk/.../Models/PortfoliosAllocationsListResponse.cs`, `Meta.cs` |
| `Portfolios.RetrievePortfoliosAllocation` | `RetrievePortfoliosAllocation(…Request req, …)` — req: `AllocationId: Guid, required` | — | `PortfoliosAllocation` | `portfolios:read`/`admin` | `map/operations/Portfolios.md` |
| `Portfolios.CreatePortfoliosConfiguration` | `CreatePortfoliosConfiguration(CreatePortfoliosConfigurationRequest req, …)` — req: `IdempotencyKey: Guid, required` (+ `Body`) | `PortfoliosConfigurationCreateRequest`: `AccountId (account_id) req`, `AllocationId (allocation_id) req`, `RebalancingStrategyIds?` | `PortfoliosConfiguration`: `AccountId`, `AllocationId` | `portfolios:admin` | `map/operations/Portfolios.md`; `sdk/.../Models/PortfoliosConfigurationCreateRequest.cs`, `PortfoliosConfiguration.cs` |
| `Portfolios.UpdatePortfoliosConfiguration` | `UpdatePortfoliosConfiguration(…Request req, …)` — req: `AccountId: Guid`, `IdempotencyKey: Guid`, both required (+ `Body`) | `PortfoliosConfigurationUpdateRequest`: `allocation_id` (switch model), `rebalancing_strategy_ids?` | `PortfoliosConfiguration` | `portfolios:admin` | `map/operations/Portfolios.md`; `sdk/.../Models/PortfoliosConfigurationUpdateRequest.cs` |
| `Portfolios.RetrievePortfoliosConfiguration` | `RetrievePortfoliosConfiguration(…Request req, …)` — req: `AccountId: Guid, required` | — | `PortfoliosConfiguration`: `AllocationId` (= chosen modelId) | `portfolios:read`/`admin` | `map/operations/Portfolios.md` |

### Flow 3 — pay in

| Op (`client.X`) | Signature · request record (required members) | Body model + fields | Returns (inner fields read) | Scopes | Source |
| --- | --- | --- | --- | --- | --- |
| `TopUps.CreateTopup` | `CreateTopup(CreateTopupRequest req, …)` — req: `IdempotencyKey: Guid, required` (+ `Body`) | `PaymentsTopUpCreateRequest`: `AccountGroupId (account_group_id) req`, `CashAmount (cash_amount) req` (string, regex `^[0-9]{1,9}(\.[0-9]{2})?$`), `Currency (currency) req = Currency.Eur` | `PaymentsTopupsResponse`: `Id`, `Status` (`Status29`: CONFIRMED/SETTLED/CANCELLED), `CashAmount` | `topups:admin` | `map/operations/TopUps.md`; `sdk/.../Models/PaymentsTopUpCreateRequest.cs`, `PaymentsTopupsResponse.cs` |
| `Portfolios.CreatePortfoliosOrder` | `CreatePortfoliosOrder(CreatePortfoliosOrderRequest req, …)` — req: `IdempotencyKey: Guid, required` (+ `Body`) | `PortfoliosOrderPlaceRequest`: `UserId (user_id) req`, `AccountId (account_id) req`, `CashAmount (cash_amount) req` (string), `Currency (currency) req = Eur`, `Side (side) req = Side15.Buy`, `PostTax?` | `PortfoliosOrder`: `Id`, `Status` (`Status65`: NEW/PROCESSING/FILLED/SETTLED/CANCELLED), `CancellationReason?` | `portfolios:admin` | `map/operations/Portfolios.md`; `sdk/.../Models/PortfoliosOrderPlaceRequest.cs`, `PortfoliosOrder.cs`; `sdk/.../Models/Enums/Side15.cs` |
| `TopUps.RetrieveTopup` | `RetrieveTopup(RetrieveTopupRequest req, …)` — req: `TopupId: Guid, required` | — | `PaymentsTopupsResponse`: `Status` | `topups:read`/`admin` | `map/operations/TopUps.md` |
| `Portfolios.RetrievePortfoliosOrder` | `RetrievePortfoliosOrder(…Request req, …)` — req: `PortfolioOrderId: Guid, required` | — | `PortfoliosOrder`: `Status`, `CancellationReason?` | `portfolios:read`/`admin` | `map/operations/Portfolios.md` |

### Flow 4 — realignment

| Op (`client.X`) | Signature · request record (required members) | Body model + fields | Returns (inner fields read) | Scopes | Source |
| --- | --- | --- | --- | --- | --- |
| `PortfoliosRebalancing.TriggerPortfolioRebalancing` | `TriggerPortfolioRebalancing(TriggerPortfolioRebalancingOperationRequest req, …)` — req: `IdempotencyKey: Guid, required`; `Body: TriggerPortfolioRebalancingRequest?` | `TriggerPortfolioRebalancingRequest` (AnyOf) — build via `TriggerPortfolioRebalancingRequest.Allocations(new Allocations{ AllocationsValue = [allocationId] })`. (`Accounts` arm is the alternative — use allocations, not both) | `TriggerPortfolioRebalancingResponse`: `Id` (only) | `portfolios:admin` | `map/operations/PortfoliosRebalancing.md`; `sdk/.../Requests/PortfoliosRebalancing/TriggerPortfolioRebalancingOperationRequest.cs`; `sdk/.../Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`; `sdk/.../Models/Allocations.cs`; `sdk/.../Models/TriggerPortfolioRebalancingResponse.cs` |
| `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` | `RetrievePortfoliosRebalancingExecution(…Request req, …)` — req: `ExecutionId: Guid, required` | — | `PortfoliosRebalancingExecution`: `Id`, `Accounts?`, `Allocations?` — **no status field** | `portfolios:read`/`admin` | `map/operations/PortfoliosRebalancing.md`; `sdk/.../Models/PortfoliosRebalancingExecution.cs` |
| `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` | `ListPortfolioRebalancingExecutionOrders(…Request req, …)` — req: `ExecutionId: Guid, required` | query: `Offset`, `Limit`, … | `PortfoliosRebalancingExecutionOrderListResponse`: `data[]` (per-order status), `meta.total_count` — aggregate to realignment status | `portfolios:read`/`admin` | `map/operations/PortfoliosRebalancing.md`; `sdk/.../Models/PortfoliosRebalancingExecutionOrderListResponse.cs` (read at build time) |

### Flow 5 — the portfolio

| Op (`client.X`) | Signature · request record (required members) | Returns (inner fields read) | Scopes | Source |
| --- | --- | --- | --- | --- |
| `Valuations.GetAccountValuation` | `GetAccountValuation(GetAccountValuationRequest req, …)` — req: `AccountId: Guid`, `PriceQuality: PriceQuality3`, both required (query `price_quality`) | `AccountValuation`: `TotalSecurityValue (total_security_value)` (→ totalValue), `SecurityPositions (security_positions)[]` = `AccountValuationSecurityPosition{Instrument, Quantity, Value?, Weight?}` (→ holdings: instrumentId/quantity/value) | `valuations:read` | `map/operations/Valuations.md`; `sdk/.../Models/AccountValuation.cs`, `AccountValuationSecurityPosition.cs`; `sdk/.../Models/Enums/PriceQuality3.cs` |
| `Positions.ListPositions` | `ListPositions(ListPositionsRequest req, …)` — req: `AccountId: Guid, required` (query `Offset`/`Limit`) | `PositionsListResponse`: `Data[]` = `Datum1{Instrument, Quantity, …}` (**no monetary value**), `Meta.TotalCount` | `positions:read` | `map/operations/Positions.md`; `sdk/.../Models/PositionsListResponse.cs`, `Datum1.cs`, `Meta.cs` |
| `Instruments.ListInstruments` | `ListInstruments(ListInstrumentsRequest req, …)` (query `TradingStatus`, `Offset`, `Limit`, …) | `InstrumentsListResponse`: `data[]`, `meta.total_count` | `instruments:read` | `map/operations/Instruments.md`; `sdk/.../Models/InstrumentsListResponse.cs` |
| `Instruments.RetrieveInstrument` | `RetrieveInstrument(RetrieveInstrumentRequest req, …)` — req: `InstrumentId: InstrumentUniqueIdentifier, required` | `InstrumentsResponse` (confirms a fund ISIN is tradable) | `instruments:read` | `map/operations/Instruments.md`; `sdk/.../Models/AnyOf/InstrumentUniqueIdentifier.cs` |

### Enum tables (only those used)

| Enum (source) | Values used |
| --- | --- |
| `Currency` (`Models/Enums/Currency.cs`) | `Currency.Eur` ("EUR"), `Currency.Gbp` ("GBP") — use `Eur` |
| `Side15` (`Models/Enums/Side15.cs`) | `Side15.Buy` ("BUY"), `Side15.Sell` ("SELL") — use `Buy` |
| `Status` user (`Models/Enums/Status.cs`) | `Active` ("ACTIVE"), `Inactive` ("INACTIVE"), `Offboarding`, `Offboarded` — **no PENDING/REJECTED** (see §6) |
| `Status29` topup (`Models/Enums/Status29.cs`) | `CONFIRMED`, `SETTLED`, `CANCELLED` |
| `Status65` order (`Models/Enums/Status65.cs`) | `New`, `Processing`, `Filled`, `Settled`, `Cancelled` |
| `Type13` acct-group (`Models/Enums/Type13.cs`) | `Personal` + others — use `Personal` |
| `Type16` account (`Models/Enums/Type16.cs`) | `Trading`, `Portfolio` — use `Portfolio` |
| `InstrumentIdType4` (`Models/Enums/InstrumentIdType4.cs`) | `Isin` (default), `Upvest` — use `Isin` |
| `PriceQuality3` (`Models/Enums/PriceQuality3.cs`) | read values at build time; pass to `GetAccountValuation` |

Enum members are **not** C# enums — use the static members (PascalCase, e.g. `Currency.Eur`); resolve a wire value via
`TryGetKnownValue`/`Match`.

### Client construction / auth / server facts

- Construct via DI: `services.AddUpvestInvestmentApiClient(options => { … })` **or** `new UpvestInvestmentApiClient(httpClient, options)`.
  Single constructor: `UpvestInvestmentApiClient(HttpClient, UpvestInvestmentApiClientOptions)`. Source `sdk-map.md`
  (*Getting a client*, *dotnet-client-initialization* for lifetime/HttpClient ownership).
- **OAuth2 client credentials**: `options.OauthClientCredentials = new OAuth2ClientCredentials { ClientId = …, ClientSecret = … }`
  (namespace `UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials`). The SDK fetches tokens from
  `https://sandbox.upvest.co/auth/token` itself per operation scope set — **do not call `AccessTokens.IssueToken`
  directly**. Source `sdk-map.md` *Servers & auth*.
- **Request signing is required on EVERY call** (incl. the token request). Provide
  `options.Hooks = [new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(keyId:…, privateKeyPath:…, upvestClientId:…, privateKeyPassphrase:…))]`.
  Without a signing hook (or its env vars) every call throws `InvalidOperationException` before being sent. Source
  `sdk-map.md` *Request signing*; `sdk/.../Signing/UpvestSigningHook.cs`, `UpvestHttpSignatureCredentials.cs`.
  Bind from config keys (task-mandated, values hard-coded nowhere): `Upvest:ClientId`, `Upvest:ClientSecret`,
  `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:SigningKeyPassphrase`, `Upvest:BaseUrl`.
- **Base URL**: set `options.Server.Default.Production.BaseUrl = <Upvest:BaseUrl verbatim>`; keep
  `options.Environment = ServerEnvironment.Production` (= sandbox `https://sandbox.upvest.co`). One server group
  (`Default`). `ServerEnvironment.Environment2` = live `https://api.upvest.co`. Source `sdk-map.md` *Servers & auth*.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| A fund `instrument_id` sent in a model (PUT model) must be a real tradable Upvest instrument | `Portfolios.CreatePortfoliosAllocation` ← `Instruments.ListInstruments` / `Instruments.RetrieveInstrument` | In the PUT-model handler, validate each `funds[].instrumentId` against `Instruments.RetrieveInstrument` (or a cached `ListInstruments` set) **before** calling `CreatePortfoliosAllocation`; reject 400 on miss |
| The `modelId` an investor chooses (PUT `/investor/model`) must be an existing model | `Portfolios.CreatePortfoliosConfiguration` / `UpdatePortfoliosConfiguration` ← app `ModelRecord` store (populated only by `CreatePortfoliosAllocation`) | In the choose-model handler, look up `modelId→allocation_id` in the app store **before** calling the configuration op; 404 if absent |
| The `{modelId}` a realignment targets must be an existing model | `PortfoliosRebalancing.TriggerPortfolioRebalancing` ← app `ModelRecord` store | In the realignment handler, resolve `modelId→allocation_id` from the app store **before** `TriggerPortfolioRebalancing`; 404 if absent |
| A payment is accepted only from an accepted investor who has chosen a model | `TopUps.CreateTopup` + `Portfolios.CreatePortfoliosOrder` ← app `InvestorRecord` (has `accountId`) + `Portfolios.RetrievePortfoliosConfiguration` (configuration exists) | In the payment handler, require the caller's `InvestorRecord` with `accountId`, and a configuration for that account, **before** either write; 409/422 otherwise |
| Each Upvest id a shopper reads/acts on must belong to that shopper | all per-caller reads/writes ← app `InvestorRecord`/`PaymentRecord` keyed by JWT `NameIdentifier` | Every handler resolves ids from the caller's own records keyed by the JWT subject; never accepts an Upvest id from the request body |

The map never states these; derived from the task. `funds[].instrumentId`, `modelId`, and every Upvest id are
caller/operator-supplied values whose legal set a different operation (or the app's own prior write) defines.

---

## 3. Trap notes

- **Money is a JSON number of euros (2dp) at the API boundary, but every Upvest cash field is a decimal *string*
  with its own regex** (`cash_amount` top-up `^[0-9]{1,9}(\.[0-9]{2})?$`; order `^[0-9]{0,63}(\.[0-9]{1,27})?$`;
  `weight` `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`). A wrong culture or format breaks the regex silently at serialize
  time. **MUST load `upvest-v4:dotnet-models`** (decimals, dates, open enums, union build/read).
- **`BirthDate` is `DateTimeOffset` with a date-only converter**; `Nationality`/`BirthCountry`/`Address2` are typed
  models/enums, not strings. **MUST load `upvest-v4:dotnet-models`**.
- **Union bodies** (`UserCreateRequest`, `UserCheckCreateRequest`, `AccountCreateRequest`, `AccountGroupCreateRequest`,
  `TriggerPortfolioRebalancingRequest`, `InstrumentId`): build via the static arm factory / implicit conversion, and
  a body no arm accepts round-trips through `Unknown`/`otherwise` — know which arm you are constructing.
  **MUST load `upvest-v4:dotnet-models`**.
- **Every non-GET already injects a random `Idempotency-Key` header, which deduplicates nothing**; the real key is the
  `IdempotencyKey: Guid` member on each create request record — persist it to replay safely. **MUST load
  `upvest-v4:dotnet-getting-started`** (Idempotency) and **`upvest-v4:dotnet-configuration-resilience`** (reconciliation).
- **A drifted 2xx body or a non-2xx body that does not match the operation's `{Operation}Error` surfaces as
  `ResponseDeserializationException` (an `ApiException`, not `ApiException<TError>`)** — a ladder catching only
  `ApiException<TError>` lets it escape. **MUST load `upvest-v4:dotnet-error-handling`**.
- **What `Retry`/`Timeout` actually bound, and which verbs the SDK may resend, are not visible from the signature**;
  the per-attempt vs whole-call distinction matters for the two-write payment path. **MUST load
  `upvest-v4:dotnet-configuration-resilience`**.
- **Auth/signing failures throw before the request is sent** (`InvalidOperationException` for a missing signing hook,
  `AuthSchemeException` for a missing OAuth credential) and look nothing like a 401. **MUST load
  `upvest-v4:dotnet-authentication`**.
- **Success status/headers are not on the return value** — only hooks expose them; relevant if a 202/async step needs
  `Retry-After`. **MUST load `upvest-v4:dotnet-configuration-resilience`**.
- **Stubbing the SDK for tests** needs the HttpClient seam, not a mock of the client. **MUST load
  `upvest-v4:dotnet-testing`**.

---

## 4. REQUIRED READING (load all before implementing)

| Skill | Governs |
| --- | --- |
| `upvest-v4:dotnet-client-initialization` | Building + DI-registering `UpvestInvestmentApiClient`, HttpClient lifetime |
| `upvest-v4:dotnet-authentication` | OAuth2 credential properties + the required `UpvestSigningHook`; fail-fast |
| `upvest-v4:dotnet-calling-endpoints` | First `client.{Group}.{Op}` call; `requestOptions`/`cancellationToken`; `Body` member |
| `upvest-v4:dotnet-models` | Decimal-string/date fields, open enums, AnyOf union build/read — pervasive here |
| `upvest-v4:dotnet-error-handling` | try/catch around every call; Case A accessors; **`ResponseDeserializationException`** (verbatim hazard row below) |
| `upvest-v4:dotnet-configuration-resilience` | Retries/timeouts, base-URL/server selection, pagination by `offset`, reconciliation, logging posture |
| `upvest-v4:dotnet-getting-started` | Idempotency (real key vs injected header), map lookups |
| `upvest-v4:dotnet-testing` | HttpClient stub seam, faking the clock, asserting outgoing requests |

**Hazard (verbatim):** a body that does not match its declared type — a drifted or malformed **2xx** response (a
missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error`
shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the
target type but is **not** an `ApiException<TError>`; a catch ladder that handles only `ApiException<TError>` lets it
escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | Bind `Upvest:*` via `IOptions`; at startup (DI registration) require **non-blank** `ClientId`, `ClientSecret`, `SigningKeyId`, `SigningKeyPath`, `SigningKeyPassphrase` (note: passphrase may legitimately be empty if the key is unencrypted — decide per key; treat the other four as mandatory) and `BaseUrl`, throwing before the host serves traffic, rather than surfacing as `InvalidOperationException`/`AuthSchemeException` on first call. Mirror the repo's `GetRequiredSection` fail-fast (`Program.cs` line 48). Every part of the multi-part signing credential is checked. **YOUR CALL — not in the map**: exact guard placement. |
| 2 | Secret sourcing & rotation | `ClientSecret` + `SigningKeyPassphrase` come from **.NET user-secrets** (never repo files); `SigningKeyPath` points at a PEM outside the repo. The client is a **singleton** built once at registration, so a rotated secret/key needs a process restart. No hot-rotation required for this sample; state it. |
| 3 | Total timeout budget | The caller's budget is bounded by `options.Retry.Timeout` (whole-call) vs the per-attempt HttpClient timeout — set an explicit overall budget on the two-write payment path. Exact numbers at implementation; what each knob bounds → `dotnet-configuration-resilience`. **YOUR CALL — not in the map**. |
| 4 | Write-retry ownership | Only idempotent verbs are auto-resent by default; the create writes (`CreateUser`, `CreateTopup`, `CreatePortfoliosOrder`, `TriggerPortfolioRebalancing`, `CreateAccount*`) must not be blindly resent without their real `IdempotencyKey`. Confirm the retried-verb set in `dotnet-configuration-resilience` before enabling retries on POSTs. |
| 5 | Idempotency & ambiguous writes | Real key = the `IdempotencyKey: Guid` member on each create request record (persist it with the app record; replay the **same** key to deduplicate). The injected `Idempotency-Key` header is **not** a key. `SetTaxResidencies`/`UpdatePortfoliosConfiguration` also carry `IdempotencyKey`. Reconciliation path: §UNKNOWN OUTCOMES. |
| 6 | Observability | Log at Information: operation name + the app's own correlation id + Upvest resource ids (user/account/topup/order/execution). On `ApiException`, log `StatusCode` and the provider correlation id read from the error body via `TryGetRawError`/`ReadAsJson`. **Never** log request bodies (§7). |
| 7 | Sensitive data | **Yes** — onboarding carries personal data (name, email, birth date/city, address, phone, taxId, identity document). Posture: `options.Logging.LogRequestBody` **off**; set `options.Logging.LoggerFactory` explicitly in production (leaving it unset arms `UPVEST_INVESTMENT_API_LOG=trace` body logging); app diagnostics never echo a request body or personal field; add any sensitive form keys to the deny-list. Task rule: personal details never logged. |
| 8 | Environment selection | One server group `Default`. Every deployment sets `Upvest:BaseUrl` and uses it verbatim via `options.Server.Default.Production.BaseUrl`; `Environment = Production`. Sandbox = `https://sandbox.upvest.co`, live = `https://api.upvest.co` (`Environment2`). Dev/test point only at sandbox; the SDK declares a sandbox environment, so test traffic is kept off live by config. |
| 9 | Duplicate prevention under concurrency | See **DUPLICATE CLAIMS**. The app's EF store (in-memory, single run) holds the claim; a primary-key insert is refused even by the in-memory provider. |
| 10 | Partial results | See **PAGED READS**. |
| 11 | Unknown outcomes | See **UNKNOWN OUTCOMES**. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `POST /api/portfolio/investor` (CreateUser + chain) | app `InvestorRecord` keyed by JWT `NameIdentifier` (PK), in `CatalogContext` via `IRepository<InvestorRecord>` | PK insert of the claim row rejects the second caller before any Upvest call | `catch` of the EF insert/`DbUpdateException` on the claim save | TBD |
| `POST /api/portfolio/payments` (CreateTopup + CreatePortfoliosOrder) | app `PaymentRecord` keyed by an app-generated `paymentId` (PK), persisted **before** the topup call, carrying the two `IdempotencyKey` guids | PK insert of the claim row rejects a replay of the same intended payment | `catch` of the EF insert on the claim save | TBD |
| `POST /api/portfolio/models/{modelId}/realignments` | app `RealignmentRecord` keyed by `(modelId, app realignmentId)` (PK) | PK insert rejects a duplicate trigger for the same claim | `catch` of the EF insert on the claim save | TBD |
| `PUT /api/portfolio/models/{modelId}` | not a duplicate concern — it is a create-or-replace keyed by `{modelId}`; app `ModelRecord` upsert by PK is the single source of truth | n/a (idempotent upsert) | n/a | n/a |

Order per row: claim write → SDK call → record result. The claim is written first so a second caller is refused by
the store before reaching Upvest; release the claim if Upvest refuses the call. Store = the app's EF store, never the
provider; the provider `IdempotencyKey` sits beside the claim, not in place of it.

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `Portfolios.ListPortfoliosAllocations` (GET models) | `Limit`/`Offset` query; server default page size | Walk pages by `offset` until `meta.offset + meta.count >= meta.total_count`; if the response DTO still caps, carry a `truncated`/`totalCount` field on the GET-models response | TBD |
| `Valuations.GetAccountValuation` `security_positions[]` | not paged (single object) | n/a | n/a |
| `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` | `Limit`/`Offset`; server page size | aggregate across all pages (loop on `meta.total_count`) before deciding realignment status; surface a `completeness` flag if not all pages were read | TBD |
| `Positions.ListPositions` (if used) | `Limit`/`Offset` | loop on `Meta.TotalCount` | TBD |

A log line is not an answer — the third column names the response field/flag the caller reads.

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `Users.CreateUser` | resend `CreateUser` with the persisted `IdempotencyKey` (Upvest replays the original response) — no list-by-email op in scope | the persisted `CreateUserRequest.IdempotencyKey` | TBD | TBD |
| `TopUps.CreateTopup` | `TopUps.ListTopups(account_group_id)` then match, else resend with persisted `IdempotencyKey` | `account_group_id` + persisted `IdempotencyKey` | TBD | TBD |
| `Portfolios.CreatePortfoliosOrder` | `Portfolios.ListPortfoliosOrders(account_id)` then match, else resend with persisted `IdempotencyKey` | `account_id` + persisted `IdempotencyKey` | TBD | TBD |
| `PortfoliosRebalancing.TriggerPortfolioRebalancing` | resend with persisted `IdempotencyKey` (execution id is unknown if the connection dropped) | the persisted `IdempotencyKey` | TBD | TBD |
| `AccountGroups.CreateAccountGroup` / `AccountsApi.CreateAccount` | `AccountGroups.ListUserAccountGroups(user_id)` / `AccountsApi.ListUserAccounts(user_id)` then match, else resend with persisted `IdempotencyKey` | `user_id` + persisted `IdempotencyKey` | TBD | TBD |

Settle each in the failing write's own `catch` (persist the `IdempotencyKey` **before** the call, re-read/resend on
`SdkConnectionException`/`SdkTimeoutException`), before returning a failure.

---

## 6. Assumptions & Blockers

**Assumptions**
- **Sandbox scope limitation is the environment, not a defect.** The task states the sandbox client is authorised
  only for user operations; calls needing `accounts:admin`, `checks:admin`, `taxes:admin`, `portfolios:admin`,
  `topups:admin`, `valuations:read`, etc. will return 401 "This client is not authorised to use these scopes". We
  build every flow in full and verify as far as the sandbox allows. **Not a Blocker.**
- **Investor status mapping is the app's call.** The Upvest user `Status` enum is `ACTIVE`/`INACTIVE`/`OFFBOARDING`/
  `OFFBOARDED` — there is no `PENDING`/`REJECTED`. The app maps: `INACTIVE` → `pending`, `ACTIVE` → `active`, and
  synthesises `rejected` from a failed/declined onboarding signal (e.g. a `UserCheck` that did not pass, read via
  `UserChecks.RetrieveUserCheck`/`ListUserChecks`) since the user object alone never reports rejection.
  **YOUR CALL — not in the map.**
- **Realignment status is derived, not returned.** `PortfoliosRebalancingExecution` carries **no status field**
  (confirmed in source). The app derives `pending`/`completed`/`failed` by aggregating the per-order statuses from
  `ListPortfolioRebalancingExecutionOrders` for the execution id. The SDK fully covers this, so it is a derivation,
  not a Blocker. **YOUR CALL — not in the map.**
- **Money-in uses `TopUps.CreateTopup`.** Per the payments workflow, a top-up "funds the account group cash balance"
  (`CONFIRMED`→`SETTLED`) — the closest match to "money credited to their Upvest account" that returns an id+status.
  The task's `paymentId`/`status` map to the top-up (credit) paired with the portfolio BUY order (investment). An
  alternative sandbox money-in (`Tests.CreateBankTransaction` via a vIBAN) exists but needs explicit Upvest
  enablement and a virtual bank account; top-up is the simpler fit. **YOUR CALL — not in the map.**
- **Per-instrument holding value comes from `Valuations.GetAccountValuation`**, because `Positions.ListPositions`
  (`Datum1`) returns quantities only, with no monetary value. `GET /api/portfolio` holdings/`totalValue`/value use the
  valuation; positions are optional quantity detail.
- **The app owns its own persistence** (investor map, models map, payments, realignments) in `CatalogContext` via
  `IRepository<T>`/`EfRepository<T>` (exemplar: `src/Infrastructure/Data/CatalogContext.cs`; endpoint exemplar:
  `src/PublicApi/CatalogItemEndpoints/CreateCatalogItemEndpoint.cs`). In-memory EF means records live only within one
  run, matching the task's note. Isolation is enforced by keying every record on the JWT subject. **YOUR CALL — not
  in the map.**
- `instrument_id_type` for model funds is `ISIN` (task says funds are given by ISIN); `Currency.Eur` throughout.
- Admin-only endpoints (`PUT /models/{id}`, `POST /realignments`) use
  `[Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearer)]`,
  mirroring `CreateCatalogItemEndpoint`; investor/payment/portfolio endpoints use `[Authorize]` with the caller
  identity from the token.

**Blockers**
- None. Every capability each flow needs maps to a concrete SDK operation; where the SDK returns no status (rebalancing
  execution) the SDK still provides the re-read (`ListPortfolioRebalancingExecutionOrders`) to derive one.

---

## 7. Source labels

Every per-operation `source` cell above cites a map page (`map/operations/*.md`) and the SDK source file(s) the row's
shapes were read from (one targeted open each). `UNVERIFIED`: none — all contract facts were settled from the map and
source this session. `YOUR CALL — not in the map`: the application-design decisions enumerated in §6 and §5 (status
mapping, realignment-status derivation, money-in choice, persistence design, guard placement, timeout budget), to be
settled against the task at implementation time and recorded back into this file once the code compiles.

---

## 8. Settled decisions & code locations (post-implementation)

Every `YOUR CALL — not in the map` marker and every `TBD` is settled below with the member that implements it.
Namespaces: entities `Microsoft.eShopWeb.ApplicationCore.Portfolio`; services `…Infrastructure.Portfolio.Services`;
endpoints `…PublicApi.PortfolioEndpoints`.

### YOUR CALL decisions
- **Investor status mapping** → `InvestorService.DeriveStatusAsync` + `IsKycFailedAsync`: `UserTol.Status==ACTIVE`→`active`;
  a KYC check `Status6.Failed`→`rejected`; otherwise `pending`.
- **Realignment status derived** (execution object has no status) → `RealignmentService.DeriveStatusAsync`: aggregates
  `Status71` across all pages of `ListPortfolioRebalancingExecutionOrders` (all `Filled`→completed; any `Cancelled`→failed;
  any `New`/`Processing` or none→pending).
- **Money-in = top-up + BUY order** → `PaymentService.PayInAsync` (`TopUps.CreateTopup` then `Portfolios.CreatePortfoliosOrder`,
  `Side15.Buy`, `Currency.Eur`, two-decimal `cash_amount`).
- **Holding value from valuation** (`Positions` carries no value) → `PortfolioQueryService.GetPortfolioAsync`
  (`Valuations.GetAccountValuation`, `PriceQuality3.HighestAvailable`).
- **Persistence**: a **dedicated `PortfolioContext`** (not `CatalogContext`) registered in
  `UpvestServiceCollectionExtensions.AddUpvestPortfolioIntegration`, in-memory/SqlServer per `UseOnlyInMemoryDatabase`.
  Chosen over sharing `CatalogContext` for clean separation; isolation keyed on the JWT subject (`Investor.ShopperId`).
- **Credential fail-fast** → `AddUpvestPortfolioIntegration` `AddOptions<UpvestSettings>().Validate(...).ValidateOnStart()`
  (`ValidateSettings` requires non-blank ClientId/ClientSecret/SigningKeyId/SigningKeyPath/BaseUrl and an existing key file;
  passphrase optional). Names the missing keys, never values.
- **Timeout budget**: per-attempt `options.Retry.Timeout = 15s` + named `HttpClient.Timeout = 30s`; each endpoint passes the
  request `CancellationToken` through to the SDK. (No separate total-budget wrapper; the 30s HttpClient timeout bounds a hung
  attempt and the two-write pay-in path is sequential.)
- **"Accepted investor with a model chosen"** for pay-in → `PaymentService.PayInAsync` requires the caller's `Investor` to have
  `AccountId`, `AccountGroupId`, `UpvestUserId` (account ready) and `ChosenModelId` (model chosen); else `PortfolioConflictException` (409).
- **Model vs allocation**: `PUT /models` persists the operator's definition locally (source of truth) and syncs the Upvest
  allocation **best-effort** (`ModelPortfolioService.UpsertModelAsync` → `SyncAllocationAsync`, provider errors logged not fatal);
  the allocation is **ensured on demand** in `ChooseModelAsync` (`EnsureAllocationAsync`). `GET /models` reads the local store
  (`ListModelsAsync`) — so `Portfolios.ListPortfoliosAllocations` is **not** used (the app owns the model set), removing that paged read.
- **Consent documents**: `Upvest:TermsConsentDocumentId` / `Upvest:DataPrivacyConsentDocumentId` (configurable, with the
  documented Upvest sandbox ids as defaults) → `InvestorService.BuildUser`.

### DUPLICATE CLAIMS — Where in the code
- `POST /investor`: `InvestorService.ClaimInvestorAsync` (PK insert of `Investor`, `catch DbUpdateException/…` reloads winner) → then `Users.CreateUser` in `SignUpAsync`.
- `POST /payments`: `PaymentService.PayInAsync` persists the `Payment` claim (PK `PaymentId`, both idempotency keys) **before** `TopUps.CreateTopup`.
- `POST /models/{id}/realignments`: `RealignmentService.TriggerAsync` persists the `Realignment` claim **before** `TriggerPortfolioRebalancing`.
  (Note: a pay-in/realignment claim uses a fresh app id per request, so it is replay-safe via the persisted idempotency keys but does not de-duplicate a double-submit, since the task provides no caller idempotency key.)

### PAGED READS — Where in the code
- `ListPortfolioRebalancingExecutionOrders`: `RealignmentService.DeriveStatusAsync` loops all pages (`Meta.Offset+Count≥TotalCount`, `MaxPages=100` cap) **before** deciding status, so the derived status reflects every order (not cut short).
- `Portfolios.ListPortfoliosAllocations`: not used (see above).

### UNKNOWN OUTCOMES — Where in the code / test
- `Users.CreateUser`: `UpvestCall.WriteAsync` → `PortfolioUnknownOutcomeException` on connection loss; `GET /investor` (`DeriveStatusAsync`) re-reads via `RetrieveUser`. Test: `UpvestCallTests.Connection_failure_on_a_write_becomes_unknown_outcome` / `Timeout_on_a_write_becomes_unknown_outcome`.
- `TopUps.CreateTopup` / `CreatePortfoliosOrder`: `PaymentService.PayInAsync` catches `PortfolioUnknownOutcomeException` and leaves the payment **pending**; `ListPaymentsAsync`→`ReconcileAsync` re-reads `RetrieveTopup`/`RetrievePortfoliosOrder`. Test: same write-unknown boundary tests.
- `TriggerPortfolioRebalancing`: `RealignmentService.TriggerAsync` leaves it **pending**; `GetAsync` reconciles via the orders list.

### Environment limitations observed against the live sandbox (not code defects)
- **Scope**: this client is authorised only for user operations, so `accounts:*`, `portfolios:*`, `topups:*`, `valuations:*`, etc. return `401 "This client is not authorised to use these scopes"`. Mapped to `502` by `PortfolioApi.ToErrorResult`. (As stated in the task.)
- **Consent documents**: `Users.CreateUser` reaches Upvest and is validated field-by-field (verified live: it rejected a `+`-prefixed phone, then — with digits — reported `invalid data privacy document uuid`). **Every** consent-document uuid documented in the reference (`d0b83880…`, `7ab0fc5c…`, and the user-management alternatives) is rejected by this sandbox tenant, and the SDK/map exposes no operation to create or list a consent document. This is un-seeded sandbox **data** ("Nothing is pre-seeded at Upvest"), not an SDK gap: `CreateUser` is correctly implemented, and the consent ids are configurable for a tenant that has valid ones.
