# Invest your change — verify it end to end

This feature lets an eShopOnWeb shopper opt in to investing their spare change through **Upvest**. Every
paid order is rounded up to the next whole euro, the difference is set aside, and once the set-aside balance
reaches **€10** it is invested in the fund configured as `Upvest:InstrumentId`. Everything is exposed on the
**PublicApi** project under `/api/`.

All Upvest access goes through the Upvest .NET SDK, and every outbound call is authenticated by a single
`UpvestSigningHandler` (OAuth bearer + HTTP message signature with the EC signing key). Credentials are read
from `Upvest:*` configuration (loaded into .NET user-secrets — never the repository).

## 1. Prerequisites (this machine)

- The Upvest mock is already running (its address is `UPVEST_BASE_URL`).
- Only the .NET 10 SDK is installed, so roll forward: `global.json` is set to `rollForward: latestMajor` and
  you run with `DOTNET_ROLL_FORWARD=Major`.
- No SQL Server LocalDB, so run with `UseOnlyInMemoryDatabase=true` (data lives only for the run).
- The HTTPS dev cert is trusted: `dotnet dev-certs https --check` (add `--trust` if needed).

## 2. Load the Upvest settings into user-secrets (once)

The eight settings are read from the environment variables and stored in user-secrets (values never touch the
repo):

```bash
P=src/PublicApi/PublicApi.csproj
dotnet user-secrets set "Upvest:ClientId"            "$UPVEST_CLIENT_ID"            --project "$P"
dotnet user-secrets set "Upvest:ClientSecret"        "$UPVEST_CLIENT_SECRET"        --project "$P"
dotnet user-secrets set "Upvest:SigningKeyId"        "$UPVEST_SIGNING_KEY_ID"       --project "$P"
dotnet user-secrets set "Upvest:SigningKeyPath"      "$UPVEST_SIGNING_KEY_PATH"     --project "$P"
dotnet user-secrets set "Upvest:SigningKeyPassphrase" "$UPVEST_SIGNING_KEY_PASSPHRASE" --project "$P"
dotnet user-secrets set "Upvest:BaseUrl"             "$UPVEST_BASE_URL"             --project "$P"
dotnet user-secrets set "Upvest:InstrumentId"        "$UPVEST_INSTRUMENT_ID"        --project "$P"
dotnet user-secrets set "Upvest:CallbackBaseUrl"     "$UPVEST_CALLBACK_BASE_URL"    --project "$P"
```

The host refuses to start if any of these is missing or blank.

## 3. Run PublicApi

```bash
export DOTNET_ROLL_FORWARD=Major
export ASPNETCORE_ENVIRONMENT=Development
export UseOnlyInMemoryDatabase=true
export ASPNETCORE_URLS="https://localhost:36683;http://localhost:36684"
dotnet run --project src/PublicApi/PublicApi.csproj --no-launch-profile
```

Wait for `Now listening on: https://localhost:36683`. (`curl` examples use `-k` for the dev cert.)

## 4. Drive the flows

```bash
B=https://localhost:36683

# Get a JWT for the seeded shopper
TOKEN=$(curl -sk -X POST "$B/api/authenticate" -H 'Content-Type: application/json' \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")

# Flow 1 — opt in (becomes an investor with Upvest)
curl -sk -X POST "$B/api/investing/enrolment" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{
  "firstName":"Ada","lastName":"Lovelace","email":"ada@example.com","birthDate":"1990-05-15",
  "nationality":"DE","address":{"line1":"10 King Street","postcode":"10115","city":"Berlin","country":"DE"},
  "phoneNumber":"491234567890","taxId":"12345678901","taxCountry":"DE"}'
# -> {"enrolmentId":1,"status":"pending"}

# Poll until Upvest accepts the shopper (~10-12s)
curl -sk "$B/api/investing/enrolment" -H "Authorization: Bearer $TOKEN"
# -> {"enrolmentId":1,"status":"active"}

# Flow 2 — place orders (catalog item 5 is €8.50 -> €0.50 set aside each). 21 orders cross €10.
for i in $(seq 1 21); do
  curl -sk -X POST "$B/api/orders" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
    -d '{"items":[{"catalogItemId":5,"quantity":1}]}'
done
# each -> {"orderId":N,"roundUpAmount":0.50}

# Flow 5 — balance (just after the 21st order, before the worker invests)
curl -sk "$B/api/investing/balance" -H "Authorization: Bearer $TOKEN"
# -> {"pendingAmount":10.50,"investedAmount":0.00}

# Flows 3 & 4 — the worker invests the whole balance and settles it (~10-15s). Poll:
curl -sk "$B/api/investing/investments" -H "Authorization: Bearer $TOKEN"
# -> [{"investmentId":1,"amount":10.50,"status":"settled"}]
curl -sk "$B/api/investing/balance" -H "Authorization: Bearer $TOKEN"
# -> {"pendingAmount":0.00,"investedAmount":10.50}
```

### What to expect
- Enrolment: `pending` → `active` (user + KYC check + tax residency submitted, Upvest activates the user,
  then a holding account group and trading account are created and activated).
- Each paid order returns `orderId` and `roundUpAmount` (the change set aside; `0.00` when the total is a
  whole number of euros or the shopper is not an accepted investor).
- Once the set-aside balance reaches €10 the whole balance is invested; the investment's status moves
  `pending` → `settled` once its order fills at Upvest (or `failed`, with the money returned to the pending
  ledger, if the order does not go through).

### Other things you can check
- A shopper who has not opted in: `GET /api/investing/enrolment` and `/balance` return `404`, and an order
  returns `roundUpAmount` `0.00` (nothing is set aside). Each shopper only ever sees their own data.
- No token → `401`. An empty order → `400`.

## 5. Automated tests

```bash
DOTNET_ROLL_FORWARD=Major dotnet test tests/UnitTests/UnitTests.csproj
```

Covers the round-up calculation and the enrolment/investment ledger transitions.
