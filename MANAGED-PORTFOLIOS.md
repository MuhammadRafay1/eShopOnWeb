# Managed investment portfolios (Upvest integration)

An **additive** capability on top of eShopOnWeb: shoppers can hold a managed investment portfolio
with **Upvest** as the investment provider. It does not touch the existing catalog/basket/order flow.

All capabilities are HTTP endpoints on **`src/PublicApi`** (JWT-authenticated), routed under
`/api/portfolio`. The caller's identity comes from the JWT; a shopper only ever sees their own
investor record, payments and portfolio.

## Endpoints

| Method & route | Who | Purpose |
|---|---|---|
| `POST /api/portfolio/investor` | shopper | Sign the shopper up as an investor at Upvest |
| `GET /api/portfolio/investor` | shopper | Where the sign-up has got to (`investorId`, `status`) |
| `PUT /api/portfolio/models/{modelId}` | **operator** | Create/replace a model portfolio (`name`, `funds`) |
| `GET /api/portfolio/models` | shopper | Every model portfolio and its funds |
| `PUT /api/portfolio/investor/model` | shopper | Choose which model the investor follows (`modelId`) |
| `POST /api/portfolio/payments` | shopper | Pay an amount in (`amount`, euros) |
| `GET /api/portfolio/payments` | shopper | The caller's payments, newest first |
| `POST /api/portfolio/models/{modelId}/realignments` | **operator** | Realign every portfolio following the model |
| `GET /api/portfolio/realignments/{realignmentId}` | shopper | Where a realignment has got to |
| `GET /api/portfolio` | shopper | The caller's portfolio (model, holdings, total value, paid in) |

**Status values** (always reflect what actually happened at Upvest, learned through this app's own
calls — this app exposes no webhook route):
- investor `status`: `pending` → `active` / `rejected`
- payment `status`: `pending` → `invested` / `failed`
- realignment `status`: `pending` → `completed` / `failed`

All money amounts are JSON numbers in euros with two decimal places.

## How it maps to Upvest

| Flow | Upvest calls (Investment API) |
|---|---|
| Sign-up | `POST /users` → `POST /users/{id}/checks` (KYC) → `POST /users/{id}/tax_residencies` → (`POST /users/{id}/identifiers` for non-CONCAT nationalities) → `POST /account_groups` (PERSONAL) → `POST /accounts` (PORTFOLIO). Status from `GET /users/{id}` + `GET /accounts/{id}`. |
| Model | `POST`/`PUT /portfolios/allocations` (weights as fractions summing to 1.0) |
| Choose model | `POST`/`PATCH /portfolios/configurations` (link account ↔ allocation) |
| Pay in | `POST /payments/topups` (credit cash) → `POST /portfolios/orders` (BUY, invested across the allocation). Status from `GET /portfolios/orders/{id}`. |
| Realignment | `POST /portfolios/rebalancings/executions` (by allocation). Status from `GET /portfolios/rebalancings/executions/{id}/orders`. |
| Portfolio | `GET /accounts/{id}/valuations/current` + locally-tracked paid-in total |

Every Upvest request is authenticated with a least-privilege, scoped OAuth2 client-credentials
token and signed with an **HTTP message signature (Upvest v6)**: an ECDSA P-521 / SHA-512
signature over the canonical component set, plus a `digest: SHA-256=…` body header. The signing
key, client secret and passphrase come from configuration and are never logged.

## Configuration

Bound from the `Upvest:` configuration section (loaded via .NET user-secrets; values never stored
in the repo):

```
Upvest:ClientId, Upvest:ClientSecret, Upvest:SigningKeyId,
Upvest:SigningKeyPath, Upvest:SigningKeyPassphrase, Upvest:BaseUrl
```

## Sandbox note

The sandbox client supplied here is authorised **only for user operations**. So, end to end:

- **Works against the live sandbox:** OAuth token + HTTP signing, and `POST /users` /
  `GET /users/{id}` — i.e. the investor is really created at Upvest and sign-up status is really read.
- **Refused with HTTP 401** ("This client is not authorised to use these scopes"): checks,
  accounts, portfolios, payments, valuations. This is the environment, not the code. Those flows are
  implemented in full; where the flow allows it the reported status simply stays `pending` (the
  honest outcome), and the `detail` field explains why. Because the account never becomes ready,
  paying in is correctly **refused** with `409`.

## Verify it yourself

Prerequisites: .NET SDK present, dev HTTPS cert trusted (`dotnet dev-certs https --trust`), and the
six `Upvest:*` user-secrets set on `src/PublicApi` (from the `UPVEST_*` environment variables).

1. **Run PublicApi** (in-memory DB; its own JWT ports):
   ```bash
   cd src/PublicApi
   DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development UseOnlyInMemoryDatabase=true \
     ASPNETCORE_URLS="https://localhost:41783;http://localhost:41784" \
     dotnet run
   ```

2. **Get bearer tokens** (storefront cookies won't work here):
   ```bash
   B=https://localhost:41783
   USER=$(curl -sk -X POST $B/api/authenticate -H 'Content-Type: application/json' \
     -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | jq -r .token)
   ADMIN=$(curl -sk -X POST $B/api/authenticate -H 'Content-Type: application/json' \
     -d '{"username":"admin@microsoft.com","password":"Pass@word1"}' | jq -r .token)
   ```

3. **Sign up as an investor** (use made-up, EEA details — the sandbox tenant requires an EEA
   nationality and a valid postcode/city):
   ```bash
   curl -sk -X POST $B/api/portfolio/investor -H "Authorization: Bearer $USER" \
     -H 'Content-Type: application/json' -d '{
       "firstName":"Alex","lastName":"Mustermann","email":"alex.muster@example.com",
       "birthDate":"1990-05-01","birthCity":"Berlin","birthCountry":"DE","nationality":"DE",
       "address":{"line1":"Rosenweg 221","postcode":"10115","city":"Berlin","country":"DE"},
       "phoneNumber":"+49 123 4567890","taxId":"12345678901","taxCountry":"DE",
       "identityDocument":{"type":"PASSPORT","number":"AB1234567","issuingCountry":"DE","expiryDate":"2030-01-01"}}'
   # → 201 {"investorId":"…","status":"pending", …}  (the user is created at Upvest)
   curl -sk $B/api/portfolio/investor -H "Authorization: Bearer $USER"
   ```

4. **Define a model (operator)** and list models:
   ```bash
   curl -sk -X PUT $B/api/portfolio/models/balanced -H "Authorization: Bearer $ADMIN" \
     -H 'Content-Type: application/json' \
     -d '{"name":"Balanced","funds":[{"instrumentId":"IE00B4L5Y983","weight":60},{"instrumentId":"IE00B3RBWM25","weight":40}]}'
   curl -sk $B/api/portfolio/models -H "Authorization: Bearer $USER"
   # A non-admin PUT returns 403; weights not summing to 100 return 400.
   ```

5. **Choose the model**, then observe pay-in is refused until the account is ready:
   ```bash
   curl -sk -X PUT $B/api/portfolio/investor/model -H "Authorization: Bearer $USER" \
     -H 'Content-Type: application/json' -d '{"modelId":"balanced"}'
   curl -sk -X POST $B/api/portfolio/payments -H "Authorization: Bearer $USER" \
     -H 'Content-Type: application/json' -d '{"amount":100.00}'    # → 409 (not active yet)
   curl -sk $B/api/portfolio/payments -H "Authorization: Bearer $USER"   # → []
   ```

6. **Realignment (operator)** and **portfolio**:
   ```bash
   RID=$(curl -sk -X POST $B/api/portfolio/models/balanced/realignments -H "Authorization: Bearer $ADMIN" | jq -r .realignmentId)
   curl -sk $B/api/portfolio/realignments/$RID -H "Authorization: Bearer $USER"
   curl -sk $B/api/portfolio -H "Authorization: Bearer $USER"
   # → {"modelId":"balanced","holdings":[],"totalValue":0.00,"paidIn":0.00}
   ```

Unit tests for the orchestration: `dotnet test tests/UnitTests/UnitTests.csproj --filter Portfolios`.
