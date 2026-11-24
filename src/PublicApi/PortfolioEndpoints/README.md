# Managed investment portfolios (Upvest integration)

This feature lets eShopOnWeb shoppers hold a **managed investment portfolio** with **Upvest** as
the investment provider. It is additive — it does not touch the existing catalog/basket/order
flow. All capabilities are exposed as JWT-authenticated HTTP endpoints on the **PublicApi**
project under `/api/portfolio`.

## How it maps to Upvest

Upvest's **Take Our License (single accounts)** model is used. The flows map to Upvest as:

| Shop concept | Upvest concept |
| --- | --- |
| Investor sign-up | `POST /users` + tax residencies + KYC check + account group + portfolio account |
| Model portfolio | portfolio **allocation** (`/portfolios/allocations`) |
| Investor follows a model | portfolio **configuration** (`/portfolios/configurations`) |
| Pay in | sandbox credit transfer (`/tests/payments/transactions`) + portfolio **order** (`/portfolios/orders`) |
| Realignment | **rebalancing execution** (`/portfolios/rebalancings/executions`) |
| Portfolio view | account **valuation** (`/accounts/{id}/valuations/current`) |

Every reported status (sign-up, payment, realignment) is derived from what Upvest reports; the app
learns outcomes only by calling Upvest (there is no inbound webhook route). Personal details are
never written to logs.

All calls are signed with **HTTP Message Signatures v15** (ECDSA P-521 / SHA-512, DER, standard
Base64) and carry a minimal-scope OAuth client-credentials token.

## Endpoints

| Method | Route | Who |
| --- | --- | --- |
| POST | `/api/portfolio/investor` | any signed-in shopper |
| GET | `/api/portfolio/investor` | any signed-in shopper |
| PUT | `/api/portfolio/models/{modelId}` | **administrator** |
| GET | `/api/portfolio/models` | any signed-in shopper |
| PUT | `/api/portfolio/investor/model` | any signed-in shopper |
| POST | `/api/portfolio/payments` | any signed-in shopper |
| GET | `/api/portfolio/payments` | any signed-in shopper |
| POST | `/api/portfolio/models/{modelId}/realignments` | **administrator** |
| GET | `/api/portfolio/realignments/{realignmentId}` | any signed-in shopper |
| GET | `/api/portfolio` | any signed-in shopper |

## Configuration

Settings bind from the `Upvest:` configuration section (`ClientId`, `ClientSecret`,
`SigningKeyId`, `SigningKeyPath`, `SigningKeyPassphrase`, `BaseUrl`). **Secrets never go into the
repository** — load them into .NET user-secrets:

```bash
cd src/PublicApi
dotnet user-secrets set "Upvest:ClientId"            "$UPVEST_CLIENT_ID"
dotnet user-secrets set "Upvest:ClientSecret"        "$UPVEST_CLIENT_SECRET"
dotnet user-secrets set "Upvest:SigningKeyId"        "$UPVEST_SIGNING_KEY_ID"
dotnet user-secrets set "Upvest:SigningKeyPath"      "$UPVEST_SIGNING_KEY_PATH"
dotnet user-secrets set "Upvest:SigningKeyPassphrase" "$UPVEST_SIGNING_KEY_PASSPHRASE"
dotnet user-secrets set "Upvest:BaseUrl"             "$UPVEST_BASE_URL"
```

## Running

```bash
export DOTNET_ROLL_FORWARD=Major
ASPNETCORE_ENVIRONMENT=Development UseOnlyInMemoryDatabase=true \
  dotnet run --project src/PublicApi/PublicApi.csproj
```

## Sandbox note

The sandbox client is authorised only for **user** operations. Calls needing other scopes
(accounts, portfolios, payments, tests, …) are refused by Upvest with `401 "This client is not
authorised to use these scopes"`. The code handles this gracefully: user creation succeeds (so an
investor record is created with status `pending`), and operations that need other scopes degrade
cleanly (logged without PII). This is an environment limit, not a code defect — every flow is
implemented in full. See `src/PublicApi/VERIFY-UPVEST.md` for a step-by-step verification guide.
