# Invest your change (Upvest integration)

An additive capability on the PublicApi: an enrolled shopper has every paid order rounded up to the
next whole euro, the difference set aside, and once the set-aside balance reaches **€10** the whole
balance is invested on their behalf in the fund configured as `Upvest:InstrumentId`.

## Endpoints (JWT-authenticated; caller identity comes from the token)

| Method & route | Purpose | Key response fields |
| --- | --- | --- |
| `POST /api/investing/enrolment` | Opt the shopper in (onboards them as an Upvest investor). | `enrolmentId`, `status` (`pending`→`active`/`rejected`) |
| `GET /api/investing/enrolment` | Where the caller's enrolment has got to. | `enrolmentId`, `status` |
| `POST /api/orders` | Place an order from catalog items; treated as paid. | `orderId`, `roundUpAmount` |
| `GET /api/investing/balance` | Set-aside and invested totals. | `pendingAmount`, `investedAmount` |
| `GET /api/investing/investments` | The caller's investments, newest first. | `investmentId`, `amount`, `status` (`pending`→`settled`/`failed`) |
| `POST /api/investing/upvest/webhook` | Receives Upvest event callbacks (the only anonymous route). | — |

All money amounts are JSON numbers in euros with two decimal places.

## How it works

- **Enrolment** (`InvestingService.EnrolAsync`) creates the Upvest user, submits the KYC and
  INSTRUMENT_FIT checks and tax residency, then stores a pending `Investor`. The user activates
  asynchronously at Upvest.
- **Reconciliation** (`InvestingReconciliationService`, a hosted worker) drives everything async:
  it activates enrolments once Upvest accepts the user (creating the trading account group and
  account), places the buy order for queued investments (funding the account first), and settles
  investments by reflecting the order's actual outcome at Upvest. Webhooks update the same state
  for immediacy; the poller guarantees correctness regardless.
- **Set-aside** happens when an order is placed (`OrderPlacementService`): the order is created with
  the app's existing `Order`/`OrderItem` model, treated as paid, and the round-up is set aside for
  accepted investors only. Placing an order never fails for any investing reason.

## Talking to Upvest

Every call to Upvest flows through one reusable `DelegatingHandler`
(`UpvestAuthenticationHandler`): it signs the request (v15 HTTP Message Signatures — ECDSA P-521 /
SHA-512, SHA-512 `content-digest`) and attaches a cached OAuth2 bearer token (fetching a new,
itself-signed token when needed). No call site attaches credentials.

## Configuration (`Upvest:` section — load via user-secrets / environment, never commit values)

`ClientId`, `ClientSecret`, `SigningKeyId`, `SigningKeyPath`, `SigningKeyPassphrase`, `BaseUrl`,
`InstrumentId`, `CallbackBaseUrl`. `ClientSecret` and `SigningKeyPassphrase` are secrets and are
never logged or returned.
