# PayPal payments & saved cards (PublicApi)

Additive capability on top of eShopOnWeb: collect money for an order via **PayPal** (authorize at
checkout, capture at fulfilment, void on cancel, refund on return) and let a shopper **save a card**
to reuse. All PayPal calls are built directly against the OpenAPI specs in `api-specs/paypal/`
(Orders v2, Payments v2, Payment Method Tokens v3, Transaction Search v1) — no third-party PayPal SDK.

## Endpoints (all under `/api/`, JWT-authenticated)

| Method & route | Who | Purpose |
|---|---|---|
| `POST /api/orders` | shopper | Place an order from catalog item ids + quantities (starts *AwaitingPayment*). |
| `POST /api/orders/{id}/pay` | shopper | **Authorize** (hold) the total — raw `card` or a saved `paymentMethodId`. |
| `POST /api/orders/{id}/fulfil` | admin | **Capture** the hold; records captured / PayPal fee / net. Renews a stale hold, else 409. |
| `POST /api/orders/{id}/cancel` | admin | **Void** the hold before fulfilment (no money moved). |
| `POST /api/orders/{id}/refunds` | shopper | **Refund** a capture, full or partial; idempotent per caller key. Returns `refundId`. |
| `GET /api/my-orders` | shopper | The caller's orders with payment state. |
| `GET /api/payment-methods` | shopper | The caller's saved cards (brand / last4 / expiry only). |
| `POST /api/payment-methods` | shopper | Vault a card. Returns `paymentMethodId`. |
| `DELETE /api/payment-methods/{id}` | shopper | Remove a saved card (then unusable to pay). |
| `GET /api/reconciliation?from&to` | admin | PayPal transactions for a range vs local orders (matched / PayPal-only / eShop-only). |

Ownership is enforced in the service layer: one shopper can never see, use, or delete another's
orders or cards. Full card details are never stored in the app DB and never logged — only the PayPal
vault token id plus a safe display description are kept.

## Configuration (`PayPal:` section — never hard-coded)

`PayPal:ClientId`, `PayPal:ClientSecret`, `PayPal:Environment`, `PayPal:Currency`, and the optional
`PayPal:BaseUrl` (used verbatim for every call, including the token request, when set). The task's
`PAYPAL_*` env vars are mapped to these keys in `Program.cs`; load them into user-secrets:

```bash
dotnet user-secrets set "PayPal:ClientId" "$PAYPAL_CLIENT_ID" --project src/PublicApi
dotnet user-secrets set "PayPal:ClientSecret" "$PAYPAL_CLIENT_SECRET" --project src/PublicApi
dotnet user-secrets set "PayPal:Environment" "$PAYPAL_ENVIRONMENT" --project src/PublicApi
dotnet user-secrets set "PayPal:Currency" "$PAYPAL_CURRENCY" --project src/PublicApi
```

## Verify it works

Prereqs on this machine: `export DOTNET_ROLL_FORWARD=Major` (SDK roll-forward), in-memory DB, and
the assigned port block (37523/37524).

### Automated

```bash
export DOTNET_ROLL_FORWARD=Major
dotnet test tests/UnitTests                 # 94 offline tests (state machine, refunds, reconciliation, money)
dotnet test tests/PublicApiIntegrationTests # drives the real sandbox with Visa 4111…; skips if creds absent
```

### Manual (real sandbox, no browser)

1. Start the API:
   ```bash
   export DOTNET_ROLL_FORWARD=Major
   ASPNETCORE_ENVIRONMENT=Development UseOnlyInMemoryDatabase=true \
     ASPNETCORE_URLS="https://localhost:37523;http://localhost:37524" \
     dotnet run --project src/PublicApi
   ```
2. Get tokens (shopper `demouser@microsoft.com`, admin `admin@microsoft.com`, password `Pass@word1`):
   ```bash
   B=https://localhost:37523
   SHOPPER=$(curl -sk -X POST $B/api/authenticate -H "Content-Type: application/json" \
     -d '{"username":"demouser@microsoft.com","password":"Pass@word1"}' | jq -r .token)
   ADMIN=$(curl -sk -X POST $B/api/authenticate -H "Content-Type: application/json" \
     -d '{"username":"admin@microsoft.com","password":"Pass@word1"}' | jq -r .token)
   ```
3. Place → pay → fulfil → refund:
   ```bash
   OID=$(curl -sk -X POST $B/api/orders -H "Authorization: Bearer $SHOPPER" -H "Content-Type: application/json" \
     -d '{"shipToAddress":{"street":"1 Main","city":"Redmond","state":"WA","country":"US","zipCode":"98052"},
          "items":[{"catalogItemId":1,"quantity":1},{"catalogItemId":2,"quantity":2}]}' | jq -r .orderId)

   curl -sk -X POST $B/api/orders/$OID/pay -H "Authorization: Bearer $SHOPPER" -H "Content-Type: application/json" \
     -d '{"card":{"name":"John Doe","number":"4111111111111111","expiry":"2028-04","securityCode":"123",
          "billingAddress":{"addressLine1":"1 Main","adminArea2":"Redmond","adminArea1":"WA","postalCode":"98052","countryCode":"US"}}}'

   curl -sk -X POST $B/api/orders/$OID/fulfil -H "Authorization: Bearer $ADMIN"        # capture (fee/net shown)
   curl -sk -X POST $B/api/orders/$OID/refunds -H "Authorization: Bearer $SHOPPER" -H "Content-Type: application/json" \
     -d '{"idempotencyKey":"k1","amount":10.00}'                                        # partial refund
   ```
4. Save a card and reuse it for a second order:
   ```bash
   PMID=$(curl -sk -X POST $B/api/payment-methods -H "Authorization: Bearer $SHOPPER" -H "Content-Type: application/json" \
     -d '{"card":{"name":"John Doe","number":"4111111111111111","expiry":"2028-04","securityCode":"123",
          "billingAddress":{"countryCode":"US"}}}' | jq -r .paymentMethodId)
   OID2=$(curl -sk -X POST $B/api/orders -H "Authorization: Bearer $SHOPPER" -H "Content-Type: application/json" \
     -d '{"shipToAddress":{"street":"2 Oak","city":"Bellevue","state":"WA","country":"US","zipCode":"98004"},
          "items":[{"catalogItemId":3,"quantity":1}]}' | jq -r .orderId)
   curl -sk -X POST $B/api/orders/$OID2/pay -H "Authorization: Bearer $SHOPPER" -H "Content-Type: application/json" \
     -d "{\"paymentMethodId\":$PMID}"
   ```
5. Reconciliation (admin) over a range that has data — note PayPal reporting lags live activity by up
   to 3 hours, so a range covering payments you just made may legitimately come back empty:
   ```bash
   FROM=$(date -u -d '30 days ago' +%Y-%m-%dT%H:%M:%SZ); TO=$(date -u +%Y-%m-%dT%H:%M:%SZ)
   curl -sk "$B/api/reconciliation?from=$FROM&to=$TO" -H "Authorization: Bearer $ADMIN"
   ```
6. Review state any time: `curl -sk $B/api/my-orders -H "Authorization: Bearer $SHOPPER"`.

Stop the API by the PID listening on your port (never by image name):
`Get-NetTCPConnection -LocalPort 37523 -State Listen | %{ Stop-Process -Id $_.OwningProcess -Force }`.
