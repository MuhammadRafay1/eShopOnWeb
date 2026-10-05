# Maxio Advanced Billing integration plan — eShopOnWeb subscriptions

## 1. Scope & sequence

Additive capability on `src/PublicApi` (JWT-auth): browse plans → subscribe → see my subscriptions. Maxio is the system of record; no local persistence of subscriptions (in-memory DB caveat + repo is published).

Steps (in order):

1. **Config & DI** — bind `Maxio:` section keys `ApiKey`, `Subdomain`, `ProductFamilyHandle`, `BaseUrl`; fail-fast on blank required parts; construct `MaxioAdvancedBillingClient` singleton. (No operation.)
2. **`GET /api/subscription-plans`** — `ProductFamilies.ListProductsForProductFamily` (walks pages until short page; throws at safety cap). Uses `Maxio:ProductFamilyHandle` as `"handle:" + handle`.
3. **`POST /api/subscriptions`** — in-process per-user+plan claim (semaphore) → `Subscriptions.FindSubscription` (idempotent pre-check) → ensure Maxio customer (`Customers.ReadCustomerByReference`, on 404 `Customers.CreateCustomer`) → validate plan handle ∈ family plans (`ProductFamilies.ListProductsForProductFamily`) → `Subscriptions.CreateSubscription` (product_handle + customer_id + deterministic subscription reference) → map response.
4. **`GET /api/my-subscriptions`** — `Customers.ReadCustomerByReference` (404 ⇒ empty list) → `Customers.ListCustomerSubscriptions`.
5. **Tests** — endpoint-level integration tests with the SDK stubbed behind `ISubscriptionBillingService`; plus `dotnet test` of the existing suite.

Layering follows repo convention (exemplar: `ITokenClaimsService` in ApplicationCore, `IdentityTokenClaimService` in Infrastructure, endpoint in PublicApi): interface + result records in `ApplicationCore/Interfaces/Billing/`, Maxio-backed implementation in `Infrastructure/MaxioBilling/`, MinimalApi `IEndpoint` classes in `PublicApi/SubscriptionEndpoints/` (exemplar: `CatalogItemListPagedEndpoint.cs`).

## CONTRACT SHEET

⚠ Signatures below are generated code, verbatim: each operation that takes input takes **ONE request record** as its first parameter (an operation with no inputs takes none), built with an object initializer whose property names are the record's own, never flat arguments.
⚠ Every SDK type is written fully-qualified with the namespace its source path implies, taken from the path the map gives for THAT type, never from where a neighbouring type sits.

| Operation (controller) | Signature · request record | Body model (wire names) | Response (fields read) | Error case | Pagination | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `client.ProductFamilies.ListProductsForProductFamily` | `Task<IReadOnlyList<ProductResponse>> ListProductsForProductFamily(ListProductsForProductFamilyRequest request, RequestOptions? = null, CancellationToken = default)` · record members: `ProductFamilyId: string, required` (id or `"handle:"+handle`), `Page: int = 1`, `PerPage: int = 20` ([Maximum(200)]), `Filter`, `IncludeArchived: bool?`, `Include` — none marked required except `ProductFamilyId` | none (GET) | `ProductResponse` → `Product: Product` (required). `Product` fields read: `Id: int?` (`id`), `Name: string?` (`name`), `Handle: string?` (`handle`), `Description: string?` (`description`), `PriceInCents: long?` (`price_in_cents`), `Interval: int?` (`interval`), `IntervalUnit: IntervalUnit?` (`interval_unit` — enum values `day`, `month`), `RequireCreditCard: bool?` (`require_credit_card`), `ArchivedAt: DateTimeOffset?` (`archived_at`) | **A**: `ApiException<ListProductsForProductFamilyError>` · `TryGetString(out string)` [404] · `TryGetRawError(out RawError)` fallback | offset-based `page`/`per_page` (defaults above) | `map/operations/ProductFamilies.md`; `Requests/ProductFamilies/ListProductsForProductFamilyRequest.cs`; `Models/ProductResponse.cs`; `Models/Product.cs`; `Models/Enums/IntervalUnit.cs` |
| `client.Customers.ReadCustomerByReference` | `Task<CustomerResponse> ReadCustomerByReference(ReadCustomerByReferenceRequest request, ...)` · required: `Reference: string` | none (query `reference`) | `CustomerResponse` → `Customer: Customer` (required). `Customer` fields read: `Id: int?` (`id`), `Reference: string?` (`reference`), `Email: string?` (`email`) | **B**: `ApiException<RawError>` · `StatusCode` / `ReadAsString()` | none | `map/operations/Customers.md`; `Models/CustomerResponse.cs`; `Models/Customer.cs` |
| `client.Customers.CreateCustomer` | `Task<CustomerResponse> CreateCustomer(CreateCustomerOperationRequest request, ...)` · record members: `Body: CreateCustomerRequest?` | `CreateCustomerRequest` → `Customer: CreateCustomer` (required). `CreateCustomer`: `FirstName: string, required` (`first_name`), `LastName: string, required` (`last_name`), `Email: string, required` (`email`), `Reference: string?` (`reference`). Optional fields left out: cc_emails, organization, address*, city, state, zip, country, phone, locale, vat*, entity_identifier*, tax_exempt*, surcharging, parent_id, salesforce_id, branding_theme_id | same envelope as ReadCustomerByReference | **A**: `ApiException<CreateCustomerError>` · `TryGetCustomerErrorResponse1(out CustomerErrorResponse1)` [422] (`CustomerErrorResponse1.Errors: Errors1?` — not read, only presence) · `TryGetRawError(out RawError)` fallback | none | `map/operations/Customers.md`; `Requests/Customers/CreateCustomerOperationRequest.cs`; `Models/CreateCustomerRequest.cs`; `Models/CreateCustomer.cs` |
| `client.Subscriptions.FindSubscription` | `Task<SubscriptionResponse> FindSubscription(FindSubscriptionRequest request, ...)` · record members: `Reference: string?` (no required members) | none (query `reference`; route `GET /subscriptions/lookup.json`, "Finds a subscription by its reference") | `SubscriptionResponse` → `Subscription: Subscription?` (nullable!) | **A**: `ApiException<FindSubscriptionError>` · `TryGetNoContent(out RawError)` [404] · `TryGetRawError(out RawError)` fallback | none | `map/operations/Subscriptions.md`; `Api/Subscriptions.cs` (FindSubscription remarks); `Models/SubscriptionResponse.cs` |
| `client.Subscriptions.CreateSubscription` | `Task<SubscriptionResponse> CreateSubscription(CreateSubscriptionOperationRequest request, ...)` · record members: `Body: CreateSubscriptionRequest?` | `CreateSubscriptionRequest` → `Subscription: CreateSubscription` (required). Members set: `ProductHandle: string?` (`product_handle`), `CustomerId: int?` (`customer_id`), `Reference: string?` (`reference`). Optional fields left out: product_id, price-point fields, custom_price, coupon*, payment_collection_method, net_terms, branding_theme_id, next_billing_at, initial_billing_at, defer_signup, stored_credential_transaction_id, sales_rep_id, payment_profile_id, customer_attributes, payment/credit/bank attributes, components, calendar_billing, metafields, customer_reference, group, ref, cancellation*, currency, expires_at, expiration_tracks_next_billing_change, agreement/ACH fields, calendar_billing_first_charge, reason_code, product_change_delayed, offer_id, prepaid_configuration, previous_billing_at, import_mrr, canceled_at, activated_at, agreement_acceptance, dunning*, skip_billing_manifest_taxes | `Subscription` fields read: `Id: int?`, `State: SubscriptionState?` (`state`), `ProductPriceInCents: long?` (`product_price_in_cents`), `CurrentPeriodEndsAt: DateTimeOffset?` (`current_period_ends_at`), `NextAssessmentAt: DateTimeOffset?` (`next_assessment_at`), `ActivatedAt: DateTimeOffset?` (`activated_at`), `Customer: Customer?`, `Product: Product?` | **A**: `ApiException<CreateSubscriptionError>` · `TryGetErrorListResponse1(out ErrorListResponse1)` [422] (`ErrorListResponse1.Errors: IReadOnlyList<string>, required`) · `TryGetRawError(out RawError)` fallback | none | `map/operations/Subscriptions.md`; `Requests/Subscriptions/CreateSubscriptionOperationRequest.cs`; `Models/CreateSubscriptionRequest.cs`; `Models/CreateSubscription.cs`; `Models/Subscription.cs`; `Models/ErrorListResponse1.cs` |
| `client.Customers.ListCustomerSubscriptions` | `Task<IReadOnlyList<SubscriptionResponse>> ListCustomerSubscriptions(ListCustomerSubscriptionsRequest request, ...)` · required: `CustomerId: int` | none | same subscription envelope as CreateSubscription | **B**: `ApiException<RawError>` | none (returns full customer list) | `map/operations/Customers.md`; `Requests/Customers/ListCustomerSubscriptionsRequest.cs` |

Enum value table (the only one needed):

| Enum | Members (C# = wire) | Source |
| --- | --- | --- |
| `SubscriptionState` (namespace `MaxioAdvancedBilling.Models.Enums`) | `Active` = `active`, `Canceled` = `canceled`, `Expired` = `expired`, `PastDue` = `past_due`, `Trialing` = `trialing`, … (15 values, compared by identity or via `Value`) | `Models/Enums/SubscriptionState.cs` |
| `IntervalUnit` (same namespace) | `Day` = `day`, `Month` = `month` | `Models/Enums/IntervalUnit.cs` |

Client construction / auth / server-node facts (source: `sdk-map.md`, *Getting a client*, *Servers & auth*):

- `MaxioAdvancedBillingClient` sole ctor: `MaxioAdvancedBillingClient(HttpClient httpClient, MaxioAdvancedBillingClientOptions options)`. Client groups: `client.Customers`, `client.ProductFamilies`, `client.Subscriptions`.
- Auth: HTTP Basic — `options.BasicAuth = new BasicAuthCredentials { Username = <Maxio API key>, Password = "x" }` (namespace `MaxioAdvancedBilling.Core.Authentication.Basic`).
- Environments: `ServerEnvironment.Us` (default) → `Production` group template `https://{site}.chargify.com`; `{site}` defaults to `"subdomain"`, override point `options.Server.Production.Us.Site` (namespace `MaxioAdvancedBilling.Servers` for the enum). Every in-scope operation resolves through the `Production` group.
- `Maxio:BaseUrl` optional override: set verbatim via `options.Server.Production.Us.BaseUrl` (template-override point named by the map's server table). When unset, derive host from `Maxio:Subdomain` via `options.Server.Production.Us.Site`.
- The generator injects `Idempotency-Key: Guid.NewGuid()` on CreateCustomer/CreateSubscription — **not** an idempotency key (fresh per call) and must not be cited as one.
- Request records live in `MaxioAdvancedBilling.Requests` (+ `.Customers` / `.Subscriptions` / `.ProductFamilies`); models in `MaxioAdvancedBilling.Models`; enums in `MaxioAdvancedBilling.Models.Enums`; typed errors in `MaxioAdvancedBilling.Errors`; `ApiException<T>`/`RawError` in `MaxioAdvancedBilling.Core.Exceptions` / `MaxioAdvancedBilling.Core.ErrorResponse`. A catch naming several needs a `using` per namespace.
- No-throw `…Result` siblings: **absent across this SDK** — throw-only.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| The `planHandle` accepted by `POST /api/subscriptions` must be one of the product handles returned by `ListProductsForProductFamily` for the configured family (the same set `GET /api/subscription-plans` offers) | `CreateSubscription` ← `ListProductsForProductFamily` | `MaxioSubscriptionBillingService.SubscribeAsync` — it calls `ListProductsForProductFamily` and rejects (returns a not-found result → HTTP 400/404 to caller) any `planHandle` not in that set, before `CreateSubscription` is called. `GET /api/subscription-plans` is served from the same read, so both read one set. |
| The `customerId` passed to `CreateSubscription` must be one `ReadCustomerByReference`/`CreateCustomer` returned for this app user | `CreateSubscription` ← `ReadCustomerByReference` (+ `CreateCustomer`) | `SubscribeAsync` — the customer id is only ever taken from the ensure-customer step's response for the user's deterministic reference, never from caller input |
| The `customerId` passed to `ListCustomerSubscriptions` must come from the same ensure-customer lookup | `ListCustomerSubscriptions` ← `ReadCustomerByReference` | `ListSubscriptionsAsync` — same rule |

## Trap notes

- Client construction/ownership: rebuilding the `HttpClient`/handler pipeline per request is wrong; the SDK client wrapper may be transient but its pipeline must be long-lived — **MUST load dotnet-client-initialization**.
- Credentials: set before the client exists; blank-part multi-part credential is not a missing one; must fail fast at startup — **MUST load dotnet-authentication**.
- Request records/envelope shapes: every operation input is one request record; response payloads wrap one level down (`ProductResponse.Product`, `SubscriptionResponse.Subscription` nullable); wire names differ from C# names — **MUST load dotnet-calling-endpoints**.
- OpenStringEnum `SubscriptionState`/`IntervalUnit` are records with static members, not C# enums; no public factory — **MUST load dotnet-models**.
- Error boundary: Case A vs Case B per operation; drifted/malformed 2xx or non-matching error bodies surface as `ResponseDeserializationException` (an `ApiException` that is NOT `ApiException<TError>`) and escape a ladder that only catches `ApiException<TError>` — **MUST load dotnet-error-handling**.
- Retry/timeout: `Timeout` is per attempt and a hung retryable call multiplies it; `POST` is never resent by default but `PUT` is; total budget needs a `CancellationToken` deadline; `MAXIOADVANCEDBILLINGCLIENT_LOG` env var can force body logging if `Logging.LoggerFactory` is unset — **MUST load dotnet-configuration-resilience**.
- Testing the SDK seam: which constructor argument to fake and how to keep tests off SDK internals — **MUST load dotnet-testing**.

## REQUIRED READING

Loaded before implementation starts (this sheet deliberately does not carry their contents):

- **dotnet-client-initialization** (this SDK, from the maxio-sdk plugin copy) — governs Step 1, client/DI registration.
- **dotnet-authentication** — governs Step 1, credential binding and fail-fast.
- **dotnet-calling-endpoints** — governs Steps 2–4, first SDK call sites.
- **dotnet-models** — governs Steps 2–4, enum/union/model handling.
- **dotnet-error-handling** — governs the error boundary in the service layer.
- **dotnet-configuration-resilience** — governs Step 1 options (retries, timeouts, logging posture) and unknown-outcome reconciliation.
- **dotnet-testing** — governs Step 5.

## PRODUCTION READINESS

| # | Concern | The decision |
| --- | --- | --- |
| 1 | Credential fail-fast | `Maxio:ApiKey`/`Subdomain`/`ProductFamilyHandle` bound via `IOptions<MaxioBillingOptions>` + `ValidateOnStart()` in PublicApi `Program.cs` registration helper; every part checked non-blank, so the host refuses to start on a missing/blank part rather than 401-ing later. `Maxio:BaseUrl` optional — validated as absolute URI only when present. |
| 2 | Secret sourcing & rotation | Secret comes from user-secrets/env (`Maxio:ApiKey`); the DI registration builds the `MaxioAdvancedBillingClientOptions` **once at registration** and captures it in the singleton client — a rotated key takes effect only after a restart. Accepted: key rotation for this app is a redeploy/restart operation. |
| 3 | Total timeout budget | SDK `Timeout` is per attempt (default retry ladder); endpoints get a hard `CancellationToken` deadline of **20 s** (linked with the request's token in the service layer), enforced inside `MaxioSubscriptionBillingService` — that is the number the caller actually gets. |
| 4 | Write-retry ownership | SDK may resend `GET`/`HEAD`/`PUT`/`OPTIONS` only (`POST` never resent by default) — the SDK will never re-send `CreateCustomer`/`CreateSubscription` (both POST); safe-but-slow GETs are capped by the 20 s token. |
| 5 | Idempotency & ambiguous writes | No real caller-supplied idempotency key exists on either write's request record (checked: `CreateSubscription` has no key-shaped member; the generator-injected `Idempotency-Key: Guid.NewGuid()` header is not one). Idempotency is app-level: deterministic Maxio references (`eshop-user:{userId}`, `eshop-sub:{userId}:{planHandle}`) + pre-checks + reconciliation. See DUPLICATE CLAIMS / UNKNOWN OUTCOMES below. |
| 6 | Observability | Service logs at Information for subscribe/list start-finish and Warning/Error for provider failures (status + `ErrorListResponse1.Errors` strings); no request bodies; `options.Logging` keeps `LogRequestBody` off and `LoggerFactory` assigned explicitly (see row 7). Provider correlation: error body strings + SDK exception `StatusCode` are logged; Maxio offers no single correlation id header in scope. |
| 7 | Sensitive data | In-scope request models carry PII (`CreateCustomer`: first/last name, email). Therefore `LogRequestBody` stays off **and** `options.Logging.LoggerFactory` is assigned the app's `ILoggerFactory` explicitly, so `MAXIOADVANCEDBILLINGCLIENT_LOG=trace` cannot switch body logging on from outside the code. App never echoes request bodies on those paths. |
| 8 | Environment selection | SDK declares 2 server groups (`Production`, `Ebb`) × environments (`Us` default → `https://{site}.chargify.com`, `Eu` → `https://{site}.ebilling.maxio.com`). Scope touches only `Production`. Default: `Environment=Us`, `Server.Production.Us.Site = Maxio:Subdomain`. When `Maxio:BaseUrl` set, it overrides `Server.Production.Us.BaseUrl` verbatim (test/prod split by config, never by code). No traffic to `Ebb`. Sandbox isolation: credentials + subdomain point at the sandbox site; the same build switches sites by config only. |
| 9 | Duplicate prevention under concurrency | See DUPLICATE CLAIMS. Claims live in the process (`ConcurrentDictionary`-keyed `SemaphoreSlim`), and durable idempotency anchors live in Maxio itself (references), so a restart or second instance cannot double-create: the pre-check + provider-reference reconciliation settle it. |
| 10 | Partial results | `ListProductsForProductFamily` is paged; the service walks pages until a short page; if the safety cap (10 pages) is hit with a still-full page it throws a dedicated exception → HTTP 503 with a distinct message rather than silently truncating. `ListCustomerSubscriptions` returns the full list in one call (no pagination on the operation). |
| 11 | Unknown outcomes | A connection failure after `CreateSubscription` may have acted at Maxio. The failing write's own `catch` re-reads via `FindSubscription(reference)`; found ⇒ the created subscription is returned as success, not found ⇒ failure is safe to report (and a retry is safe because the pre-check would find it). See UNKNOWN OUTCOMES. |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `CreateCustomer` (ensure-customer) | Process-wide `ConcurrentDictionary<string, SemaphoreSlim>` in the singleton billing service, keyed by customer reference `eshop-user:{userId}` — second caller blocks until the first finishes, then the post-claim `ReadCustomerByReference` finds the existing customer and no create happens; durable anchor = Maxio customer `reference` | The semaphore claim (in-process) + the pre-claim read; across restarts/instances, a racing create surfaces as 422 duplicate-reference | `SubscribeAsync` semaphore acquisition; `ApiException<CreateCustomerError>` 422 handler re-reads by reference | TBD |
| `CreateSubscription` (double-click POST) | Same claim store, keyed `eshop-sub:{userId}:{planHandle}`; durable anchor = Maxio subscription `reference` | The semaphore claim + `FindSubscription(reference)` pre-check returning the existing subscription | `SubscribeAsync` semaphore acquisition; `FindSubscription` hit short-circuits; 422 `TryGetErrorListResponse1` path re-checks `FindSubscription` before surfacing | TBD |

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `ListProductsForProductFamily` (plans) | Safety cap of 10 pages × `PerPage` 200 | It is never silently cut: hitting the cap with a full last page throws `MaxioBillingException` (distinct type) → endpoint returns HTTP 503 with an explicit "plan catalog page budget exceeded" message | TBD |

### UNKNOWN OUTCOMES

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `CreateSubscription` | `Subscriptions.FindSubscription` | the deterministic subscription reference `eshop-sub:{userId}:{planHandle}` passed to the create | TBD | TBD |

(`CreateCustomer` is also a write whose connection can fail after acting; its settle path is the `ReadCustomerByReference` in the *next* call or the 422-reconcile re-read in `SubscribeAsync`.)

## Assumptions & Blockers

- **Assumption (YOUR CALL — not in the map):** the JWT carries `ClaimTypes.Name` = username (verified in `IdentityTokenClaimService.cs`); endpoints resolve the `ApplicationUser` via `UserManager.FindByNameAsync` and use its Id/Email. No blockers.
- **Assumption (YOUR CALL):** one subscription per (user, plan) — re-subscribing to the same plan returns the existing subscription (its Maxio reference makes it idempotent). Whether Maxio enforces unique subscription references is **UNVERIFIED**; the 422-reconcile path (`FindSubscription` after 422) covers both behaviours.
- **Assumption (YOUR CALL):** plan display currency is USD, derived from `price_in_cents`/`product_price_in_cents` (integer cents).
- **Assumption (YOUR CALL):** `MAXIO_ENVIRONMENT` selects no additional bind key — the mandated bind keys are exactly `Maxio:ApiKey`, `Maxio:Subdomain`, `Maxio:ProductFamilyHandle`, `Maxio:BaseUrl`; the default `ServerEnvironment.Us` server template is the correct host family for this sandbox site (subdomain on `chargify.com`).
- **Assumption (YOUR CALL):** app user's email local-part is the fallback first/last name for the Maxio customer when the username is a single token.
- **Blockers: none.**