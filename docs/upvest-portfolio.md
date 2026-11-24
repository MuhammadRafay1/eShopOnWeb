# Managed investment portfolios (Upvest integration)

This capability lets an eShopOnWeb shopper hold a **managed investment portfolio** with **Upvest**
as the investment provider. It is additive — it does not touch the catalog/basket/order flow — and
is exposed entirely as JWT-authenticated HTTP endpoints on the **`src/PublicApi`** project, routed
under `/api/portfolio`.

Operating model: **Take Our License (TOL), single accounts**. All Upvest interaction is driven from
Upvest's public documentation at <https://docs.upvest.co>.

## Endpoints

| Flow | Method & route | Who | Maps to Upvest |
|------|----------------|-----|----------------|
| 1 | `POST /api/portfolio/investor` | shopper | `POST /users`, `POST /account_groups`, `POST /accounts`, `POST /users/{id}/checks`, `/identifiers`, `/tax_residencies` |
| 1 | `GET /api/portfolio/investor` | shopper | `GET /users/{id}`, `GET /accounts/{id}` |
| 2 | `PUT /api/portfolio/models/{modelId}` | **admin** | `POST`/`PUT /portfolios/allocations` |
| 2 | `GET /api/portfolio/models` | shopper | (shop store; mirrors allocations) |
| 2 | `PUT /api/portfolio/investor/model` | shopper | `POST`/`PATCH /portfolios/configurations` |
| 3 | `POST /api/portfolio/payments` | shopper | `POST /virtual_cash_balances/increases` + `POST /portfolios/orders` |
| 3 | `GET /api/portfolio/payments` | shopper | `GET /portfolios/orders/{id}` |
| 4 | `POST /api/portfolio/models/{modelId}/realignments` | **admin** | `POST /portfolios/rebalancings/executions` |
| 4 | `GET /api/portfolio/realignments/{realignmentId}` | shopper | `GET .../executions/{id}/orders` |
| 5 | `GET /api/portfolio` | shopper | `GET /accounts/{id}/valuations/current` |

Statuses always reflect what actually happened at Upvest, read back on demand (this app exposes no
callback route for Upvest): sign-up `pending`/`active`/`rejected`; payment `pending`/`invested`/`failed`;
realignment `pending`/`completed`/`failed`. Every shopper only ever sees their own records (scoped by
the JWT identity). Personal details are sent to Upvest but never persisted or logged by the shop.

## Design

- **ApplicationCore** — domain entities (`Investor`, `ModelPortfolio`/`ModelFund`, `PortfolioPayment`,
  `PortfolioRealignment`), the `IUpvestGateway` port, and `PortfolioService` which orchestrates the
  five flows and maps Upvest statuses.
- **Infrastructure/Upvest** — `UpvestGateway` (the HTTP client), `UpvestSigningHandler`
  (RFC 9421 request signing), `UpvestTokenService` (scoped OAuth tokens), `UpvestSigningKey`
  (loads/decrypts the signing key). Entities persist via the existing EF `CatalogContext` + generic
  repository.
- **PublicApi/PortfolioEndpoints** — one endpoint class per action, following the project's
  `IEndpoint` convention.

### Authentication & request signing

Every Upvest call carries an OAuth2 client-credentials bearer token (scoped per operation) and an
HTTP Message Signature (**Upvest v15 / RFC 9421**): ECDSA **P-521 / SHA-512**, DER-encoded, with a
`content-digest` (SHA-512) on bodies and the `upvest-signature-version: 15` header. The signing key is
read from the configured PEM (legacy OpenSSL-encrypted PEM is supported).

### Configuration & secrets

Settings bind from the `Upvest:` section with exactly these keys: `Upvest:ClientId`,
`Upvest:ClientSecret`, `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:SigningKeyPassphrase`,
`Upvest:BaseUrl`. **No values are stored in the repo** — load them into .NET user-secrets:

```bash
P=src/PublicApi/PublicApi.csproj
dotnet user-secrets --project $P set "Upvest:ClientId"            "$UPVEST_CLIENT_ID"
dotnet user-secrets --project $P set "Upvest:ClientSecret"        "$UPVEST_CLIENT_SECRET"
dotnet user-secrets --project $P set "Upvest:SigningKeyId"        "$UPVEST_SIGNING_KEY_ID"
dotnet user-secrets --project $P set "Upvest:SigningKeyPath"      "$UPVEST_SIGNING_KEY_PATH"
dotnet user-secrets --project $P set "Upvest:SigningKeyPassphrase" "$UPVEST_SIGNING_KEY_PASSPHRASE"
dotnet user-secrets --project $P set "Upvest:BaseUrl"            "$UPVEST_BASE_URL"
```

## Sandbox scope limitation

The sandbox client supplied for this environment is authorised **only for `users:admin`**. Any call
needing another scope is refused by Upvest at token issuance with `401 "This client is not authorised
to use these scopes"`. This is the environment, not a bug: the integration builds and drives every
flow in full, but at runtime only investor creation / status read reach Upvest; account provisioning,
allocations, orders, rebalancing and valuations return an upstream error (logged; surfaced as `502`).
Flows that depend on those steps therefore refuse cleanly (e.g. a pay-in before the account is active
returns `400`).

## Run & verify

```bash
export DOTNET_ROLL_FORWARD=Major
export ASPNETCORE_ENVIRONMENT=Development
export UseOnlyInMemoryDatabase=true
export ASPNETCORE_URLS="https://localhost:42343;http://localhost:42344"

dotnet dev-certs https --check            # ensure the dev cert is trusted
dotnet run --project src/PublicApi/PublicApi.csproj
```

Then, against `https://localhost:42343` (use `curl -k` for the dev cert):

1. Get a bearer token (the storefront cookie won't work here):
   ```bash
   curl -sk -X POST https://localhost:42343/api/authenticate \
     -H 'Content-Type: application/json' \
     -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}'
   # admin token: username admin@microsoft.com / Pass@word1
   ```
2. `POST /api/portfolio/investor` with the sign-up form (a real Upvest user is created) → `201 {investorId, status:"pending"}`.
3. `GET /api/portfolio/investor` → the sign-up status.
4. As **admin**, `PUT /api/portfolio/models/{modelId}` with `{name, funds:[{instrumentId, weight}]}` (weights sum to 100) → `200`. A non-admin token → `403`; weights ≠ 100 → `400`.
5. `GET /api/portfolio/models`; then `PUT /api/portfolio/investor/model {"modelId":"..."}` → `204`.
6. `POST /api/portfolio/payments {"amount":250}`; `GET /api/portfolio/payments`.
7. As **admin**, `POST /api/portfolio/models/{modelId}/realignments`; `GET /api/portfolio/realignments/{id}`.
8. `GET /api/portfolio` → `{modelId, holdings, totalValue, paidIn}`.

In this sandbox (scope limited to `users:admin`) steps 2–3 reach Upvest end-to-end; steps that need
account/portfolio scopes return `502`/refuse cleanly, as described above.

