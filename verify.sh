#!/usr/bin/env bash
# End-to-end verification of the PayPal integration against the sandbox.
# Requires the PublicApi running on https://localhost:37603.
set -u
B=https://localhost:37603
CARD='{"name":"John Doe","number":"4111111111111111","expiry":"2028-12","cvv":"123","billingAddress":{"addressLine1":"1 Market St","city":"SF","state":"CA","postalCode":"94105","countryCode":"US"}}'
jq() { python -m json.tool; }
tok() { curl -sk -X POST $B/api/authenticate -H "Content-Type: application/json" -d "{\"username\":\"$1\",\"password\":\"Pass@word1\"}" | python -c "import sys,json;print(json.load(sys.stdin)['token'])"; }
field() { python -c "import sys,json;print(json.load(sys.stdin)$1)"; }

SHOP=$(tok demouser@microsoft.com); ADMIN=$(tok admin@microsoft.com)
SHOPH="Authorization: Bearer $SHOP"; ADMINH="Authorization: Bearer $ADMIN"; JSON="Content-Type: application/json"
N=$(date +%s)
mk_order() { curl -sk -X POST $B/api/orders -H "$SHOPH" -H "$JSON" -d '{"items":[{"catalogItemId":5,"quantity":2},{"catalogItemId":4,"quantity":1}],"shipToAddress":{"street":"1 Market St","city":"SF","state":"CA","country":"US","zipCode":"94105"}}'; }

echo "################ FLOW 1: pay -> fulfil -> refund ################"
O1=$(mk_order | field "['orderId']"); echo "order A id=$O1"
echo "--- authorize A (one-off card) ---"; curl -sk -X POST $B/api/orders/$O1/pay -H "$SHOPH" -H "$JSON" -d "{\"card\":$CARD}" | jq
echo "--- fulfil A (capture; expect fee+net populated) ---"; curl -sk -X POST $B/api/orders/$O1/fulfil -H "$ADMINH" | jq
echo "--- partial refund 10.00 A ---"; curl -sk -X POST $B/api/orders/$O1/refunds -H "$SHOPH" -H "$JSON" -d "{\"amount\":10.00,\"idempotencyKey\":\"v-$N-A1\"}" | jq
echo "--- repeat SAME idempotency key (no double refund) ---"; curl -sk -X POST $B/api/orders/$O1/refunds -H "$SHOPH" -H "$JSON" -d "{\"amount\":10.00,\"idempotencyKey\":\"v-$N-A1\"}" | jq
echo "--- over-refund 25.00 (remaining 19.00) -> 409 ---"; curl -sk -X POST $B/api/orders/$O1/refunds -H "$SHOPH" -H "$JSON" -d "{\"amount\":25.00,\"idempotencyKey\":\"v-$N-A2\"}" | jq

echo "################ FLOW 1: pay -> cancel (void, no capture) ################"
O2=$(mk_order | field "['orderId']"); echo "order B id=$O2"
echo "--- authorize B ---"; curl -sk -X POST $B/api/orders/$O2/pay -H "$SHOPH" -H "$JSON" -d "{\"card\":$CARD}" | field "['status']"
echo "--- cancel B (expect Cancelled, no captureId) ---"; curl -sk -X POST $B/api/orders/$O2/cancel -H "$ADMINH" | jq

echo "################ FLOW 2: save card -> reuse to pay a 2nd order ################"
echo "--- save card ---"; PM=$(curl -sk -X POST $B/api/payment-methods -H "$SHOPH" -H "$JSON" -d "{\"name\":\"John Doe\",\"number\":\"4111111111111111\",\"expiry\":\"2028-12\",\"cvv\":\"123\",\"billingAddress\":{\"addressLine1\":\"1 Market St\",\"city\":\"SF\",\"state\":\"CA\",\"postalCode\":\"94105\",\"countryCode\":\"US\"}}" | tee /tmp/pm.json | field "['paymentMethodId']"); cat /tmp/pm.json | jq
echo "--- list saved cards ---"; curl -sk $B/api/payment-methods -H "$SHOPH" | jq
O3=$(mk_order | field "['orderId']"); echo "order C id=$O3"
echo "--- pay C with saved card $PM ---"; curl -sk -X POST $B/api/orders/$O3/pay -H "$SHOPH" -H "$JSON" -d "{\"paymentMethodId\":\"$PM\"}" | jq
echo "--- fulfil C ---"; curl -sk -X POST $B/api/orders/$O3/fulfil -H "$ADMINH" | field "['payment']['captureStatus']"

echo "################ my-orders ################"
curl -sk $B/api/my-orders -H "$SHOPH" | jq

echo "################ authorization / ownership checks ################"
echo "--- shopper hitting admin fulfil -> 403 ---"; curl -sk -o /dev/null -w "%{http_code}\n" -X POST $B/api/orders/$O1/fulfil -H "$SHOPH"
echo "--- unauthenticated pay -> 401 ---"; curl -sk -o /dev/null -w "%{http_code}\n" -X POST $B/api/orders/$O1/pay -H "$JSON" -d "{\"card\":$CARD}"
echo "--- admin (different buyer) my-orders is empty for admin ---"; curl -sk $B/api/my-orders -H "$ADMINH" | field "['orders']" | head -c 80; echo
echo "--- admin paying shopper's order -> 404 (ownership) ---"; curl -sk -o /dev/null -w "%{http_code}\n" -X POST $B/api/orders/$O3/pay -H "$ADMINH" -H "$JSON" -d "{\"card\":$CARD}"
echo "--- delete saved card, then it must be gone ---"; curl -sk -o /dev/null -w "delete=%{http_code}\n" -X DELETE $B/api/payment-methods/$PM -H "$SHOPH"; curl -sk $B/api/payment-methods -H "$SHOPH" | jq
echo "--- pay with deleted card -> 404 ---"; O4=$(mk_order | field "['orderId']"); curl -sk -o /dev/null -w "%{http_code}\n" -X POST $B/api/orders/$O4/pay -H "$SHOPH" -H "$JSON" -d "{\"paymentMethodId\":\"$PM\"}"

echo "################ reconciliation ################"
echo "--- recent range (may be sparse/empty due to reporting lag) ---"; curl -sk "$B/api/reconciliation?from=$(date -u -d '2 days ago' +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || echo 2026-09-28T00:00:00Z)&to=$(date -u +%Y-%m-%dT%H:%M:%SZ)" -H "$ADMINH" | jq
echo "--- shopper hitting reconciliation -> 403 ---"; curl -sk -o /dev/null -w "%{http_code}\n" "$B/api/reconciliation?from=2026-09-01T00:00:00Z&to=2026-09-15T00:00:00Z" -H "$SHOPH"
echo "DONE"
