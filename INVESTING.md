# Invest your change — Upvest integration

Additive capability letting an eShopOnWeb shopper invest their spare change through **Upvest**.
A shopper opts in; every paid order is rounded up to the next whole euro and the difference set
aside; once the set-aside balance reaches **€10** the whole balance is invested in the fund named
by `Upvest:InstrumentId`; the shopper can see their balance and investments. Everything is exposed
as JWT-authenticated HTTP endpoints on **`src/PublicApi`** and belongs to the signed-in shopper
alone.

## HTTP surface (PublicApi)

| Method & route | Purpose | Key response fields |
| --- | --- | --- |
| `POST /api/investing/enrolment` | Opt the signed-in shopper in (carries the sign-up form) | `enrolmentId`, `status` |
| `GET /api/investing/enrolment` | Where the caller's enrolment has got to | `enrolmentId`, `status` (`pending`/`active`/`rejected`) |
| `POST /api/orders` | Place an order from catalog item ids + quantities; treated as paid | `orderId`, `roundUpAmount` |
| `GET /api/investing/investments` | The caller's investments, newest first | `investmentId`, `amount`, `status` (`pending`/`settled`/`failed`) |
| `GET /api/investing/balance` | Set-aside and invested totals | `pendingAmount`, `investedAmount` |
| `POST /api/investing/upvest-webhook` | The only route Upvest calls — no shopper token; HTTP-message-signature verified | — |

All money amounts are JSON numbers with two decimal places (euros).

## How it maps onto Upvest

Discovered entirely through the **upvest-docs** MCP server. The shopper's identity (the JWT
`name`) is the key tying together their enrolment, ledger and investments; personal data is sent
to Upvest but never written to logs.

- **Authentication** — every call to Upvest goes through one reusable `DelegatingHandler`
  (`UpvestAuthenticationHandler`): it obtains/caches an OAuth2 client-credentials token and signs
  each request with **Upvest HTTP Message Signatures v15** (ECDSA P-521 / SHA-512). The token
  request is itself signed by the same handler (it carries a signature but no bearer). No call
  site attaches credentials.
- **Enrolment** — `POST /users` (the investor) → `POST /users/{id}/tax_residencies` → `POST
  /users/{id}/checks` (KYC). Acceptance is the KYC check reaching `PASSED` (→ `active`) or `FAILED`
  (→ `rejected`); until then `pending`.
- **Account** — once accepted, `POST /account_groups` then `POST /accounts` (TRADING). The account
  activates a short time later; investing waits for that.
- **Investing** — fund the account (`POST /virtual_cash_balances/increases`) with the set-aside
  amount, then `POST /orders` (BUY the instrument by cash amount).
- **Settlement** — the order's status at Upvest (`FILLED`/`CANCELLED`) becomes the investment's
  `settled`/`failed`, observed both via the webhook and by polling.

A background `InvestmentReconciliationService` advances enrolments, provisions accounts, places the
investment once the balance crosses €10, and settles investments. It is the authoritative path;
the signed webhook does the same work sooner when it arrives. All investor writes are serialised
through a single-writer gate so the in-memory provider never loses an update, and placing an order
never calls Upvest, so an order can never fail because of investing.

## Configuration & secrets

Bound from the `Upvest:` configuration section: `ClientId`, `ClientSecret`, `SigningKeyId`,
`SigningKeyPath`, `SigningKeyPassphrase`, `BaseUrl`, `InstrumentId`, `CallbackBaseUrl`. The values
are loaded into **.NET user-secrets** (for the PublicApi project) from the `UPVEST_*` environment
variables and are also mapped from those variables at startup; no value is written into any file
in the repository. `Upvest:BaseUrl` is used verbatim for every Upvest call. The client secret and
signing-key passphrase are never logged or returned.

## Running / verifying (this machine)

The SDK is pinned to 8.0.x but only .NET 10 is installed, so `global.json` rolls forward and the
host runs with `DOTNET_ROLL_FORWARD=Major`; the in-memory database is used (`UseOnlyInMemoryDatabase=true`).
See the step-by-step guide below (or the final hand-off message).
