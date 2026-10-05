# API Endpoints

This folder demonstrates how to configure API endpoints as individual classes. You can compare it to the traditional controller-based approach found in /Web/Controllers/Api.


## Subscription billing (Maxio Advanced Billing)

Recurring subscriptions are billed through Maxio Advanced Billing, which is the system of record. This runs
alongside the one-time Catalog → Basket → Order flow and does not replace it. Every endpoint needs a JWT bearer
token from `POST /api/authenticate`.

| Endpoint | What it does |
| --- | --- |
| `GET /api/subscription-plans` | Plans (products) in the configured Maxio product family |
| `POST /api/subscriptions` `{ "planHandle": "...", "firstName"?: "...", "lastName"?: "..." }` | Subscribes the caller. `201` = created, `200` = already subscribed (idempotent), `409` = the same request is still in flight, `400` = unknown plan |
| `GET /api/my-subscriptions` | The caller's subscriptions, read from Maxio |

When Maxio fails, the API answers with `{ "StatusCode", "Message" }`: `504` = Maxio did not respond within
`Maxio:RequestBudgetSeconds` (25 s by default), `503` = Maxio unreachable or rate-limiting, `422` = Maxio rejected
the request, `502` = configuration or provider error.

### Configuration (`Maxio:` section — never commit values)

| Key | Source | Notes |
| --- | --- | --- |
| `Maxio:ApiKey` | user-secrets / `Maxio__ApiKey` / `MAXIO_API_KEY` | required |
| `Maxio:Subdomain` | user-secrets / `MAXIO_SITE_SUBDOMAIN` | required unless `Maxio:BaseUrl` is set |
| `Maxio:ProductFamilyHandle` | user-secrets / `MAXIO_DEFAULT_PRODUCT_FAMILY` | required |
| `Maxio:BaseUrl` | optional | used verbatim as the API base address instead of the subdomain-derived one |
| `Maxio:Environment` | optional, `MAXIO_ENVIRONMENT` | `US` (default) or `EU` |
| `Maxio:PaymentCollectionMethod` | optional | `remittance` (default; invoiced, so no card is needed), `automatic`, `prepaid`, `invoice` |
| `Maxio:RequestBudgetSeconds` / `AttemptTimeoutSeconds` / `PlanCacheSeconds` | optional | 25 / 8 / 300 |

The host refuses to start if a required key is missing. To load the provisioning environment variables into
user-secrets:

```bash
cd src/PublicApi
dotnet user-secrets set "Maxio:ApiKey" "$MAXIO_API_KEY"
dotnet user-secrets set "Maxio:Subdomain" "$MAXIO_SITE_SUBDOMAIN"
dotnet user-secrets set "Maxio:ProductFamilyHandle" "$MAXIO_DEFAULT_PRODUCT_FAMILY"
```

The Maxio .NET SDK is shipped as source inside the `maxio` plugin, not on NuGet. `PublicApi.csproj` references it
through the `MaxioSdkProject` property, which defaults to `../../../marketplace/plugins/maxio/sdk/dotnet`. Where
the plugin is installed somewhere else, override it with `-p:MaxioSdkProject=<path>/MaxioAdvancedBilling.csproj`.
