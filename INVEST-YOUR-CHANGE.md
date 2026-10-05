# Invest your change — Upvest integration

Lets an eShopOnWeb shopper opt in to investing their spare change. Every order they place is rounded up to
the next whole euro; the difference is set aside, and once the set-aside balance reaches **€10** it is
invested on their behalf in the fund configured as `Upvest:InstrumentId`, via **Upvest**.

All capability is exposed on **`src/PublicApi`** (JWT), routed under `/api/`:

| Method & route | Purpose | Response fields |
| --- | --- | --- |
| `POST /api/investing/enrolment` | Opt the signed-in shopper in (carries the sign-up form) | `enrolmentId`, `status` (`pending`→`active`/`rejected`) |
| `GET /api/investing/enrolment` | Where enrolment has got to (reconciles acceptance) | `enrolmentId`, `status` |
| `POST /api/orders` | Place & pay an order from catalog items; sets aside the round-up for an accepted investor | `orderId`, `roundUpAmount` |
| `GET /api/investing/investments` | The shopper's investments, newest first (reconciles) | `[ { investmentId, amount, status } ]` (`pending`→`settled`/`failed`) |
| `GET /api/investing/balance` | Set-aside and total-invested amounts | `pendingAmount`, `investedAmount` |

All money amounts are JSON numbers in euros with two decimal places.

## How it maps to Upvest (all calls via the `upvest` SDK)

- **Enrol** → `Users.CreateUser` (TOL) + `UserChecks.CreateUserCheck` (KYC) + `TaxResidencies.SetTaxResidencies`.
  Acceptance follows asynchronously; `GET /api/investing/enrolment` (and placing orders) drives
  `Users.RetrieveUser` → once accepted, `AccountGroups.CreateAccountGroup` + `AccountsApi.CreateAccount`, then
  `AccountsApi.RetrieveAccount` until the account is `ACTIVE` (enrolment → `active`).
- **Invest** → `VirtualCashBalances.CreateVirtualCashIncrease` (moves the set-aside change into the Upvest cash
  balance) then `Orders.PlaceOrder` (BUY of the fund for the whole balance).
- **Settle** → `Orders.RetrieveOrder` / `Orders.ListAccountOrders` reconcile each investment's status to what
  Upvest actually did (`FILLED`→`settled`, `CANCELLED`→`failed`).

Every outbound call is authenticated by a single `UpvestAuthDelegatingHandler`: it attaches the OAuth bearer
(acquired via `AccessTokens.IssueToken` and cached) and the Upvest HTTP message signature (ECDSA P-521/SHA-512).
No call site attaches credentials.

## Configuration & secrets

Settings are bound from the `Upvest:` section (`UpvestSettings`), with every value required and validated at
startup (`.ValidateDataAnnotations().ValidateOnStart()`). **Secret values are never written into the repo** —
load them into .NET user-secrets for `src/PublicApi` from the environment variables:

```bash
P=src/PublicApi
dotnet user-secrets set "Upvest:ClientId"            "$UPVEST_CLIENT_ID"            --project $P
dotnet user-secrets set "Upvest:ClientSecret"        "$UPVEST_CLIENT_SECRET"        --project $P
dotnet user-secrets set "Upvest:SigningKeyId"        "$UPVEST_SIGNING_KEY_ID"       --project $P
dotnet user-secrets set "Upvest:SigningKeyPath"      "$UPVEST_SIGNING_KEY_PATH"     --project $P
dotnet user-secrets set "Upvest:SigningKeyPassphrase" "$UPVEST_SIGNING_KEY_PASSPHRASE" --project $P
dotnet user-secrets set "Upvest:BaseUrl"             "$UPVEST_BASE_URL"             --project $P
dotnet user-secrets set "Upvest:InstrumentId"        "$UPVEST_INSTRUMENT_ID"        --project $P
dotnet user-secrets set "Upvest:CallbackBaseUrl"     "$UPVEST_CALLBACK_BASE_URL"    --project $P
```

## Run it

```bash
export DOTNET_ROLL_FORWARD=Major
ASPNETCORE_ENVIRONMENT=Development \
UseOnlyInMemoryDatabase=true \
ASPNETCORE_URLS="https://localhost:36583;http://localhost:36584" \
dotnet run --project src/PublicApi --no-launch-profile
```

In-memory data is per-process, so enrol/invest within a single run.

## Verify end to end

```bash
B=https://localhost:36583
T=$(curl -sk -X POST "$B/api/authenticate" -H "Content-Type: application/json" \
      -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' \
      | python -c "import sys,json;print(json.load(sys.stdin)['token'])")

# 1) opt in
curl -sk -X POST "$B/api/investing/enrolment" -H "Authorization: Bearer $T" -H "Content-Type: application/json" -d '{
  "firstName":"Mara","lastName":"Vogt","email":"mara.vogt@example.de","birthDate":"1990-05-17","nationality":"DE",
  "address":{"line1":"Hauptstrasse 5","postcode":"10115","city":"Berlin","country":"DE"},
  "phoneNumber":"491512345678","taxId":"26954371827","taxCountry":"DE"}'

# 2) poll until "active" (Upvest accepts after a few seconds)
curl -sk "$B/api/investing/enrolment" -H "Authorization: Bearer $T"

# 3) place 20 orders of item 5 (EUR 8.50 -> EUR 0.50 set aside each); the 20th crosses EUR 10 and invests
for i in $(seq 1 20); do
  curl -sk -X POST "$B/api/orders" -H "Authorization: Bearer $T" -H "Content-Type: application/json" \
       -d '{"items":[{"catalogItemId":5,"quantity":1}]}'; echo
done

# 4) balance + investment (invested 10.00, investment pending)
curl -sk "$B/api/investing/balance" -H "Authorization: Bearer $T"
curl -sk "$B/api/investing/investments" -H "Authorization: Bearer $T"

# 5) after ~10s the Upvest order fills; re-read to see it settle
sleep 12
curl -sk "$B/api/investing/investments" -H "Authorization: Bearer $T"   # status: settled
curl -sk "$B/api/investing/balance" -H "Authorization: Bearer $T"       # invested 10.00, pending 0.00
```
