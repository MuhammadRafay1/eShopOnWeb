# PayPal payments + saved cards — eShopOnWeb PublicApi

This adds real money movement to eShopOnWeb via **PayPal** (direct card processing + card
vaulting), exposed as JWT-authenticated HTTP endpoints on **`src/PublicApi`**. It is additive: the
existing catalog/basket/order flow is untouched.

- **Authorize at checkout, capture at fulfilment, refund on return.** Paying an order places a
  *hold* (authorization); an operator *fulfilling* it captures the money and records PayPal's fee
  and net proceeds; *cancelling* before fulfilment voids the hold; *refunding* after fulfilment
  returns money in full or in part.
- **Saved cards.** A shopper vaults a card once (PayPal Vault) and reuses it for later orders. Full
  card details are never stored in this app's database and never written to logs.

## Architecture

| Layer | What |
|---|---|
| `ApplicationCore/Interfaces/IPayPalGateway` | Thin abstraction over PayPal REST (Orders v2, Payments v2, Vault v3, Transaction Search v1). |
| `Infrastructure/PayPal/PayPalGateway` | HTTP implementation: OAuth token cache, `PayPal-Request-Id` idempotency headers, error-envelope → typed exceptions, ≤31-day chunked + fully-paged transaction search. Never logs card-bearing bodies. |
| `ApplicationCore/Services/PaymentService` | Orchestrates the order/payment state machine, ownership checks, idempotency guards, and persistence; delegates PayPal calls to the gateway. |
| `ApplicationCore/Entities/PaymentAggregate` | `Payment` (mirrors PayPal's hold/capture/refund state), `Refund`, `PaymentEvent` (audit), `PaymentMethod` (saved card — brand/last4/expiry + vault id only). |
| `PublicApi/PaymentEndpoints` | The HTTP surface below (`MinimalApi.Endpoint` style). |

**Idempotency.** Every money-moving call is protected two ways: (1) a server-side state guard —
a repeated `pay`/`fulfil` returns the stored result without re-calling PayPal; (2) a deterministic
`PayPal-Request-Id` so even a true concurrent double-submit dedupes at PayPal. Refunds use the
caller's idempotency key (`(paymentId, key)` is unique); a repeat under the same key returns the
stored refund, while a different key is a legitimately separate partial refund. Refunds are capped
locally to the captured amount before PayPal is ever called.

## API surface

| Endpoint | Role | Purpose |
|---|---|---|
| `POST /api/orders` | shopper | Place an order from `{ items:[{catalogItemId,quantity}], shippingAddress? }`. Returns `orderId`. |
| `POST /api/orders/{orderId}/pay` | shopper (own) | Authorize the total with `{ card:{…} }` **or** `{ paymentMethodId }`. |
| `POST /api/orders/{orderId}/fulfil` | admin | Capture the held funds; response shows captured amount, PayPal fee, net. Renews a stale hold. |
| `POST /api/orders/{orderId}/cancel` | admin | Void the hold before fulfilment (no money moves). |
| `POST /api/orders/{orderId}/refunds` | shopper (own) | Refund `{ idempotencyKey, amount? }`. Returns `refundId`. |
| `GET /api/my-orders` | shopper | The caller's orders with payment state. |
| `GET /api/reconciliation?from=&to=` | admin | PayPal's transactions for a range lined up against eShop payments. ISO-8601 date-times. |
| `POST /api/payment-methods` | shopper | Vault a card `{ card:{…} }`. Returns `paymentMethodId` + brand/last4/expiry. |
| `GET /api/payment-methods` | shopper | The caller's saved cards. |
| `DELETE /api/payment-methods/{paymentMethodId}` | shopper (own) | Remove a saved card (PayPal vault + local). |

Card object shape: `{ number, expiry:"YYYY-MM", securityCode, name, addressLine1, addressLine2?, city, state, postalCode, countryCode }`.

## Configuration (secrets never live in the repo)

Settings bind from the `PayPal:` section — keys exactly `PayPal:ClientId`, `PayPal:ClientSecret`,
`PayPal:Environment`, `PayPal:Currency`, `PayPal:BaseUrl`. Values come only from the environment /
.NET user-secrets, never from a file in the repository.

The task's env vars use single underscores (`PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`,
`PAYPAL_ENVIRONMENT`, `PAYPAL_CURRENCY`, and optional `PAYPAL_BASE_URL`); `Program.cs` bridges these
to the `PayPal:` keys at startup. Load them into user-secrets once:

```bash
dotnet user-secrets set "PayPal:ClientId"     "$PAYPAL_CLIENT_ID"     --project src/PublicApi
dotnet user-secrets set "PayPal:ClientSecret" "$PAYPAL_CLIENT_SECRET" --project src/PublicApi
dotnet user-secrets set "PayPal:Environment"  "$PAYPAL_ENVIRONMENT"   --project src/PublicApi
dotnet user-secrets set "PayPal:Currency"     "$PAYPAL_CURRENCY"      --project src/PublicApi
```

`PayPal:BaseUrl` is optional: when set it is used verbatim as the API base for **every** call
(including the OAuth token request); otherwise it is derived from `PayPal:Environment`
(`sandbox` → `https://api-m.sandbox.paypal.com`, `live`/`production` → `https://api-m.paypal.com`).

## Run it (this machine)

```bash
export DOTNET_ROLL_FORWARD=Major          # .NET 10 SDK present, ASP.NET Core 8 runtime absent
export ASPNETCORE_ENVIRONMENT=Development   # loads user-secrets
export UseOnlyInMemoryDatabase=true         # no LocalDB here
export ASPNETCORE_URLS="https://localhost:37623;http://localhost:37624"
dotnet run --project src/PublicApi
```

> In-memory data does not survive a restart and is per-host, so pay/fulfil/refund the orders you
> create in the **same** run, through PublicApi. A real EF Core migration (`AddPayPalPayments`) is
> committed for the SQL Server path.

## Verify it yourself (curl)

```bash
B=https://localhost:37623
ADMIN=$(curl -sk -X POST $B/api/authenticate -H "Content-Type: application/json" \
  -d '{"username":"admin@microsoft.com","password":"Pass@word1"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
SHOP=$(curl -sk -X POST $B/api/authenticate -H "Content-Type: application/json" \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
CARD='{"number":"4111111111111111","expiry":"2030-01","securityCode":"123","name":"Test Buyer","addressLine1":"123 Main St.","city":"Kent","state":"OH","postalCode":"44240","countryCode":"US"}'

# 1) Place an order (shopper)
OID=$(curl -sk -X POST $B/api/orders -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" \
  -d '{"items":[{"catalogItemId":1,"quantity":2},{"catalogItemId":2,"quantity":1}]}' | python -c "import sys,json;print(json.load(sys.stdin)['orderId'])")

# 2) Pay = authorize a hold (sandbox test card)
curl -sk -X POST $B/api/orders/$OID/pay -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d "{\"card\":$CARD}"

# 3) Fulfil = capture (admin) → response shows capturedAmount, payPalFee, netAmount
curl -sk -X POST $B/api/orders/$OID/fulfil -H "Authorization: Bearer $ADMIN"

# 4) Refund: partial, then the SAME key again (no second refund), then a different key
curl -sk -X POST $B/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{"idempotencyKey":"r1","amount":10.00}'
curl -sk -X POST $B/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{"idempotencyKey":"r1","amount":10.00}'  # same refundId
curl -sk -X POST $B/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{"idempotencyKey":"r2","amount":5.00}'
curl -sk -o /dev/null -w "over-refund -> %{http_code}\n" -X POST $B/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{"idempotencyKey":"r3","amount":999}'  # 422

# 5) Cancel path: new order → pay → cancel (void) before fulfilment
OID2=$(curl -sk -X POST $B/api/orders -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{"items":[{"catalogItemId":3,"quantity":1}]}' | python -c "import sys,json;print(json.load(sys.stdin)['orderId'])")
curl -sk -X POST $B/api/orders/$OID2/pay -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d "{\"card\":$CARD}" >/dev/null
curl -sk -X POST $B/api/orders/$OID2/cancel -H "Authorization: Bearer $ADMIN"

# 6) Saved card: save → list → pay a NEW order with it → delete → confirm unusable
PMID=$(curl -sk -X POST $B/api/payment-methods -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d "{\"card\":$CARD}" | python -c "import sys,json;print(json.load(sys.stdin)['paymentMethodId'])")
curl -sk $B/api/payment-methods -H "Authorization: Bearer $SHOP"
OID3=$(curl -sk -X POST $B/api/orders -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d '{"items":[{"catalogItemId":4,"quantity":1}]}' | python -c "import sys,json;print(json.load(sys.stdin)['orderId'])")
curl -sk -X POST $B/api/orders/$OID3/pay -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d "{\"paymentMethodId\":$PMID}"
curl -sk -X DELETE $B/api/payment-methods/$PMID -H "Authorization: Bearer $SHOP"                       # 204
curl -sk -o /dev/null -w "pay-with-deleted -> %{http_code}\n" -X POST $B/api/orders/$OID3/pay -H "Authorization: Bearer $SHOP" -H "Content-Type: application/json" -d "{\"paymentMethodId\":$PMID}"  # 404

# 7) Reconciliation (admin) over a range; empty for very recent activity is expected (see below)
FROM=$(python -c "import datetime;print((datetime.datetime.now(datetime.UTC)-datetime.timedelta(days=40)).strftime('%Y-%m-%dT%H:%M:%SZ'))")
TO=$(python -c "import datetime;print(datetime.datetime.now(datetime.UTC).strftime('%Y-%m-%dT%H:%M:%SZ'))")
curl -sk -G "$B/api/reconciliation" --data-urlencode "from=$FROM" --data-urlencode "to=$TO" -H "Authorization: Bearer $ADMIN"

# 8) See it all
curl -sk $B/api/my-orders -H "Authorization: Bearer $SHOP"
```

## Notes / deliberate decisions

- **Reconciliation lag is expected.** PayPal's Transaction Search lags live activity by up to ~3
  hours, and the in-memory store resets each run, so a reconciliation range covering payments you
  just created may legitimately come back with them under `eShopOnly` (or empty). The report is
  built to be correct over a range that *has* data — it chunks any range into ≤31-day windows and
  pages each fully (verified against a 40-day range returning 2400+ transactions).
- **Stale-authorization renewal** (reauthorize-then-retry) and the *not-renewable* path only become
  reachable days after an authorization, so they are proven by unit tests with a faked gateway
  (`tests/UnitTests/.../PaymentServiceTests`), not against live PayPal.
- **3-D Secure / `PAYER_ACTION_REQUIRED`** is treated as a stop-and-escalate condition (HTTP 502),
  not a browser round-trip — as required. It does not occur for the sandbox test card.
- **Currency** is assumed 2-decimal (USD and most majors). A zero/3-decimal currency would need
  amount-formatting adjustment.
- **PCI:** direct raw-PAN processing requires PCI SAQ D in production; the sandbox account is
  provisioned for it and the app keeps no PAN and logs no card-bearing bodies.
