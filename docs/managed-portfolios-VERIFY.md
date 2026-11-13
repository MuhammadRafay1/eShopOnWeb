# Managed Investment Portfolios (Upvest) — verification guide

This capability is **additive** to eShopOnWeb. All endpoints live on **`src/PublicApi`**, are
JWT-authenticated, and are routed under `/api/portfolio…`. Upvest is reached over its signed
(v6 HTTP message signatures, ECDSA P‑521/SHA‑512) API; credentials come from **.NET user‑secrets**
(never the repo).

## 0. One‑time setup

Credentials are read from the `Upvest:` config section. Load them into user‑secrets for
`src/PublicApi` from the provided environment variables (values never touch the repo):

```bash
P=src/PublicApi/PublicApi.csproj
dotnet user-secrets set "Upvest:ClientId"            "$UPVEST_CLIENT_ID"            --project $P
dotnet user-secrets set "Upvest:ClientSecret"        "$UPVEST_CLIENT_SECRET"        --project $P
dotnet user-secrets set "Upvest:SigningKeyId"        "$UPVEST_SIGNING_KEY_ID"        --project $P
dotnet user-secrets set "Upvest:SigningKeyPath"      "$UPVEST_SIGNING_KEY_PATH"      --project $P
dotnet user-secrets set "Upvest:SigningKeyPassphrase" "$UPVEST_SIGNING_KEY_PASSPHRASE" --project $P
dotnet user-secrets set "Upvest:BaseUrl"             "$UPVEST_BASE_URL"             --project $P
```

Ensure the HTTPS dev cert is trusted: `dotnet dev-certs https --check` (add `--trust` if needed).

## 1. Build & run

The machine has only the .NET 10 SDK (global.json pins 8.0.x) and no SQL LocalDB, so roll forward
and use the in‑memory store:

```bash
export DOTNET_ROLL_FORWARD=Major
dotnet build src/PublicApi/PublicApi.csproj
UseOnlyInMemoryDatabase=true ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/PublicApi
```

PublicApi listens on `https://localhost:42043` (and `http://localhost:42044`).
Swagger UI: `https://localhost:42043/swagger`.

> In‑memory note: data lives only for the run, and Web/PublicApi have separate stores — drive
> everything through PublicApi.

## 2. Get bearer tokens (storefront cookies don't work here)

```bash
BASE=https://localhost:42043
ADMIN=$(curl -sk -X POST $BASE/api/authenticate -H 'Content-Type: application/json' \
  -d '{"username":"admin@microsoft.com","password":"Pass@word1"}' | jq -r .token)      # operator
USER=$(curl -sk -X POST $BASE/api/authenticate -H 'Content-Type: application/json' \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | jq -r .token)   # shopper
```

## 3. Walk the flows

```bash
# Flow 2 (operator): create/replace a model. Admin-only (a shopper token returns 403).
curl -sk -X PUT $BASE/api/portfolio/models/balanced -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Balanced","funds":[{"instrumentId":"IE00B0M62Q58","weight":60},
                                   {"instrumentId":"US0378331005","weight":40}]}'
curl -sk $BASE/api/portfolio/models -H "Authorization: Bearer $USER"          # modelId,name,funds

# Flow 1 (shopper): sign up as an investor, then check status.
curl -sk -X POST $BASE/api/portfolio/investor -H "Authorization: Bearer $USER" \
  -H 'Content-Type: application/json' -d '{
   "firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","birthDate":"1990-03-15",
   "birthCity":"London","birthCountry":"GB","nationality":"DE",
   "address":{"line1":"1 Analytical Way","postcode":"10115","city":"Berlin","country":"DE"},
   "phoneNumber":"+49 151 12345678","taxId":"12345678901","taxCountry":"DE",
   "identityDocument":{"type":"PASSPORT","number":"X1234567","issuingCountry":"DE","expiryDate":"2030-01-01"}}'
curl -sk $BASE/api/portfolio/investor -H "Authorization: Bearer $USER"        # investorId,status

# Flow 2 (shopper): choose a model.
curl -sk -X PUT $BASE/api/portfolio/investor/model -H "Authorization: Bearer $USER" \
  -H 'Content-Type: application/json' -d '{"modelId":"balanced"}'

# Flow 3 (shopper): pay in, list payments.
curl -sk -X POST $BASE/api/portfolio/payments -H "Authorization: Bearer $USER" \
  -H 'Content-Type: application/json' -d '{"amount":100.00,"currency":"EUR"}'
curl -sk $BASE/api/portfolio/payments -H "Authorization: Bearer $USER"        # paymentId,amount,status

# Flow 4 (operator): realign followers of a model, then check it.
curl -sk -X POST $BASE/api/portfolio/models/balanced/realignments -H "Authorization: Bearer $ADMIN"
curl -sk $BASE/api/portfolio/realignments/1 -H "Authorization: Bearer $USER" # realignmentId,status

# Flow 5 (shopper): the portfolio.
curl -sk $BASE/api/portfolio -H "Authorization: Bearer $USER"  # modelId,holdings,totalValue,paidIn
```

## 4. What you will see in THIS sandbox (and why)

The sandbox client is authorised **only for user operations** (`users:admin`). Every other scope
(`checks`, `accounts`, `portfolios`, `topups`, `valuations`, …) is refused by Upvest with
`401 "This client is not authorised to use these scopes"`. This is the environment, not the code.
Each flow is implemented in full and calls the real Upvest API; where a scope is unavailable the
endpoint stays correct and explains itself in a `detail` field. Concretely:

| Call | Result here | Why |
|---|---|---|
| `POST /api/portfolio/investor` | `200`, `status: pending`, real `investorId` | **`POST /users` really runs at Upvest.** Checks/account‑group/account need other scopes → noted in `detail`; the shopper stays `pending` (user is `INACTIVE` until checks pass). |
| `GET /api/portfolio/investor` | `status: pending` | `GET /users/{id}` needs `users:read` (not granted) → last Upvest‑derived status kept. |
| `PUT /api/portfolio/models/{id}` | `200`, model saved | Stored locally; the Upvest allocation mirror needs `portfolios:admin` → noted in `detail`. |
| `GET /api/portfolio/models` | `200`, full list | Local. |
| `PUT /api/portfolio/investor/model` | `200` | Selection stored; applied at Upvest once the account & allocation exist. |
| `POST /api/portfolio/payments` | `409` | Correctly **refused** — a pay‑in requires an *active* investor with a model chosen; the sandbox investor can't reach `active`. |
| `POST /api/portfolio/models/{id}/realignments` | `200`, `status: pending` | Recorded; the Upvest rebalancing needs `portfolios:admin`. |
| `GET /api/portfolio` | `200`, empty holdings, `totalValue`/`paidIn` as `0.00` | Valuation needs `valuations:read`; `paidIn` is summed locally. |

### Behaviours worth checking explicitly
- **Operator‑only:** a shopper token on `PUT /models/{id}` or the realignment `POST` → `403`;
  unauthenticated → `401`.
- **Per‑shopper isolation:** authenticate as a *different* user and `GET /api/portfolio/investor`
  → `404` (you never see another shopper's investor/payments/portfolio).
- **Validation:** model weights not summing to exactly 100 → `400`; pay‑in `amount ≤ 0` → `400`;
  unknown model on selection or unknown realignment id → `404`.
- **Money:** every amount is a JSON number with two decimals (e.g. `0.00`).
- **Privacy:** personal details and secrets never appear in logs.

## Where the code lives
- Domain: `src/ApplicationCore/Entities/PortfolioAggregate/*`, `…/Interfaces/IUpvestService.cs`,
  `…/Specifications/*`.
- Upvest client (auth, v6 signing, retries, scope handling): `src/Infrastructure/Upvest/*`.
- Persistence: `src/Infrastructure/Data/Config/*` + DbSets in `CatalogContext`.
- HTTP endpoints: `src/PublicApi/PortfolioEndpoints/*`; DI in
  `src/PublicApi/Configuration/UpvestServiceCollectionExtensions.cs`.
