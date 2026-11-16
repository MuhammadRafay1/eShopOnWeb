# Managed Investment Portfolios (Upvest)

An **additive** capability on top of eShopOnWeb: a signed-in shopper can become an investor, follow
an operator-defined model portfolio, pay money in, and see what their portfolio holds — with
**Upvest** (Take-Our-License model) as the investment provider. The existing catalog / basket /
order flow is untouched.

All capabilities are exposed as JWT-authenticated HTTP endpoints on **`src/PublicApi`**, under
`/api/portfolio`.

## Endpoints

| Method & route | Who | Purpose |
|---|---|---|
| `POST /api/portfolio/investor` | shopper | Sign up as an investor → `{ investorId, status }` |
| `GET  /api/portfolio/investor` | shopper | Where the sign-up has got to → `{ investorId, status }` |
| `PUT  /api/portfolio/models/{modelId}` | **operator** | Create/replace a model portfolio → `{ modelId, name, funds }` |
| `GET  /api/portfolio/models` | shopper | All model portfolios → `{ models: [{ modelId, name, funds }] }` |
| `PUT  /api/portfolio/investor/model` | shopper | Choose which model to follow (`{ modelId }`) → `204` |
| `POST /api/portfolio/payments` | shopper | Pay an amount in (`{ amount }`) → `{ paymentId, status }` |
| `GET  /api/portfolio/payments` | shopper | Payments, newest first → `{ payments: [{ paymentId, amount, status }] }` |
| `POST /api/portfolio/models/{modelId}/realignments` | **operator** | Realign all portfolios following a model → `{ realignmentId, status }` |
| `GET  /api/portfolio/realignments/{realignmentId}` | shopper | Where a realignment has got to → `{ realignmentId, status }` |
| `GET  /api/portfolio` | shopper | The caller's portfolio → `{ modelId, holdings:[{instrumentId,quantity,value}], totalValue, paidIn }` |

Statuses: investor `pending`/`active`/`rejected`; payment `pending`/`invested`/`failed`;
realignment `pending`/`completed`/`failed`. Operator endpoints require the existing
`Administrators` role. Money amounts are JSON numbers in euros with two decimal places.

## How the flows map to Upvest

| Flow | Upvest calls |
|---|---|
| Become an investor | `POST /users` → `POST /users/{id}/checks` (KYC + INSTRUMENT_FIT) → `POST /users/{id}/tax_residencies` → `POST /account_groups` (PERSONAL) → `POST /accounts` (PORTFOLIO). Status polled from `GET /accounts/{id}`. |
| Model portfolios | `POST`/`PUT /portfolios/allocations` (weights as fractions summing to 1.0); listed via local store. Choosing a model = `POST`/`PATCH /portfolios/configurations/{account_id}`. |
| Pay in | `POST /virtual_cash_balances/increases` (sandbox credit) → `POST /portfolios/orders` (BUY, distributed by allocation weights). Status polled from `GET /portfolios/orders/{id}`. |
| Realignment | `POST /portfolios/rebalancings/executions` (by allocation). Status derived from `GET /portfolios/rebalancings/executions/{id}/orders`. |
| Portfolio view | `GET /accounts/{id}/valuations/current`; `paidIn` is summed from the shopper's payments. |

Every reported status reflects Upvest's actual state, learned through this app's own calls (no
inbound webhook route is exposed).

## Security & privacy

- **HTTP Message Signatures (v15 / RFC 9421)** on every call: ECDSA P-521 + SHA-512, DER-encoded,
  with `content-digest` (SHA-512), scoped OAuth2 bearer tokens, `idempotency-key` on writes.
  The encrypted EC key (legacy OpenSSL PEM) is decrypted in memory at first use.
- **Credentials** are bound from the `Upvest:` configuration section (`ClientId`, `ClientSecret`,
  `SigningKeyId`, `SigningKeyPath`, `SigningKeyPassphrase`, `BaseUrl`) and loaded from
  **.NET user-secrets** — never committed. The client secret and key passphrase are never logged
  or returned.
- **Per-shopper isolation**: every per-shopper record is keyed by the caller's token identity; one
  shopper can never see another's investor record, payments or portfolio.
- **No personal data is persisted or logged**: sign-up details are forwarded to Upvest only; the
  local store keeps just Upvest id references and operator model definitions.

## Architecture

- `ApplicationCore/Portfolio` + `ApplicationCore/Interfaces` — domain models and the
  `IPortfolioService`, `IUpvestClient`, `IPortfolioStore` abstractions.
- `Infrastructure/Upvest` — `UpvestMessageSigner`, `UpvestTokenProvider` (scoped-token cache),
  `UpvestClient` (signed, retried calls), options and error mapping.
- `Infrastructure/Portfolio` — `PortfolioService` (orchestration + status derivation) and the
  singleton `InMemoryPortfolioStore`.
- `PublicApi/PortfolioEndpoints` — the HTTP endpoints following the project's `IEndpoint` pattern.

## Sandbox note

The provided sandbox client is authorised only for **user** operations (`users:admin`). So
`POST /api/portfolio/investor` creates a real user at Upvest (verifiable), while calls needing other
scopes (checks, accounts, portfolios, valuations, payments) return `401
"not authorised to use these scopes"`. The integration handles this gracefully — sign-ups report
`pending`, model definitions are stored locally, pay-ins are refused until the account is active —
and would run end to end against a fully-scoped client with no code changes.

## Verify it yourself

See the step-by-step guide in the project hand-off notes, or:

1. `dotnet user-secrets` for `src/PublicApi` already holds the `Upvest:*` values.
2. Run: `DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development UseOnlyInMemoryDatabase=true dotnet run --project src/PublicApi`
3. Get a token from `POST /api/authenticate` (`admin@microsoft.com` / `Pass@word1` for operator
   actions, `demouser@microsoft.com` / `Pass@word1` for shopper actions), then call the endpoints
   above with `Authorization: Bearer <token>`. Use an EEA nationality (e.g. `DE`) for sign-up.
