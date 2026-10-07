# Invest your change — Upvest integration

Shoppers can opt in to investing their spare change. Every order an enrolled shopper pays for is
rounded up to the next whole euro; the difference is set aside, and once the set-aside balance
reaches **€10** it is invested on their behalf in the fund configured as `Upvest:InstrumentId`.
It is an additive capability on top of the existing catalog/basket/order flow.

Everything is driven through the **PublicApi** project (JWT-authenticated; the caller is taken from
the token). All money amounts are euros with two decimal places.

## Endpoints

| Method & route | Purpose | Response fields |
| --- | --- | --- |
| `POST /api/orders` | Place an order from catalog items (reuses the existing order model); for an enrolled shopper, sets aside the round-up. | `orderId`, `roundUpAmount` |
| `POST /api/investing/enrolment` | Opt in — becomes an investor with Upvest. Body = sign-up form. | `enrolmentId`, `status` |
| `GET /api/investing/enrolment` | Where the caller's enrolment has got to. | `enrolmentId`, `status` (`pending`/`active`/`rejected`) |
| `GET /api/investing/balance` | Set-aside and invested totals. | `pendingAmount`, `investedAmount` |
| `GET /api/investing/investments` | The caller's investments, newest first. | `investmentId`, `amount`, `status` (`pending`/`settled`/`failed`) |
| `POST /api/investing/upvest-webhook` | The route Upvest calls back (no shopper token). | — |

## Configuration (secrets stay out of the repo)

Settings bind from the `Upvest:` section — `ClientId`, `ClientSecret`, `SigningKeyId`,
`SigningKeyPath`, `SigningKeyPassphrase`, `BaseUrl`, `InstrumentId`, `CallbackBaseUrl`. Load them
into **.NET user-secrets** for the PublicApi project from the environment (values never go into any
file in the repo):

```bash
cd src/PublicApi
dotnet user-secrets set "Upvest:ClientId"            "$UPVEST_CLIENT_ID"
dotnet user-secrets set "Upvest:ClientSecret"        "$UPVEST_CLIENT_SECRET"
dotnet user-secrets set "Upvest:SigningKeyId"        "$UPVEST_SIGNING_KEY_ID"
dotnet user-secrets set "Upvest:SigningKeyPath"      "$UPVEST_SIGNING_KEY_PATH"
dotnet user-secrets set "Upvest:SigningKeyPassphrase" "$UPVEST_SIGNING_KEY_PASSPHRASE"
dotnet user-secrets set "Upvest:BaseUrl"             "$UPVEST_BASE_URL"
dotnet user-secrets set "Upvest:InstrumentId"        "$UPVEST_INSTRUMENT_ID"
dotnet user-secrets set "Upvest:CallbackBaseUrl"     "$UPVEST_CALLBACK_BASE_URL"
```

## Run

This machine has only the .NET 10 SDK and no SQL LocalDB, so roll forward and use the in-memory
store (data lives for one run):

```bash
export DOTNET_ROLL_FORWARD=Major
export UseOnlyInMemoryDatabase=true
dotnet dev-certs https --check        # ensure the HTTPS dev cert exists
dotnet run --project src/PublicApi/PublicApi.csproj \
  --no-launch-profile
# listens on https://localhost:36923 and http://localhost:36924 (ASPNETCORE_URLS or the PublicApi launch profile)
```

## Verify end to end (curl)

```bash
B=https://localhost:36923

# 1) Get a bearer token (the storefront cookie does not work here)
TOKEN=$(curl -sk -X POST $B/api/authenticate -H 'Content-Type: application/json' \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
AUTH="Authorization: Bearer $TOKEN"

# 2) An order before enrolling sets aside nothing (roundUpAmount = 0)
curl -sk -X POST $B/api/orders -H "$AUTH" -H 'Content-Type: application/json' \
  -d '{"items":[{"catalogItemId":5,"quantity":1}]}'

# 3) Opt in (made-up details; Upvest sandbox)
curl -sk -X POST $B/api/investing/enrolment -H "$AUTH" -H 'Content-Type: application/json' -d '{
  "firstName":"Grace","lastName":"Hopper","email":"grace@example.com","birthDate":"1985-03-12",
  "nationality":"DE","address":{"line1":"Rosenweg 221","postcode":"45678","city":"Berlin","country":"DE"},
  "phoneNumber":"+491234567890","taxId":"12345678901","taxCountry":"DE"}'

# 4) Poll until Upvest accepts the shopper (pending -> active, ~8s)
curl -sk $B/api/investing/enrolment -H "$AUTH"

# 5) Place 20 orders of item 5 (€8.50 -> €0.50 round-up each). The 20th crosses €10 and invests.
for i in $(seq 1 20); do
  curl -sk -X POST $B/api/orders -H "$AUTH" -H 'Content-Type: application/json' \
    -d '{"items":[{"catalogItemId":5,"quantity":1}]}' >/dev/null; done

# 6) Balance: pendingAmount back to 0, investedAmount 10.00
curl -sk $B/api/investing/balance -H "$AUTH"

# 7) Investment: amount 10.00, status pending -> settled (poll a few times, ~10s)
curl -sk $B/api/investing/investments -H "$AUTH"
```

Expected: step 2 → `roundUpAmount: 0`; step 4 eventually `status: "active"`; step 6 →
`{"pendingAmount":0,"investedAmount":10.00}`; step 7 → one investment `amount 10.00` that becomes
`"settled"`. A different signed-in user sees none of this (enrolment `404`, investments empty).

## How it works (brief)

- Every Upvest call goes through one reusable `UpvestSigningHandler` (`Infrastructure/Investing`)
  that attaches the HTTP message signature and routes to `Upvest:BaseUrl`; the OAuth token is
  fetched by the SDK through the same handler. No call site attaches credentials.
- Enrolment creates an Upvest user (with tax residency and a KYC check), then provisions a trading
  account; the shopper is `active` once Upvest has accepted them and the account can hold
  investments.
- Each paid order's round-up is set aside on the shopper's `Investor` record. When the balance
  reaches €10 the whole balance funds and places a BUY order for the configured fund; the balance
  resets to zero.
- Each investment's status mirrors its Upvest order (FILLED → settled, CANCELLED → failed),
  reconciled when the balance/investments endpoints are read and via the webhook callback.
