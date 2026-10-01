# Verifying the PayPal payments + saved-cards integration

Everything below runs against the **PayPal sandbox** through the `src/PublicApi` HTTP API alone — no
browser step, no storefront. It was self-verified end to end (real authorization, real capture at
fulfilment, real refund, and a saved card reused to pay a second order).

> **Design docs:** `pay-pal-server-sdk-plan.md` (SDK contract sheet + production-readiness decisions, incl.
> the verified sandbox deviations in §10). `PLAN.md` is the original design.

---

## 0. One-time setup

**Credentials → user-secrets** (values come from the environment; they are never written into the repo):

```bash
cd src/PublicApi
dotnet user-secrets set "PayPal:ClientId"     "$PAYPAL_CLIENT_ID"
dotnet user-secrets set "PayPal:ClientSecret" "$PAYPAL_CLIENT_SECRET"
dotnet user-secrets set "PayPal:Environment"  "$PAYPAL_ENVIRONMENT"   # sandbox
dotnet user-secrets set "PayPal:Currency"     "$PAYPAL_CURRENCY"      # e.g. USD
# PayPal:BaseUrl is optional; leave unset to use the SDK's sandbox default.
```

**HTTPS dev cert** (a valid cert must exist; curl uses `-k` so it need not be trusted):

```bash
dotnet dev-certs https --check
```

This machine runs the .NET 10 SDK while the solution pins 8.0.x, and has no SQL LocalDB — so run with
roll-forward and the in-memory database (orders/payments/cards live only for one run).

---

## 1. Start the API

```bash
cd src/PublicApi
UseOnlyInMemoryDatabase=true \
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="https://localhost:37843;http://localhost:37844" \
DOTNET_ROLL_FORWARD=Major \
dotnet run --no-launch-profile
```

If it refuses to start with a `PayPal:*` message, a credential is missing/blank — that fail-fast is by
design. Base URL below: `API=https://localhost:37843`.

---

## 2. Get bearer tokens (shopper + administrator)

```bash
API=https://localhost:37843
SHOP=$(curl -sk -X POST $API/api/authenticate -H "Content-Type: application/json" \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
ADMIN=$(curl -sk -X POST $API/api/authenticate -H "Content-Type: application/json" \
  -d '{"username":"admin@microsoft.com","password":"Pass@word1"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
```

`demouser` is the shopper; `admin` has the Administrator role (fulfil / cancel / refund / reconciliation).

> **Sandbox note:** direct card authorizations occasionally come back `TRANSACTION_REFUSED` (HTTP 400,
> `"PayPal rejected CreateOrder ..."`). That is an intermittent sandbox decline, not a bug — the order
> stays `AwaitingPayment`, so just call `/pay` again. The commands below assume a successful attempt.

---

## 3. Flow 1 — pay, fulfil, refund

```bash
# Place an order (catalog items 1 @ 19.50 + 2 @ 8.50 = 28.00)
ORD=$(curl -sk -X POST $API/api/orders -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{
  "items":[{"catalogItemId":1,"quantity":1},{"catalogItemId":2,"quantity":1}],
  "shipToAddress":{"street":"1 Market St","city":"San Jose","state":"CA","country":"USA","zipCode":"95131"}}')
echo "$ORD"                      # -> {"orderId":1,"status":"AwaitingPayment"}
OID=$(echo "$ORD" | python -c "import sys,json;print(json.load(sys.stdin)['orderId'])")

# Authorize with the sandbox Visa (places a hold = order total, to the cent)
curl -sk -X POST $API/api/orders/$OID/pay -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{
  "card":{"number":"4111111111111111","expiry":"2030-01","securityCode":"123","name":"Test Buyer",
          "billingAddress":{"line1":"1 Market St","city":"San Jose","state":"CA","postalCode":"95131","countryCode":"US"}}}'
#  -> status "Authorized", total 28.00, authorizedAmount 28.00, an authorizationId, no payer action

# Fulfil (administrator) — this is when the money is captured; shows gross/fee/net
curl -sk -X POST $API/api/orders/$OID/fulfil -H "Authorization: Bearer $ADMIN"
#  -> status "Fulfilled", capturedAmount 28.00, payPalFee <x>, netProceeds <28.00 - x>, a captureId

# Partial refund 10.00 with an idempotency key
curl -sk -X POST $API/api/orders/$OID/refunds -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"amount":10.00,"idempotencyKey":"r1"}'
#  -> {"refundId":1,"order":{...,"status":"PartiallyRefunded","refundedAmount":10.0}}

# Repeat the SAME key -> same refundId, refundedAmount still 10.0 (no second refund)
curl -sk -X POST $API/api/orders/$OID/refunds -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"amount":10.00,"idempotencyKey":"r1"}'

# A DISTINCT key is a second legitimate partial refund
curl -sk -X POST $API/api/orders/$OID/refunds -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"amount":5.00,"idempotencyKey":"r2"}'            # -> refundedAmount 15.0

# Over-refunding beyond what was captured is rejected
curl -sk -o /dev/null -w "%{http_code}\n" -X POST $API/api/orders/$OID/refunds \
  -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" -d '{"amount":999,"idempotencyKey":"r3"}'   # -> 400
```

**Cancel instead of fulfil** (release the hold before capture — run on a *fresh* authorized order):

```bash
curl -sk -X POST $API/api/orders/$OID/cancel -H "Authorization: Bearer $ADMIN"     # -> status "Cancelled", authorizationStatus "VOIDED"
curl -sk -o /dev/null -w "%{http_code}\n" -X POST $API/api/orders/$OID/fulfil -H "Authorization: Bearer $ADMIN"   # -> 409 (already cancelled)
```

---

## 4. Flow 2 — save a card and reuse it

```bash
# Save (vault) a card — response describes it safely, never full PAN
PM=$(curl -sk -X POST $API/api/payment-methods -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{
  "card":{"number":"4111111111111111","expiry":"2030-01","name":"Test Buyer",
          "billingAddress":{"city":"San Jose","state":"CA","postalCode":"95131","countryCode":"US"}}}')
echo "$PM"                      # -> {"paymentMethodId":1,"brand":"VISA","lastDigits":"1111","expiry":"2030-01"}
PMID=$(echo "$PM" | python -c "import sys,json;print(json.load(sys.stdin)['paymentMethodId'])")

curl -sk $API/api/payment-methods -H "Authorization: Bearer $SHOP"      # lists the caller's saved cards

# New order paid with the saved card — no card details re-entered
ORD2=$(curl -sk -X POST $API/api/orders -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{
  "items":[{"catalogItemId":2,"quantity":2}],
  "shipToAddress":{"street":"1 Market St","city":"San Jose","state":"CA","country":"USA","zipCode":"95131"}}')
OID2=$(echo "$ORD2" | python -c "import sys,json;print(json.load(sys.stdin)['orderId'])")
curl -sk -X POST $API/api/orders/$OID2/pay -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d "{\"paymentMethodId\":$PMID}"        # -> status "Authorized", total 17.00, authorizedAmount 17.00

# Delete the card -> gone from the list and no longer usable to pay
curl -sk -o /dev/null -w "%{http_code}\n" -X DELETE $API/api/payment-methods/$PMID -H "Authorization: Bearer $SHOP"  # -> 204
curl -sk $API/api/payment-methods -H "Authorization: Bearer $SHOP"      # -> []
```

---

## 5. My orders, reconciliation, and access control

```bash
# The caller's own orders with payment state
curl -sk $API/api/my-orders -H "Authorization: Bearer $SHOP"

# Reconciliation (administrator): PayPal's transaction records for an ISO-8601 range, lined up to eShop orders
FROM=$(python -c "import datetime;print((datetime.datetime.now(datetime.UTC)-datetime.timedelta(days=2)).strftime('%Y-%m-%dT%H:%M:%SZ'))")
TO=$(python   -c "import datetime;print((datetime.datetime.now(datetime.UTC)+datetime.timedelta(days=1)).strftime('%Y-%m-%dT%H:%M:%SZ'))")
curl -sk "$API/api/reconciliation?from=$FROM&to=$TO" -H "Authorization: Bearer $ADMIN"
#  -> { from, to, truncated, payPalTransactions:[{transactionId, amount, status, invoiceId, matchedOrderId}], eShopOrdersMissingFromPayPal:[...] }
```

> **Reconciliation lag:** PayPal's transaction reporting trails live activity, so a range covering orders you
> just created may legitimately come back with those transactions not yet listed (and in-memory orders reset
> each run). The report is still correct — it pages the whole range and lines the two sides up; run it over a
> window that includes earlier sandbox activity to see non-empty matches.

**Access control (all return the HTTP code shown):**

```bash
# A shopper cannot perform operator actions
curl -sk -o /dev/null -w "%{http_code}\n" -X POST $API/api/orders/1/fulfil -H "Authorization: Bearer $SHOP"          # 403
curl -sk -o /dev/null -w "%{http_code}\n" "$API/api/reconciliation?from=$FROM&to=$TO" -H "Authorization: Bearer $SHOP"  # 403

# One shopper cannot see/act on another's order or saved card (admin acting as a different shopper here)
curl -sk -o /dev/null -w "%{http_code}\n" -X POST $API/api/orders/1/pay -H "Authorization: Bearer $ADMIN" \
  -H "Content-Type: application/json" -d '{"card":{"number":"4111111111111111","expiry":"2030-01","securityCode":"123","billingAddress":{"countryCode":"US"}}}'  # 404
```

---

## 6. Run the automated tests

```bash
DOTNET_ROLL_FORWARD=Major dotnet test tests/UnitTests/UnitTests.csproj
```

The payment tests (`tests/UnitTests/Infrastructure/PayPal/`) cover the gateway (success paths, typed-error
status/debug-id, drifted-body handling, and a connection failure that resends under the *same* idempotency
key then reports an unknown outcome) and the service (authorize, ownership, refund idempotency, two distinct
partial refunds, over-refund guard, and exact-total capture).
