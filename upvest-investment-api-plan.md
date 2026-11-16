# Upvest Investment API integration plan — managed investment portfolios in eShopOnWeb

Plan written by `upvest-investment-api-planner`. Every SDK fact below is grounded in the SDK map
(`sdk-map.md` + `map/operations/*`) and the source files it names, all under the plugin-relative SDK
root `sdk/dotnet/` (absolute this machine: `C:\claude-runs\t10rafay-task10-plugin-opus48medium-007\marketplace\plugins\upvest-v4\sdk\dotnet\`).
`Source` cells cite paths relative to that SDK root. API spec `1.172.0`, root namespace `UpvestInvestmentApi`.

The main agent implements from this sheet; it does not re-derive contracts from memory. Flows map onto the
new PublicApi surface described in the task. The managed-portfolio capability is additive and does not touch
the catalog/basket/order flow.

---

## How the five app flows sit on Upvest resources (the entity chain)

Upvest models a managed portfolio as: **User** → **Account Group** (a `PERSONAL` group owns the cash
balance; top-ups are keyed by `account_group_id`) → **Account** of type `PORTFOLIO` (holds positions; orders,
positions, valuations and portfolio configuration are keyed by `account_id`). A **model portfolio** is an Upvest
**Allocation** (named list of instrument/weight). An investor "follows a model" through a **PortfoliosConfiguration**
that links one `account_id` to one `allocation_id`. Paying in = a **Top-up** (credits the account group cash) made
spendable in sandbox by a **Tests bank transaction**, then a **Portfolio Order** (`BUY`) that invests the cash across
the account's allocation. Realignment = **TriggerPortfolioRebalancing** over the allocation's followers.

The app must persist, per shopper (keyed by its own username/`ClaimsPrincipal` name), the Upvest `user_id`,
`account_group_id`, `account_id` and chosen `allocation_id`, plus the ids and statuses of each payment and
realignment it started — Upvest exposes no route back into this app, so every status is learned by re-reading
(§ UNKNOWN OUTCOMES, trap notes).

> ⚠ **SANDBOX SCOPE WALL (environment, not a gap or bug).** The task states the sandbox client is authorised
> **only for user operations**. Of the operations below, only `Users.CreateUser` / `Users.RetrieveUser`
> (`users:*`) are expected to succeed in sandbox; every `accounts:*`, `portfolios:*`, `topups:*`, `tests:*`,
> `positions:*`, `valuations:*`, `instruments:*` call returns `401 "This client is not authorised to use these
> scopes"`. Build every flow in full anyway and verify as far as the sandbox allows. See PRODUCTION READINESS
> row 8 and Assumptions & Blockers.

---

## 1. Scope & sequence

Implementation order. Each step names its Upvest operations (controller.method). App-side endpoints are the
new `/api/portfolio*` routes (all JWT-authenticated on `src/PublicApi`, caller identity from the token's
`ClaimTypes.Name`, per the catalog endpoint exemplars).

1. **Wiring & config** — reference the SDK csproj; register `UpvestInvestmentApiClient` as a singleton via DI
   (`services.AddUpvestInvestmentApiClient`), bound from the `Upvest:` config section; install the signing hook;
   fail fast on missing credentials. (PRODUCTION READINESS rows 1,2,3,7,8.)
2. **Persistence** — add aggregate entities for the per-shopper Upvest linkage and for model portfolios /
   payments / realignments the app tracks (see CROSS-OPERATION INVARIANTS & DUPLICATE CLAIMS for why the app
   must own these records). Follow `BaseEntity`/`IAggregateRoot` + `IEntityTypeConfiguration` + `EfRepository<T>`.
3. **Flow 1 — become an investor** (`POST/GET /api/portfolio/investor`): `Users.CreateUser` (TOL body) →
   `AccountGroups.CreateAccountGroup` (`PERSONAL`) → `AccountsApi.CreateAccount` (`PORTFOLIO`). Status re-read:
   `Users.RetrieveUser` + `AccountGroups.RetrieveAccountGroup` (+ `AccountsApi.RetrieveAccount`).
4. **Flow 2 — model portfolios** (`PUT /api/portfolio/models/{modelId}`, `GET /api/portfolio/models`,
   `PUT /api/portfolio/investor/model`): `Portfolios.CreatePortfoliosAllocation` / `Portfolios.UpdatePortfoliosAllocation`;
   `Portfolios.ListPortfoliosAllocations`; `Portfolios.CreatePortfoliosConfiguration` /
   `Portfolios.UpdatePortfoliosConfiguration`. Validate ISINs with `Instruments.ListInstruments`.
5. **Flow 3 — pay in** (`POST/GET /api/portfolio/payments`): `TopUps.CreateTopup` → `Tests.CreateBankTransaction`
   (sandbox settlement) → `Portfolios.CreatePortfoliosOrder` (`BUY`). Status re-read: `TopUps.RetrieveTopup` and
   `Portfolios.RetrievePortfoliosOrder`. List: `TopUps.ListTopups` (and the app's own payment records).
6. **Flow 4 — realignment** (`POST /api/portfolio/models/{modelId}/realignments`,
   `GET /api/portfolio/realignments/{realignmentId}`): `PortfoliosRebalancing.TriggerPortfolioRebalancing`
   (`Allocations` arm); re-read `PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution`
   (+ `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` — see Blocker B2 on status).
7. **Flow 5 — the investor's portfolio** (`GET /api/portfolio`): `Portfolios.RetrievePortfoliosConfiguration`
   (modelId followed) + `Valuations.GetAccountValuation` (holdings + total value) and/or `Positions.ListPositions`
   (quantities); `paidIn` from the app's own payment records.
8. **Error boundary, logging posture, tests.**

A capability the map lacks is a Blocker (§6), never a data path invented to replace it.

---

## 2. CONTRACT SHEET

> ⚠ **Signatures below are generated code, verbatim.** Each operation that takes input takes **ONE request
> record as its first parameter** (plus optional `RequestOptions? requestOptions = null, CancellationToken
> cancellationToken = default`); build the record with an **object initializer using the record's own property
> names** — never flat positional arguments. An operation with no inputs takes no record (none here).
> ⚠ **Every SDK type must be written fully-qualified** with the namespace its source path implies (taken from
> the path given for THAT type, never from a neighbour's): `src/UpvestInvestmentApi/Models/X.cs` →
> `UpvestInvestmentApi.Models.X`; `.../Models/AnyOf/X.cs` → `UpvestInvestmentApi.Models.AnyOf.X`;
> `.../Models/Enums/X.cs` → `UpvestInvestmentApi.Models.Enums.X`; `.../Requests/<Ctrl>/X.cs` →
> `UpvestInvestmentApi.Requests.<Ctrl>.X`; `.../Errors/X.cs` → `UpvestInvestmentApi.Errors.X`.

**Universal facts (apply to every row unless the row says otherwise):** throw-only (no `…Result` variant);
server group `Default` (base URL from § client/auth); every write request record carries `IdempotencyKey`
(`required Guid`, a **real** caller-supplied idempotency key) and `UpvestApiVersion` (defaults to
`UpvestApiVersion._1`) and an optional `Body`; every operation is **Case A** with accessors
`TryGetNoContent([NotNullWhen(true)] out RawError? value)` (covers the listed 4xx/5xx, no typed payload) and
`TryGetRawError([NotNullWhen(true)] out RawError? error)` (fallback). `RawError` members:
`StatusCode: HttpStatusCode` · `ReadAsString(): string` · `ReadAsJson<T>(): T?` · `ReadAsBytes()`.
All money/weight/quantity values cross the wire as **decimal strings**, not JSON numbers (see trap note T1).

### Flow 1 — become an investor

| Operation | Request record + members | Body model + fields (wire) | Response envelope → fields read | Error (Case A accessors) | Pag. | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `client.Users.CreateUser` (scopes `users:admin`) | `UpvestInvestmentApi.Requests.Users.CreateUserRequest` { `IdempotencyKey: Guid` req; `UpvestApiVersion`; `Body: UserCreateRequest?` } | `UpvestInvestmentApi.Models.AnyOf.UserCreateRequest` (union) → build **TOL** arm `UpvestInvestmentApi.Models.UserTolCreateRequest`: req `FirstName(first_name)`, `LastName(last_name)`, `Email(email)`, `BirthDate(birth_date): DateTimeOffset` (date-only), `Nationalities(nationalities): IReadOnlyList<Nationality>` (ISO-3166 a2, ≥1), `Address(address): Address2`, `Fatca(fatca): Fatca`; opt `BirthCity(birth_city)`, `BirthCountry(birth_country)`, `PhoneNumber(phone_number)`, `TermsAndConditions`, `DataPrivacyAndSharingAgreement`, … | `UpvestInvestmentApi.Models.AnyOf.UserCreateRequest1` (union) → `TryGetUserTol(out UserTol)` → `UserTol.Id: Guid` (store as `investorId`), `UserTol.Status: Status` | `ApiException<UpvestInvestmentApi.Errors.CreateUserError>` — `TryGetNoContent` [400,401,403,406,429,500,503,504] · `TryGetRawError` | none | map `map/operations/Users.md`; `Models/UserTolCreateRequest.cs`, `Models/UserTol.cs`, `Models/AnyOf/UserCreateRequest{,1}.cs`, `Requests/Users/CreateUserRequest.cs` |
| `client.Users.RetrieveUser` (scopes `users:read`/`admin`) | `UpvestInvestmentApi.Requests.Users.RetrieveUserRequest` { `UserId: Guid` req } | — | `UpvestInvestmentApi.Models.AnyOf.UserGetResponse` → `TryGetUserTol(out UserTol)` → `UserTol.Status: Status` (ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED) | `ApiException<UpvestInvestmentApi.Errors.RetrieveUserError>` — `TryGetNoContent` [401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `Users.md`; `Models/AnyOf/UserGetResponse.cs`, `Models/UserTol.cs` |
| `client.AccountGroups.CreateAccountGroup` (scopes `accounts:admin`) | `UpvestInvestmentApi.Requests.AccountGroups.CreateAccountGroupRequest` { `IdempotencyKey` req; `UpvestApiVersion`; `Body: AccountGroupCreateRequest?` } | `UpvestInvestmentApi.Models.AnyOf.AccountGroupCreateRequest` → **user** arm `UpvestInvestmentApi.Models.AccountGroupCreateUserRequest`: req `UserId(user_id): Guid`, `Type(type): Type13` = `UpvestInvestmentApi.Models.Enums.Type13.Personal`; opt `SecuritiesAccountNumber` | `UpvestInvestmentApi.Models.AnyOf.AccountGroupCreateResponse` → account-group id + status (read `account_group_id`, group `status`; open `Models/AnyOf/AccountGroupCreateResponse.cs` at impl) | `ApiException<UpvestInvestmentApi.Errors.CreateAccountGroupError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `map/operations/AccountGroups.md`; `Models/AccountGroupCreateUserRequest.cs`, `Models/AnyOf/AccountGroupCreateRequest.cs`, `Requests/AccountGroups/CreateAccountGroupRequest.cs` |
| `client.AccountGroups.RetrieveAccountGroup` (scopes `accounts:read`/`admin`) | `UpvestInvestmentApi.Requests.AccountGroups.RetrieveAccountGroupRequest` { `AccountGroupId: Guid` req } | — | `UpvestInvestmentApi.Models.AnyOf.AccountGroupRetrieveResponse` → group `status` (PENDING_APPROVAL vs ACTIVE = "account ready"); open file at impl | `ApiException<UpvestInvestmentApi.Errors.RetrieveAccountGroupError>` — `TryGetNoContent` [401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `AccountGroups.md`; `Models/AnyOf/AccountGroupRetrieveResponse.cs` |
| `client.AccountsApi.CreateAccount` (scopes `accounts:admin`) | `UpvestInvestmentApi.Requests.AccountsApi.CreateAccountRequest` { `IdempotencyKey` req; `UpvestApiVersion`; `Body: AccountCreateRequest?` } | `UpvestInvestmentApi.Models.AnyOf.AccountCreateRequest` → **user** arm `UpvestInvestmentApi.Models.AccountCreateUserRequest`: req `UserId(user_id): Guid`, `AccountGroupId(account_group_id): Guid`, `Type(type): Type16` = `UpvestInvestmentApi.Models.Enums.Type16.Portfolio`; opt `Name(name)` | `UpvestInvestmentApi.Models.AnyOf.AccountCreateResponse` → `TryGetAccount(out Account)` → account id (store as `account_id`); open `Models/Account.cs` at impl for id/status fields | `ApiException<UpvestInvestmentApi.Errors.CreateAccountError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `map/operations/AccountsApi.md`; `Models/AccountCreateUserRequest.cs`, `Models/AnyOf/AccountCreateRequest.cs`, `Models/AnyOf/AccountCreateResponse.cs`, `Requests/AccountsApi/CreateAccountRequest.cs` |
| `client.AccountsApi.ListUserAccounts` (scopes `accounts:read`/`admin`) — reconciliation re-read for Flow 1 | `UpvestInvestmentApi.Requests.AccountsApi.ListUserAccountsRequest` { `UserId: Guid` req; opt `Sort`,`Order`,`Offset`,`Limit` } | — | `UpvestInvestmentApi.Models.AccountsListResponse` (data+meta; open at impl) | `ApiException<UpvestInvestmentApi.Errors.ListUserAccountsError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | offset/limit (`data`+`meta`) | map `AccountsApi.md` |

> **Fields the shop collects that have NO slot on the TOL create body** (`UserTolCreateRequest`): `taxId`,
> `taxCountry`, and the `identityDocument` block (type/number/issuingCountry/expiryDate). The TOL body carries
> `nationalities` (list), `address`, `fatca`, consents — but no tax-residency or identity-document members.
> At Upvest these belong to separate resources (`TaxResidencies`, `UserIdentifiers`/`UserChecks`) outside the
> five-flow scope and outside the user-only sandbox scope. Decision in Assumptions A3 / Blocker B1: accept and
> store them on the app side, do not drop them silently, and treat Upvest-side persistence of them as out of
> scope. `single nationality (alpha-2)` from the form → one-element `Nationalities` list.

### Flow 2 — model portfolios

| Operation | Request record + members | Body model + fields (wire) | Response → fields read | Error (Case A) | Pag. | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `client.Portfolios.CreatePortfoliosAllocation` (scopes `portfolios:admin`) | `UpvestInvestmentApi.Requests.Portfolios.CreatePortfoliosAllocationRequest` { `IdempotencyKey` req; `UpvestApiVersion`; `Body: PortfoliosAllocationCreateRequest?` } | `UpvestInvestmentApi.Models.PortfoliosAllocationCreateRequest`: req `Allocation(allocation): IReadOnlyList<Allocation>`; opt `Name(name)`, `RebalancingStrategyIds`. `UpvestInvestmentApi.Models.Allocation`: req `InstrumentId(instrument_id): UpvestInvestmentApi.Models.AnyOf.InstrumentId` (ISIN as `InstrumentId.String("DE000…")`), `Weight(weight): string` (regex `^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$`, must sum to 100); `InstrumentIdType(instrument_id_type): InstrumentIdType4` defaults `.Isin` | `UpvestInvestmentApi.Models.PortfoliosAllocation` → `Id: Guid` (store as the model's Upvest `allocation_id`), `Name`, `Allocation` | `ApiException<UpvestInvestmentApi.Errors.CreatePortfoliosAllocationError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `map/operations/Portfolios.md`; `Models/PortfoliosAllocationCreateRequest.cs`, `Models/Allocation.cs`, `Models/AnyOf/InstrumentId.cs`, `Models/PortfoliosAllocation.cs` |
| `client.Portfolios.UpdatePortfoliosAllocation` (scopes `portfolios:admin`) | `UpvestInvestmentApi.Requests.Portfolios.UpdatePortfoliosAllocationRequest` { `AllocationId: Guid` req; `UpvestApiVersion`; `Body: PortfoliosAllocationUpdateRequest?` } | `UpvestInvestmentApi.Models.PortfoliosAllocationUpdateRequest` (open at impl; same instrument/weight shape) | `UpvestInvestmentApi.Models.PortfoliosAllocation` → `Id`, `Allocation` | `ApiException<UpvestInvestmentApi.Errors.UpdatePortfoliosAllocationError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `Portfolios.md`; `Models/PortfoliosAllocationUpdateRequest.cs` |
| `client.Portfolios.ListPortfoliosAllocations` (scopes `portfolios:read`/`admin`) | `UpvestInvestmentApi.Requests.Portfolios.ListPortfoliosAllocationsRequest` { opt `Sort`,`Order`,`Offset`,`Limit`,`StartDate`,`EndDate`,`InstrumentIds` } | — | `UpvestInvestmentApi.Models.PortfoliosAllocationsListResponse` (data of `PortfoliosAllocation` + `meta`; open at impl) | `ApiException<UpvestInvestmentApi.Errors.ListPortfoliosAllocationsError>` — `TryGetNoContent` [400,401,403,404,405,406,429,500,503,504] · `TryGetRawError` | **offset/limit** (`data`+`Meta`: `offset,limit,count,total_count`) | map `Portfolios.md`; `Models/PortfoliosAllocationsListResponse.cs`, `Models/Meta.cs` |
| `client.Portfolios.CreatePortfoliosConfiguration` (scopes `portfolios:admin`) | `UpvestInvestmentApi.Requests.Portfolios.CreatePortfoliosConfigurationRequest` { `IdempotencyKey` req; `UpvestApiVersion`; `Body: PortfoliosConfigurationCreateRequest?` } | `UpvestInvestmentApi.Models.PortfoliosConfigurationCreateRequest`: req `AccountId(account_id): Guid`, `AllocationId(allocation_id): Guid`; opt `RebalancingStrategyIds` | `UpvestInvestmentApi.Models.PortfoliosConfiguration` → `AccountId`, `AllocationId`, `CreatedAt`,`UpdatedAt` | `ApiException<UpvestInvestmentApi.Errors.CreatePortfoliosConfigurationError>` — `TryGetNoContent` [400,401,403,404,406,422,429,500,503,504] · `TryGetRawError` | none | map `Portfolios.md`; `Models/PortfoliosConfigurationCreateRequest.cs`, `Models/PortfoliosConfiguration.cs` |
| `client.Portfolios.UpdatePortfoliosConfiguration` (scopes `portfolios:admin`) — switch model later | `UpvestInvestmentApi.Requests.Portfolios.UpdatePortfoliosConfigurationRequest` { `AccountId: Guid` req; `IdempotencyKey: Guid` req; `UpvestApiVersion`; `Body: PortfoliosConfigurationUpdateRequest?` } | `UpvestInvestmentApi.Models.PortfoliosConfigurationUpdateRequest`: opt `AllocationId(allocation_id): Guid?`, `RebalancingStrategyIds` (set `AllocationId` to switch) | `UpvestInvestmentApi.Models.PortfoliosConfiguration` → `AllocationId` | `ApiException<UpvestInvestmentApi.Errors.UpdatePortfoliosConfigurationError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `Portfolios.md`; `Models/PortfoliosConfigurationUpdateRequest.cs`, `Requests/Portfolios/UpdatePortfoliosConfigurationRequest.cs` |
| `client.Portfolios.RetrievePortfoliosConfiguration` (scopes `portfolios:read`/`admin`) | `UpvestInvestmentApi.Requests.Portfolios.RetrievePortfoliosConfigurationRequest` { `AccountId: Guid` req } | — | `UpvestInvestmentApi.Models.PortfoliosConfiguration` → `AllocationId` (= modelId the account follows) | `ApiException<UpvestInvestmentApi.Errors.RetrievePortfoliosConfigurationError>` — `TryGetNoContent` [401,403,404,405,406,429,500,503,504] · `TryGetRawError` | none | map `Portfolios.md` |
| `client.Instruments.ListInstruments` (scopes `instruments:read`) — ISIN validation | `UpvestInvestmentApi.Requests.Instruments.ListInstrumentsRequest` { opt `TradingStatus`,`Sort`,`Order`,`Offset`,`Limit` } | — | `UpvestInvestmentApi.Models.InstrumentsListResponse` (data+meta; open at impl) | `ApiException<UpvestInvestmentApi.Errors.ListInstrumentsError>` — `TryGetNoContent` [400,401,403,406,429,500,503,504] · `TryGetRawError` | **offset/limit** | map `map/operations/Instruments.md` |

### Flow 3 — paying in

| Operation | Request record + members | Body model + fields (wire) | Response → fields read | Error (Case A) | Pag. | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `client.TopUps.CreateTopup` (scopes `topups:admin`) | `UpvestInvestmentApi.Requests.TopUps.CreateTopupRequest` { `IdempotencyKey` req; `UpvestApiVersion`; `Body: PaymentsTopUpCreateRequest?` } | `UpvestInvestmentApi.Models.PaymentsTopUpCreateRequest`: req `AccountGroupId(account_group_id): Guid`, `CashAmount(cash_amount): string` (regex `^[0-9]{1,9}(\.[0-9]{2})?$`), `Currency(currency): Currency` = `.Eur` | `UpvestInvestmentApi.Models.PaymentsTopupsResponse` → `Id: Guid` (paymentId), `CashAmount`, `Currency`, `SettlementReference: string?`, `Status: Status29?` (CONFIRMED/SETTLED/CANCELLED) | `ApiException<UpvestInvestmentApi.Errors.CreateTopupError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `map/operations/TopUps.md`; `Models/PaymentsTopUpCreateRequest.cs`, `Models/PaymentsTopupsResponse.cs`, `Models/Enums/Status29.cs` |
| `client.Tests.CreateBankTransaction` (scopes `tests:admin`) — **sandbox settlement** | `UpvestInvestmentApi.Requests.Tests.CreateBankTransactionRequest` { `IdempotencyKey` req; `UpvestApiVersion`; `Body: PaymentsBankTransactionCreateRequest?` } | `UpvestInvestmentApi.Models.PaymentsBankTransactionCreateRequest`: req `Account(account): Account29`, `Amount(amount): string` (2dp), `Currency(currency): BankTransactionCurrency`, `ClientReference(client_reference): string`, `CounterpartyName(counterparty_name): string`, `CounterpartyAccount(counterparty_account): CounterpartyAccount`; opt `RemittanceInformation(remittance_information)` ← set to the top-up `SettlementReference`, `VirtualBankAccountId`. Open `Models/Account29.cs`, `Models/CounterpartyAccount.cs`, `Models/Enums/BankTransactionCurrency.cs` at impl | `UpvestInvestmentApi.Models.PaymentsBankTransactionCreateResponse` → `Id: Guid`, `BankReference: string` | `ApiException<UpvestInvestmentApi.Errors.CreateBankTransactionError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `map/operations/Tests.md`; `Models/PaymentsBankTransactionCreateRequest.cs`, `Models/PaymentsBankTransactionCreateResponse.cs` |
| `client.Portfolios.CreatePortfoliosOrder` (scopes `portfolios:admin`) — invest the cash | `UpvestInvestmentApi.Requests.Portfolios.CreatePortfoliosOrderRequest` { `IdempotencyKey` req; `UpvestApiVersion`; `Body: PortfoliosOrderPlaceRequest?` } | `UpvestInvestmentApi.Models.PortfoliosOrderPlaceRequest`: req `UserId(user_id): Guid`, `AccountId(account_id): Guid`, `CashAmount(cash_amount): string` (regex allows ≤27 dp), `Currency(currency): Currency` = `.Eur`, `Side(side): Side15` = `.Buy`; opt `PostTax(post_tax): bool?` default false | `UpvestInvestmentApi.Models.PortfoliosOrder` → `Id: Guid`, `Status: Status65` (NEW/PROCESSING/FILLED/SETTLED/CANCELLED), `CancellationReason?` | `ApiException<UpvestInvestmentApi.Errors.CreatePortfoliosOrderError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `Portfolios.md`; `Models/PortfoliosOrderPlaceRequest.cs`, `Models/PortfoliosOrder.cs`, `Models/Enums/{Side15,Status65,Currency}.cs` |
| `client.TopUps.RetrieveTopup` (scopes `topups:read`/`admin`) — status re-read | `UpvestInvestmentApi.Requests.TopUps.RetrieveTopupRequest` { `TopupId: Guid` req } | — | `UpvestInvestmentApi.Models.PaymentsTopupsResponse` → `Status: Status29?` | `ApiException<UpvestInvestmentApi.Errors.RetrieveTopupError>` — `TryGetNoContent` [401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `TopUps.md` |
| `client.Portfolios.RetrievePortfoliosOrder` (scopes `portfolios:read`/`admin`) — status re-read | `UpvestInvestmentApi.Requests.Portfolios.RetrievePortfoliosOrderRequest` { `PortfolioOrderId: Guid` req } | — | `UpvestInvestmentApi.Models.PortfoliosOrder` → `Status: Status65` | `ApiException<UpvestInvestmentApi.Errors.RetrievePortfoliosOrderError>` — `TryGetNoContent` [401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `Portfolios.md` |
| `client.TopUps.ListTopups` (scopes `topups:read`/`admin`) — list / `paidIn` cross-check | `UpvestInvestmentApi.Requests.TopUps.ListTopupsRequest` { `AccountGroupId: Guid` req; opt `Sort`,`Order`,`Limit`,`Offset` } | — | `UpvestInvestmentApi.Models.PaymentsTopUpsListResponse` (data of `PaymentsTopupsResponse` + meta; open at impl) | `ApiException<UpvestInvestmentApi.Errors.ListTopupsError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | **offset/limit** | map `TopUps.md` |

> **Flow 3 status mapping:** app `status` = `pending` while top-up `Status29` ∈ {null,`Confirmed`} or order
> `Status65` ∈ {`New`,`Processing`}; `invested` when order `Status65` ∈ {`Filled`,`Settled`}; `failed` when
> order `Status65` = `Cancelled` or top-up `Status29` = `Cancelled`. GET `/api/portfolio/payments` returns the
> app's own records (newest first) each carrying `paymentId, amount, status`.

### Flow 4 — realignment

| Operation | Request record + members | Body model + fields (wire) | Response → fields read | Error (Case A) | Pag. | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `client.PortfoliosRebalancing.TriggerPortfolioRebalancing` (scopes `portfolios:admin`) | `UpvestInvestmentApi.Requests.PortfoliosRebalancing.TriggerPortfolioRebalancingOperationRequest` { `IdempotencyKey` req; `UpvestApiVersion`; `Body: TriggerPortfolioRebalancingRequest?` } | `UpvestInvestmentApi.Models.AnyOf.TriggerPortfolioRebalancingRequest` (union) → **`Allocations`** arm `UpvestInvestmentApi.Models.Allocations`: req `AllocationsValue(allocations): IReadOnlyList<Guid>` (the model's `allocation_id`). (`Accounts` arm exists but realignment is per-model.) | `UpvestInvestmentApi.Models.TriggerPortfolioRebalancingResponse` → `Id: Guid` (realignmentId / execution id) | `ApiException<UpvestInvestmentApi.Errors.TriggerPortfolioRebalancingError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `map/operations/PortfoliosRebalancing.md`; `Models/AnyOf/TriggerPortfolioRebalancingRequest.cs`, `Models/Allocations.cs`, `Models/TriggerPortfolioRebalancingResponse.cs` |
| `client.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecution` (scopes `portfolios:read`/`admin`) | `UpvestInvestmentApi.Requests.PortfoliosRebalancing.RetrievePortfoliosRebalancingExecutionRequest` { `ExecutionId: Guid` req } | — | `UpvestInvestmentApi.Models.PortfoliosRebalancingExecution` → `Id`, `CreatedAt`, `UpdatedAt`, `Accounts: IReadOnlyList<Guid>?`, `Allocations: IReadOnlyList<Guid>?` — **NO status field** (see Blocker B2) | `ApiException<UpvestInvestmentApi.Errors.RetrievePortfoliosRebalancingExecutionError>` — `TryGetNoContent` [401,403,404,405,406,429,500,503,504] · `TryGetRawError` | none | map `PortfoliosRebalancing.md`; `Models/PortfoliosRebalancingExecution.cs` |
| `client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (scopes `portfolios:read`/`admin`) — derive completed/failed | `UpvestInvestmentApi.Requests.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrdersRequest` { `ExecutionId: Guid` req; opt `Sort`,`Order`,`Offset`,`Limit`,`StartDate`,`EndDate` } | — | `UpvestInvestmentApi.Models.PortfoliosRebalancingExecutionOrderListResponse` (data of execution orders carrying per-order status + `meta`; open at impl) | `ApiException<UpvestInvestmentApi.Errors.ListPortfolioRebalancingExecutionOrdersError>` — `TryGetNoContent` [400,401,403,404,405,406,429,500,503,504] · `TryGetRawError` | **offset/limit** | map `PortfoliosRebalancing.md`; `Models/PortfoliosRebalancingExecutionOrderListResponse.cs` |

### Flow 5 — the investor's portfolio

| Operation | Request record + members | Response → fields read | Error (Case A) | Pag. | Source |
| --- | --- | --- | --- | --- | --- |
| `client.Portfolios.RetrievePortfoliosConfiguration` (scopes `portfolios:read`/`admin`) | `RetrievePortfoliosConfigurationRequest` { `AccountId: Guid` req } | `UpvestInvestmentApi.Models.PortfoliosConfiguration` → `AllocationId` (= `modelId` followed) | `ApiException<UpvestInvestmentApi.Errors.RetrievePortfoliosConfigurationError>` — `TryGetNoContent` [401,403,404,405,406,429,500,503,504] · `TryGetRawError` | none | map `Portfolios.md` |
| `client.Valuations.GetAccountValuation` (scopes `valuations:read`) | `UpvestInvestmentApi.Requests.Valuations.GetAccountValuationRequest` { `AccountId: Guid` req; `PriceQuality: PriceQuality3` req (query `price_quality`; `.Eod`/`.HighestAvailable`) } | `UpvestInvestmentApi.Models.AccountValuation` → `TotalSecurityValue` (`Amount: string`,`Currency`) = **totalValue**; `SecurityPositions: IReadOnlyList<AccountValuationSecurityPosition>` each → `Instrument(Instrument6)` (→ isin/uuid), `Quantity: string`, `Value: Value?` (`Amount: string`,`Currency`; **null when no price**) = per-fund value; `ValuationTime` | `ApiException<UpvestInvestmentApi.Errors.GetAccountValuationError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | none | map `map/operations/Valuations.md`; `Models/AccountValuation.cs`, `Models/AccountValuationSecurityPosition.cs`, `Models/{TotalSecurityValue,Value}.cs`, `Models/Enums/PriceQuality3.cs` |
| `client.Positions.ListPositions` (scopes `positions:read`) — quantities (no value) | `UpvestInvestmentApi.Requests.Positions.ListPositionsRequest` { `AccountId: Guid` req; opt `Offset`,`Limit` } | `UpvestInvestmentApi.Models.PositionsListResponse` → `Data: IReadOnlyList<Datum1>` each → `Instrument(Instrument)` (`Uuid: Guid`, `Isin: string?`), `Quantity: string`, `SettledQuantity`, …; `Meta` | `ApiException<UpvestInvestmentApi.Errors.ListPositionsError>` — `TryGetNoContent` [400,401,403,404,406,429,500,503,504] · `TryGetRawError` | **offset/limit** (`Data`+`Meta`) | map `map/operations/Positions.md`; `Models/PositionsListResponse.cs`, `Models/Datum1.cs`, `Models/Instrument.cs`, `Models/Meta.cs` |

> **Flow 5 mapping:** `GET /api/portfolio` → `modelId` = `PortfoliosConfiguration.AllocationId`; `holdings` =
> `AccountValuation.SecurityPositions` (instrumentId from `Instrument6`, `quantity`, `value.amount`);
> `totalValue` = `AccountValuation.TotalSecurityValue.Amount`; `paidIn` = sum of the app's own `invested`/settled
> payment records (the SDK has no "total paid in" field — YOUR CALL below). Use `GetAccountValuation` as the
> primary read (it carries both quantity and value); `ListPositions` is the fallback when only quantities are
> needed.

### CROSS-OPERATION INVARIANTS

The map never states these; derived from the task. "Enforced where" names the check that must run before the write.

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| A `modelId` a shopper may follow (`PUT /investor/model`) must be one the operator defined | `Portfolios.UpdatePortfoliosConfiguration`/`CreatePortfoliosConfiguration` ← app's own model store (populated by `Portfolios.CreatePortfoliosAllocation`; cross-checkable via `Portfolios.ListPortfoliosAllocations`) | In the `PUT /api/portfolio/investor/model` handler: look up the app's model record by `{modelId}`, resolve its stored Upvest `allocation_id`; 400/404 if unknown — before calling Upvest |
| A `modelId` a realignment targets must be an existing operator model | `PortfoliosRebalancing.TriggerPortfolioRebalancing` ← app model store (← `Portfolios.CreatePortfoliosAllocation`) | In `POST /api/portfolio/models/{modelId}/realignments`: resolve `{modelId}`→`allocation_id` from the app store before calling Upvest |
| Each fund ISIN in a model must be a real, tradeable Upvest instrument | `Portfolios.CreatePortfoliosAllocation`/`UpdatePortfoliosAllocation` ← `Instruments.ListInstruments` | In the `PUT /api/portfolio/models/{modelId}` handler: validate every `instrumentId` against `ListInstruments` results before building the allocation body (also reject when weights ≠ 100) |
| A payment is accepted only from a shopper who is an accepted investor **and** has chosen a model | `TopUps.CreateTopup`→`Portfolios.CreatePortfoliosOrder` ← app investor record (`status=active`, `account_id`) + `PortfoliosConfiguration` existing (← `Portfolios.CreatePortfoliosConfiguration`) | In `POST /api/portfolio/payments`: refuse (409/400) unless the caller's stored investor `status` is `active` and a chosen `allocation_id` exists — before any Upvest call |
| Payment/portfolio reads act only on the caller's own `account_id`/`account_group_id` | all Flow 3/5 reads | Every handler resolves ids from the caller's own stored record keyed by `ClaimTypes.Name`; never accepts an account/user id from the request body (no cross-tenant access) |

### Client construction, auth, signing, servers

- **Client**: `new UpvestInvestmentApi.UpvestInvestmentApiClient(HttpClient httpClient, UpvestInvestmentApi.UpvestInvestmentApiClientOptions options)` — the only constructor. Register once (singleton) via
  `services.AddUpvestInvestmentApiClient(options => { … })` (DI extension; `ServiceCollectionExtensions.cs`). Source: `sdk-map.md` "Getting a client".
- **OAuth2 (client credentials)**: `options.OauthClientCredentials = new UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials.OAuth2ClientCredentials { ClientId = Upvest:ClientId, ClientSecret = Upvest:ClientSecret }`. Token minted per scope set from `{BaseUrl}/auth/token`. Source: `sdk-map.md` "Servers & auth".
- **Request signing (required on EVERY call, including the token request)**: `options.Hooks = [ new UpvestInvestmentApi.Signing.UpvestSigningHook( UpvestInvestmentApi.Signing.UpvestHttpSignatureCredentials.FromFile(keyId: Upvest:SigningKeyId, privateKeyPath: Upvest:SigningKeyPath, upvestClientId: Upvest:ClientId, privateKeyPassphrase: Upvest:SigningKeyPassphrase)) ]`. If no hook and no signing env vars are set, **every call throws `InvalidOperationException` before being sent**. Source: `sdk-map.md` "Request signing".
- **Base URL**: bind `Upvest:BaseUrl` onto the `Default` server group: `options.Server.Default.Production.BaseUrl = Upvest:BaseUrl` (map default `Production`=`https://sandbox.upvest.co`). Keep `options.Environment = UpvestInvestmentApi.Servers.ServerEnvironment.Production` for sandbox. Source: `sdk-map.md` "Servers & auth" (1 server group `Default`; `Production`/`Environment2`).

### Enums used (values verbatim; all `OpenStringEnum<T>` — use static members, open on read)

| Enum | Members (C# → wire) | Source |
| --- | --- | --- |
| `UpvestInvestmentApi.Models.Enums.UpvestApiVersion` | `_1`→`1` (request-record default) | `Models/Enums/UpvestApiVersion.cs` |
| `UpvestInvestmentApi.Models.Enums.Currency` | `Eur`→`EUR`, `Gbp`→`GBP` | `Models/Enums/Currency.cs` |
| `UpvestInvestmentApi.Models.Enums.Side15` | `Buy`→`BUY`, `Sell`→`SELL` | `Models/Enums/Side15.cs` |
| `UpvestInvestmentApi.Models.Enums.Type13` (account-group) | `Personal`→`PERSONAL` (+ CHILD/JOINT/ISA/…) | `Models/AccountGroupCreateUserRequest.cs` → `Models/Enums/Type13.cs` |
| `UpvestInvestmentApi.Models.Enums.Type16` (account) | `Trading`→`TRADING`, `Portfolio`→`PORTFOLIO` | `Models/Enums/Type16.cs` |
| `UpvestInvestmentApi.Models.Enums.Status` (user) | `Active`,`Inactive`,`Offboarding`,`Offboarded` (no REJECTED — Blocker B3) | `Models/Enums/Status.cs` |
| `UpvestInvestmentApi.Models.Enums.Status29` (top-up) | `Confirmed`,`Settled`,`Cancelled` | `Models/Enums/Status29.cs` |
| `UpvestInvestmentApi.Models.Enums.Status65` (order) | `New`,`Processing`,`Filled`,`Settled`,`Cancelled` | `Models/Enums/Status65.cs` |
| `UpvestInvestmentApi.Models.Enums.PriceQuality3` (valuation query) | `Eod`→`EOD`, `HighestAvailable`→`HIGHEST_AVAILABLE` | `Models/Enums/PriceQuality3.cs` |
| `UpvestInvestmentApi.Models.Enums.InstrumentIdType4` | default `Isin` (+ `Upvest`) | `Models/Allocation.cs` → `Models/Enums/InstrumentIdType4.cs` |

---

## 3. Trap notes (hazard + cost; load the named skill before coding the step)

- **T1 — amounts are decimal strings, not numbers.** `CashAmount`/`Amount`/`Weight`/`Quantity`/`Value.Amount`
  are wire strings with their own regexes (top-up/bank-tx 2dp; order ≤27dp; weight `(100|dd(.ddddd)?)`). The app's
  JSON contract uses euro numbers with 2 decimals; converting number↔string wrongly (culture, trailing zeros,
  rounding) silently fails validation or distorts money. **MUST load upvest-v4:dotnet-models.**
- **T2 — union request/response arms.** `UserCreateRequest`, `AccountCreateRequest`, `AccountGroupCreateRequest`,
  `TriggerPortfolioRebalancingRequest`, `InstrumentId`, and the `*Response`/`UserGetResponse` unions are `AnyOf`
  with `TryGet…`/`Match`/factory/implicit-conversion; picking the wrong arm or reading before narrowing misbehaves.
  **MUST load upvest-v4:dotnet-models.**
- **T3 — open enums.** All status/type enums are `OpenStringEnum<T>`; an unrecognised wire value lands in the
  `Match` `otherwise` arm, so status mapping must handle the unknown branch rather than assume the five values.
  **MUST load upvest-v4:dotnet-models.**
- **T4 — signing + OAuth are mandatory on every call.** A missing signing hook/key throws
  `InvalidOperationException` before send; a missing/blank OAuth credential throws `AuthSchemeException`; a wrong
  base URL or scope yields `401`. Know which failure is config vs server before reacting. **MUST load
  upvest-v4:dotnet-authentication.**
- **T5 — client lifetime, HttpClient ownership, DI, singleton capture of options.** Build the options object once
  and register the client as a singleton; getting lifetime/HttpClient wrong leaks sockets or re-reads config
  unexpectedly. **MUST load upvest-v4:dotnet-client-initialization.**
- **T6 — what `Timeout` bounds, retried verbs, base-URL/server selection, and pagination mechanics.** Several
  reads here are offset/limit paged; the total call budget, which verbs auto-retry, and how the `Default` server
  group resolves are not visible from signatures. **MUST load upvest-v4:dotnet-configuration-resilience.**
- **T7 — the error ladder and non-2xx/drifted-body surfacing.** Case-A `TryGetNoContent`/`TryGetRawError`
  mechanics, and the fact a drifted body becomes `ResponseDeserializationException` (not `ApiException<TError>`).
  **MUST load upvest-v4:dotnet-error-handling.**
- **T8 — stubbing the SDK for tests** (HttpClient seam, faking the clock for poll/re-read, constructing SDK
  exceptions, asserting the outgoing request). **MUST load upvest-v4:dotnet-testing.**

---

## 4. REQUIRED READING (load ALL before implementation starts; names plugin-qualified)

- `upvest-v4:dotnet-client-initialization` — step 1 (client/DI/HttpClient lifetime).
- `upvest-v4:dotnet-authentication` — step 1 (OAuth + signing hook, credential fail-fast).
- `upvest-v4:dotnet-configuration-resilience` — step 1 & paged reads (timeout budget, retries, base URL, pagination, logging posture).
- `upvest-v4:dotnet-models` — every flow (string amounts, `AnyOf` unions, open enums, dates, `AdditionalProperties`).
- `upvest-v4:dotnet-calling-endpoints` — first call per controller (request record, `requestOptions`/`cancellationToken`, response shapes).
- `upvest-v4:dotnet-error-handling` — the error boundary (always required). **Hazard row:** a body that does not
  match its declared type — a drifted/malformed **2xx** (missing `required` member) or a **non-2xx** body that does
  not match the operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an
  `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a
  catch ladder handling only `ApiException<TError>` lets it escape, so it must also catch
  `ResponseDeserializationException` (or `ApiException`).
- `upvest-v4:dotnet-testing` — step 8 (stubbing).

---

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | **Credential fail-fast** | Bind `Upvest:ClientId`, `Upvest:ClientSecret`, `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:SigningKeyPassphrase`, `Upvest:BaseUrl` from `IConfiguration` at DI registration (eShopOnWeb binds config in `Program.cs`/`Infrastructure/Dependencies.cs`). Add a startup check that throws if any of ClientId/ClientSecret/SigningKeyId/SigningKeyPath/BaseUrl is **missing or blank** (passphrase may legitimately be empty if the key is unencrypted — decide per key, YOUR CALL). Rationale: the SDK otherwise only fails on the first call (`InvalidOperationException` for signing, `AuthSchemeException` for OAuth). Do NOT rely on the SDK's `UPVEST_*` env fallbacks in production — credentials come from the `Upvest:` section. Source: `sdk-map.md` "Request signing"/"Servers & auth"; YOUR CALL — not in the map (exact check placement). |
| 2 | **Secret sourcing & rotation** | `Upvest:ClientSecret` and `Upvest:SigningKeyPassphrase` are secrets (user-secrets/KeyVault/env in deployment; `appsettings` only for non-secret dev). DI builds the options object **once at registration** and the singleton client captures it, so a rotated secret/key takes effect **only on process restart**. No hot-rotation is implemented; document the restart requirement. Source: `sdk-map.md` client construction; YOUR CALL. |
| 3 | **Total timeout budget** | The caller-visible budget is the per-attempt `options.Retry.Timeout` multiplied across `MaxRetries` plus backoff, bounded by the request `CancellationToken`. Decide a concrete budget (e.g. per-attempt timeout + max retries) and thread the ASP.NET request `CancellationToken` into every call. Exact meaning of `Timeout` vs a whole-call bound: confirm in `dotnet-configuration-resilience`. Source: `sdk-map.md` `RetryOptions`; decision YOUR CALL after reading that skill. |
| 4 | **Write-retry ownership** | Writes here (`CreateUser`, `CreateAccountGroup`, `CreateAccount`, `CreatePortfoliosAllocation`, `UpdatePortfoliosAllocation`, `CreatePortfoliosConfiguration`, `UpdatePortfoliosConfiguration`, `CreateTopup`, `CreateBankTransaction`, `CreatePortfoliosOrder`, `TriggerPortfolioRebalancing`) are POST/PUT. Which verbs the SDK auto-resends is set by `options.Retry.HttpMethodsToRetry` — confirm in `dotnet-configuration-resilience` and decide whether POST stays out of the retry set (each of these writes carries a real `IdempotencyKey`, so an Upvest-side replay is deduplicated — see row 5). Source: `sdk-map.md` `RetryOptions`; decision YOUR CALL. |
| 5 | **Idempotency & ambiguous writes** | Every write request record carries a **real** `IdempotencyKey: Guid` (per-member docs: "prevents a duplicate request from being replayed"). Generate it **deterministically from the app's own claim/record** (not a fresh `Guid` per attempt) and persist it with the claim, so a retry replays the same key. The auto-injected `Idempotency-Key` header on non-GET calls is **not** this key and must not be cited as one. Reconciliation path for a lost response: re-read by the stored reference (§ UNKNOWN OUTCOMES). Source: `sdk-getting-started` "Idempotency"; request-record files. |
| 6 | **Observability** | Log at Information the app-side event + Upvest resource id (`user_id`, `allocation_id`, top-up/order/execution id) and status transitions; at Warning/Error the `RawError.StatusCode` and `RawError.ReadAsString()` / a correlation id from the error body (confirm field via `ReadAsJson<T>` in `dotnet-error-handling`). **Never** log personal data or secrets (row 7). Source: `sdk-map.md` error model; YOUR CALL on levels. |
| 7 | **Sensitive data** | **Yes — Flow 1 carries personal data** (`UserTolCreateRequest`: name, email, birth date/city, nationalities, address, phone; plus app-collected taxId/identityDocument). Posture: keep `options.Logging.LogRequestBody` **off**; set `options.Logging.LoggerFactory` **explicitly** (do not leave it unset — that arms the `UPVEST_INVESTMENT_API_LOG=trace` body-logging backdoor); never echo request bodies on these paths; the task's "personal details never written to logs" is mandatory. Confirm deny-list/form-body masking in `dotnet-configuration-resilience`. Source: `sdk-getting-started` "Sensitive data"; `Models/UserTolCreateRequest.cs`. |
| 8 | **Environment selection** | One server group `Default`, two environments: `Production`=`https://sandbox.upvest.co` (sandbox), `Environment2`=`https://api.upvest.co` (live). `Upvest:BaseUrl` overrides the group's base URL; set `Production` for dev/test and point live deployments at the live URL/environment. **Sandbox scope wall:** the sandbox client is authorised only for `users:*`, so only `CreateUser`/`RetrieveUser` succeed there; all other operations return `401 "not authorised to use these scopes"` — this is an environment limitation, not a defect (Assumptions/Blockers). Source: `sdk-map.md` "Servers & auth". |
| 9 | **Duplicate prevention under concurrency** | See DUPLICATE CLAIMS. The in-memory EF provider (`UseOnlyInMemoryDatabase=true`) persists only within a run, but a primary-key/unique claim row is still refused by a concurrent second writer within the process. |
| 10 | **Partial results** | See PAGED READS. |
| 11 | **Unknown outcomes** | See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS — one row per write a caller can trigger twice

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| Become an investor (`POST /api/portfolio/investor` → CreateUser→CreateAccountGroup→CreateAccount) | App `Investor` record keyed by `ShopperUserName` (unique index, `InvestorConfiguration`) via `EfRepository<T>` | Existing record with a set `UpvestUserId` short-circuits; a unique-index clash on the username refuses a concurrent second sign-up (SQL Server) | `PortfolioInvestorService.SignUpAsync` — existing-record check returns the current view; a re-POST reuses the stored keys | **claim:** `PortfolioInvestorService.SignUpAsync` → `_investors.AddAsync(new Investor(userName))`; **SDK call:** `UpvestInvestmentGateway.CreateInvestorAsync` → `client.Users.CreateUser` (IdempotencyKey = `Investor.UserIdempotencyKey`) |
| Create/replace a model (`PUT /api/portfolio/models/{modelId}`) | App `ModelPortfolio` record keyed by `ModelKey` (unique index) | Lookup by `ModelKey` selects create vs replace; existing → `UpdatePortfoliosAllocation`, absent → create | `ModelPortfolioService.UpsertModelAsync` | **claim:** `ModelPortfolioService.UpsertModelAsync` → `_models.AddAsync(new ModelPortfolio(...))`; **SDK call:** `gateway.CreateModelAllocationAsync` → `client.Portfolios.CreatePortfoliosAllocation` (IdempotencyKey = `ModelPortfolio.AllocationIdempotencyKey`) |
| Choose/switch model (`PUT /api/portfolio/investor/model`) | The caller's single `Investor` record (`SelectedModelPortfolioId`) | Row-level update of the caller's own record serializes concurrent writes | `ModelPortfolioService.ChooseModelAsync` | **claim/update:** `ModelPortfolioService.ChooseModelAsync` → `investor.ChooseModel(id)` + `_investors.UpdateAsync`; **SDK call:** `gateway.SetFollowedModelAsync` (IdempotencyKey = `Investor.ModelChoiceIdempotencyKey`) |
| Pay in (`POST /api/portfolio/payments` → CreateTopup→…→CreatePortfoliosOrder) | App `PortfolioPayment` record with `PaymentKey` (unique index), written **before** the Upvest calls | Unique `PaymentKey` row refuses a duplicate before any Upvest call | `PortfolioPaymentService.PayInAsync` | **claim:** `PortfolioPaymentService.PayInAsync` → `_payments.AddAsync(new PortfolioPayment(userName, amount))`; **SDK call:** `gateway.PayInAsync` → `CreateTopup`/`CreateBankTransaction`/`CreatePortfoliosOrder` (IdempotencyKeys from the payment record) |
| Trigger realignment (`POST /api/portfolio/models/{modelId}/realignments`) | App `PortfolioRealignment` record with `RealignmentKey` (unique index), written before the trigger | Unique `RealignmentKey` row refuses the duplicate before `TriggerPortfolioRebalancing` | `PortfolioRealignmentService.TriggerAsync` | **claim:** `PortfolioRealignmentService.TriggerAsync` → `_realignments.AddAsync(new PortfolioRealignment(modelKey))`; **SDK call:** `gateway.TriggerRealignmentAsync` → `client.PortfoliosRebalancing.TriggerPortfolioRebalancing` (IdempotencyKey = `PortfolioRealignment.IdempotencyKey`) |

Order per row: **claim → SDK call → record result.** The deterministic `IdempotencyKey` (row 5) is stored
beside each claim as the provider-side backstop, never in place of the app claim.

### PAGED READS — one row per read that walks pages

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `Instruments.ListInstruments` (ISIN validation) | `Limit`/`Offset` (100) vs `Meta.TotalCount`, capped at `maxPages=200` | The read returns a complete `IReadOnlySet<string>` of ISINs; validation gives a definite valid/invalid result, not a truncated page | `UpvestInvestmentGateway.ListTradeableIsinsAsync` — loops until `offset >= Meta.TotalCount` |
| `Positions.ListPositions` / `GetAccountValuation` (Flow 5 holdings) | `GetAccountValuation` returns the full `SecurityPositions` list in one response (no paging); `ListPositions` is the fallback | `GetPortfolioAsync` returns the full holdings array from the single valuation response | `UpvestInvestmentGateway.GetPortfolioAsync` — uses `GetAccountValuation` (unpaged) |
| `PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders` (status derivation) | `Limit`/`Offset` (100) vs `Meta.TotalCount`, capped at `maxPages=200` | Pages to completion before deriving `completed`/`failed`, so a late page's order is never missed; the derived status is the return value | `UpvestInvestmentGateway.GetRealignmentStatusAsync` — loops until `offset >= Meta.TotalCount`, then `DeriveRealignmentStatus` |
| `Portfolios.ListPortfoliosAllocations` / `TopUps.ListTopups` | n/a — not used | `GET /models` is served from the app's own `ModelPortfolio` store; `paidIn` is summed from the app's own `PortfolioPayment` records, so no Upvest list is paged | not used |

A log line is not a legal answer; the field/return type changes.

### UNKNOWN OUTCOMES — one row per write whose connection can fail after the provider may have acted

A transport failure on any write surfaces from `UpvestInvestmentGateway.CallAsync` as
`UpvestGatewayException(OutcomeUnknown = true)` (built in the `OperationCanceledException` budget catch, the
`SdkTimeoutException` catch and the `SdkConnectionException` catch, all with `isWrite: true`). The owning
service treats `OutcomeUnknown` as **pending**, never **failed**, and records the claim so the next GET
reconciles it. The deterministic, persisted `IdempotencyKey` on each record makes the re-attempt replay-safe.

| Write | Re-read with | Reference searched by | Where in the code | Test that fails the connection |
| --- | --- | --- | --- | --- |
| `Users.CreateUser` + `CreateAccountGroup` + `CreateAccount` | `Users.RetrieveUser` + `AccountGroups.RetrieveAccountGroup`; account/group re-created with stored keys | stored `UpvestUserId`/`AccountGroupId`; a lost `CreateUser` keeps the pending claim and a re-POST replays with `Investor.UserIdempotencyKey` | `PortfolioInvestorService.SignUpAsync` catch → record stays pending; `GetStatusAsync`→`ReconcileAsync`→`gateway.ProvisionAccountAsync`/`DeriveInvestorStatusAsync` self-heals | `PortfolioInvestorServiceTests` cover the view mapping; live reconcile is driven by `GET /api/portfolio/investor` |
| `TopUps.CreateTopup` / `Tests.CreateBankTransaction` / `Portfolios.CreatePortfoliosOrder` | `Portfolios.RetrievePortfoliosOrder`, else `TopUps.RetrieveTopup` | stored `UpvestOrderId`/`UpvestTopupId` on the `PortfolioPayment` | `PortfolioPaymentService.PayInAsync` catch (`OutcomeUnknown`→Pending); `ListPaymentsAsync`→`gateway.GetPaymentStatusAsync` reconciles pending payments | `PortfolioPaymentServiceTests.InvestsWhenAcceptedWithModel` asserts the success path; pending-on-failure is the `catch` branch |
| `Portfolios.CreatePortfoliosConfiguration`/`UpdatePortfoliosConfiguration` | `Portfolios.RetrievePortfoliosConfiguration` | `AccountId` | `ModelPortfolioService.ChooseModelAsync` (gateway `SetFollowedModelAsync` reads config first, then create/update) | covered by `ChooseModelRefusesWhenAccountNotReady` (state guard) |
| `PortfoliosRebalancing.TriggerPortfolioRebalancing` | `RetrievePortfoliosRebalancingExecution` + `ListPortfolioRebalancingExecutionOrders` | stored `UpvestExecutionId` on `PortfolioRealignment`; without it the claim stays pending (Blocker B2) | `PortfolioRealignmentService.TriggerAsync` catch (`OutcomeUnknown`→Pending); `GetAsync`→`gateway.GetRealignmentStatusAsync` reconciles | reconcile driven by `GET /api/portfolio/realignments/{id}` |

Settle the outcome in the failing write's own `catch` (or record it as unknown for a reconciliation re-read),
never report a bare failure or leave the caller to retry blindly.

---

## 6. Assumptions & Blockers

**Assumptions**

- **A1 — operating model is TOL (Take Our Licence).** The task's sign-up form (the shop collects KYC and Upvest
  holds the ledger) matches the `UserTolCreateRequest` arm of `UserCreateRequest`, not BYOL. Build the TOL arm.
- **A2 — the managed-portfolio account is an Upvest `PERSONAL` account group + `PORTFOLIO` account.** The task's
  "account able to hold a managed portfolio" maps to account type `Type16.Portfolio` (portfolio-basis orders +
  portfolio functionality) inside a `Type13.Personal` group; top-ups fund the group, orders/positions/valuation
  act on the account.
- **A3 — the app's `modelId` is its own identifier, mapped to an Upvest allocation UUID.** `PUT /models/{modelId}`
  creates (first time) or replaces (`UpdatePortfoliosAllocation`) the Upvest allocation; the app stores the
  `{modelId}`→`allocation_id` mapping. The route's `{modelId}` is never sent to Upvest as-is.
- **A4 — `paidIn` is the app's own sum of settled/invested payments** (no single Upvest "total paid in" field).
- **A5 — Flow 3 "pay in" = top-up (fund account-group cash) + sandbox bank transaction (settle) + portfolio
  order BUY (invest across the allocation).** This is the only SDK path that both credits cash and invests it per
  the model weights; `Tests.CreateBankTransaction` is Upvest's sandbox mechanism for money reaching the account.
- **A6 — money is euros, two decimals, EUR currency** (`Currency.Eur`); the app converts its JSON euro numbers to
  the SDK's decimal-string wire format and back (trap T1).

**Blockers**

- **B1 — `taxId`, `taxCountry`, and `identityDocument` have no slot on the TOL create body.** `UserTolCreateRequest`
  carries name/email/birth/nationalities/address/fatca/consents but no tax-residency or identity-document members;
  those live on separate Upvest resources (`TaxResidencies`, `UserIdentifiers`/`UserChecks`) that are outside the
  five flows *and* outside the user-only sandbox scope. **Decision needed:** store these on the app side only and
  treat Upvest-side submission as out of scope, OR expand scope to those controllers. Planner recommends the
  former; confirm with the user.
- **B2 — a rebalancing execution exposes no status field.** `PortfoliosRebalancingExecution` has only
  `id/created_at/updated_at/accounts/allocations` — no `pending/completed/failed`. The task requires the
  realignment status to reflect Upvest. The only derivation is polling
  `ListPortfolioRebalancingExecutionOrders` and inferring from the per-execution-order statuses, which is
  **UNVERIFIED** against live traffic. **Decision needed:** accept the derived status (document it as best-effort)
  or treat exact completed/failed reporting as unsupported. Also: if the `Trigger` response is lost, there is no
  documented search key to find the execution (UNKNOWN OUTCOMES last row) — that write is not safely reconcilable
  without the returned id.
- **B3 — no `rejected` user status.** `Status` is `ACTIVE/INACTIVE/OFFBOARDING/OFFBOARDED`; there is no `REJECTED`.
  The app can report `pending` (user `INACTIVE` or account group `PENDING_APPROVAL`) vs `active` (user `ACTIVE` +
  account group `ACTIVE`), but a true Upvest `rejected` outcome is not expressible from these fields (rejection
  surfaces through `UserChecks`, out of scope/sandbox-scope). **Decision needed:** map `rejected` only when a
  check/offboarding signal is available later, or drop the `rejected` state. UNVERIFIED.
- **B4 — sandbox scope wall (environment limitation, not a code gap).** Per the task, the sandbox client is
  authorised only for `users:*`; every `accounts/portfolios/topups/tests/positions/valuations/instruments`
  operation returns `401 "This client is not authorised to use these scopes"`. End-to-end verification in sandbox
  is limited to Flow 1's `CreateUser`/`RetrieveUser`; the rest are built in full and verified structurally
  (unit tests, request-shape assertions) but cannot be exercised live until broader scopes are granted.

**YOUR CALL — not in the map — SETTLED at implementation:**

- **Row 1 (fail-fast placement & passphrase policy):** `UpvestServiceRegistration.AddManagedPortfolio` throws
  `InvalidOperationException` at startup when `Upvest:ClientId/ClientSecret/SigningKeyId/SigningKeyPath/BaseUrl`
  is missing or blank (naming the key, never the value). `Upvest:SigningKeyPassphrase` is **not** required
  (an unencrypted key is legitimate); it is passed as `null` when empty.
- **Row 2 (rotation):** the options object is built once in the `AddUpvestInvestmentApiClient` callback and
  captured by the singleton client — a rotated secret/key takes effect only on **process restart** (documented).
- **Row 3 (timeout budget):** per-attempt `options.Retry.Timeout = 15s`; whole-call budget `30s` enforced by a
  linked `CancellationTokenSource` in `UpvestInvestmentGateway.CallAsync` (also linked to the request's token).
- **Row 4 (POST retry):** default `HttpMethodsToRetry` (GET/HEAD/PUT/OPTIONS) kept — POSTs are not auto-resent;
  every write carries a real `IdempotencyKey`, so a PUT resend or OAuth-`401` replay is deduplicated at Upvest.
- **Row 6 (observability):** Info logs app events + Upvest resource ids + status transitions; Warning logs the
  provider `StatusCode`. Personal data and request bodies are never logged (`LogRequestBody` stays off).
- **Row 7 (sensitive data):** request bodies carry personal data → `LogRequestBody` off (default); under DI the
  `LoggerFactory` is filled from the container, disarming the `UPVEST_INVESTMENT_API_LOG` body-logging backdoor.
- **A3 (create-vs-replace):** `ModelPortfolioService.UpsertModelAsync` looks the model up by `ModelKey`; absent →
  `CreatePortfoliosAllocation`, present with an allocation id → `UpdatePortfoliosAllocation`, present without one →
  create (self-heal).
- **A4 (`paidIn`):** `PortfolioQueryService.GetPortfolioAsync` sums the app's own `PortfolioPayment` rows with
  status `Invested`.
- **Idempotency keys (row 5 / DUPLICATE CLAIMS):** generated once in each entity's constructor and persisted
  (`Investor.UserIdempotencyKey/AccountGroupIdempotencyKey/AccountIdempotencyKey/ModelChoiceIdempotencyKey`,
  `ModelPortfolio.AllocationIdempotencyKey`, `PortfolioPayment.{Topup,BankTransaction,Order}IdempotencyKey`,
  `PortfolioRealignment.IdempotencyKey`) — reused verbatim on every retry.
- **B1 (tax/identity fields):** decided to **submit** to Upvest, not drop. Identity document →
  `UserIdentifiers.CreateIdentifier` (retries without the value for CONCAT jurisdictions — verified `200` live);
  tax residency → `TaxResidencies.SetTaxResidencies` (best-effort; `taxes:admin` is outside the sandbox scope).
  `identityDocument.expiryDate` has no Upvest slot and is accepted but not persisted.
- **B2 (realignment status):** derived best-effort from `ListPortfolioRebalancingExecutionOrders`
  (`DeriveRealignmentStatus`): any NEW/PROCESSING → pending, all FILLED → completed, otherwise failed. `UNVERIFIED`
  against live traffic (scope-walled).
- **B3 (`rejected`):** mapped from user `Status` OFFBOARDING/OFFBOARDED → `rejected`; `ACTIVE` user + `ACTIVE`
  account group → `active`; otherwise `pending` (`DeriveInvestorStatusAsync`). `UNVERIFIED` (RetrieveUser is
  scope-walled in sandbox).

---

## 7. Source labels

All `Source` cells above cite a map page (`sdk-map.md` / `map/operations/*.md`) and/or the one SDK source file
declaring the type, under the plugin SDK root `sdk/dotnet/`. `UNVERIFIED` is used only where live traffic alone can
settle it (B2 derived rebalancing status, B3 rejected mapping). `YOUR CALL — not in the map` marks application
decisions (listed above). Nothing is deferred to a later source lookup.
