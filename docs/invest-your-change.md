# Invest your change

An additive capability on top of eShopOnWeb: a logged-in shopper can opt in to investing their spare
change. Every order they pay for is rounded up to the next whole euro; the difference is set aside, and
once the set-aside balance reaches **€10** it is invested on the shopper's behalf in a single
exchange-traded fund at **Upvest**. The shopper can see what they have set aside and what has been
invested. The existing catalog/basket/order flow is untouched.

## Endpoints (all on `src/PublicApi`, JWT-authenticated; identity comes from the token)

| Method & route | Purpose | Key response fields |
| --- | --- | --- |
| `POST /api/investing/enrolment` | Opt the shopper in (carries the investor sign-up form). | `enrolmentId`, `status` |
| `GET /api/investing/enrolment` | Where the caller's enrolment has got to. | `enrolmentId`, `status` (`pending`→`active`/`rejected`) |
| `POST /api/orders` | Place an order from catalog item ids + quantities (reuses the existing Order model). | `orderId`, `roundUpAmount` |
| `GET /api/investing/investments` | The caller's investments, newest first. | `investmentId`, `amount`, `status` (`pending`→`settled`/`failed`) |
| `GET /api/investing/balance` | What the caller has set aside and invested so far. | `pendingAmount`, `investedAmount` |
| `POST /api/investing/upvest-webhook` | Callback Upvest invokes on status changes. **The only route with no shopper token.** | — |

All money amounts are JSON numbers in euros with two decimal places.

## How it works

- **Enrolment** maps to Upvest's TOL onboarding: create user → submit KYC check → declare tax
  residency. The account group and trading account are provisioned later, once Upvest has activated the
  user (a user cannot own an account group until then).
- **Setting aside** happens entirely locally on the order path: for an accepted investor, the round-up
  to the next whole euro is added to their pending balance. A non-accepted shopper, or a whole-euro
  order, sets aside nothing. Placing an order never fails because of investing.
- **Investing** happens off the request path, in a background reconciliation pass: when a shopper's
  pending balance reaches €10, the account group is funded with that cash (sandbox virtual cash) and a
  single nominal MARKET buy order is placed for the configured fund (`Upvest:InstrumentId`). The balance
  then starts again from zero.
- **Settlement** is reconciled from Upvest's authoritative state: the investment's status follows the
  order (`FILLED` → `settled`, `CANCELLED` → `failed`). The reconciliation runs on a short timer and is
  also triggered by the webhook.
- **The Upvest connection**: every call to Upvest goes through one reusable `DelegatingHandler`
  (`UpvestAuthenticationHandler`) that signs the request (the credential Upvest enforces on every call,
  the OAuth token request included) and pins the configured base URL. No call site attaches credentials.
- A shopper only ever sees their own enrolment, balance and investments. Personal details are never
  logged.

## Configuration

Bound from the `Upvest:` configuration section (loaded into .NET user-secrets, never committed):
`ClientId`, `ClientSecret`, `SigningKeyId`, `SigningKeyPath`, `SigningKeyPassphrase`, `BaseUrl`,
`InstrumentId`, `CallbackBaseUrl`.

## Verifying it end-to-end

> Prerequisites on this machine: the .NET 10 SDK with `DOTNET_ROLL_FORWARD=Major`, the in-memory
> database (`UseOnlyInMemoryDatabase=true`), and the Upvest credentials already in user-secrets for
> `src/PublicApi`. The HTTPS dev cert should be trusted (`dotnet dev-certs https --check`).

1. **Load the credentials into user-secrets** (one-time; reads them from the environment):

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

2. **Run PublicApi** (binds to its assigned port block; the callback URL is `https://localhost:36903`):

   ```bash
   DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development \
   ASPNETCORE_URLS="https://localhost:36903;http://localhost:36904" UseOnlyInMemoryDatabase=true \
   dotnet run --project src/PublicApi --no-launch-profile
   ```

3. **Get a bearer token** (the seeded demo shopper):

   ```bash
   B=https://localhost:36903
   TOKEN=$(curl -sk -X POST $B/api/authenticate -H 'Content-Type: application/json' \
     -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | jq -r .token)
   ```

4. **Enrol** (made-up data; a German nationality skips the extra identifier step; use a fresh email per
   run because the Upvest sandbox persists users across restarts):

   ```bash
   curl -sk -X POST $B/api/investing/enrolment -H "Authorization: Bearer $TOKEN" \
     -H 'Content-Type: application/json' -d '{
       "firstName":"Max","lastName":"Mustermann","email":"max+'$(date +%s)'@example.de",
       "birthDate":"1990-05-15","nationality":"DE",
       "address":{"line1":"Unter den Linden 1","postcode":"10117","city":"Berlin","country":"DE"},
       "phoneNumber":"+49301234567","taxId":"12345678901","taxCountry":"DE"}'
   # -> {"enrolmentId":"...","status":"pending"}
   ```

5. **Wait for acceptance** (a few seconds; reconciliation provisions the account once Upvest activates
   the user):

   ```bash
   curl -sk $B/api/investing/enrolment -H "Authorization: Bearer $TOKEN"   # -> "status":"active"
   ```

6. **Place orders until the set-aside balance crosses €10.** The seeded €8.50 item sets aside €0.50 per
   order, so 20 orders reach €10.00:

   ```bash
   for i in $(seq 1 20); do
     curl -sk -X POST $B/api/orders -H "Authorization: Bearer $TOKEN" \
       -H 'Content-Type: application/json' -d '{"items":[{"catalogItemId":5,"quantity":1}]}'
   done
   curl -sk $B/api/investing/balance -H "Authorization: Bearer $TOKEN"
   # -> {"pendingAmount":10.00,"investedAmount":0.00}
   ```

7. **Watch it get invested and settle** (background reconciliation, a few seconds):

   ```bash
   curl -sk $B/api/investing/investments -H "Authorization: Bearer $TOKEN"
   # -> {"investments":[{"investmentId":"...","amount":10.00,"status":"settled"}]}
   curl -sk $B/api/investing/balance -H "Authorization: Bearer $TOKEN"
   # -> {"pendingAmount":0.00,"investedAmount":10.00}
   ```

(`jq` is used above only for brevity when extracting the token; it is not required.)
