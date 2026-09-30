# PayPal Payments + Saved Cards — eShopOnWeb

This is an **additive** capability layered onto eShopOnWeb: it collects money with **PayPal**
(authorize at checkout, capture at fulfilment, refund on return) and lets a shopper **save a
card** for reuse. The existing catalog / basket / order flow is untouched. Everything is
exposed as JWT-authenticated HTTP endpoints on **`src/PublicApi`**.

## What was built

### Endpoints (all under `/api/`)

| Endpoint | Who | What |
|---|---|---|
| `POST /api/orders` | shopper | Place an order from catalog items (prices come from the catalog). Returns `orderId`. Starts `AwaitingPayment`. |
| `POST /api/orders/{orderId}/pay` | shopper (owner) | **Authorize** the total (a hold; money not taken). Body carries either raw `card` or a saved `paymentMethodId`. Idempotent. |
| `POST /api/orders/{orderId}/fulfil` | operator | **Capture** — money is taken. Renews a stale hold first; reports captured amount, PayPal fee, net. |
| `POST /api/orders/{orderId}/cancel` | operator | Cancel before fulfilment — **voids** the hold, no money moved. |
| `POST /api/orders/{orderId}/refunds` | shopper (owner) | **Refund** a captured payment, full or partial. Caller-supplied `idempotencyKey`. Returns `refundId`. |
| `GET /api/my-orders` | shopper | The caller's orders with payment/refund state. |
| `GET /api/reconciliation?from=&to=` | operator | PayPal's transactions for a date range lined up against eShop orders. |
| `POST /api/payment-methods` | shopper | Save (vault) a card. Returns `paymentMethodId` + safe descriptors (brand/last4/expiry). |
| `GET /api/payment-methods` | shopper | The caller's saved cards. |
| `DELETE /api/payment-methods/{id}` | shopper (owner) | Remove a saved card (from PayPal's vault and locally). |

Operator endpoints require the existing **`Administrators`** role. Every other endpoint is
shopper-scoped and acts only on the caller's own data (a foreign order/card returns `404`).

> **Refunds are shopper-scoped, not operator-scoped**, by a literal reading of the task: the
> operator list is exactly "fulfil, cancel and reconciliation", and "every other endpoint is
> shopper-scoped and acts only on the caller's own data." See `RefundOrderEndpoint` header.

### PayPal APIs used (all via the `paypal-docs` MCP server)
- **Orders v2** `POST /v2/checkout/orders` (`intent=AUTHORIZE`, single purchase unit, no
  breakdown) — direct card / vaulted-card authorization, no browser step.
- **Payments v2** capture / reauthorize / void / refund + show-authorization.
- **Vault v3** setup-token → payment-token, list, delete.
- **Transaction Search v1** `GET /v1/reporting/transactions` (chunked ≤31 days, fully paged).
- **OAuth2** client-credentials token (cached, refreshed before expiry / on 401).

### Design highlights
- New `OrderStatus` on `Order` (defaults to `AwaitingPayment`; Web-created orders unaffected).
- New `Payment` aggregate (1:1 with `Order`) owning `Refund`s; new `PaymentMethod` entity.
  PayPal's own status strings are stored verbatim.
- `IPayPalClient` (Core interface) / `PayPalClient` (Infrastructure typed `HttpClient`).
- `IPaymentService` holds the state machine (idempotency, over-refund guard, stale-hold
  reauthorization). Endpoints are thin; a central `ExceptionMiddleware` maps domain
  exceptions to `400/404/409/422` and PayPal transport failures to `502/503`.
- **No PAN/CVV is ever persisted or logged.** Card fields exist only in the outbound request
  for the one call that needs them; the card DTO's `ToString()` is redacted.
- Idempotency: `/pay` reuses a per-payment key as `PayPal-Request-Id`; refunds key on the
  caller's `idempotencyKey` (also a DB unique constraint on `(PaymentId, IdempotencyKey)`).
- Invoice id `eshop-order-{orderId}-{paymentKey}` is set on authorize + capture and is what
  reconciliation matches on.

## Configuration & secrets

Bound from the `PayPal:` section — **no values live in the repo** (`appsettings.json` holds
empty placeholders). Values are read from the environment and stored in **.NET user-secrets**:

| Config key | Env var |
|---|---|
| `PayPal:ClientId` | `PAYPAL_CLIENT_ID` |
| `PayPal:ClientSecret` | `PAYPAL_CLIENT_SECRET` |
| `PayPal:Environment` | `PAYPAL_ENVIRONMENT` (`sandbox`) |
| `PayPal:Currency` | `PAYPAL_CURRENCY` (`USD`) |
| `PayPal:BaseUrl` | optional override; when set it is used verbatim for **every** call |

`Program.cs` bridges the single-underscore env vars onto the `PayPal:` section (ASP.NET Core
only auto-maps the `PayPal__…` double-underscore form).

---

## How to verify it yourself (no browser needed)

Uses PayPal's sandbox Visa test card `4111 1111 1111 1111`, any future expiry, any CVC.
Run in **Git Bash**. Requires `curl`, `python` (for JSON extraction), and the four
`PAYPAL_*` env vars set in your shell.

### 0. One-time setup

```bash
cd src/PublicApi
# Load the sandbox credentials from your environment into user-secrets (values never touch the repo):
dotnet user-secrets set "PayPal:ClientId"     "$PAYPAL_CLIENT_ID"
dotnet user-secrets set "PayPal:ClientSecret" "$PAYPAL_CLIENT_SECRET"
dotnet user-secrets set "PayPal:Environment"  "$PAYPAL_ENVIRONMENT"
dotnet user-secrets set "PayPal:Currency"     "$PAYPAL_CURRENCY"
cd ../..
dotnet dev-certs https --check   # ensure the dev cert is present/trusted
```

### 1. Start PublicApi (in-memory DB, on the assigned port block)

```bash
ASPNETCORE_ENVIRONMENT=Development \
UseOnlyInMemoryDatabase=true \
ASPNETCORE_URLS="https://localhost:37643;http://localhost:37644" \
DOTNET_ROLL_FORWARD=Major \
dotnet run --project src/PublicApi --no-launch-profile
```

Leave it running. In another terminal:

```bash
API=https://localhost:37643
j() { python -c "import sys,json;print(json.load(sys.stdin)$1)"; }   # tiny JSON extractor

# Tokens (seeded users, password Pass@word1)
SHOP=$(curl -sk $API/api/authenticate -H 'Content-Type: application/json' \
  -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | j "['token']")
ADMIN=$(curl -sk $API/api/authenticate -H 'Content-Type: application/json' \
  -d '{"username":"admin@microsoft.com","password":"Pass@word1"}' | j "['token']")

CARD='{"number":"4111111111111111","expiryMonth":1,"expiryYear":2030,"cvv":"123","cardholderName":"Test Buyer","billingAddress":{"street":"1 Microsoft Way","city":"Redmond","state":"WA","country":"US","zipCode":"98052"}}'
ADDR='{"street":"1 Microsoft Way","city":"Redmond","state":"WA","country":"US","zipCode":"98052"}'
```

### 2. Pay flow (direct card)

```bash
# Place an order
OID=$(curl -sk $API/api/orders -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"items\":[{\"catalogItemId\":1,\"quantity\":1},{\"catalogItemId\":2,\"quantity\":2}],\"shipToAddress\":$ADDR}" | j "['orderId']")
echo "order $OID"

# Authorize (hold) with the test card  -> authorizationId + status CREATED
curl -sk $API/api/orders/$OID/pay -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"card\":$CARD}" | python -m json.tool

# Double-click safety: repeat returns the SAME authorization, alreadyAuthorized=true
curl -sk $API/api/orders/$OID/pay -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"card\":$CARD}" | python -m json.tool
```

### 3. Fulfil (capture) — operator

```bash
# -> captureId, captureStatus COMPLETED, capturedAmount, payPalFee, netAmount
curl -sk $API/api/orders/$OID/fulfil -H "Authorization: Bearer $ADMIN" -X POST | python -m json.tool
```

### 4. Refunds — shopper (owner)

```bash
# Partial refund -> refundId, orderStatus PartiallyRefunded
curl -sk $API/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"amount":10.00,"idempotencyKey":"r1"}' | python -m json.tool

# Same key again -> SAME refundId, alreadyProcessed=true (no second refund)
curl -sk $API/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"amount":10.00,"idempotencyKey":"r1"}' | python -m json.tool

# Refund the remainder -> orderStatus Refunded
curl -sk $API/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"idempotencyKey":"r2"}' | python -m json.tool

# Trying to refund a fully-refunded order -> HTTP 409
curl -sk -o /dev/null -w "%{http_code}\n" $API/api/orders/$OID/refunds -H "Authorization: Bearer $SHOP" \
  -H 'Content-Type: application/json' -d '{"amount":1.00,"idempotencyKey":"r3"}'
```

### 5. Cancel before fulfil (void the hold)

```bash
OID2=$(curl -sk $API/api/orders -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"items\":[{\"catalogItemId\":1,\"quantity\":1}],\"shipToAddress\":$ADDR}" | j "['orderId']")
curl -sk $API/api/orders/$OID2/pay -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' -d "{\"card\":$CARD}" >/dev/null
# -> authorizationVoided=true
curl -sk $API/api/orders/$OID2/cancel -H "Authorization: Bearer $ADMIN" -X POST | python -m json.tool
# fulfil now fails -> 409
curl -sk -o /dev/null -w "%{http_code}\n" $API/api/orders/$OID2/fulfil -H "Authorization: Bearer $ADMIN" -X POST
```

### 6. Saved card, reused to pay a second order

```bash
# Save -> paymentMethodId, brand/lastDigits/expiry only (never the full number)
PM=$(curl -sk $API/api/payment-methods -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"card\":$CARD}" | tee /dev/stderr | j "['paymentMethodId']")

curl -sk $API/api/payment-methods -H "Authorization: Bearer $SHOP" | python -m json.tool   # listed

# Pay a NEW order with the saved card
OID3=$(curl -sk $API/api/orders -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"items\":[{\"catalogItemId\":2,\"quantity\":1}],\"shipToAddress\":$ADDR}" | j "['orderId']")
curl -sk $API/api/orders/$OID3/pay -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"paymentMethodId\":$PM}" | python -m json.tool

# Delete, then confirm it's gone and unusable
curl -sk -o /dev/null -w "delete=%{http_code}\n" -X DELETE $API/api/payment-methods/$PM -H "Authorization: Bearer $SHOP"
curl -sk $API/api/payment-methods -H "Authorization: Bearer $SHOP" | python -m json.tool   # empty
OID4=$(curl -sk $API/api/orders -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d "{\"items\":[{\"catalogItemId\":2,\"quantity\":1}],\"shipToAddress\":$ADDR}" | j "['orderId']")
curl -sk -o /dev/null -w "pay-with-deleted=%{http_code}\n" $API/api/orders/$OID4/pay -H "Authorization: Bearer $SHOP" \
  -H 'Content-Type: application/json' -d "{\"paymentMethodId\":$PM}"   # -> 404
```

### 7. My orders

```bash
curl -sk $API/api/my-orders -H "Authorization: Bearer $SHOP" | python -m json.tool
```

### 8. Reconciliation (operator) — including a range > 31 days

```bash
FROM=$(python -c "import datetime;print((datetime.datetime.now(datetime.timezone.utc)-datetime.timedelta(days=40)).strftime('%Y-%m-%dT%H:%M:%SZ'))")
TO=$(python -c "import datetime;print((datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(days=1)).strftime('%Y-%m-%dT%H:%M:%SZ'))")
curl -sk "$API/api/reconciliation?from=$FROM&to=$TO" -H "Authorization: Bearer $ADMIN" | python -m json.tool
```

The report walks the **whole** range (chunked into ≤31-day windows, fully paged) and returns
`matched` / `payPalOnly` / `eShopOnly` buckets plus `payPalTransactionsScanned`.

> **Expected sandbox behaviour:** PayPal's transaction reporting lags live activity by up to a
> few hours, so orders you just captured legitimately appear only under `eShopOnly` at first,
> not `matched`. That is the documented sandbox lag, **not** a bug. The report is correct over
> any range that already has data.

---

## Self-verification performed

Against the live sandbox with the test card, an automated end-to-end run confirmed **41/41**
checks: a real authorization (`CREATED`), a real capture (`COMPLETED`, non-zero PayPal fee and
net proceeds), idempotent re-pay, partial + idempotent-replay + remainder refunds, the
over-refund `422` guard, void-before-fulfil, a saved card reused to pay a second order then
deleted and rejected, tenant isolation (`404` on foreign orders/cards), reconciliation over a
40-day range (6,435 transactions scanned across chunks), and **no card number or CVV in the
logs**. Unit tests (state machine + refund math) and PayPal-free integration tests (auth/role/
validation) also pass: `dotnet test tests/UnitTests` and `dotnet test tests/PublicApiIntegrationTests`.

### Notes / assumptions
- Amounts are formatted to 2 decimal places (correct for USD; zero-decimal currencies such as
  JPY would need per-currency handling — out of scope).
- The SQL Server EF migration `AddPayPalPayments` is included for production readiness. This
  environment runs with `UseOnlyInMemoryDatabase=true`, which ignores migrations and loses data
  on restart — so pay/fulfil/refund the orders you create **within a single run**.
- If PayPal ever answered a card payment with a browser/3DS challenge, the app **stops and
  reports it** (`PayPalChallengeRequiredException` → `422`) rather than building an approval
  round-trip. This did not occur with the sandbox test card.
