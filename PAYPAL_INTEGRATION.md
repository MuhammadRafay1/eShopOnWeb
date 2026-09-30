# PayPal payments + saved cards (PublicApi)

This adds real money movement to eShopOnWeb as an **additive** capability on the
`src/PublicApi` project: a shopper places and pays for an order by card (holding the money at
checkout), an operator fulfils it (capturing the money), and returns are refunded — plus saved
cards that can be reused for a later order. PayPal is the processor; everything is driven
through the JWT-authenticated `/api/` surface, no storefront UI.

## Endpoints

| Method & route | Who | What |
|---|---|---|
| `POST /api/orders` | shopper | Place an order from catalog item ids + quantities. Returns `orderId`. |
| `POST /api/orders/{orderId}/pay` | shopper | Authorize (hold) the total with a one-off `card` **or** a saved `paymentMethodId`. |
| `POST /api/orders/{orderId}/fulfil` | admin | Capture the hold (money taken); renews a stale authorization first. |
| `POST /api/orders/{orderId}/cancel` | admin | Void the hold before fulfilment; no money moves. |
| `POST /api/orders/{orderId}/refunds` | shopper | Refund a captured payment, full or partial. Carries `idempotencyKey`. Returns `refundId`. |
| `GET /api/my-orders` | shopper | The caller's orders with payment state. |
| `GET /api/reconciliation?from=&to=` | admin | PayPal's transactions lined up against eShop orders over the range. |
| `POST /api/payment-methods` | shopper | Save (vault) a card. Returns `paymentMethodId`. |
| `GET /api/payment-methods` | shopper | The caller's saved cards. |
| `DELETE /api/payment-methods/{paymentMethodId}` | shopper | Remove a saved card. |

Shopper endpoints act only on the caller's own data (identity comes from the token, never the
body); a foreign order or card returns **404**, not 403. `fulfil`, `cancel` and
`reconciliation` require the administrator role.

## How it maps to PayPal (specs in `api-specs/paypal/`)

The PayPal OpenAPI specs are the authoritative contract. A hand-written client
(`src/Infrastructure/PayPal/PayPalClient.cs`) is built against them — no third-party SDK.

- **Authorize** → `POST /v2/checkout/orders` with `intent=AUTHORIZE`, one `purchase_unit`
  (`amount` = order total, `custom_id` = eShop order id) and `payment_source.card` populated
  with either full card fields or `{ vault_id }` (a saved card). PayPal processes it
  synchronously — no buyer redirect.
- **Fulfil** → `GET /v2/payments/authorizations/{id}` to check status/expiry, then
  `POST .../capture`; if the hold is stale, `POST .../reauthorize` first. The capture's
  `seller_receivable_breakdown` (fetched via `GET /v2/payments/captures/{id}` when the POST
  omits it for card payments) gives the captured amount, PayPal's fee and the net proceeds.
- **Cancel** → `POST /v2/payments/authorizations/{id}/void`.
- **Refund** → `POST /v2/payments/captures/{id}/refund` (empty/partial amount), with the
  caller's idempotency key sent as `PayPal-Request-Id`.
- **Save / list / delete card** → `POST|GET|DELETE /v3/vault/payment-tokens`.
- **Reconciliation** → `GET /v1/reporting/transactions`, split into the ≤31-day windows the
  spec mandates and paged in full (`page_size=500`), matched to eShop payments by
  `paypal_reference_id` / `custom_field` against the stored PayPal order/authorization/capture
  ids.

Auth is OAuth2 client-credentials (`POST /v1/oauth2/token`, HTTP Basic), the token cached in
memory until shortly before it expires.

### Idempotency
- Every mutating endpoint state-checks the order/payment first, so a double-click never reaches
  PayPal twice for the same logical action.
- Deterministic `PayPal-Request-Id` values (per order + action, with a per-process nonce)
  cover the crash-between-call-and-commit window.
- Refunds are keyed by the caller's idempotency key (unique per payment in the DB); a repeat
  returns the original refund, while distinct partial refunds remain independent. Over-refunds
  are rejected before PayPal is called.

## Configuration

Settings bind from the `PayPal:` section — never hard-coded, so the same build runs against a
different account by changing configuration:

- `PayPal:ClientId`, `PayPal:ClientSecret`, `PayPal:Environment`, `PayPal:Currency`
- `PayPal:BaseUrl` (optional) — when set, used verbatim as the base for **every** PayPal call
  (token request included); otherwise the sandbox/live host is derived from `Environment`.

The four credentials also accept the flat env vars `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`,
`PAYPAL_ENVIRONMENT`, `PAYPAL_CURRENCY` (bridged to `PayPal:*` in `Program.cs`). Load the real
values into .NET user-secrets — never commit them.

```bash
cd src/PublicApi
dotnet user-secrets set "PayPal:ClientId"     "$PAYPAL_CLIENT_ID"
dotnet user-secrets set "PayPal:ClientSecret" "$PAYPAL_CLIENT_SECRET"
dotnet user-secrets set "PayPal:Environment"  "$PAYPAL_ENVIRONMENT"
dotnet user-secrets set "PayPal:Currency"     "$PAYPAL_CURRENCY"
```

## Run it (this machine)

```bash
# .NET 8 SDK/runtime present; global.json rolls forward if only newer SDKs exist.
ASPNETCORE_ENVIRONMENT=Development \
UseOnlyInMemoryDatabase=true \
DOTNET_ROLL_FORWARD=Major \
ASPNETCORE_URLS="https://localhost:37603;http://localhost:37604" \
dotnet run --project src/PublicApi/PublicApi.csproj --no-launch-profile
```

> In-memory mode keeps a per-host store that is wiped on restart, so place, pay, fulfil and
> refund the orders you created **within the same run**.

## Verify it yourself

`verify.sh` drives the whole thing end to end against the running API. Or step through it
manually (uses the sandbox Visa test card `4111 1111 1111 1111`, any future expiry/CVC):

```bash
B=https://localhost:37603
tok() { curl -sk -X POST $B/api/authenticate -H 'Content-Type: application/json' \
  -d "{\"username\":\"$1\",\"password\":\"Pass@word1\"}" | python -c "import sys,json;print(json.load(sys.stdin)['token'])"; }
SHOP=$(tok demouser@microsoft.com); ADMIN=$(tok admin@microsoft.com)
CARD='{"name":"John Doe","number":"4111111111111111","expiry":"2028-12","cvv":"123","billingAddress":{"addressLine1":"1 Market St","city":"SF","state":"CA","postalCode":"94105","countryCode":"US"}}'

# 1) place, pay (authorize), fulfil (capture), refund
curl -sk -X POST $B/api/orders -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"items":[{"catalogItemId":5,"quantity":2}],"shipToAddress":{"street":"1 Market","city":"SF","state":"CA","country":"US","zipCode":"94105"}}'
curl -sk -X POST $B/api/orders/1/pay    -H "Authorization: Bearer $SHOP"  -H 'Content-Type: application/json' -d "{\"card\":$CARD}"
curl -sk -X POST $B/api/orders/1/fulfil -H "Authorization: Bearer $ADMIN"        # captured amount + paypalFee + netAmount
curl -sk -X POST $B/api/orders/1/refunds -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"amount":5.00,"idempotencyKey":"demo-key-1"}'                              # refundId

# 2) save a card and reuse it to pay a second order
PM=$(curl -sk -X POST $B/api/payment-methods -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"name":"John Doe","number":"4111111111111111","expiry":"2028-12","cvv":"123","billingAddress":{"city":"SF","state":"CA","postalCode":"94105","countryCode":"US"}}' \
  | python -c "import sys,json;print(json.load(sys.stdin)['paymentMethodId'])")
curl -sk -X POST $B/api/orders -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' \
  -d '{"items":[{"catalogItemId":4,"quantity":1}],"shipToAddress":{"street":"1 Market","city":"SF","state":"CA","country":"US","zipCode":"94105"}}'
curl -sk -X POST $B/api/orders/2/pay -H "Authorization: Bearer $SHOP" -H 'Content-Type: application/json' -d "{\"paymentMethodId\":\"$PM\"}"

# 3) cancel-before-fulfil, my-orders, reconciliation
curl -sk $B/api/my-orders -H "Authorization: Bearer $SHOP"
curl -sk "$B/api/reconciliation?from=2026-09-01T00:00:00Z&to=2026-09-30T00:00:00Z" -H "Authorization: Bearer $ADMIN"
```

> PayPal's transaction reporting lags live activity, so a reconciliation range covering
> payments you just created may legitimately come back sparse/empty — that is expected, not a
> gap. Run it over an older, already-settled range to see matched rows.

## Tests

- `tests/UnitTests` — order state-machine and payment guards (illegal transitions throw; the
  over-refund invariant is enforced in one place).
- `tests/PublicApiIntegrationTests` — endpoint auth/ownership (401 without a token, 403 on
  operator routes for shoppers, 404 for unknown orders) and the create-order → my-orders flow.
  These don't call PayPal.
