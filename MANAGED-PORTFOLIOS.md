# Managed Investment Portfolios (Upvest integration)

An **additive** capability on top of eShopOnWeb that lets a signed-in shopper hold a managed
investment portfolio, with **Upvest** as the investment provider. It does not touch the existing
catalog/basket/order flow. All endpoints live on **`src/PublicApi`** (JWT-authenticated) under
`/api/portfolio`.

## What was built

| Flow | Endpoint | Who | Upvest mapping |
|------|----------|-----|----------------|
| Become an investor | `POST /api/portfolio/investor` | any shopper | `POST /users` → `POST /account_groups` → `POST /accounts`; identity doc → `POST /users/{id}/checks` (KYC); tax → `POST /users/{id}/tax_residencies` |
| Investor status | `GET /api/portfolio/investor` | any shopper | `GET /accounts/{id}` (+ KYC checks) |
| Create/replace model | `PUT /api/portfolio/models/{modelId}` | **admin** | `POST`/`PUT /portfolios/allocations` |
| List models | `GET /api/portfolio/models` | any shopper | local store of operator-defined models |
| Choose model | `PUT /api/portfolio/investor/model` | any shopper | `POST`/`PATCH /portfolios/configurations` |
| Pay in | `POST /api/portfolio/payments` | any shopper | `POST /virtual_cash_balances/increases` → `POST /portfolios/orders` (BUY) |
| List payments | `GET /api/portfolio/payments` | any shopper | `GET /portfolios/orders/{id}` per payment |
| Realign a model | `POST /api/portfolio/models/{modelId}/realignments` | **admin** | `POST /portfolios/rebalancings/executions` |
| Realignment status | `GET /api/portfolio/realignments/{id}` | any shopper | derived from `GET /portfolios/rebalancings/executions/{id}/orders` |
| View portfolio | `GET /api/portfolio` | any shopper | `GET /accounts/{id}/valuations/current` |

**Status vocabulary** (always read live from Upvest): investor `pending`→`active`/`rejected`;
payment `pending`→`invested`/`failed`; realignment `pending`→`completed`/`failed`. All money
amounts are JSON numbers in euros with two decimal places.

### Design

- **ApplicationCore** — domain entities (`Entities/PortfolioAggregate`: `Investor`,
  `ModelPortfolio`/`ModelFund`, `PortfolioPayment`, `Realignment`), the `IManagedPortfolioService`
  port + its result contracts, specifications, and `PortfolioExceptions`.
- **Infrastructure** — the Upvest client (`Upvest/`): OAuth2 token provider, HTTP **message
  signing** (ECDSA P-521 / SHA-512, v15), typed `IUpvestClient`, and the orchestration
  `Services/ManagedPortfolioService`. New aggregates are stored via the existing `CatalogContext`
  / `IRepository<T>`.
- **PublicApi** — endpoints (`PortfolioEndpoints/`) following the project's `IEndpoint`
  convention, request/response DTOs, a money JSON converter, and exception→status mapping.

A shopper's investor record, payments and portfolio are keyed to their token identity, so one
shopper can never see another's. Personal details are sent straight to Upvest and are never
written to logs.

## Secrets

Credentials are read from the environment and loaded into **.NET user-secrets** on the PublicApi
project — **no secret value is ever written into the repository**. Settings bind from the
`Upvest:` section (`ClientId`, `ClientSecret`, `SigningKeyId`, `SigningKeyPath`,
`SigningKeyPassphrase`, `BaseUrl`). To (re)load them:

```bash
cd src/PublicApi
dotnet user-secrets set "Upvest:ClientId"            "$UPVEST_CLIENT_ID"
dotnet user-secrets set "Upvest:ClientSecret"        "$UPVEST_CLIENT_SECRET"
dotnet user-secrets set "Upvest:SigningKeyId"        "$UPVEST_SIGNING_KEY_ID"
dotnet user-secrets set "Upvest:SigningKeyPath"      "$UPVEST_SIGNING_KEY_PATH"
dotnet user-secrets set "Upvest:SigningKeyPassphrase" "$UPVEST_SIGNING_KEY_PASSPHRASE"
dotnet user-secrets set "Upvest:BaseUrl"             "$UPVEST_BASE_URL"
```

## Run it

```bash
cd <repo root>
export DOTNET_ROLL_FORWARD=Major
export UseOnlyInMemoryDatabase=true
export ASPNETCORE_ENVIRONMENT=Development          # so user-secrets load
export ASPNETCORE_URLS="https://localhost:42363;http://localhost:42364"
dotnet run --project src/PublicApi --no-launch-profile
```

PublicApi listens on `https://localhost:42363` (Swagger at `/swagger`). The in-memory database
resets each run, so investors/models/payments live only within a single run.

## Verify it yourself

Use `-k` with curl (local dev HTTPS cert). First get bearer tokens:

```bash
B=https://localhost:42363
DEMO=$(curl -sk -X POST "$B/api/authenticate" -H "Content-Type: application/json" \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | sed -E 's/.*"token":"([^"]+)".*/\1/')
ADMIN=$(curl -sk -X POST "$B/api/authenticate" -H "Content-Type: application/json" \
  -d '{"username":"admin@microsoft.com","password":"Pass@word1"}' | sed -E 's/.*"token":"([^"]+)".*/\1/')
```

### 1. Become an investor (creates a real user at Upvest)

```bash
curl -sk -X POST "$B/api/portfolio/investor" -H "Authorization: Bearer $DEMO" \
 -H "Content-Type: application/json" -d '{
 "firstName":"Max","lastName":"Mustermann","email":"max.test@example.com","birthDate":"1985-03-10",
 "birthCity":"Berlin","birthCountry":"DE","nationality":"DE",
 "address":{"line1":"Musterstrasse 1","postcode":"10115","city":"Berlin","country":"DE"},
 "phoneNumber":"+49 1234 567890","taxId":"12345678901","taxCountry":"DE",
 "identityDocument":{"type":"ID_CARD","number":"T22000129","issuingCountry":"DE","expiryDate":"2030-01-01"}}'
# → 201 {"investorId":"<real Upvest user UUID>","status":"pending"}
```

```bash
curl -sk "$B/api/portfolio/investor" -H "Authorization: Bearer $DEMO"
# → 200 {"investorId":"…","status":"pending"}
```

### 2. Isolation & auth (expect the stated codes)

```bash
curl -sk -o /dev/null -w "%{http_code}\n" "$B/api/portfolio/investor" -H "Authorization: Bearer $ADMIN"  # 404 (admin not signed up)
curl -sk -o /dev/null -w "%{http_code}\n" "$B/api/portfolio"                                             # 401 (no token)
curl -sk -o /dev/null -w "%{http_code}\n" -X PUT "$B/api/portfolio/models/x" -H "Authorization: Bearer $DEMO" \
  -H "Content-Type: application/json" -d '{"name":"X","funds":[{"instrumentId":"IE00B4L5Y983","weight":100}]}' # 403 (not admin)
```

### 3. Model validation

```bash
curl -sk -X PUT "$B/api/portfolio/models/bad" -H "Authorization: Bearer $ADMIN" \
 -H "Content-Type: application/json" -d '{"name":"Bad","funds":[{"instrumentId":"A","weight":60},{"instrumentId":"B","weight":30}]}'
# → 400 "Fund weights must sum to exactly 100 but summed to 90."
```

### 4. The rest of the flow

```bash
curl -sk "$B/api/portfolio/models"            -H "Authorization: Bearer $DEMO"   # 200 []
curl -sk -X PUT "$B/api/portfolio/investor/model" -H "Authorization: Bearer $DEMO" \
  -H "Content-Type: application/json" -d '{"modelId":"balanced"}'                # 409 account not ready yet
curl -sk -X POST "$B/api/portfolio/payments"  -H "Authorization: Bearer $DEMO" \
  -H "Content-Type: application/json" -d '{"amount":100.50}'                     # 409 account not ready yet
curl -sk "$B/api/portfolio/payments"          -H "Authorization: Bearer $DEMO"   # 200 []
curl -sk "$B/api/portfolio"                   -H "Authorization: Bearer $DEMO"   # 200 {"modelId":null,"holdings":[],"totalValue":0.00,"paidIn":0.00}
curl -sk -X PUT "$B/api/portfolio/models/balanced" -H "Authorization: Bearer $ADMIN" \
  -H "Content-Type: application/json" -d '{"name":"Balanced","funds":[{"instrumentId":"IE00B4L5Y983","weight":60},{"instrumentId":"IE00B3RBWM25","weight":40}]}'
# → 502 (Upvest 403 Forbidden – portfolios:admin not granted to this sandbox client; see below)
```

### Automated checks

```bash
dotnet test tests/UnitTests/UnitTests.csproj                       # 54 passed (incl. new portfolio domain tests)
dotnet test tests/PublicApiIntegrationTests/PublicApiIntegrationTests.csproj  # 15 passed (existing endpoints unaffected)
```

## Sandbox limitations (environment, not the integration)

This sandbox client is **authorised only for user operations** (`users:admin`). Therefore:

- The OAuth2 **token request** fails if it asks for any scope the client lacks, so
  `Upvest:Scopes` defaults to `users:admin`. **Widen it in production** to the full set the
  integration uses (`accounts:admin`, `portfolios:admin`, `orders:admin`, `payments:admin`,
  `topups:admin`, `virtual_cash_balances:admin`, `positions:read`, `valuations:read`, …).
- **Creating a user works** and is verified above (a real Upvest user is created). Every other
  Upvest call (account groups, accounts, allocations, configurations, orders, virtual-cash,
  rebalancing, positions, valuations) is refused with 401/403 by this environment. The endpoints
  implement those flows in full; sign-up deliberately keeps the investor as `pending` when account
  provisioning is refused, so the flows remain drivable. With a fully-provisioned client the status
  progresses to `active` and pay-ins/realignments/portfolio views populate from Upvest.
- **Consent documents:** the documented sandbox consent-document UUIDs are rejected by this
  specific client (`invalid argument`), and the API exposes no endpoint to list valid ones, so the
  create-user request omits the consent blocks by default (`Upvest:TermsAndConditionsConsentId` /
  `Upvest:DataPrivacyConsentId` are empty) and the user is still accepted. Set them to your
  client's real consent-document ids in production.
- **Nationality** must be an EEA country (e.g. `DE`); non-EEA nationalities such as `GB` are
  rejected by Upvest onboarding.
- Optionally set `Upvest:BusinessId` if your operating model requires it on accounts / account
  groups.
