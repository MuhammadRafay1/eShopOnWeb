# Verifying the managed-portfolio (Upvest) integration

A concise, copy-pasteable walkthrough. It drives everything through **PublicApi** alone.

## 0. Prerequisites

- .NET SDK present (this machine has .NET 10/11; the projects target net8.0 and roll forward).
- The Upvest env vars are set in your shell: `UPVEST_CLIENT_ID`, `UPVEST_CLIENT_SECRET`,
  `UPVEST_SIGNING_KEY_ID`, `UPVEST_SIGNING_KEY_PATH`, `UPVEST_SIGNING_KEY_PASSPHRASE`,
  `UPVEST_BASE_URL`.
- `python` and `curl` available (for pretty-printing / calls). `curl -k` is used because the dev
  cert may be untrusted; run `dotnet dev-certs https --trust` to avoid `-k`.

## 1. Load the secrets into user-secrets (one time)

```bash
cd src/PublicApi
dotnet user-secrets set "Upvest:ClientId"             "$UPVEST_CLIENT_ID"
dotnet user-secrets set "Upvest:ClientSecret"         "$UPVEST_CLIENT_SECRET"
dotnet user-secrets set "Upvest:SigningKeyId"         "$UPVEST_SIGNING_KEY_ID"
dotnet user-secrets set "Upvest:SigningKeyPath"       "$UPVEST_SIGNING_KEY_PATH"
dotnet user-secrets set "Upvest:SigningKeyPassphrase" "$UPVEST_SIGNING_KEY_PASSPHRASE"
dotnet user-secrets set "Upvest:BaseUrl"              "$UPVEST_BASE_URL"
cd ../..
```

## 2. Run the API

```bash
export DOTNET_ROLL_FORWARD=Major
ASPNETCORE_ENVIRONMENT=Development UseOnlyInMemoryDatabase=true \
  ASPNETCORE_URLS="https://localhost:42403;http://localhost:42404" \
  dotnet run --project src/PublicApi/PublicApi.csproj
```

Wait until it logs `Now listening on: https://localhost:42403`. Swagger: <https://localhost:42403/swagger>.

## 3. Get bearer tokens

```bash
BASE=https://localhost:42403
tok(){ curl -sk -X POST "$BASE/api/authenticate" -H "Content-Type: application/json" \
  -d "{\"username\":\"$1\",\"password\":\"Pass@word1\"}" | python -c "import sys,json;print(json.load(sys.stdin)['token'])"; }
ADMIN=$(tok admin@microsoft.com)       # operator
DEMO=$(tok demouser@microsoft.com)     # shopper
```

## 4. Flow 1 — become an investor (shopper)

```bash
curl -sk -X POST "$BASE/api/portfolio/investor" -H "Authorization: Bearer $DEMO" \
 -H "Content-Type: application/json" -d '{
   "firstName":"Alex","lastName":"Muster","email":"alex.muster@example.com",
   "birthDate":"1990-05-14","birthCity":"Berlin","birthCountry":"DE","nationality":"DE",
   "address":{"line1":"Hauptstrasse 1","postcode":"10115","city":"Berlin","country":"DE"},
   "phoneNumber":"+491700000000","taxId":"12345678901","taxCountry":"DE",
   "identityDocument":{"type":"PASSPORT","number":"C01X00T47","issuingCountry":"DE","expiryDate":"2030-01-01"}
 }'
# -> 201 {"investorId":"<upvest-user-uuid>","status":"pending"}   (real Upvest user created)

curl -sk "$BASE/api/portfolio/investor" -H "Authorization: Bearer $DEMO"
# -> 200 {"investorId":"...","status":"pending"}   (status read back from Upvest)
```

`status` stays `pending` in the sandbox: the user is really created at Upvest, but making the
account "ready" needs scopes the sandbox client does not have (see the Sandbox note).

## 5. Flow 2 — model portfolios

```bash
# Operator creates/replaces a model (weights must sum to exactly 100)
curl -sk -X PUT "$BASE/api/portfolio/models/balanced" -H "Authorization: Bearer $ADMIN" \
 -H "Content-Type: application/json" \
 -d '{"name":"Balanced","funds":[{"instrumentId":"IE00B4L5Y983","weight":60},{"instrumentId":"IE00B3RBWM25","weight":40}]}'
# -> 200 {"modelId":"balanced","name":"Balanced","funds":[...]}

# A shopper cannot (operator-only)
curl -sko /dev/null -w "%{http_code}\n" -X PUT "$BASE/api/portfolio/models/x" -H "Authorization: Bearer $DEMO" \
 -H "Content-Type: application/json" -d '{"name":"X","funds":[{"instrumentId":"IE00B4L5Y983","weight":100}]}'
# -> 403

# Anyone signed in can list
curl -sk "$BASE/api/portfolio/models" -H "Authorization: Bearer $DEMO"
# -> 200 [{"modelId":"balanced","name":"Balanced","funds":[...]}]

# Shopper chooses the model to follow
curl -sk -X PUT "$BASE/api/portfolio/investor/model" -H "Authorization: Bearer $DEMO" \
 -H "Content-Type: application/json" -d '{"modelId":"balanced"}'
# -> 200 {"investorId":"...","status":"pending"}
```

Bad weights are rejected: `... -d '{"name":"Bad","funds":[{"instrumentId":"IE00B4L5Y983","weight":90}]}'` → **400**.

## 6. Flow 3 — pay in

```bash
curl -sk -X POST "$BASE/api/portfolio/payments" -H "Authorization: Bearer $DEMO" \
 -H "Content-Type: application/json" -d '{"amount":100.50}'
# -> 400  "Upvest has not yet accepted this shopper as an investor with a ready account."

curl -sk "$BASE/api/portfolio/payments" -H "Authorization: Bearer $DEMO"
# -> 200 []
```

The pay-in is correctly **refused** because the sandbox investor can never reach `active` (account
not ready). The full credit-transfer + invest path is implemented and covered by unit tests
(`PayIn_credits_and_invests_and_reports_invested`).

## 7. Flow 4 — realignment (operator)

```bash
RID=$(curl -sk -X POST "$BASE/api/portfolio/models/balanced/realignments" -H "Authorization: Bearer $ADMIN" \
  | python -c "import sys,json;print(json.load(sys.stdin)['realignmentId'])")
curl -sk "$BASE/api/portfolio/realignments/$RID" -H "Authorization: Bearer $DEMO"
# -> 200 {"realignmentId":"...","status":"failed"}
```

`failed` is honest: the Upvest allocation/rebalancing calls need `portfolios:admin`, which the
sandbox client lacks, so there is nothing to realign to. With those scopes the status would track
the rebalancing orders (`pending` → `completed`/`failed`).

## 8. Flow 5 — the portfolio

```bash
curl -sk "$BASE/api/portfolio" -H "Authorization: Bearer $DEMO"
# -> 200 {"modelId":"balanced","holdings":[],"totalValue":0.00,"paidIn":0.00}
```

Empty in the sandbox (no funded account yet); money amounts are JSON numbers with two decimals.

## 9. Isolation & auth checks

```bash
curl -sko /dev/null -w "no-token=%{http_code}\n" "$BASE/api/portfolio/investor"          # 401
curl -sk -w "\n" "$BASE/api/portfolio/investor" -H "Authorization: Bearer $ADMIN"        # 404 (admin never signed up -> sees nothing of the shopper's)
curl -sk -w "\n" "$BASE/api/portfolio" -H "Authorization: Bearer $ADMIN"                 # 404
```

## 10. Run the unit tests

```bash
export DOTNET_ROLL_FORWARD=Major
dotnet test tests/UnitTests/UnitTests.csproj --filter "FullyQualifiedName~Upvest"
```

Covers the HTTP-signature crypto (incl. the real OpenSSL-legacy encrypted-key format via an
independent `openssl` oracle) and the orchestration logic (sign-up, isolation, model validation,
pay-in success/refusal).
