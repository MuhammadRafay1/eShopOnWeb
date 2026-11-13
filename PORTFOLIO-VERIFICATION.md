# Managed investment portfolios (Upvest) — how to verify

This adds managed investment portfolios to **`src/PublicApi`** (JWT-authenticated), backed by Upvest, as an
additive capability. It does not touch the existing catalog/basket/order flow.

## What to know first (this machine)

- Build/run with `DOTNET_ROLL_FORWARD=Major` (only the .NET 10 SDK is installed; `global.json` pins 8.0.x).
- Run with `UseOnlyInMemoryDatabase=true` (no LocalDB). The in-memory store is per-host and lost on restart,
  so drive **every** flow through PublicApi alone.
- Credentials are already loaded into PublicApi **user-secrets** (`Upvest:ClientId/ClientSecret/SigningKeyId/
  SigningKeyPath/SigningKeyPassphrase/BaseUrl`). No secret values live in the repo.
- **Sandbox reality (not a bug):** the sandbox client is authorised **only for user operations**. So
  `CreateUser` works and a sign-up returns a real `investorId`, but account/portfolio/top-up/valuation/
  rebalancing calls are refused at the token endpoint (`401 "not authorised to use these scopes"`). The app
  degrades gracefully: sign-up stays `pending`, pay-ins are refused until the account is ready, the portfolio
  view shows the model and paid-in with no live holdings. Build is complete; only the provider happy-path
  beyond user ops cannot be exercised in this sandbox.

## 1. Build and run the API

```bash
cd <repo root>
export DOTNET_ROLL_FORWARD=Major
dotnet build src/PublicApi/PublicApi.csproj

ASPNETCORE_ENVIRONMENT=Development \
UseOnlyInMemoryDatabase=true \
ASPNETCORE_URLS="https://localhost:42023;http://localhost:42024" \
dotnet run --project src/PublicApi --no-launch-profile
```

Wait for `Now listening on: https://localhost:42023`. Swagger: <https://localhost:42023/swagger>.

## 2. Get bearer tokens (PublicApi uses JWT, not the storefront cookie)

```bash
B=https://localhost:42023
ADMIN=$(curl -sk -X POST $B/api/authenticate -H "Content-Type: application/json" \
  -d '{"username":"admin@microsoft.com","password":"Pass@word1"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
USER=$(curl -sk -X POST $B/api/authenticate -H "Content-Type: application/json" \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
```

`admin@microsoft.com` is the operator (Administrators role); `demouser@microsoft.com` is a shopper. `-k` trusts the dev cert.

## 3. Flow 2 — operator defines a model portfolio (admin)

```bash
# create/replace a model (weights must sum to exactly 100)
curl -sk -X PUT "$B/api/portfolio/models/balanced" -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"name":"Balanced","funds":[{"instrumentId":"US0378331005","weight":60},{"instrumentId":"US5949181045","weight":40}]}'
# list all models (any signed-in shopper)
curl -sk "$B/api/portfolio/models" -H "Authorization: Bearer $USER"
```

Negative checks:
```bash
# weights != 100  -> 400
curl -sk -o /dev/null -w "%{http_code}\n" -X PUT "$B/api/portfolio/models/bad" -H "Authorization: Bearer $ADMIN" \
  -H "Content-Type: application/json" -d '{"name":"Bad","funds":[{"instrumentId":"US0378331005","weight":50},{"instrumentId":"US5949181045","weight":40}]}'
# non-admin PUT -> 403
curl -sk -o /dev/null -w "%{http_code}\n" -X PUT "$B/api/portfolio/models/x" -H "Authorization: Bearer $USER" \
  -H "Content-Type: application/json" -d '{"name":"x","funds":[{"instrumentId":"US0378331005","weight":100}]}'
```

## 4. Flow 1 — a shopper becomes an investor

Send the full sign-up form (the sandbox's TOL `CreateUser` needs the complete form — an incomplete body is
correctly rejected with 400):

```bash
curl -sk -X POST "$B/api/portfolio/investor" -H "Authorization: Bearer $USER" -H "Content-Type: application/json" -d '{
  "firstName":"Alex","lastName":"Shopper","email":"alex.shopper@example.com","birthDate":"1990-05-15",
  "birthCity":"Berlin","birthCountry":"DE","nationality":"DE",
  "address":{"line1":"Hauptstr 1","postcode":"10115","city":"Berlin","country":"DE"},
  "phoneNumber":"491234567890","taxId":"DE123456789","taxCountry":"DE",
  "identityDocument":{"type":"PASSPORT","number":"X1234567","issuingCountry":"DE","expiryDate":"2030-01-01"}}'
# -> {"investorId":"<guid>","status":"pending"}   (real Upvest user created; account not ready in sandbox -> pending)

curl -sk "$B/api/portfolio/investor" -H "Authorization: Bearer $USER"      # same investorId + status
```

`status` is `pending` until Upvest has accepted the shopper and the account is ready, then `active`, or
`rejected`. In this sandbox it stays `pending` because account creation needs a scope the client lacks.
Re-POSTing the same shopper returns the **same** investor (idempotent; no duplicate).

## 5. Flow 2 — the investor chooses a model

```bash
curl -sk -X PUT "$B/api/portfolio/investor/model" -H "Authorization: Bearer $USER" \
  -H "Content-Type: application/json" -d '{"modelId":"balanced"}'
```

## 6. Flow 3 — paying in

```bash
curl -sk -X POST "$B/api/portfolio/payments" -H "Authorization: Bearer $USER" \
  -H "Content-Type: application/json" -d '{"amount":100.00}'
curl -sk "$B/api/portfolio/payments" -H "Authorization: Bearer $USER"   # newest first; paymentId, amount, status
```

In the sandbox the investor is still `pending`, so a pay-in is **refused with 409** ("account not ready") —
this is the correct refusal (a payment from a shopper who is not an accepted investor is refused). The
happy-path (top-up + BUY order → `invested`) is covered by the automated service tests with a fake gateway.

## 7. Flow 4 — realignment (admin)

```bash
RID=$(curl -sk -X POST "$B/api/portfolio/models/balanced/realignments" -H "Authorization: Bearer $ADMIN" \
  | python -c "import sys,json;print(json.load(sys.stdin).get('realignmentId',''))")
curl -sk "$B/api/portfolio/realignments/$RID" -H "Authorization: Bearer $USER"
```

In the sandbox the model's allocation was never created at Upvest (scope-refused), so the trigger returns
`409` ("not mirrored to Upvest yet, so it cannot be realigned"). With portfolio scopes available, this
returns `{"realignmentId","status":"pending"}` and the GET reconciles to `completed`/`failed`.

## 8. Flow 5 — the investor's portfolio

```bash
curl -sk "$B/api/portfolio" -H "Authorization: Bearer $USER"
# -> {"modelId":"balanced","holdings":[],"totalValue":0,"paidIn":...}
```

Shows the model followed, each holding (instrumentId/quantity/value), total value, and total paid in. In the
sandbox valuations are scope-refused, so holdings/totalValue are empty/0 while the model and paid-in still
show. One shopper never sees another's data (every lookup is keyed by the caller's JWT identity).

## 9. Automated tests

```bash
export DOTNET_ROLL_FORWARD=Major
dotnet test tests/PublicApiIntegrationTests/PublicApiIntegrationTests.csproj   # 51 pass (incl. 36 new)
dotnet test tests/UnitTests/UnitTests.csproj                                   # 44 pass
```

The new tests cover money/weight conversion, sign-up (active / pending-when-scope-refused / idempotent
retry), model create-replace & weight validation, pay-in refusal and happy-path, per-shopper isolation,
realignment, and the portfolio view — plus endpoint authorization (401/403/400) through the real host.

## Notes

- The Upvest SDK is referenced from the installed `upvest-v4` plugin by a local project path; it builds only
  where the plugin is installed at that path (a CI runner without the plugin cannot build it).
- Secrets are read from configuration/user-secrets only; they are never logged (verified: no personal data or
  secret appears in the app log) and never returned by an endpoint.
