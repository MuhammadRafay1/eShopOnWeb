# Verifying the PayPal payments & saved-cards integration

Everything below drives the **PublicApi** project against the **PayPal sandbox**. No browser step,
no storefront. All nine endpoints live under `/api/` on PublicApi and are JWT-authenticated.

## 0. One-time setup

**Credentials → user-secrets** (values come from the environment; they never go into the repo):

```bash
cd src/PublicApi
dotnet user-secrets set "PayPal:ClientId"     "$PAYPAL_CLIENT_ID"
dotnet user-secrets set "PayPal:ClientSecret" "$PAYPAL_CLIENT_SECRET"
dotnet user-secrets set "PayPal:Environment"  "$PAYPAL_ENVIRONMENT"
dotnet user-secrets set "PayPal:Currency"     "$PAYPAL_CURRENCY"
# Optional: only if overriding the API host (used verbatim for every call incl. the token request)
# dotnet user-secrets set "PayPal:BaseUrl"    "$PAYPAL_BASE_URL"
cd ../..
```

Trust the dev cert if needed: `dotnet dev-certs https --check` (add `--trust` if it isn't).

## 1. Run the automated tests (offline — fake gateway, no PayPal calls)

```bash
dotnet test tests/UnitTests/UnitTests.csproj                       # 59 pass — order state machine, refund math
dotnet test tests/IntegrationTests/IntegrationTests.csproj         #  8 pass — SDK→record translation, error mapping
dotnet test tests/PublicApiIntegrationTests/PublicApiIntegrationTests.csproj   # 31 pass — routing/auth/ownership/idempotency
```

## 2. Start PublicApi against the sandbox (in-memory DB)

```bash
ASPNETCORE_ENVIRONMENT=Development \
UseOnlyInMemoryDatabase=true \
ASPNETCORE_URLS="https://localhost:37583;http://localhost:37584" \
dotnet run --project src/PublicApi --no-launch-profile
```

> **Single-run rule (in-memory DB):** the store resets on every restart, so order ids restart at 1.
> Idempotency keys are deterministic per order id, so **paying an order id you already paid in a
> previous run replays the old PayPal request and errors**. Do a full verification in **one** server
> run; if you restart, use fresh order ids (create a few throwaway orders to advance the counter).

## 3. Get tokens (curl `-k` accepts the dev cert)

```bash
B=https://localhost:37583
tok() { curl -sk -X POST $B/api/authenticate -H "Content-Type: application/json" \
  -d "{\"username\":\"$1\",\"password\":\"Pass@word1\"}" | python3 -c "import sys,json;print(json.load(sys.stdin)['token'])"; }
SHOP=$(tok demouser@microsoft.com)   # shopper
ADMIN=$(tok admin@microsoft.com)     # operator (Administrators role)
```

## 4. Flow 1 — pay, fulfil, refund

```bash
# Create an order (shopper). -> {"orderId":N,"status":"AwaitingPayment","total":39.0,...}
OID=$(curl -sk -X POST $B/api/orders -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d '{"items":[{"catalogItemId":1,"quantity":2}]}' | python3 -c "import sys,json;print(json.load(sys.stdin)['orderId'])")

# Pay = AUTHORIZE (hold, not captured). Sandbox Visa 4111 1111 1111 1111.
curl -sk -X POST $B/api/orders/$OID/pay -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d '{"card":{"name":"Test Buyer","number":"4111111111111111","expiry":"2027-12","securityCode":"123"}}'
#   -> status PaymentAuthorized, payment.authorizationId set, authorizationStatus CREATED, expiry ~29 days out

# Fulfil = CAPTURE (operator). -> captureId, captureStatus COMPLETED, capturedAmount 39.0, payPalFee, netAmount
curl -sk -X POST $B/api/orders/$OID/fulfil -H "Authorization: Bearer $ADMIN"

# Partial refund; repeating the same idempotencyKey does NOT refund twice.
curl -sk -X POST $B/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d '{"amount":10.00,"idempotencyKey":"K1"}'      # -> 201, refundId, remainingRefundable 29.0
curl -sk -X POST $B/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d '{"amount":10.00,"idempotencyKey":"K1"}'      # -> 201 SAME refundId (idempotent replay, no 2nd refund)
curl -sk -X POST $B/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d '{"amount":999,"idempotencyKey":"K3"}'        # -> 400 exceeds remaining refundable

# The caller's orders with payment state (capture + refunds).
curl -sk $B/api/my-orders -H "Authorization: Bearer $SHOP"
```

**Cancel instead of fulfil** (release the hold, on a *different, freshly paid* order):

```bash
curl -sk -X POST $B/api/orders/$OID2/cancel -H "Authorization: Bearer $ADMIN"   # -> status Cancelled, auth VOIDED
```

## 5. Flow 2 — saved cards

```bash
# Save a card. -> paymentMethodId, brand VISA, lastDigits 1111 (never the vault id or PAN)
PMID=$(curl -sk -X POST $B/api/payment-methods -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d '{"cardholderName":"Test Buyer","cardNumber":"4111111111111111","expiry":"2027-12","securityCode":"123"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['paymentMethodId'])")

curl -sk $B/api/payment-methods -H "Authorization: Bearer $SHOP"   # -> lists the saved card safely

# Reuse the saved card to pay a NEW order (no card details re-entered)
OID3=$(curl -sk -X POST $B/api/orders -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d '{"items":[{"catalogItemId":3,"quantity":1}]}' | python3 -c "import sys,json;print(json.load(sys.stdin)['orderId'])")
curl -sk -X POST $B/api/orders/$OID3/pay -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d "{\"paymentMethodId\":$PMID}"                 # -> status PaymentAuthorized

# Delete the saved card (removed locally AND at PayPal); then it is gone and unusable.
curl -sk -X DELETE $B/api/payment-methods/$PMID -H "Authorization: Bearer $SHOP" -w "\nHTTP %{http_code}\n"  # 204
curl -sk $B/api/payment-methods -H "Authorization: Bearer $SHOP"   # -> empty
```

## 6. Reconciliation (operator)

```bash
curl -sk -G $B/api/reconciliation -H "Authorization: Bearer $ADMIN" \
  --data-urlencode "from=2026-09-01T00:00:00Z" --data-urlencode "to=2026-10-01T00:00:00Z"
```

Returns rows classified `Matched` / `PayPalOnly` / `EShopOnly` plus a summary, paging PayPal's report
over the **whole range** (not just page 1). Because PayPal's transaction reporting **lags** live
activity, transactions you just created legitimately show as `EShopOnly` (and older PayPal records
show as `PayPalOnly`) — that is the expected sandbox result, not a defect.

## 7. Authorization & ownership (quick checks)

```bash
curl -sk -o /dev/null -w "%{http_code}\n" -X POST $B/api/orders/$OID/fulfil -H "Authorization: Bearer $SHOP"  # 403 (admin only)
curl -sk -o /dev/null -w "%{http_code}\n" $B/api/reconciliation?from=2026-09-01T00:00:00Z\&to=2026-10-01T00:00:00Z -H "Authorization: Bearer $SHOP"  # 403
curl -sk -o /dev/null -w "%{http_code}\n" $B/api/my-orders                                                    # 401 (no token)
curl -sk -o /dev/null -w "%{http_code}\n" -X POST $B/api/orders/$OID/pay -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" -d '{"card":{"name":"x","number":"4111111111111111","expiry":"2027-12","securityCode":"123"}}'  # 404 (not the admin's order)
```

## Status → HTTP mapping

| Situation | HTTP |
|---|---|
| Illegal state transition / authorization not renewable | 409 |
| PayPal declined or unreachable (carries PayPal error name + debug id) | 502 |
| Card needs browser (3DS) approval | 422 (reported, not worked around) |
| Order/card not found **or** owned by another shopper | 404 |
| Bad request shape / over-refund | 400 |
