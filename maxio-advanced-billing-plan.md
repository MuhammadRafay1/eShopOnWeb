# Maxio Advanced Billing — subscription billing plan (eShopOnWeb / PublicApi)

SDK source (read-only, referenced by path, never copied): `<maxio-plugin-root>/sdk/dotnet/` — all `Source` cells below are relative to that SDK root.

## 1. Scope & sequence

| # | Step | Operations |
| --- | --- | --- |
| 1 | `global.json` roll-forward; reference SDK project from `src/PublicApi` | — |
| 2 | `MaxioSettings` bound from `Maxio:` (`ApiKey`, `Subdomain`, `ProductFamilyHandle`, `BaseUrl`, + optional `Environment`, `RequestBudgetSeconds`), `ValidateOnStart` | — |
| 3 | Named `HttpClient` + singleton `MaxioAdvancedBillingClient` (Basic auth, US/EU, base-URL override) | — |
| 4 | EF entities in `CatalogContext`: `BillingCustomer` (PK `BuyerId`), `SubscriptionEnrollment` (PK `BuyerId`+`PlanHandle`) — the claims | — |
| 5 | `MaxioBillingGateway` (the only class that touches the SDK; one error ladder; one deadline) | all below |
| 6 | `GET /api/subscription-plans` | `ProductFamilies.ListProductsForProductFamily` |
| 7 | `POST /api/subscriptions` — validate plan → claim → ensure customer → create subscription → record | `ListProductsForProductFamily`, `Customers.ReadCustomerByReference`, `Customers.CreateCustomer`, `Subscriptions.CreateSubscription`, `Customers.ListCustomerSubscriptions` (reconcile) |
| 8 | `GET /api/my-subscriptions` (Maxio is system of record; also settles pending claims) | `ReadCustomerByReference`, `ListCustomerSubscriptions` |
| 9 | Error mapping in `ExceptionMiddleware`; offline tests (stub `HttpMessageHandler`) | — |

## 2. CONTRACT SHEET

> ⚠ Signatures are generated code, verbatim — each operation that takes input takes ONE request record as its first parameter, built with an object initializer using the record's own property names; never flat arguments. Pass `cancellationToken:` by name (a `RequestOptions?` sits before it).
> ⚠ Every SDK type is written with the namespace its own source path implies (`Requests/Customers/*` → `MaxioAdvancedBilling.Requests.Customers`, `Models/*` → `.Models`, `Models/Enums/*` → `.Models.Enums`, `Models/AnyOf/*` → `.Models.AnyOf`, `Errors/*` → `.Errors`, `Core/Exceptions/*` → `.Core.Exceptions`, `Core/ErrorResponse/*` → `.Core.ErrorResponse`), never from a neighbouring type.

| Controller · method | Request record (members) | Body model (fields) | Response + fields read | Error | Paging | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `client.ProductFamilies` · `ListProductsForProductFamily(ListProductsForProductFamilyRequest request, RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)` | `ProductFamilyId: string, required` (id **or** `handle:<handle>`) · `Page: int = 1` `[Minimum(1)]` · `PerPage: int = 20` `[Maximum(200)]` · `IncludeArchived: bool?` (left unset) · others unused | — (GET) | `IReadOnlyList<ProductResponse>`; `ProductResponse.Product (product): Product, required` → `Id (id) int?`, `Name (name) string?`, `Handle (handle) string?`, `Description (description) string?`, `PriceInCents (price_in_cents) long?`, `Interval (interval) int?`, `IntervalUnit (interval_unit) IntervalUnit?`, `ArchivedAt (archived_at) DateTimeOffset?` | Case A `ApiException<ListProductsForProductFamilyError>`: `TryGetString(out string)` [404] · `TryGetRawError(out RawError)` [fallback] | none declared — hand-driven `Page`/`PerPage` | `map/operations/ProductFamilies.md`; `Requests/ProductFamilies/ListProductsForProductFamilyRequest.cs`; `Models/ProductResponse.cs`; `Models/Product.cs`; `Errors/ListProductsForProductFamilyError.cs` |
| `client.Customers` · `ReadCustomerByReference(ReadCustomerByReferenceRequest request, RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)` | `Reference: string, required` | — (GET `/customers/lookup.json`) | `CustomerResponse.Customer (customer): Customer, required` → `Id (id) int?`, `Reference (reference) string?` | Case B `ApiException<RawError>` | none | `map/operations/Customers.md`; `Requests/Customers/ReadCustomerByReferenceRequest.cs`; `Models/CustomerResponse.cs`; `Models/Customer.cs` |
| `client.Customers` · `CreateCustomer(CreateCustomerOperationRequest request, RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)` | `Body: CreateCustomerRequest?` (nothing required — set it) | `CreateCustomerRequest.Customer (customer): CreateCustomer, required` → `FirstName (first_name) string, required`, `LastName (last_name) string, required`, `Email (email) string, required`, `Reference (reference) string?` (set — unique per site per `<remarks>`). Left out: address/phone/vat/cc_emails/organization etc. | `CustomerResponse.Customer.Id` | Case A `ApiException<CreateCustomerError>`: `TryGetCustomerErrorResponse1(out CustomerErrorResponse1)` [422] · `TryGetRawError(out RawError)` [fallback]; `CustomerErrorResponse1.Errors (errors): Errors1?` (AnyOf: `TryGetCustomerError(out CustomerError)` → `Customer (customer) string?`; `TryGetListOfString(out IReadOnlyList<string>)`) | none | `map/operations/Customers.md`; `Requests/Customers/CreateCustomerOperationRequest.cs`; `Models/CreateCustomerRequest.cs`; `Models/CreateCustomer.cs`; `Errors/CreateCustomerError.cs`; `Models/CustomerErrorResponse1.cs`; `Models/AnyOf/Errors1.cs`; `Models/CustomerError.cs` |
| `client.Subscriptions` · `CreateSubscription(CreateSubscriptionOperationRequest request, RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)` | `Body: CreateSubscriptionRequest?` (set it) | `CreateSubscriptionRequest.Subscription (subscription): CreateSubscription, required` → `ProductHandle (product_handle) string?` ("Required, unless a product_id is given") · `CustomerId (customer_id) int?` ("Required, unless customer_reference or customer_attributes") · `Reference (reference) string?` (app's reference for the subscription) · `PaymentCollectionMethod (payment_collection_method) CollectionMethod?` (`Models/Enums/CollectionMethod.cs`: `Automatic` automatic · `Remittance` remittance · `Prepaid` prepaid · `Invoice` invoice) — set from `Maxio:PaymentCollectionMethod` (default `remittance`). Left out: price points, coupons, payment profile/card attrs, components | `SubscriptionResponse.Subscription (subscription): Subscription?` (nullable!) | Case A `ApiException<CreateSubscriptionError>`: `TryGetErrorListResponse1(out ErrorListResponse1)` [422] → `Errors (errors): IReadOnlyList<string>, required` · `TryGetRawError(out RawError)` [fallback] | none | `map/operations/Subscriptions.md`; `Requests/Subscriptions/CreateSubscriptionOperationRequest.cs`; `Models/CreateSubscriptionRequest.cs`; `Models/CreateSubscription.cs`; `Errors/CreateSubscriptionError.cs`; `Models/ErrorListResponse1.cs` |
| `client.Customers` · `ListCustomerSubscriptions(ListCustomerSubscriptionsRequest request, RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)` | `CustomerId: int, required` | — (GET) | `IReadOnlyList<SubscriptionResponse>` → `Subscription?` → `Id (id) int?`, `State (state) SubscriptionState?`, `Reference (reference) string?`, `ProductPriceInCents (product_price_in_cents) long?`, `CurrentPeriodEndsAt (current_period_ends_at) DateTimeOffset?` (next scheduled charge), `NextAssessmentAt (next_assessment_at) DateTimeOffset?`, `ActivatedAt`, `CreatedAt`, `Currency (currency) string?`, `Product (product) Product?` (null when no product, per `<remarks>`) | Case B `ApiException<RawError>` | none (returns all) | `map/operations/Customers.md`; `Requests/Customers/ListCustomerSubscriptionsRequest.cs`; `Models/SubscriptionResponse.cs`; `Models/Subscription.cs` |

**Enums needed**
- `IntervalUnit` (`Models/Enums/IntervalUnit.cs`): `Day`="day", `Month`="month"; read `.Value`, never `ToString()`.
- `SubscriptionState` (`Models/Enums/SubscriptionState.cs`): `Pending` pending · `FailedToCreate` failed_to_create · `Trialing` · `Assessing` · `Active` · `SoftFailure` · `PastDue` · `Suspended` · `Canceled` · `Expired` · `Paused` · `Unpaid` · `TrialEnded` · `OnHold` · `AwaitingSignup`; surface `.Value`.
- `ServerEnvironment` (`Servers/ServerEnvironment.cs`, closed): `Us`="US" (default), `Eu`="EU", `MaxioApiGateway`; resolve via `TryGetKnownValue`.

**Client / auth / server**
- Ctor: `new MaxioAdvancedBillingClient(HttpClient httpClient, MaxioAdvancedBillingClientOptions options)` (`MaxioAdvancedBillingClient.cs`).
- Auth: `options.BasicAuth = new MaxioAdvancedBilling.Core.Authentication.Basic.BasicAuthCredentials { Username = <API key>, Password = "x" }` — US/EU only (sdk-map *Servers & auth*). `BearerAuth` not set.
- Server group used: `Production` only. `options.Server.Production.Us.Site` / `.Eu.Site` = subdomain; `options.Server.Production.Us.BaseUrl` / `.Eu.BaseUrl` = override (literal URL used as-is) (`Servers/ProductionOptions.cs`).
- Options: `Retry: RetryOptions` (`Core/Configuration/RetryOptions.cs`, start from `RetryOptions.Default() with {…}`), `Logging: LoggingOptions` (`Core/Configuration/LoggingOptions.cs`: `LoggerFactory`, `LogRequestBody`, …), `Environment`.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| `planHandle` accepted by `POST /api/subscriptions` must be a non-archived product handle returned for the configured family | `CreateSubscription` ← `ListProductsForProductFamily` (`handle:{Maxio:ProductFamilyHandle}`) | `SubscriptionService.SubscribeAsync`, before the claim and before `CreateSubscription` |
| `CustomerId` sent to `CreateSubscription` / `ListCustomerSubscriptions` must be the id of the customer this app created/looked up for the caller's own reference | `CreateSubscription`, `ListCustomerSubscriptions` ← `CreateCustomer` / `ReadCustomerByReference` (stored in `BillingCustomer`) | `SubscriptionService.EnsureCustomerAsync` (only source of the id) |

## 3. Trap notes

| Step | Hazard → consequence | Skill |
| --- | --- | --- |
| 3 | `HttpClient` lifetime / singleton + DNS staleness; which `HttpClient` the DI extension resolves → shared default client or stale connections | MUST load `maxio:dotnet-client-initialization` |
| 3 | Unset credential is sent as no credential (401 one layer away) | MUST load `maxio:dotnet-authentication` |
| 3 | What `Retry.Timeout` vs `HttpClient.Timeout` vs a token actually bound → the 30 s rule can be silently broken | MUST load `maxio:dotnet-configuration-resilience` |
| 3 | Env-var logging switch and unredacted JSON bodies → customer email/name in logs | MUST load `maxio:dotnet-configuration-resilience` |
| 3 | Environment captured at construction vs server options re-read → base-URL override on the wrong environment is ignored | MUST load `maxio:dotnet-configuration-resilience` |
| 5 | Case A vs Case B catch per operation; `TryGetRawError` ordering → a typed 422 silently dropped | MUST load `maxio:dotnet-error-handling` |
| 5 | Connection/timeout leaves a write's outcome unknown → duplicate customer/subscription on caller retry | MUST load `maxio:dotnet-configuration-resilience` |
| 6 | Hand-driven page loop stop conditions → unbounded loop or silent truncation | MUST load `maxio:dotnet-configuration-resilience` |
| 6/8 | Open enums & AnyOf errors body reading → debug-form strings / wrong accessor | MUST load `maxio:dotnet-models` |
| 7 | Request-record shape, `requestOptions` position | MUST load `maxio:dotnet-calling-endpoints` |
| 9 | Which seam to fake; request body disposed after the call | MUST load `maxio:dotnet-testing` |

## 4. REQUIRED READING (load all before implementation; this sheet deliberately does not carry their contents)

- `maxio:dotnet-client-initialization` (this plugin's copy) — step 3
- `maxio:dotnet-authentication` — step 3
- `maxio:dotnet-configuration-resilience` — steps 3, 5, 6, 7
- `maxio:dotnet-error-handling` — steps 5, 9
- `maxio:dotnet-calling-endpoints` — steps 6–8
- `maxio:dotnet-models` — steps 6–8
- `maxio:dotnet-testing` — step 9
- Hazard (verbatim): a body that does not match its declared type — a drifted or malformed **2xx** response (a missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch ladder that handles only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).

## 5. PRODUCTION READINESS

| # | Concern | Decision |
| --- | --- | --- |
| 1 | Credential fail-fast | `MaxioSettings` bound from `Maxio:`; `ValidateOnStart` with custom validation: `ApiKey`, `ProductFamilyHandle` non-blank; `Subdomain` non-blank unless `BaseUrl` set; `BaseUrl` (if set) absolute https/http URI; `Environment` ∈ {US, EU}. Host refuses to start; message names the key, never the value. Password half is the constant `x` (provider doc). |
| 2 | Secret sourcing & rotation | Dev: .NET user-secrets (PublicApi `UserSecretsId`); prod: `Maxio__ApiKey` env / secret store; `MAXIO_*` env vars mapped as lowest-priority fallback. Options captured once in the singleton client → rotation requires restart (accepted; documented). |
| 3 | Total timeout budget | Every API request's Maxio work runs under ONE deadline: `Maxio:RequestBudgetSeconds` (default 25, validated 1–29) linked to `RequestAborted`; per-attempt `Retry.Timeout` = `Maxio:AttemptTimeoutSeconds` (8 s) and `HttpClient.Timeout` = attempt + 2 s (10 s) as backstops. Writes reserve 5 s of the budget for reconciliation. Expiry → 504 "Maxio did not respond". Enforced in `MaxioBillingGateway`. |
| 4 | Write-retry ownership | `HttpMethodsToRetry` left at default (GET/HEAD/PUT/OPTIONS). Our writes are `POST` (CreateCustomer, CreateSubscription) → never resent by SDK; GETs retried (bounded by deadline). No PUT in scope. |
| 5 | Idempotency & ambiguous writes | No caller-supplied key on either request record (`CreateCustomerOperationRequest`/`CreateSubscriptionOperationRequest` carry only `Body`); injected `Idempotency-Key` header is not a key. CreateCustomer: deterministic `reference` (`eshop-` + SHA-256 of normalized user name), provider-unique → re-read by `ReadCustomerByReference`. CreateSubscription: `reference` = `eshop-sub-{attemptId}` stored on the claim → re-read via `ListCustomerSubscriptions` matching `Reference`. |
| 6 | Observability | SDK logger = host `ILoggerFactory` (request line Info, failures Warning/Error, URL query masked by SDK allow-list); `LogRequestBody`/headers off. Our logs: user-claim key, plan handle, Maxio ids, status codes; Maxio 422 error strings logged at Warning. Maxio error bodies declare no correlation id; we log `ex.StatusCode` + request method/URI path from the exception. |
| 7 | Sensitive data | CreateCustomer carries name + email (PII) → `LogRequestBody=false`, `LoggerFactory` assigned explicitly (env var `MAXIOADVANCEDBILLINGCLIENT_LOG` inert); never log request bodies or emails ourselves. No card data in scope. |
| 8 | Environment selection | Group `Production` only (Ebb/Oauth unused). `Maxio:Environment` US (default)/EU selects `ServerEnvironment`; `Site` = `Maxio:Subdomain`; `Maxio:BaseUrl` overrides BaseUrl verbatim on the selected env. SDK has no sandbox env: test traffic isolation = the sandbox site's subdomain/API key (dev user-secrets); automated tests use a stub handler + fake base URL — no network. |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. |
| 10 | Partial results | See PAGED READS. |
| 11 | Unknown outcomes | See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| CreateSubscription (per user + plan) | `CatalogContext.SubscriptionEnrollments`, PK (`BuyerId`,`PlanHandle`), row inserted `Pending` before any Maxio write | EF primary-key uniqueness → `DbUpdateException` (SQL Server) / `ArgumentException` (in-memory) on `SaveChanges` | `SubscriptionClaimStore.TryInsertAsync` catch → returns false; `SubscriptionService.SubscribeAsync` then reads the existing enrollment (Active → idempotent 200; fresh Pending → 409; stale Pending/terminal → reconcile or release) | `SubscriptionClaimStore.TryClaimEnrollmentAsync` → `MaxioBillingGateway.CreateSubscriptionAsync` |
| CreateCustomer (per user) | `CatalogContext.BillingCustomers`, PK `BuyerId`, row inserted `Pending` before `CreateCustomer` | EF primary-key uniqueness (same as above); second line: provider-unique customer `reference` (422) → `ReadCustomerByReference` | `SubscriptionClaimStore.TryInsertAsync` catch → returns false; `SubscriptionService.EnsureCustomerAsync` reads the row (has id → use it; fresh pending → lookup by reference, else 409) | `SubscriptionClaimStore.TryClaimCustomerAsync` → `MaxioBillingGateway.CreateCustomerAsync` |

Release: claim row deleted when Maxio definitively refuses (4xx); stale `Pending` (older than 2 min) is settled by reconciliation on the next POST/GET.

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `ListProductsForProductFamily` (hand-driven `Page`, `PerPage` = 200) | page cap 5 (≤ 1000 plans) + request deadline; stop when a page returns < `PerPage` | `PlanCatalog.IsTruncated` → `ListSubscriptionPlansResponse.Truncated` (JSON `truncated: true`) | `MaxioBillingGateway.ListPlansAsync` sets `PlanCatalog.IsTruncated` |

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| CreateCustomer | `Customers.ReadCustomerByReference` | customer `reference` (`eshop-<hash>`) | `MaxioBillingGateway.CreateCustomerAsync` catch → `TryFindCustomerAfterWriteAsync`; not found → `BillingOutcomeUnknownException`, claim stays `Pending` (settled by the reference lookup in `SubscriptionService.EnsureCustomerAsync` / `FindCustomerIdAsync` on the next call) | `MaxioBillingGatewayTests.CreateCustomer_connection_failure_settles_by_reference_lookup` |
| CreateSubscription | `Customers.ListCustomerSubscriptions` | subscription `reference` (`eshop-sub-<attemptId>`) | `MaxioBillingGateway.CreateSubscriptionAsync` catch → `TryFindSubscriptionAfterWriteAsync`; not found → `BillingOutcomeUnknownException`, enrollment stays `Pending`; settled by `SubscriptionService.SettlePendingEnrollmentsAsync` (GET my-subscriptions) and `SubscriptionService.FindHeldSubscriptionAsync` (POST retry) | `MaxioBillingGatewayTests.CreateSubscription_connection_failure_settles_by_reference_lookup`; `SubscriptionEndpointsTest.Unknown_write_outcome_is_settled_on_the_next_request` |

## 6. Assumptions & Blockers

- Assumption: one subscription per (user, plan); re-subscribing the same plan is allowed only after the Maxio subscription is `canceled`/`expired`/`failed_to_create`. A different plan creates a second subscription (no upgrade/downgrade flow in scope).
- Assumption: eShop identity for billing = JWT `ClaimTypes.Name` (user name); the token carries no user-id claim.
- UNVERIFIED: `ReadCustomerByReference` signals "no such customer" with HTTP 404 (Case B, not documented) — code treats 404 as absent, anything else as an error (never as absence).
- UNVERIFIED: whether Maxio enforces uniqueness of subscription `reference` — not relied on; local claim is the guard.
- UNVERIFIED: the 404 body of `ListProductsForProductFamily` is a JSON string — code also catches `ResponseDeserializationException` with status 404 as "family not found".
- Blockers: none.

## 7. Implementation notes (after live sandbox verification, 2026-10-05)

- Live finding: with the site-default collection (`automatic`) Maxio answered `CreateSubscription` with 422 "No payment method was on file for the $299.00 balance". Fix grounded in `Models/CreateSubscription.cs` (`payment_collection_method`: `remittance`/`automatic`/`prepaid` on Relationship Invoicing, `invoice`/`automatic` on legacy): new setting `Maxio:PaymentCollectionMethod`, default `remittance`, validated at startup via `CollectionMethod.TryGetKnownValue`. Verified live: subscription created `active`.
- Store-loss safety (in-memory DB restart): before `CreateSubscription`, `SubscriptionService.SubscribeAsync` reads `ListCustomerSubscriptions` and adopts a live subscription to the same plan instead of creating a second one (Maxio is the system of record). Verified live after a restart.
- HTTP mapping (`ExceptionMiddleware`): Timeout → 504 "Maxio did not respond within N seconds." · Unreachable/RateLimited → 503 · Rejected (400/422) → 422 with Maxio's messages · Misconfigured/ProviderError → 502 · in-progress claim → 409. Verified live with an unroutable `Maxio:BaseUrl`: 504 at ~25 s on GET and POST.
- Write deadline: writes run under `RequestBudget − WriteReconciliationReserve` (`MaxioBillingGateway._writeDeadline`) so the reconciliation read still fits in the budget.
