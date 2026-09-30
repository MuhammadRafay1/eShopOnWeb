# Implementation Plan — PayPal payments & saved cards for eShopOnWeb

This plan was written by a planning-only session (no code was changed to produce it — this
`PLAN.md` is the only file that session touched). It is split into two parts:

- **Part A** (this part) — the application architecture: domain model changes, persistence,
  configuration, endpoint-by-endpoint design, idempotency strategy, error handling, security,
  reconciliation design, build order, and testing strategy. Every open design call the task
  left to the implementer is decided here.
- **Part B** (below, headed "PayPal .NET SDK Integration Plan — eShopOnWeb PublicApi") — the
  PayPal .NET SDK **contract sheet**, produced by the `paypal-sdk` agent (per the mandatory
  `paypal-sdk` marketplace plugin) and grounded entirely in its bundled SDK map/source. It has
  its own internal numbering (1.–5.) independent of Part A's. Treat every signature, wire name,
  and enum value in Part B as authoritative — Part A's endpoint designs call directly into it.

The build session should read Part A first for the "what/where/why", then Part B for the exact
SDK calls each step in Part A makes, then follow Part B's own `paypal-sdk`/`dotnet-*` skill
process (spawn `paypal-sdk` once more only if a genuinely new contract fact is needed — Part B
already answers everything this plan's design required).

---

# Part A — Application architecture and design decisions

## A.1 Overview

eShopOnWeb's `Order`/`OrderItem` model today is written once at checkout and never touched
again — no payment, no status. This plan adds, additively, on top of the existing model:

- An `OrderStatus` lifecycle on `Order` itself (`AwaitingPayment → PaymentAuthorized →
  Fulfilled → PartiallyRefunded/Refunded`, with `Cancelled` as a side branch before fulfilment).
- A new `Payment` aggregate (1:1 with an `Order` by `OrderId`, not an EF navigation — see
  A.3.2) that holds everything PayPal owns: the PayPal order id, the current authorization id
  and status, the capture id/status/amount/fee/net, and a child list of `PaymentRefund`s.
- A new `PaymentMethod` aggregate for saved cards, keyed by `BuyerId` (the same username string
  `Order.BuyerId` already uses), holding only a PayPal vault id and a display-safe card summary
  (brand, last 4 digits, expiry) — never a PAN.
- A new `IPaymentGatewayService` port in `ApplicationCore`, implemented in `Infrastructure` on
  top of the PayPal .NET SDK per Part B, so `ApplicationCore` never takes a compile-time
  dependency on the SDK (same separation the project already uses for `IEmailSender`).
- Nine new endpoints in `src/PublicApi`, using the project's existing `MinimalApi.Endpoint`
  conventions, wired to JWT auth exactly like the existing Catalog endpoints.

Nothing about the existing catalog/basket/checkout flow changes. `Order`/`OrderItem`/
`CatalogItemOrdered` are reused as-is (per the task's own requirement) with one addition
(`Order.Status`) and no breaking changes to their existing constructors' call sites (the
existing `OrderService.CreateOrderAsync` basket-checkout path keeps working unmodified —
new orders it creates simply start in `OrderStatus.AwaitingPayment` like any other `Order`,
even though nothing in the Web storefront ever calls `pay`/`fulfil` on them; that's fine,
they're just never carried further, exactly like today).

## A.2 Layering — where new code goes

Follows the repo's existing Clean Architecture split exactly (confirmed from
`IEmailSender`/`IOrderService`/`IUriComposer` all living the same way):

| Layer | New code |
|---|---|
| `ApplicationCore` | `IPaymentGatewayService` port + plain result/request records (no SDK types), `Order.Status` + transition methods, `Payment`/`PaymentRefund`/`PaymentMethod` entities, specifications, new exception types |
| `Infrastructure` | `PayPalPaymentGatewayService : IPaymentGatewayService` (the only place `AsadAli.Checkout.Sdk`/`PayPalServerSdk` is referenced), `PayPalOptions`, EF configs for the three new entity types, DI wiring (`Dependencies.cs` or a new `PayPalDependencies.cs`) |
| `PublicApi` | Nine endpoint classes (below), request/response DTOs, `ExceptionMiddleware` extensions for the new exception types |

**Hard rule:** `ApplicationCore.csproj` must **not** reference `AsadAli.Checkout.Sdk`. If a
build ever needs an SDK type in `ApplicationCore`, that is a sign the port (`IPaymentGatewayService`)
is leaking the wrong abstraction — translate to a plain type in `Infrastructure` instead.

**Naming collision to avoid:** the PayPal SDK's own `PayPalServerSdk.Models.Enums.OrderStatus`
enum (`CREATED`/`APPROVED`/`PAYER_ACTION_REQUIRED`/…, see Part B §2.9) has the exact same short
name as the new eShop domain enum `Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate.OrderStatus`
this plan defines below (§A.3.1). Any file in `Infrastructure` that needs both (i.e.
`PayPalPaymentGatewayService.cs`) must alias the SDK one to keep them unambiguous:
```csharp
using PayPalOrderStatus = PayPalServerSdk.Models.Enums.OrderStatus;
```
Never `using PayPalServerSdk.Models.Enums;` unqualified in a file that also uses the eShop
`OrderStatus` type.

## A.3 Domain model changes (`ApplicationCore`)

### A.3.1 `Order` — add a status

New file `src/ApplicationCore/Entities/OrderAggregate/OrderStatus.cs`:
```csharp
public enum OrderStatus
{
    AwaitingPayment,
    PaymentAuthorized,
    Fulfilled,
    PartiallyRefunded,
    Refunded,
    Cancelled
}
```

Modify `src/ApplicationCore/Entities/OrderAggregate/Order.cs`:
- Add `public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;` (private
  setter; existing constructor unchanged, so every existing call site — including
  `OrderService.CreateOrderAsync` — keeps compiling and new/legacy orders both start
  `AwaitingPayment`).
- Add behavior methods, each using `Ardalis.GuardClauses` the same way `EmptyBasketOnCheckoutException`-style
  guards are used elsewhere in this codebase, throwing the new `InvalidOrderStateException`
  (§A.9) on an illegal transition — these are the **single source of truth** for the state
  machine, not duplicated per-endpoint:
  - `MarkPaymentAuthorized()` — legal only from `AwaitingPayment`.
  - `MarkFulfilled()` — legal only from `PaymentAuthorized`.
  - `MarkCancelled()` — legal only from `AwaitingPayment` or `PaymentAuthorized`.
  - `MarkRefunded(bool isFullRefund)` — legal only from `Fulfilled` or `PartiallyRefunded`; sets
    `Status = isFullRefund ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded`.
  Each method is a **no-op guard, not a throw**, for the exact state it would already be in as
  a *result* of the same action (e.g. calling `MarkCancelled()` when already `Cancelled` should
  not throw — the endpoint layer decides idempotent-return-200 vs conflict, see §A.6; give the
  domain method a `CurrentlyCancellable`/`CurrentlyPayable`/etc. read-only predicate per state
  instead of only a throwing mutator, so endpoints can check-before-calling without try/catch
  for the expected idempotent-replay case).

### A.3.2 `Payment` and `PaymentRefund` — new aggregate

New folder `src/ApplicationCore/Entities/PaymentAggregate/`:

```csharp
// Payment.cs
public class Payment : BaseEntity, IAggregateRoot
{
    public int OrderId { get; private set; }           // FK by value only — NOT a navigation
                                                          // property on Order. Payment and Order
                                                          // stay separate aggregate roots, exactly
                                                          // like Order and Basket already are in
                                                          // this codebase; cross-aggregate reads
                                                          // are stitched in the endpoint layer via
                                                          // two repository calls (§A.7), not via
                                                          // EF .Include() across aggregates.
    public string Currency { get; private set; }
    public decimal Amount { get; private set; }          // the order total this Payment secures

    public string PayPalOrderId { get; private set; }
    public string AuthorizationId { get; private set; }  // latest — overwritten on reauthorize
    public string AuthorizationStatus { get; private set; } // raw PayPal wire string, e.g. "CREATED"
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }

    private readonly List<PaymentRefund> _refunds = new();
    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();

    public decimal TotalRefunded => _refunds.Sum(r => r.Amount);
    public decimal RemainingRefundable => (CapturedAmount ?? 0m) - TotalRefunded;

    // Constructor + RecordAuthorization(...)/RecordReauthorization(...)/RecordCapture(...)/
    // AddRefund(...) mutator methods — each just assigns fields; no PayPal calls happen inside
    // the entity, those live in IPaymentGatewayService/the endpoint. Guard.Against.NegativeOrZero
    // etc. on amounts.
}

// PaymentRefund.cs — NOT an IAggregateRoot; only reachable through Payment.Refunds.
public class PaymentRefund : BaseEntity
{
    public int PaymentId { get; private set; }
    public string PayPalRefundId { get; private set; }
    public decimal Amount { get; private set; }
    public string Status { get; private set; }            // raw PayPal wire string
    public string IdempotencyKey { get; private set; }     // the caller-supplied key (§A.6)
    public DateTimeOffset CreatedAt { get; private set; }
}
```

### A.3.3 `PaymentMethod` — repurpose the existing dead scaffolding

The repo already has `src/ApplicationCore/Entities/BuyerAggregate/PaymentMethod.cs` and
`Buyer.cs`, but **both are unused dead code today**: `PaymentMethod` has no public constructor,
only `Alias`/`CardId`/`Last4`, is not an `IAggregateRoot`, has no `DbSet`, no EF configuration,
and is never constructed anywhere; `Buyer` (keyed by `IdentityGuid`) is never created or seeded
either. Reviving `Buyer` just to hang saved cards off it would mean building an entire unused
buyer-provisioning flow this task doesn't ask for.

**Decision:** move `PaymentMethod.cs` out of `BuyerAggregate` into a new
`src/ApplicationCore/Entities/PaymentAggregate/PaymentMethod.cs`, keep the class name (no
collision — the old one is deleted, not duplicated), and make it a **standalone aggregate root
keyed by `BuyerId : string`**, mirroring exactly how `Order.BuyerId` already works (a plain
username string from `User.Identity.Name`, not a foreign key to any `Buyer` row):

```csharp
public class PaymentMethod : BaseEntity, IAggregateRoot
{
    public string BuyerId { get; private set; }
    public string PayPalVaultId { get; private set; }   // never expose this over the API
    public string Brand { get; private set; }             // e.g. "VISA" — from CardBrand (Part B §2.9)
    public string LastDigits { get; private set; }
    public string Expiry { get; private set; }             // "YYYY-MM"
    public DateTimeOffset CreatedAt { get; private set; }
}
```
Leave `Buyer.cs` and its own `PaymentMethod`-shaped comment untouched — it stays unused dead
code, out of scope, harmless. Do not delete `Buyer.cs`; deleting unrelated dead code is not
part of this task.

### A.3.4 New exceptions (`ApplicationCore/Exceptions/`, same one-line style as `DuplicateException`)

```csharp
public class InvalidOrderStateException : Exception { public InvalidOrderStateException(string message) : base(message) {} }
public class PaymentGatewayException : Exception
{
    public string? PayPalErrorName { get; }
    public string? PayPalDebugId { get; }
    public PaymentGatewayException(string message, string? payPalErrorName = null, string? payPalDebugId = null) : base(message)
    { PayPalErrorName = payPalErrorName; PayPalDebugId = payPalDebugId; }
}
public class PaymentAuthorizationNotRenewableException : Exception { public PaymentAuthorizationNotRenewableException(string reason) : base(reason) {} }
```
`OrderNotFoundException`/`PaymentMethodNotFoundException` are **not** added — see §A.9 on why
not-found is returned inline as `Results.NotFound()` rather than thrown.

### A.3.5 `IPaymentGatewayService` (`ApplicationCore/Interfaces/`)

Plain-type port — every parameter/return type below is a primitive or a record defined in
`ApplicationCore` itself, never a `PayPalServerSdk.*` type:

```csharp
public interface IPaymentGatewayService
{
    Task<AuthorizationResult> AuthorizeAsync(AuthorizationRequest request, CancellationToken ct = default);
    Task<AuthorizationSnapshot> GetAuthorizationStatusAsync(string authorizationId, CancellationToken ct = default);
    Task<AuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string orderIdForIdempotency, CancellationToken ct = default);
    Task VoidAuthorizationAsync(string authorizationId, string orderIdForIdempotency, CancellationToken ct = default);
    Task<CaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string orderIdForIdempotency, CancellationToken ct = default);
    Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct = default);
    Task<VaultedCardResult> VaultCardAsync(CardDetails card, CancellationToken ct = default);
    Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct = default);
    Task<IReadOnlyList<ReconciliationTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

public record CardDetails(string Name, string Number, string Expiry, string SecurityCode, BillingAddress? BillingAddress);
public record BillingAddress(string? Line1, string? Line2, string? City, string? State, string? PostalCode, string CountryCode);
public record AuthorizationRequest(decimal Amount, string Currency, CardDetails? Card, string? VaultId, string idempotencyKeyBase);
public record AuthorizationResult(bool PayerActionRequired, string? PayPalOrderId, string? AuthorizationId, string? Status, DateTimeOffset? ExpiresAt);
public record AuthorizationSnapshot(string Status, DateTimeOffset? ExpiresAt);
public record CaptureResult(string CaptureId, string Status, decimal Amount, decimal? PayPalFee, decimal? NetAmount, string Currency);
public record RefundResult(string RefundId, string Status, decimal Amount);
public record VaultedCardResult(string VaultId, string Brand, string LastDigits, string Expiry);
public record ReconciliationTransaction(string TransactionId, decimal Amount, string Currency, string Status, DateTimeOffset InitiatedAt);
```

`AuthorizeAsync` returning `PayerActionRequired = true` (rather than throwing) is deliberate:
it is an expected, first-class outcome (Part B §2.2's `OrderStatus.PayerActionRequired` check),
not a failure — the endpoint decides what HTTP response that becomes (§A.7.2).

`PayPalPaymentGatewayService` (Infrastructure) implements this by calling Part B's operations
in sequence and translating every SDK model field named in Part B into these plain records —
it is the only file that does that translation. Any `SdkException<T>` it cannot resolve into a
first-class outcome (declines, PayPal-side rejections, the "cannot renew" case) is translated
into `PaymentGatewayException`/`PaymentAuthorizationNotRenewableException` with the PayPal
`Error.Name`/`Message`/`DebugId` (or `Error1` for Vault ops, per Part B §2.7) copied in verbatim
— never swallowed, never re-derived into an invented code.

## A.4 Persistence (`Infrastructure`)

- `src/Infrastructure/Data/CatalogContext.cs`: add `DbSet<Payment> Payments`,
  `DbSet<PaymentMethod> PaymentMethods` (no separate `DbSet<PaymentRefund>` — it's reached only
  through `Payment.Refunds`).
- New EF configs under `src/Infrastructure/Data/Config/`, added the same way `OrderConfiguration`
  already is (picked up automatically by the existing `ApplyConfigurationsFromAssembly` call —
  no change needed to that call itself):
  - `PaymentConfiguration.cs` — `HasIndex(p => p.OrderId).IsUnique()` (enforces the 1:1 with
    `Order` at the DB level even though there's no navigation property); `HasMany` (not
    `OwnsMany`) to `PaymentRefund` with `SetPropertyAccessMode(PropertyAccessMode.Field)` on the
    `_refunds` backing field, same pattern as `OrderConfiguration` does for `OrderItems`.
  - `PaymentRefundConfiguration.cs` — `HasIndex(r => new { r.PaymentId, r.IdempotencyKey }).IsUnique()`
    — this is the DB-level backstop for "repeating a request under the same key must not refund
    twice" (see §A.6): even a would-be race between two requests bearing the same key hits a
    unique-constraint violation on `SaveChangesAsync`, not just an application-level check.
  - `PaymentMethodConfiguration.cs` — `HasIndex(m => m.BuyerId)` (not unique — one buyer can
    save many cards).
- **Migration:** generate an EF Core migration for `CatalogContext`
  (`dotnet ef migrations add AddPaymentsAndSavedCards --project src/Infrastructure --startup-project src/PublicApi`)
  so the change is real and deployable against SQL Server, even though the in-memory provider
  used for local dev/verification on this machine ignores migrations entirely (per the task's
  own environment note). Review the generated `Up()` for correctness; it cannot be applied
  against a real SQL Server here (no LocalDB), so this is a static-review step, not a run step.

## A.5 Configuration & secrets

New `src/Infrastructure/PayPalOptions.cs`:
```csharp
public class PayPalOptions
{
    public const string ConfigSectionName = "PayPal";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Environment { get; set; } = "sandbox";  // informational only — see Part B GAP-1
    public string Currency { get; set; } = "USD";
    public string? BaseUrl { get; set; }                   // optional override, Part B §2.1
}
```
Bind with `builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection(PayPalOptions.ConfigSectionName));`
in the new `Infrastructure.ConfigurePayPalServices(...)` (§A.6 below) — standard `IOptions<T>`
binding, matching the exact keys `PayPal:ClientId`/`PayPal:ClientSecret`/`PayPal:Environment`/
`PayPal:Currency`/`PayPal:BaseUrl` the task mandates. **No value is ever hardcoded** anywhere in
`appsettings*.json` or code.

**Getting the env vars into that exact nested shape — do not build an env-var name-bridge.**
The task's own build-phase constraints already say the build session must read
`PAYPAL_CLIENT_ID`/`PAYPAL_CLIENT_SECRET`/`PAYPAL_ENVIRONMENT`/`PAYPAL_CURRENCY` from its shell
and load them into **.NET user-secrets** — and user-secrets supports nested keys directly:
```
dotnet user-secrets set "PayPal:ClientId" "$PAYPAL_CLIENT_ID" --project src/PublicApi
dotnet user-secrets set "PayPal:ClientSecret" "$PAYPAL_CLIENT_SECRET" --project src/PublicApi
dotnet user-secrets set "PayPal:Environment" "$PAYPAL_ENVIRONMENT" --project src/PublicApi
dotnet user-secrets set "PayPal:Currency" "$PAYPAL_CURRENCY" --project src/PublicApi
```
(and `PayPal:BaseUrl` the same way, only if an override is actually needed). This sidesteps the
mismatch between the given env var names (`PAYPAL_CLIENT_ID`, single underscore) and ASP.NET
Core's default double-underscore env-var-to-config-key convention (`PayPal__ClientId`) entirely
— no bridging code is needed in `Program.cs`. This requires:
- Adding `<UserSecretsId>` (a fresh GUID) to `src/PublicApi/PublicApi.csproj`.
- `WebApplication.CreateBuilder` already adds user secrets automatically in `Development`
  when `UserSecretsId` is present on the entry assembly — no explicit
  `builder.Configuration.AddUserSecrets<Program>()` call is required, but add one defensively
  under `if (builder.Environment.IsDevelopment())` right after `CreateBuilder` if it's ever
  unclear whether `ASPNETCORE_ENVIRONMENT=Development` is actually set when running locally.

Never write any of these values into `appsettings.json`, `appsettings.Development.json`,
`appsettings.test.json`, a launch profile, a test fixture, or this plan.

## A.6 DI wiring

New `src/Infrastructure/PayPalDependencies.cs`:
```csharp
public static class PayPalDependencies
{
    public static void ConfigurePayPalServices(IConfiguration configuration, IServiceCollection services)
    {
        services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.ConfigSectionName));
        services.AddHttpClient("PayPalServerSdk"); // long-lived handler pool — see Part B §2.1's
                                                     // ⚠ re: IHttpClientFactory; follow
                                                     // dotnet-client-initialization for the
                                                     // exact factory/registration shape.
        services.AddScoped<IPaymentGatewayService, PayPalPaymentGatewayService>();
    }
}
```
Call **only** from `src/PublicApi/Program.cs` (immediately after the existing
`Microsoft.eShopWeb.Infrastructure.Dependencies.ConfigureServices(...)` call):
```csharp
Microsoft.eShopWeb.Infrastructure.PayPalDependencies.ConfigurePayPalServices(builder.Configuration, builder.Services);
```
**Deliberately not** added to `src/Web`'s DI setup — Web never calls `IPaymentGatewayService`
(no storefront UI is required), so it should not need a `PayPal` config section at all, and
should not pay for an unused `HttpClient` registration.

Exact `PayPalServerSdkClient` construction (which options object shape, whether it's built once
as a singleton wrapping the named `HttpClient` or built per-scope) is governed by
`dotnet-client-initialization` (Part B REQUIRED READING) — follow that skill's guidance
verbatim rather than re-deriving a lifetime/DI shape here.

## A.7 Endpoints (`src/PublicApi`)

### A.7.0 Endpoint-authoring pattern (confirmed against the actual `MinimalApi.Endpoint` package)

The installed package (`MinimalApi.Endpoint` v1.3.0) only ships `IEndpoint<TResponse>`,
`IEndpoint<TResponse, TRequest>`, `IEndpoint<TResponse, TRequest1, TRequest2>`, and
`IEndpoint<TResponse, TRequest1, TRequest2, TRequest3>` — there is no `TDependency`-branded
generic slot; the repo's own convention of writing `IEndpoint<IResult, XRequest, IRepository<X>>`
is just using the package's generic 2-request-like-slots arity for "request + one dependency".
**Confirmed from `CatalogItemListPagedEndpoint.cs` (already in this repo) that the `AddRoute`
lambda is not limited to the interface's arity**: that endpoint's lambda already takes 5
parameters (`int? pageSize, int? pageIndex, int? catalogBrandId, int? catalogTypeId,
IRepository<CatalogItem> itemRepository`) while the class implements only the 2-slot interface,
packing everything into one request DTO before calling `HandleAsync(request, itemRepository)`.
Use the same pattern for every new endpoint:

- Implement `IEndpoint<IResult, TRequest, TDependency>` where `TDependency` is the one
  repository/service most central to the endpoint (usually `IRepository<Order>` or
  `IRepository<PaymentMethod>`).
- Inject any **other** services the endpoint needs (`IPaymentGatewayService`,
  `IOptions<PayPalOptions>`, `IRepository<Payment>`, `IMapper`) via the **endpoint class's own
  constructor** — already precedented in this exact package/repo (`CatalogItemListPagedEndpoint`
  constructor-injects both `IUriComposer` and `IMapper` alongside its route-lambda-injected
  repository).
- Get the caller's identity by adding a `ClaimsPrincipal user` parameter directly to the
  `MapPost`/`MapGet` lambda (ASP.NET Core minimal APIs bind this automatically from
  `HttpContext.User`, no DI registration needed) and set it onto the request DTO (e.g.
  `request.BuyerId = user.Identity!.Name!;`) before calling `HandleAsync`, exactly the way
  `CatalogItemListPagedEndpoint`'s lambda already builds its request DTO inline from bound
  parameters before calling `HandleAsync`. This is the first endpoint in this project to read
  the current user's identity this way — there is no existing precedent to deviate from, and no
  interface change is needed because the identity is folded into the request object the
  interface already expects.
- Route registration/auth attribute style is unchanged from the existing admin endpoints:
  `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` for shopper-scoped
  routes (any authenticated user), and additionally `Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS`
  for the three operator routes (fulfil, cancel, reconciliation) — identical attribute shape to
  `CreateCatalogItemEndpoint.cs` today, so no new authorization plumbing is introduced.

### A.7.1 Folder layout

```
src/PublicApi/OrderEndpoints/
  CreateOrderEndpoint.cs (+ Request/Response)
  PayOrderEndpoint.cs (+ Request/Response)
  FulfilOrderEndpoint.cs (+ Response)
  CancelOrderEndpoint.cs (+ Response)
  RefundOrderEndpoint.cs (+ Request/Response)
  GetMyOrdersEndpoint.cs (+ Response)
  OrderItemDto.cs, PaymentDto.cs   // shared response shapes
src/PublicApi/PaymentMethodEndpoints/
  SavePaymentMethodEndpoint.cs (+ Request/Response)
  ListPaymentMethodsEndpoint.cs (+ Response)
  DeletePaymentMethodEndpoint.cs
src/PublicApi/ReconciliationEndpoints/
  ReconciliationEndpoint.cs (+ Response)
```
Every request/response DTO derives from `BaseRequest`/`BaseResponse` (the existing
`CorrelationId()` convention) exactly like `CreateCatalogItemRequest`/`Response` do.

### A.7.2 `POST api/orders` — `CreateOrderEndpoint`

Auth: `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` (any
authenticated user). Dependency: `IRepository<Order>` (route lambda) + `IRepository<CatalogItem>`
and `IUriComposer` (constructor — same two services `OrderService.CreateOrderAsync` already
uses to build `CatalogItemOrdered`/`OrderItem`).

Request: `{ items: [{ catalogItemId, quantity }], shippingAddress?: { street, city, state, country, zipCode } }`.
`shippingAddress` is optional — `Order`'s existing constructor requires a non-null `Address`,
but this new creation path has no basket/checkout page supplying one; if omitted, construct
`new Address("", "", "", "", "")`. Shipping is not part of what this task tests.

Logic: `Guard` items non-empty, quantities > 0; load requested `CatalogItem`s via
`CatalogItemsSpecification` (existing); 400 (`Results.BadRequest`) inline, no exception, if any
requested id doesn't resolve. Build `OrderItem`s exactly like `OrderService.CreateOrderAsync`
does (current `CatalogItem.Price` as `UnitPrice`, `CatalogItemOrdered` snapshot via
`IUriComposer.ComposePicUri`). `new Order(user.Identity!.Name!, address, items)` — starts
`AwaitingPayment` by the field initializer. `AddAsync`. Return 201 with
`{ correlationId, orderId, status: "AwaitingPayment", total, currency: PayPal:Currency, items }`.

### A.7.3 `POST api/orders/{orderId}/pay` — `PayOrderEndpoint`

Auth: shopper-scoped, any authenticated user. Ownership: load the order by id; if it doesn't
exist **or** `order.BuyerId != user.Identity!.Name` → `Results.NotFound()` (not 403 — see §A.9
on why not-found beats forbidden for cross-tenant access).

Request: `{ card?: { name, number, expiry, securityCode, billingAddress }, paymentMethodId?: int }`
— exactly one of the two must be set (400 otherwise). If `paymentMethodId` is set, load that
`PaymentMethod`, 404 if missing or not owned by the caller, and use its `PayPalVaultId`.

State check (idempotent-in-effect, per the task's explicit requirement):
- `order.Status != AwaitingPayment`: if it's already `PaymentAuthorized` or later, **return 200
  with the existing stored `Payment` snapshot** (no new PayPal call) — a double-click is a no-op
  success, not an error. If it's `Cancelled`, return 409 via `InvalidOrderStateException`.
- `order.Status == AwaitingPayment`: proceed to call PayPal.

Call `IPaymentGatewayService.AuthorizeAsync(new AuthorizationRequest(order.Total(),
payPalOptions.Currency, card, vaultId, idempotencyKeyBase: $"order:{order.Id}"))`. The
implementation threads that base into **two** deterministic PayPal-Request-Id values (Part B
§2.2's two-call `CreateOrder`→`AuthorizeOrder` sequence): `$"create-order:{orderId}"` and
`$"authorize:{orderId}"` — see §A.8 for why they must differ per call, not just per order.

- If `AuthorizationResult.PayerActionRequired == true`: **do not** persist a `Payment`, leave
  `order.Status` at `AwaitingPayment`, return **422 Unprocessable Entity** with a clear message
  ("This card requires shopper approval in a browser; this API does not support that flow — try
  a different card."). This is the task's explicitly anticipated "stop and report" case — if it
  is hit against the real sandbox Visa card during verification, report it rather than building
  a redirect flow (the task says so explicitly); it is not expected to happen with the given
  test card under a default `CardRequest` (Part B §2.2's default `ScaWhenRequired` is left
  unmodified — see the note at the end of §A.7.2/Part B §2.2's ⚠, this plan does not invent a
  specific alternate enum value to force off, since the task itself treats a real challenge as
  an expected, reportable outcome rather than a bug to engineer around).
- Otherwise: create the `Payment` (or update it, if `pay` is somehow reached twice concurrently
  — the unique index in §A.4 plus PayPal's own idempotent replay of the deterministic key is the
  actual safety net, see §A.8), `order.MarkPaymentAuthorized()`, `SaveChangesAsync` on both
  repositories. Return 200 with `{ correlationId, orderId, status, payment: { authorizationId,
  authorizationStatus, expiresAt } }`.
- Any `PaymentGatewayException` (a real decline/rejection) bubbles to `ExceptionMiddleware` →
  502 with the PayPal error details (§A.9); order stays `AwaitingPayment` so the shopper can
  retry with a different card.

### A.7.4 `POST api/orders/{orderId}/fulfil` — `FulfilOrderEndpoint`

Auth: `Roles = ADMINISTRATORS`. Not buyer-scoped — an admin may fulfil any order. 404 if the
order id doesn't exist at all.

State check: `Fulfilled` already → 200 idempotent no-op with the existing capture snapshot.
`AwaitingPayment`/`Cancelled`/refund states → 409 via `InvalidOrderStateException`.
`PaymentAuthorized` → proceed:

1. `GetAuthorizationStatusAsync(payment.AuthorizationId)`.
2. If `ExpiresAt` is in the past, or `Status` is one of the non-renewable values (Part B §2.4:
   `Voided`/`Denied`, or already `Captured`) → attempt `ReauthorizeAsync` only when the status is
   a plausible candidate (`Created`/`Pending`/`PartiallyCaptured`); for the definitively
   non-renewable statuses, skip straight to throwing `PaymentAuthorizationNotRenewableException`
   with an operator-actionable reason ("Authorization was voided/denied and cannot be renewed;
   cancel this order instead of fulfilling it.").
3. If reauthorization is attempted and PayPal rejects it, catch that and likewise throw
   `PaymentAuthorizationNotRenewableException`, with the verbatim PayPal `Error.Name`/`Message`/
   `DebugId` folded into the reason string (Part B §2.4's defensive directive — no assumed error
   code). If it succeeds, **overwrite `payment.AuthorizationId` with the id from the reauthorize
   response**, never the pre-reauthorize id (Part B §2.4's UNVERIFIED-but-resolved-defensively
   directive), and update `AuthorizationStatus`/`AuthorizationExpiresAt`.
4. `CaptureAsync(payment.AuthorizationId, payment.Amount, payment.Currency,
   orderIdForIdempotency: order.Id.ToString())` — the gateway builds the PayPal-Request-Id as
   `$"capture:{orderId}:{authorizationId}"` (including the authorization id, not just the order
   id, so a capture retried after a reauthorize-driven authorization-id change is not
   accidentally deduplicated against the pre-reauthorize attempt — see §A.8).
5. `payment` records `CaptureId`/`CaptureStatus`/`CapturedAmount`/`PayPalFee`/`NetAmount`;
   `order.MarkFulfilled()`.
6. Return 200: `{ correlationId, orderId, status: "Fulfilled", payment: { captureId,
   captureStatus, capturedAmount, payPalFee, netAmount, currency } }` — the exact fields the
   task requires an operator see after fulfilment.

`PaymentAuthorizationNotRenewableException` maps to **409 Conflict** with the reason as the
message (§A.9) — distinct from a generic `PaymentGatewayException`/502, because the task
explicitly asks for an operator-actionable statement here, not a generic gateway failure.

### A.7.5 `POST api/orders/{orderId}/cancel` — `CancelOrderEndpoint`

Auth: `Roles = ADMINISTRATORS`. 404 if order id doesn't exist.

- `Cancelled` already → 200 idempotent no-op.
- `Fulfilled`/`PartiallyRefunded`/`Refunded` → 409 ("cannot cancel a fulfilled order; use
  refund instead").
- `AwaitingPayment` → `order.MarkCancelled()` directly; no PayPal call (nothing was ever held).
- `PaymentAuthorized` → `VoidAuthorizationAsync(payment.AuthorizationId, order.Id.ToString())`
  (deterministic key `$"void:{orderId}:{authorizationId}"`); on success, record
  `AuthorizationStatus = "VOIDED"` on `payment`, `order.MarkCancelled()`. If PayPal rejects the
  void with a 409 (e.g. concurrently fulfilled by another admin request) — surface that as its
  own 409 ("order was concurrently fulfilled; it can no longer be cancelled, refund it instead")
  rather than a generic gateway error, since it's a legitimate, explainable race, not an outage.

Return 200: `{ correlationId, orderId, status: "Cancelled" }`.

### A.7.6 `POST api/orders/{orderId}/refunds` — `RefundOrderEndpoint`

Auth: shopper-scoped (this route is **not** in the task's operator list — only fulfil/cancel/
reconciliation are — so it is deliberately self-service, acting only on the caller's own order,
same ownership check as §A.7.3).

Request: `{ amount?: decimal, idempotencyKey: string }` — `idempotencyKey` required, 400 if
missing/empty. `amount` omitted = full refund of whatever remains captured.

State check: `Fulfilled`/`PartiallyRefunded` required; otherwise 409.

**Idempotency (exact mechanism, per the task's explicit requirement):** look up an existing
`PaymentRefund` by `(PaymentId, IdempotencyKey)` first (the unique index from §A.4 makes this a
simple, race-safe lookup-or-insert). If found, **return it as-is, unchanged, without calling
PayPal again** — this is the literal "repeating a request under the same key must not refund
twice" requirement. If not found:
- Compute `remaining = payment.RemainingRefundable`. If `request.Amount` is provided and
  `> remaining` → **400** ("refund amount exceeds remaining refundable balance of {remaining}
  {currency}") — an eShop-side guard in addition to whatever PayPal itself would also reject,
  satisfying "a partly-refunded order must never become refundable beyond what was captured" as
  a belt-and-suspenders check, not reliance on PayPal's error path alone.
- Call `RefundAsync(payment.CaptureId, request.Amount, payment.Currency,
  idempotencyKey: $"refund:{orderId}:{request.IdempotencyKey}")`. Passing `request.Amount` as
  `null` through to the SDK's `RefundRequest.Amount` (omitted) when the shopper asked for a full
  refund — Part B §2.6 confirms omitting refunds "the remaining captured amount" as PayPal
  itself computes it (i.e. correctly accounts for any prior partial refunds without eShop having
  to compute and pass an explicit remaining figure).
- On success: `payment.AddRefund(...)` (persists the new `PaymentRefund` row keyed by the
  caller's idempotency key); `order.MarkRefunded(isFullRefund: payment.RemainingRefundable <= 0m)`.
- On PayPal rejection (e.g. a genuine race past the eShop-side guard): `PaymentGatewayException`
  → 502, verbatim PayPal error details, no assumed error code (Part B §2.6's defensive directive).

Return 201: `{ correlationId, orderId, refundId, status, amount, currency, remainingRefundable }`
— `refundId` is eShop's own `PaymentRefund.Id` (top-level, per the task's response-identifier
requirement), not PayPal's refund id (which is still included, just not as the top-level id).

### A.7.7 `GET api/my-orders` — `GetMyOrdersEndpoint`

Auth: shopper-scoped. Query the caller's orders via the existing `CustomerOrdersSpecification`/
`CustomerOrdersWithItemsSpecification`-style pattern (already in
`src/ApplicationCore/Specifications/`), then a second query
`IRepository<Payment>.ListAsync(new PaymentsByOrderIdsSpecification(orderIds))` (new
specification, `Query.Where(p => orderIds.Contains(p.OrderId)).Include(p => p.Refunds)`), and
stitch the two lists together in the endpoint by `OrderId` (a simple in-memory join — this
project's scale doesn't call for a cross-aggregate SQL join, and keeping `Payment` a genuinely
separate aggregate, per §A.3.2, means it isn't reachable via a single EF query anyway). Return
`{ correlationId, orders: [{ orderId, status, total, currency, items: [...], payment: {
authorizationId, authorizationStatus, captureId, captureStatus, capturedAmount, payPalFee,
netAmount, refunds: [...] } | null }] }` (`payment` is `null` for an order that predates this
feature or was never paid).

### A.7.8 `POST api/payment-methods` — `SavePaymentMethodEndpoint`

Auth: shopper-scoped. Request: `{ cardholderName, cardNumber, expiry, securityCode,
billingAddress: { line1?, line2?, city?, state?, postalCode?, countryCode } }`. Call
`VaultCardAsync(new CardDetails(...))`. Persist `new PaymentMethod(user.Identity!.Name!,
result.VaultId, result.Brand, result.LastDigits, result.Expiry)`. Return 201:
`{ correlationId, paymentMethodId, brand, lastDigits, expiry }` — never the vault id, never the
PAN, never the CVV (which isn't stored anywhere after this call returns).

### A.7.9 `GET api/payment-methods` — `ListPaymentMethodsEndpoint`

Auth: shopper-scoped. `IRepository<PaymentMethod>` with a new `PaymentMethodsByBuyerSpecification(buyerId)`.
Return `{ correlationId, paymentMethods: [{ paymentMethodId, brand, lastDigits, expiry,
createdAt }] }`.

### A.7.10 `DELETE api/payment-methods/{paymentMethodId}` — `DeletePaymentMethodEndpoint`

Auth: shopper-scoped. Load by id; 404 if missing or `BuyerId != user.Identity!.Name`. Call
`DeleteVaultedCardAsync(method.PayPalVaultId)` **then** delete the local row — deleting at
PayPal too (not just locally) is what actually satisfies "must no longer be usable to pay" as a
PayPal-side fact, not merely an eShop-routing fact. Return **204 No Content**.

### A.7.11 `GET api/reconciliation?from={from}&to={to}` — `ReconciliationEndpoint`

Auth: `Roles = ADMINISTRATORS`. Parse `from`/`to` as `DateTimeOffset` (400 if unparseable or
`from > to`).

1. Call `SearchTransactionsAsync(from, to)` — the `Infrastructure` implementation loops every
   page internally per Part B §2.8 ("It covers the whole range, not just the first page") and
   returns the full flattened list; the endpoint itself does no paging logic.
2. Query local `Payment`s (`CreatedAt`-ish timestamp fields — add a `CapturedAt`/`RefundedAt`
   consideration, or simplest: treat every `Payment.CaptureId` and every `PaymentRefund` whose
   own `CreatedAt` falls in `[from, to]` as one local "transaction" with its own id (`CaptureId`
   or `PayPalRefundId`) and amount) plus their child `PaymentRefund`s in range.
3. Build a lookup set of local transaction ids (captures + refunds). For each PayPal transaction
   returned: `Matched` if its id is in that set, else `PayPalOnly`. For each local transaction:
   `Matched` if its id appeared in PayPal's result, else `EShopOnly` — an expected, non-error
   result when the range covers very recent activity, per the task's own note about PayPal's
   reporting lag; do not treat a nonempty `EShopOnly` list over a fresh range as a bug.
4. Return `{ correlationId, from, to, rows: [{ matchStatus, transactionId, amount, currency,
   payPalStatus?, eShopOrderId?, eShopKind? ("Capture"|"Refund") }], summary: { matchedCount,
   payPalOnlyCount, eShopOnlyCount } }`.

If `SearchTransactionsAsync` itself throws (e.g. PayPal rejects an overly wide range) let that
surface as `PaymentGatewayException` → 502 with PayPal's message verbatim — this plan does not
assert a specific maximum range in days anywhere (no such constant exists in the SDK map per
Part B §2.8; inventing one would violate "do not rely on general/external knowledge for PayPal
API details").

## A.8 Idempotency strategy (cross-cutting)

Two layers, deliberately not just one:

1. **PayPal-side** (Part B §2.10): every write op takes a `payPalRequestId` →
   `PayPal-Request-Id` header. This plan uses **deterministic**, not random, keys wherever an
   operation is tied to a specific eShop resource the caller might retry (place-order's
   `create-order:{orderId}`/`authorize:{orderId}`, fulfil's `capture:{orderId}:{authorizationId}`,
   cancel's `void:{orderId}:{authorizationId}`, refund's `refund:{orderId}:{callerIdempotencyKey}`)
   — so that even if two racing HTTP requests both pass the app-level status check before either
   commits (see point 2), PayPal itself deduplicates them into a single real-world effect. Vault
   `CreatePaymentToken` uses a fresh `Guid` per call instead — saving a card has no "retry the
   exact same save" requirement in the task, and each save is a deliberate new card.
2. **App-side**: `Order.Status` (and the `(PaymentId, IdempotencyKey)` unique index for refunds)
   is checked *before* calling PayPal, and a request that finds the order already past the state
   it would cause returns the **existing** stored result rather than erroring — this is what
   makes "a double-click never authorizes or captures the shopper twice" true from the caller's
   point of view, not just true at PayPal.
3. **Explicitly not built:** DB-level optimistic concurrency tokens (`RowVersion`) on `Order`/
   `Payment` to close the race between "check status" and "commit new status" at the database
   layer. The in-memory EF provider used for this task's verification has inconsistent support
   for concurrency tokens, and PayPal's own per-operation idempotency key (point 1) is the actual
   backstop for the specific "double-click" scenario the task describes — both racing requests
   still only ever produce one real authorization/capture/refund at PayPal, which is the
   observable guarantee the task asks for. Adding a second, redundant guard at the DB layer for
   a scenario PayPal already makes safe would be exactly the kind of unrequested abstraction the
   project's own conventions ask to avoid.

## A.9 Error handling / HTTP status mapping

Extend `src/PublicApi/Middleware/ExceptionMiddleware.cs`'s existing `if (exception is
DuplicateException ...)` chain (same style, just more branches — it already special-cases one
exception type today, so this is additive, not a redesign):

| Exception / situation | HTTP status |
|---|---|
| `DuplicateException` (existing) | 409 (unchanged) |
| `InvalidOrderStateException` | 409 |
| `PaymentAuthorizationNotRenewableException` | 409 |
| `PaymentGatewayException` | 502, body includes `PayPalErrorName`/`PayPalDebugId` when present |
| anything else (existing fallback) | 500 (unchanged) |

**Not-found is never thrown as an exception** — every endpoint that looks up an order/payment
method by id and finds nothing, *or* finds one belonging to a different buyer, returns
`Results.NotFound()` inline. Returning 404 (rather than 403 Forbidden) for "exists but isn't
yours" is deliberate: it means a shopper probing another buyer's order/payment-method id gets no
signal distinguishing "doesn't exist" from "exists but isn't yours" — directly satisfying "one
shopper must never see, use, or delete another's" without a separate leak channel through the
HTTP status code itself.

**Validation failures** (missing/malformed request fields, unresolvable catalog item ids,
refund amount exceeding remaining) are returned as `Results.BadRequest(...)` **inline**, not
thrown — consistent with keeping simple request-shape validation next to the request, and
exceptions reserved for state-machine/gateway conditions that could occur inside several
different endpoints and would otherwise duplicate their status-code mapping.

## A.10 Security

- **No PAN/CVV persistence, ever.** `CardDetails`/`Number`/`SecurityCode` exist only as
  in-memory values passed straight into `IPaymentGatewayService`; they are never assigned to any
  EF-tracked entity, never appear in any DTO returned to a caller, and the request DTOs that
  carry them (`PayOrderRequest.Card`, `SavePaymentMethodRequest`) must not be logged — this
  project has no request-body logging middleware today (confirmed), so this is a "don't add
  one without redaction" constraint on future changes, not a fix needed now.
- **`PaymentMethod`/`Payment` never expose PayPal's own ids externally where avoidable** —
  `PayPalVaultId` is never returned by any endpoint (§A.7.8/§A.7.9 only ever return
  brand/last-4/expiry); PayPal's own order/authorization/capture/refund ids *are* returned
  (the task explicitly requires the payment to "carry enough of the state PayPal owns... that a
  later request can act on it"), which is fine — those aren't secrets, unlike a vault id tied
  directly to a stored payment instrument.
- **Every shopper endpoint scopes by `BuyerId == user.Identity.Name`** — no endpoint accepts a
  buyer id from the request body/query string for a shopper-scoped route; it always comes from
  the validated JWT's claims, never from caller-supplied input (this is what makes cross-tenant
  access structurally impossible, not just checked).
- **Admin endpoints are impersonation-proof the same way the existing Catalog admin endpoints
  are** — `[Authorize(Roles = ADMINISTRATORS)]`, nothing new introduced.

## A.11 Testing strategy

- **`tests/UnitTests`** (xUnit + NSubstitute, references `ApplicationCore`): new
  `ApplicationCore/Entities/OrderTests/OrderStatusTransitionTests.cs` (every legal/illegal
  transition in §A.3.1, using the existing `Builders/OrderBuilder.cs`-style test-object-builder
  convention), `ApplicationCore/Entities/PaymentTests/PaymentTests.cs` (refund-total/remaining
  math, `RecordCapture`/`AddRefund` guards).
- **`tests/IntegrationTests`** (xUnit + NSubstitute + EF InMemory, references `Infrastructure`):
  new `PaymentGateway/PayPalPaymentGatewayServiceTests.cs` stubbing the SDK's `HttpClient` seam
  (Part B's own REQUIRED READING flags `dotnet-testing` for exactly this — the constructor
  argument to the SDK client is the test seam) so these run fast, deterministically, and without
  hitting the real PayPal sandbox or needing credentials in CI; assert the translation from SDK
  response shapes into the plain `AuthorizationResult`/`CaptureResult`/etc. records, and from
  `SdkException<T>` into `PaymentGatewayException`/`PaymentAuthorizationNotRenewableException`.
- **`tests/PublicApiIntegrationTests`** (MSTest + `WebApplicationFactory<Program>`, the existing
  `ApiTokenHelper.GetAdminUserToken()`/`GetNormalUserToken()` pattern): new
  `OrderEndpoints/`, `PaymentMethodEndpoints/`, `ReconciliationEndpoints/` folders. **Register a
  fake `IPaymentGatewayService` test double** via `WebApplicationFactory.WithWebHostBuilder(...)
  .ConfigureTestServices(services => services.Replace(ServiceDescriptor.Scoped<IPaymentGatewayService, FakePaymentGatewayService>()))`
  for this whole suite — these tests exist to prove routing/auth/ownership/state-machine
  correctness (e.g. the same "assert `HttpStatusCode.Forbidden` for a non-admin token hitting an
  admin-only route" pattern the existing `CreateCatalogItemEndpointTest.cs` already uses, applied
  to `fulfil`/`cancel`/`reconciliation`; a 404 test for one buyer's token against another buyer's
  order/payment-method), not to re-prove PayPal connectivity — real-sandbox verification is the
  task's own separately-required manual step (§A.12), and making this automated suite depend on
  live network calls to a third party would make it flaky and credential-dependent for no
  correctness benefit.

## A.12 Build order

1. `ApplicationCore`: `OrderStatus` enum + `Order` methods, `Payment`/`PaymentRefund`/
   `PaymentMethod` entities (moving/replacing the old `BuyerAggregate/PaymentMethod.cs`),
   `IPaymentGatewayService` + records, new exceptions, new specifications. Build.
2. Add the SDK package: `<PackageVersion Include="AsadAli.Checkout.Sdk" Version="1.0.1" />` to
   `Directory.Packages.props` (confirm the exact NuGet version string with the `paypal-sdk` agent
   if `1.0.1` doesn't resolve — Part B pins by git tag, which is not guaranteed to be identical
   to the published NuGet version), `<PackageReference Include="AsadAli.Checkout.Sdk" />` to
   `src/Infrastructure/Infrastructure.csproj`.
3. `Infrastructure`: `PayPalOptions`, `PayPalDependencies.ConfigurePayPalServices`,
   `PayPalPaymentGatewayService` (per Part B, step-by-step in Part B §1's own order), EF configs
   + `DbSet`s, migration. Build after each SDK operation is wired (Part B's own §1 sequencing).
4. `PublicApi`: `UserSecretsId`, the nine endpoints (§A.7), `ExceptionMiddleware` extension,
   Program.cs wiring (`ConfigurePayPalServices` call). Build.
5. Tests per §A.11.
6. Manual self-verification against the real PayPal sandbox (per the task's "Rules of
   engagement" — place an order, pay with the sandbox Visa card, fulfil, refund, save+reuse a
   card, run a reconciliation query over a range with known-older data) and write the concise
   verification guide the task asks for as the final deliverable of the build session.

---

# Part B — PayPal .NET SDK Integration Plan — eShopOnWeb PublicApi

SDK: `AsadAli.Checkout.Sdk` (root namespace `PayPalServerSdk`), tag `v1.0.1`. This plan and its
contract sheet ground every fact in the bundled SDK map (`sdk-map.md` + `map/operations/*.md` +
`map/models/*.md`) or, on a real map gap, the pinned SDK source. No fact below is invented.

---

## 1. Scope & sequence

Implement in this order (each step names the operations/models it uses):

1. **Client registration, auth, base-URL override** — `PayPalServerSdkClient` / `PayPalServerSdkClientOptions`,
   `OAuth2ClientCredentials`, `ServerOptions`/`DefaultOptions`/`ServerEnvironment`. No operation calls yet.
2. **Direct card authorization (no redirect)** — `Orders.CreateOrder` then `Orders.AuthorizeOrder`
   (intent `AUTHORIZE`, raw card in the `AuthorizeOrder` request body).
3. **Capture a stand-alone authorization** — `Payments.CaptureAuthorizedPayment`.
4. **Reauthorize a stale authorization** — `Payments.ReauthorizePayment`, with a pre-flight staleness
   check from the authorization's own fields.
5. **Void an authorization** — `Payments.VoidPayment`.
6. **Refund a capture (full/partial) with idempotency** — `Payments.RefundCapturedPayment`.
7. **Vault: save a card, pay with a saved token, delete a saved token** — `Vault.CreatePaymentToken`,
   `Orders.CreateOrder`/`Orders.AuthorizeOrder` with `CardRequest.VaultId`, `Vault.DeletePaymentToken`.
8. **Transaction-search reconciliation report** — `TransactionSearch.SearchTransactions`, looped over
   `page` until `TotalPages`.
9. **Error-handling boundary** — written alongside step 1, not bolted on later (see REQUIRED READING).

Idempotency (item 7 of the brief) is not a separate step — it is the `payPalRequestId` parameter
threaded through steps 2, 3, 5, 6, 7 below; see the matrix at the end of the CONTRACT SHEET.

---

## 2. CONTRACT SHEET

> **Signatures are generated code, verbatim — every parameter name is the literal
> C# identifier. The cancellation-token parameter really is named `ct`: in named
> arguments write `ct:`, never `cancellationToken:`.**
>
> **Every SDK type is written fully-qualified with the namespace the map gives it** — take
> each one from that type's own map row, never from where a neighbouring type sits. A members
> table names the namespace outright; otherwise the row's source path implies it
> (`Core/Configuration/…` ⇒ `…Core.Configuration`; a file at the repo root ⇒ the root
> namespace). Enums, unions, auth, server and client-config types are spread across different
> child namespaces, and two types configured side by side in the same options object routinely
> live in different ones. Dropping a type to the root or to `.Models` makes the implementer
> guess the wrong `using`, and the build breaks.

### 2.1 Client, auth, environment, base-URL override (Step 1)

| Type | Namespace | Members used |
|---|---|---|
| `PayPalServerSdkClient` | `PayPalServerSdk` | ctor `PayPalServerSdkClient(HttpClient httpClient, PayPalServerSdkClientOptions options)` |
| `PayPalServerSdkClientOptions` | `PayPalServerSdk` | `Environment: ServerEnvironment`, `Oauth2: OAuth2ClientCredentials?`, `Oauth2TokenStrategy: IOAuth2TokenStrategy<OAuth2ClientCredentials>?`, `Server: ServerOptions`, `Retry: RetryOptions`, `Logging: LoggingOptions` |
| `OAuth2ClientCredentials` | `PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials` | `required string ClientId { get; init; }`, `required string ClientSecret { get; init; }`, `string? Scope { get; init; }` — **this namespace is not in the map's namespace table; resolved from SDK source (`Core/Authentication/OAuth2/ClientCredentials/OAuth2ClientCredentials.cs`) because it's a real map gap.** |
| `ServerEnvironment` | `PayPalServerSdk.Servers` | Only static member: `ServerEnvironment.Sandbox`. **No `Live`/`Production` member exists** (constructor is `private`, so the implementer cannot add one either) — see GAP-1 below. |
| `ServerOptions` | `PayPalServerSdk` | `Default: DefaultOptions` (settable) |
| `DefaultOptions` | `PayPalServerSdk.Servers` | `Sandbox: DefaultOptions.SandboxOptions` (settable) |
| `DefaultOptions.SandboxOptions` | `PayPalServerSdk.Servers` (nested) | `BaseUrl: string`, defaults to `"https://api-m.sandbox.paypal.com"` |

**Auth wiring** (confirmed from `AuthSchemes.cs`): setting only `options.Oauth2` is sufficient — when
`options.Oauth2TokenStrategy` is `null`, the client defaults to
`OAuth2ClientCredentialsStrategy.ForBasicAuthRequest(...)`, i.e. HTTP Basic auth
(`Authorization: Basic base64(ClientId:ClientSecret)`) plus a `grant_type=client_credentials` form
body, POSTed to `{baseUrl}/v1/oauth2/token`. Leave `Oauth2TokenStrategy` unset unless you need a
custom strategy.

```csharp
using PayPalServerSdk;
using PayPalServerSdk.Servers;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;

var options = new PayPalServerSdkClientOptions
{
    Environment = ServerEnvironment.Sandbox,   // the only value that exists
    Oauth2 = new OAuth2ClientCredentials
    {
        ClientId = config["PayPal:ClientId"]!,
        ClientSecret = config["PayPal:ClientSecret"]!
    },
    Server = new ServerOptions
    {
        Default = new DefaultOptions
        {
            Sandbox = new DefaultOptions.SandboxOptions
            {
                // "PayPal:BaseUrl" config knob: when set, used verbatim for every call
                // INCLUDING the /v1/oauth2/token request (both go through the same
                // Server.Default(path) resolver — confirmed in Servers/DefaultOptions.cs
                // and Api/*.cs, and AuthSchemes.cs for the token call).
                BaseUrl = config["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com"
            }
        }
    }
};
```

**GAP-1 (report to user — do not work around):** This SDK (v1.0.1) has **no live/production
environment selector**. `ServerEnvironment` is a closed `StringEnum<T>` with a `private` constructor
and exactly one static member, `Sandbox`; `DefaultOptions.Resolve` pattern-matches only that one
branch and throws `ArgumentOutOfRangeException` for anything else. The **only** way this SDK can ever
reach a non-sandbox host is the `PayPal:BaseUrl` override above (still selecting
`ServerEnvironment.Sandbox`, just pointing its `BaseUrl` at a different host string) — there is no
SDK-native "Live" mode. This is exactly why the requested `PayPal:BaseUrl` knob is load-bearing:
it is not just a convenience, it is the *only* lever this SDK exposes for anything other than its
baked-in sandbox host. `PayPal:Environment` (from `PAYPAL_ENVIRONMENT`) has nothing to bind to on
this SDK beyond always passing `ServerEnvironment.Sandbox` — if the value is ever anything other
than "sandbox", the app can only honor it via `PayPal:BaseUrl`, never via a different
`ServerEnvironment` member.

⚠ Step 1 — the `HttpClient`/handler pipeline must be long-lived and reused via `IHttpClientFactory`,
not rebuilt per request. **MUST load `dotnet-client-initialization`** before writing DI registration.

⚠ Step 1 — `RetryOptions.HttpMethodsToRetry` gates only the *status*-triggered retry; a transport
failure retries on every verb including `POST`, so a non-idempotent write can double-fire on a
dropped connection even before any `PayPal-Request-Id` logic runs. `Timeout` is per-attempt, not
total. **MUST load `dotnet-configuration-resilience`** before tuning `options.Retry`/`options.Server`.

Map/source refs: `sdk-map.md` §"Getting a client" and §"Servers & auth"; source
`Servers/ServerEnvironment.cs`, `Servers/DefaultOptions.cs`, `Server.cs`, `AuthSchemes.cs`,
`Core/Authentication/OAuth2/ClientCredentials/OAuth2ClientCredentials.cs`.

---

### 2.2 Direct card authorization — no redirect (Step 2)

Grounded flow (this is what the map's own operation notes document, not an inference): create the
order shell with `Orders.CreateOrder`, then supply the raw card in `Orders.AuthorizeOrder` — the
`AuthorizeOrder` notes state explicitly that authorization succeeds when *either* the buyer approved
via redirect *or* "a valid `payment_source` must be provided in the request" (i.e. in the
`AuthorizeOrder` call itself). This is the SDK-documented no-redirect path.

| | |
|---|---|
| Controller property | `client.Orders` |
| Op 1 | `CreateOrder(string? payPalMockResponse, string? payPalRequestId, string? payPalPartnerAttributionId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderRequest body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` → returns `Order` |
| Op 2 | `AuthorizeOrder(string id, string? payPalMockResponse, string? payPalRequestId, string? payPalClientMetadataId, string? payPalAuthAssertion, OrderAuthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` → returns `OrderAuthorizeResponse` |

**`prefer` is critical**: both operations default to `"return=minimal"` (literal default shown in the
signature). A minimal response omits `PurchaseUnits`/nested payment data — the exact fields this step
needs to read. **Pass `prefer: "return=representation"` explicitly on both calls.**

Request model — `OrderRequest` (namespace `PayPalServerSdk.Models`), for `CreateOrder`:
- `Intent (intent): CheckoutPaymentIntent !req` → `PayPalServerSdk.Models.Enums.CheckoutPaymentIntent.Authorize` (wire `AUTHORIZE`)
- `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnitRequest> !req` — each item's
  `Amount (amount): AmountWithBreakdown !req` = `{ CurrencyCode (currency_code): string !req, Value (value): string !req }`
  (e.g. `CurrencyCode = "USD"`, `Value = "10.00"`)
- `PaymentSource (payment_source): PaymentSource?` — leave `null` on `CreateOrder` (card supplied in `AuthorizeOrder` instead)
- `Payer (payer): Payer?` — optional

Request model — `OrderAuthorizeRequest` (namespace `PayPalServerSdk.Models`):
- `PaymentSource (payment_source): OrderAuthorizeRequestPaymentSource?` → `.Card: CardRequest?`

`CardRequest` (namespace `PayPalServerSdk.Models`) — exact wire names and validation (from source,
`Models/CardRequest.cs`, since the map lists only `string?` without format/regex):
- `Name (name): string?` — cardholder name, 1–300 chars
- `Number (number): string?` — PAN, regex `^[0-9]{13,19}$` (13–19 digits) — sandbox Visa `4111111111111111` (16 digits) is valid
- `Expiry (expiry): string?` — **ISO-8601 `YYYY-MM`**, regex `^[0-9]{4}-(0[1-9]|1[0-2])$` (e.g. `"2027-12"`) — NOT `MM/YY`
- `SecurityCode (security_code): string?` — CVV, regex `^[0-9]{3,4}$` (3–4 digits)
- `BillingAddress (billing_address): Address?` = `{ AddressLine1, AddressLine2, AdminArea2, AdminArea1, PostalCode, CountryCode !req }` (all `string?` except `CountryCode`)
- `VaultId (vault_id): string?` — used in the "pay with a saved token" path (§2.7), leave `null` here
- `Attributes`, `SingleUseToken`, `StoredCredential`, `NetworkToken`, `ExperienceContext` — not needed for this flow, leave `null`

Response — `OrderAuthorizeResponse` (namespace `PayPalServerSdk.Models`):
- `Status (status): OrderStatus?` — read this FIRST (see 3DS detection below)
- `PurchaseUnits (purchase_units): IReadOnlyList<PurchaseUnit>?` → `[0].Payments (payments): PaymentCollection?` → `.Authorizations (authorizations): IReadOnlyList<AuthorizationWithAdditionalData>?`
- `AuthorizationWithAdditionalData` fields you need: `Id (id): string?` (the Authorization id to store), `Status (status): AuthorizationStatus?`, `Amount (amount): Money?`, `ExpirationTime (expiration_time): string?`

**3DS / "payer action required" detection (exact field/enum):** check
`OrderAuthorizeResponse.Status` (or `Order.Status` from `CreateOrder`) against
`PayPalServerSdk.Models.Enums.OrderStatus.PayerActionRequired` (wire `PAYER_ACTION_REQUIRED`). This
enum member is declared in the SDK (`Models/Enums/OrderStatus.cs`) — it is a certain, grounded fact.
**If `Status == OrderStatus.PayerActionRequired`, abort the flow and surface an operator-facing
error; do not attempt to follow any HATEOAS link in `Order.Links`.** (The specific `rel` value a
challenge link would carry is not a typed constant in this SDK — `LinkDescription.Rel` is a plain
`string?` — so do not build redirect logic against an assumed literal; the `Status` enum check above
is the one certain signal and is sufficient to abort.)

Error case — both ops are **Case A (typed)**:
- `CreateOrder`: `SdkException<CreateOrderError>`, `TryGetError(out Error)` [400,401,422], `TryGetRawError(out RawError)` fallback.
- `AuthorizeOrder`: `SdkException<AuthorizeOrderError>`, `TryGetError(out Error)` [400,401,403,404,422,500], `TryGetRawError(out RawError)` fallback.
- `Error` (namespace `PayPalServerSdk.Models`): `Name (name): string !req`, `Message (message): string !req`, `DebugId (debug_id): string !req`, `Details (details): IReadOnlyList<ErrorDetails>?`, `Links (links): IReadOnlyList<LinkDescription>?`.
- `ErrorDetails`: `Field (field): string?`, `Value (value): string?`, `Location (location): string?`, `Issue (issue): string !req`, `Description (description): string?`.

**`PurchaseUnitRequest` required vs optional (confirmed from the record's own `!req` markers,
`map/models/records-2-Pa-Ve.md`):** only `Amount (amount): AmountWithBreakdown !req` is required.
Every other field — `ReferenceId`, `Payee (payee): PayeeBase?`, `PaymentInstruction`, `Description`,
`CustomId`, `InvoiceId`, `SoftDescriptor`, `Items`, `Shipping`, `SupplementaryData` — is optional
(`?`, no `!req`). For a direct merchant charge (the calling app IS the merchant, no
marketplace/platform split), `Amount` alone is sufficient; no `Payee`/`reference_id` is required by
the model.

**UNVERIFIED — double-`AuthorizeOrder` / idempotent-replay behavior:** the map notes and the source
XML doc for `AuthorizeOrder` (`Api/Orders.cs`) say nothing about what happens if it is called twice
for the same order id with the same `payPalRequestId` — no "already authorized" wording, and no
dedicated typed error case for it. `AuthorizeOrderError`'s only typed accessor is the generic
`TryGetError(out Error)` covering statuses [400,401,403,404,422,500] — there is no separate
`OrderAlreadyAuthorized`-style error type in this SDK to branch on. **Defensive directive:** do not
assume PayPal replays the same authorization idempotently on a repeated `payPalRequestId` for this
operation, and do not pattern-match a specific `Error.Name`/`Issue` string for "already authorized" —
gate the call on your own `Order.Status` (as already planned) and, if `AuthorizeOrder` is ever called
a second time anyway and returns a 4xx, extract `Error.Name`/`Message`/`Details` verbatim for the
caller rather than assuming it means "already authorized, treat as success."

⚠ Step 2 — `CardRequest.Attributes.Verification.Method` (`OrdersCardVerificationMethod`) defaults to
`ScaWhenRequired`, which can itself trigger the very 3DS challenge this step is designed to reject.
**MUST load `dotnet-models`** before constructing `CardRequest`/`CardAttributes` to see how optional
nested request objects are built without accidentally opting into behaviour you didn't set explicitly.

⚠ Step 2 — building nested request records (`OrderRequest` → `PurchaseUnitRequest` →
`AmountWithBreakdown`, `OrderAuthorizeRequest` → `OrderAuthorizeRequestPaymentSource` → `CardRequest`)
uses `required`/`init`-only members with no positional constructor shortcuts. **MUST load
`dotnet-models`** before writing the object initializers.

Map refs: `map/operations/Orders.md` (`CreateOrder`, `AuthorizeOrder`), `map/models/records-1-Ac-Pa.md`
(`OrderRequest`, `PurchaseUnitRequest`, `AmountWithBreakdown`, `Order`, `Authorization`,
`AuthorizationWithAdditionalData`, `Error`, `ErrorDetails`, `LinkDescription`),
`map/models/records-2-Pa-Ve.md` (`OrderAuthorizeRequest`, `OrderAuthorizeResponse`,
`OrderAuthorizeRequestPaymentSource`, `OrderAuthorizeResponsePaymentSource`, `PaymentAuthorization`),
`map/models/enums.md` (`CheckoutPaymentIntent`, `OrderStatus`, `AuthorizationStatus`). Source ref
(real gap — card field formats): `Models/CardRequest.cs`.

---

### 2.3 Capture a previously created authorization (Step 3)

| | |
|---|---|
| Controller property | `client.Payments` |
| Signature | `CaptureAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, CaptureRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` |
| Returns | `CapturedPayment` |

Pass `prefer: "return=representation"` (same default trap as §2.2) to get `SellerReceivableBreakdown`
back in the response.

Request — `CaptureRequest` (namespace `PayPalServerSdk.Models`): `Amount (amount): Money?` (omit for
full capture of the authorized amount, set for partial), `FinalCapture (final_capture): bool? = false`
(set `true` if this is the last capture against the authorization), `InvoiceId`, `NoteToPayer`,
`SoftDescriptor`, `PaymentInstruction` — all optional.

Response — `CapturedPayment` (namespace `PayPalServerSdk.Models`) — exact fields the brief asked for:
- `Id (id): string?` — the capture id
- `Status (status): CaptureStatus?` — capture status enum (see §2.9 for values)
- `Amount (amount): Money?` = `{ CurrencyCode, Value }` — the captured amount
- `SellerReceivableBreakdown (seller_receivable_breakdown): SellerReceivableBreakdown?` — the fee/net breakdown:
  - `GrossAmount (gross_amount): Money !req`
  - `PaypalFee (paypal_fee): Money?` — PayPal's transaction fee
  - `PaypalFeeInReceivableCurrency (paypal_fee_in_receivable_currency): Money?`
  - `NetAmount (net_amount): Money?` — **net amount to the merchant**
  - `ReceivableAmount (receivable_amount): Money?`
  - `ExchangeRate (exchange_rate): ExchangeRate?`
  - `PlatformFees (platform_fees): IReadOnlyList<PlatformFee>?`
- `FinalCapture (final_capture): bool?`, `DisbursementMode (disbursement_mode): DisbursementMode?`

Error — **Case A (typed)**: `SdkException<CaptureAuthorizedPaymentError>`,
`TryGetError(out Error)` [400,401,403,404,409,422], `TryGetNoContent(out RawError)` [500],
`TryGetRawError(out RawError)` fallback. `Error`/`ErrorDetails` shapes as in §2.2.

Map refs: `map/operations/Payments.md` (`CaptureAuthorizedPayment`), `map/models/records-1-Ac-Pa.md`
(`CapturedPayment`, `CaptureRequest`), `map/models/records-2-Pa-Ve.md` (`SellerReceivableBreakdown`,
`Money`), `map/models/enums.md` (`CaptureStatus`).

---

### 2.4 Reauthorize a stale authorization (Step 4)

| | |
|---|---|
| Controller property | `client.Payments` |
| Signature | `ReauthorizePayment(string authorizationId, string? payPalRequestId, string? payPalAuthAssertion, ReauthorizeRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` |
| Returns | `PaymentAuthorization` |

Request — `ReauthorizeRequest` (namespace `PayPalServerSdk.Models`): single field
`Amount (amount): Money?` (per the API's own notes: "Supports only the `amount` request parameter").

**Pre-flight staleness check (concrete, resolvable facts — do this before calling `ReauthorizePayment`):**
1. `GetAuthorizedPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, RequestOptions? requestOptions = null, CancellationToken ct = default)` → returns
   `PaymentAuthorization` → check `ExpirationTime (expiration_time): string?` against current time.
   If already past, do not call `ReauthorizePayment` — tell the operator it cannot be renewed.
2. Check `Status (status): AuthorizationStatus?` (namespace `PayPalServerSdk.Models.Enums`):
   `AuthorizationStatus.Voided` or `AuthorizationStatus.Denied` → definitively not reauthorizable
   (cancelled/declined). `AuthorizationStatus.Captured` → already fully used, nothing to reauthorize.
   Only `Created`/`Pending`/`PartiallyCaptured` are candidates for reauthorization.
   **Note: this enum has no `Expired` member** (`Models/Enums/AuthorizationStatus.cs` declares only
   `Created, Captured, Denied, PartiallyCaptured, Voided, Pending`) — expiry is a time comparison
   against `ExpirationTime`, not a status value.

**UNVERIFIED — reauthorize-rejected error shape:** if the 29–30 day authorization window has fully
elapsed and you call `ReauthorizePayment` anyway, PayPal is expected to reject it, surfaced as
`SdkException<ReauthorizePaymentError>` → `TryGetError(out Error)` [400,401,403,404,422]. The SDK
types `Error.Name` and `ErrorDetails.Issue`/`.Description` as plain `string`/`string?` — the SDK does
**not** define a typed constant for "reauthorization window expired" (no such enum/constant exists in
the map or source; it is a runtime data value only PayPal's live response can confirm). **Defensive
directive:** on catching `SdkException<ReauthorizePaymentError>`, best-effort extract
`Error.Name`, `Error.Message`, `Error.DebugId`, and each `ErrorDetails.Issue`/`.Description` into the
operator-facing message; do not pattern-match on an assumed issue-code string, and do not silently
swallow the error — always surface it as "cannot renew: <extracted text>" rather than a generic
failure.

Error — **Case A (typed)**: `SdkException<ReauthorizePaymentError>`, `TryGetError(out Error)`
[400,401,403,404,422], `TryGetNoContent(out RawError)` [500], `TryGetRawError(out RawError)` fallback.

**UNVERIFIED — authorization identity across reauthorize:** does the successful `ReauthorizePayment`
response's `PaymentAuthorization.Id` come back as the SAME value as the original `authorizationId`,
or a NEW id that must be used for the subsequent `CaptureAuthorizedPayment` call? Neither the map's
operation notes nor the source XML doc/remarks for `ReauthorizePayment` (`Api/Payments.cs`) state
this either way — the remarks describe the honor-period/amount-cap mechanics only, never the
identity of the returned resource. The one structural fact that IS confirmed from source: the HTTP
route is `POST /v2/payments/authorizations/{authorization_id}/reauthorize` — i.e. the *original*
`authorizationId` is the path parameter for the reauthorize call itself (you must already have it to
call this operation at all). What is genuinely unconfirmed is whether the **response's** `Id` field
echoes that same value or mints a new one. **Defensive directive (do this unconditionally, it is
correct either way):** after a successful `ReauthorizePayment`, always read `PaymentAuthorization.Id`
from that response and use it for the subsequent `CaptureAuthorizedPayment(authorizationId: ...)`
call — never assume the original `authorizationId` remains valid, and never persist the
pre-reauthorize id as the one to capture against.

⚠ Step 4 — `GetAuthorizedPayment` and `ReauthorizePayment` are two separate calls with a time gap
between them; the authorization could change state between the check and the reauthorize attempt.
**MUST load `dotnet-error-handling`** to make sure the reauthorize call's own catch block (not just
the pre-flight check) is what ultimately decides "cannot be renewed" for the operator.

Map refs: `map/operations/Payments.md` (`ReauthorizePayment`, `GetAuthorizedPayment`),
`map/models/records-2-Pa-Ve.md` (`ReauthorizeRequest`, `PaymentAuthorization`),
`map/models/enums.md` (`AuthorizationStatus`).

---

### 2.5 Void an authorization (Step 5)

| | |
|---|---|
| Controller property | `client.Payments` |
| Signature | `VoidPayment(string authorizationId, string? payPalMockResponse, string? payPalAuthAssertion, string? payPalRequestId, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` |
| Returns | `PaymentAuthorization` |
| Error | `SdkException<VoidPaymentError>` — Case A: `TryGetError(out Error)` [401,403,404,409,422], `TryGetNoContent(out RawError)` [500], `TryGetRawError(out RawError)` fallback |

No request body — void takes no payload beyond the id and the standard headers. A `409` here
generally means the authorization was already captured/voided; extract `Error.Name`/`Details` the
same defensive way as §2.4 (exact issue-code string is likewise not SDK-typed).

Map ref: `map/operations/Payments.md` (`VoidPayment`).

---

### 2.6 Refund a capture, full/partial, idempotent (Step 6)

| | |
|---|---|
| Controller property | `client.Payments` |
| Signature | `RefundCapturedPayment(string captureId, string? payPalMockResponse, string? payPalRequestId, string? payPalAuthAssertion, RefundRequest? body, string? prefer = "return=minimal", RequestOptions? requestOptions = null, CancellationToken ct = default)` |
| Returns | `Refund` |

**Idempotency key — exact mechanism:** the `payPalRequestId` parameter (5th positional/named param)
is sent as the `PayPal-Request-Id` HTTP header (confirmed in `Api/Payments.cs`:
`new HeaderParam("PayPal-Request-Id", payPalRequestId)`). Pass a caller-generated stable string
(e.g. a GUID tied to your own refund request record) so a retried call is deduplicated by PayPal
rather than issuing a second refund.

Request — `RefundRequest` (namespace `PayPalServerSdk.Models`): `Amount (amount): Money?` — **omit
(`null`) for a full refund of the remaining captured amount; set for a partial refund.**
`CustomId`, `InvoiceId`, `NoteToPayer`, `PaymentInstruction` — optional.

Response — `Refund` (namespace `PayPalServerSdk.Models`):
- `Id (id): string?` — refund id
- `Status (status): RefundStatus?` — `Cancelled`, `Failed`, `Pending`, `Completed` (wire `CANCELLED`/`FAILED`/`PENDING`/`COMPLETED`)
- `Amount (amount): Money?`
- `SellerPayableBreakdown (seller_payable_breakdown): SellerPayableBreakdown?` = `{ GrossAmount, PaypalFee, NetAmount, TotalRefundedAmount, ... }`

**Over-refund vs. legitimate multiple partial refunds:** the SDK gives no dedicated typed error for
"refund exceeds captured amount" — it surfaces through the same
`SdkException<RefundCapturedPaymentError>` → `TryGetError(out Error)` [400,401,403,404,409,422] path
as any other rejection. **Defensive directive:** do not assume a specific `Error.Name`/`Issue` string
for the over-refund case (UNVERIFIED — only a live 422/400 response body would confirm the exact
code); instead, always read back `Refund.Amount` plus track your own running total of refunds issued
against a `CapturedPayment.Amount`/`SellerReceivableBreakdown` before calling, and surface whatever
`Error.Name`/`Details` PayPal actually returns verbatim to the operator rather than translating it
through an assumed code.

Error — **Case A (typed)**: `SdkException<RefundCapturedPaymentError>`,
`TryGetError(out Error)` [400,401,403,404,409,422], `TryGetNoContent(out RawError)` [500],
`TryGetRawError(out RawError)` fallback.

⚠ Step 6 — `RefundRequest.Amount` uses the same `Money` shape as everywhere else but a `null` vs. an
explicit `Money` with the full amount are **not interchangeable on the wire** (one omits the field
for a full refund, the other pins an exact currency+value) — get this wrong and a "full refund" can
be sent as a partial refund for a mistyped amount. **MUST load `dotnet-models`** before building
`RefundRequest`.

Map refs: `map/operations/Payments.md` (`RefundCapturedPayment`), `map/models/records-2-Pa-Ve.md`
(`RefundRequest`, `Refund`, `SellerPayableBreakdown`), `map/models/enums.md` (`RefundStatus`).
Source ref (header name — real gap, map doesn't list header wire names for non-query params):
`Api/Payments.cs`.

---

### 2.7 Vault — save a card, pay with a saved token, delete it (Step 7)

**Save (vault) a card from raw details** — `client.Vault`:

| | |
|---|---|
| Signature | `CreatePaymentToken(string? payPalRequestId, PaymentTokenRequest body, RequestOptions? requestOptions = null, CancellationToken ct = default)` |
| Returns | `PaymentTokenResponse` |
| Idempotency | `payPalRequestId` → `PayPal-Request-Id` header (confirmed `Api/Vault.cs`) |
| Error | `SdkException<CreatePaymentTokenError>` — Case A: `TryGetError1(out Error1)` [400,403,404,422,500], `TryGetRawError(out RawError)` fallback |

Request — `PaymentTokenRequest` (namespace `PayPalServerSdk.Models`):
- `Customer (customer): Customer?` = `{ Id (id): string?, MerchantCustomerId (merchant_customer_id): string? }` — **confirmed optional** (`?`, no `!req`, per the record's own marker in `map/models/records-2-Pa-Ve.md`); only `PaymentSource` is required on this record. This app can vault a raw card standalone per-buyer without first creating a PayPal "Customer" resource — omit `Customer` entirely (or populate it later purely as your own correlation id, it has no PayPal-side prerequisite).
- `PaymentSource (payment_source): PaymentTokenRequestPaymentSource !req` → `.Card: PaymentTokenRequestCard?` = `{ Name, Number, Expiry, SecurityCode (security_code), Brand, BillingAddress }` — same raw-card shape as `CardRequest` (§2.2); no vault-time verification method field beyond `Brand`.

Response — `PaymentTokenResponse` (namespace `PayPalServerSdk.Models`):
- **`Id (id): string?` — this is the vault/payment-token id to store** and later pass back as `CardRequest.VaultId`.
- `PaymentSource (payment_source): PaymentTokenResponsePaymentSource?` → `.Card: CardPaymentTokenEntity?` — safe-to-display card summary, **contains no PAN**:
  - `LastDigits (last_digits): string?`
  - `Brand (brand): CardBrand?`
  - `Expiry (expiry): string?`
  - `Type (type): CardType?`
  - (also `VerificationStatus`, `BinDetails`, `AuthenticationResult`, `NetworkTransactionReference` — none carry the full number)
- `Customer (customer): CustomerResponse?` = `{ Id, MerchantCustomerId }`

**Pay using the saved token instead of raw card fields** — reuse §2.2's `CreateOrder`/`AuthorizeOrder`
flow, but build `CardRequest` as:
```csharp
new CardRequest { VaultId = storedPaymentTokenId }   // CardRequest.VaultId (vault_id): string?
```
i.e. `OrderAuthorizeRequestPaymentSource.Card = new CardRequest { VaultId = "<PaymentTokenResponse.Id>" }`
— all other `CardRequest` fields (`Number`, `Expiry`, `SecurityCode`, ...) stay `null`. This is the
same `CardRequest` type used for raw-card authorize; the map defines no separate "authorize with
token" request shape because `CardRequest.VaultId` already covers it.

**Delete/invalidate a saved token** — `client.Vault`:

| | |
|---|---|
| Signature | `DeletePaymentToken(string id, RequestOptions? requestOptions = null, CancellationToken ct = default)` |
| Returns | `void` (Task) |
| Error | `SdkException<DeletePaymentTokenError>` — Case A: `TryGetError1(out Error1)` [400,403,500], `TryGetRawError(out RawError)` fallback |

`Error1` (namespace `PayPalServerSdk.Models`, used by every `Vault` operation's typed error instead of
`Error`): `Name (name): string !req`, `Message (message): string !req`, `DebugId (debug_id): string !req`,
`Details (details): IReadOnlyList<ErrorDetails1>?`, `Links (links): IReadOnlyList<ErrorLinkDescription>?`.
`ErrorDetails1` mirrors `ErrorDetails` (`Field`, `Value`, `Location`, `Issue !req`, `Description`) but
its `Links` element type is `ErrorLinkDescription`, not `LinkDescription` — **do not mix the two link
types up**; `ErrorLinkDescription.Rel` is `string?` (optional) whereas `LinkDescription.Rel` is
`string !req`.

⚠ Step 7 — `Vault`'s typed error accessor is `TryGetError1`/`Error1`, not `TryGetError`/`Error` like
`Orders`/`Payments` — a catch ladder copy-pasted from §2.2–§2.6 will not compile against
`SdkException<CreatePaymentTokenError>`. **MUST load `dotnet-error-handling`** before writing the
Vault error boundary.

Map refs: `map/operations/Vault.md` (`CreatePaymentToken`, `DeletePaymentToken`),
`map/models/records-1-Ac-Pa.md` (`Error1`, `ErrorDetails1`, `ErrorLinkDescription`, `CardRequest`),
`map/models/records-2-Pa-Ve.md` (`PaymentTokenRequest`, `PaymentTokenRequestPaymentSource`,
`PaymentTokenRequestCard`, `PaymentTokenResponse`, `PaymentTokenResponsePaymentSource`,
`CardPaymentTokenEntity`, `Customer`, `CustomerResponse`). Source ref (header name — real gap):
`Api/Vault.cs`.

---

### 2.8 Transaction search / reporting reconciliation (Step 8)

| | |
|---|---|
| Controller property | `client.TransactionSearch` |
| Signature | `SearchTransactions(string startDate, string endDate, string? transactionId, string? transactionType, string? transactionStatus, string? transactionAmount, string? transactionCurrency, string? paymentInstrumentType, string? storeId, string? terminalId, string? fields = "transaction_info", string? balanceAffectingRecordsOnly = "Y", int? pageSize = 100, int? page = 1, RequestOptions? requestOptions = null, CancellationToken ct = default)` |
| Returns | `SearchResponse` |

`startDate`/`endDate` are wire `start_date`/`end_date`, plain `string` — pass ISO-8601 (e.g.
`"2026-09-01T00:00:00-0700"`); the SDK does not type these as `DateTimeOffset`, so format them
yourself before calling. All 8 filter params (`transactionId` … `terminalId`) are nullable with no
C# default — **must pass `null` explicitly** if unused; call this with **named arguments** (the
signature has 10+ params in a row with no defaults, easy to mis-bind positionally).

Response — `SearchResponse` (namespace `PayPalServerSdk.Models`):
- `TransactionDetails (transaction_details): IReadOnlyList<TransactionDetails>?` — each item:
  `TransactionInfo (transaction_info): TransactionInformation?` →
  - `TransactionId (transaction_id): string?`
  - `TransactionAmount (transaction_amount): Money?`
  - `TransactionStatus (transaction_status): string?` — **plain `string`, not a generated enum.**
    UNVERIFIED which literal codes appear on the wire (the SDK declares no enum/constant for this
    field, so only a live response could confirm them). **Defensive directive:** store the raw
    string as-is for cross-referencing; do not hardcode a switch over assumed values, and route any
    value your reconciliation logic doesn't recognize to a manual-review bucket instead of silently
    treating it as success or failure.
  - `TransactionInitiationDate (transaction_initiation_date): string?`, `TransactionUpdatedDate (transaction_updated_date): string?`
- `Page (page): int?`, `TotalItems (total_items): int?`, `TotalPages (total_pages): int?`

**Pagination — exact shape:** page-number based (`page`/`page_size` on the request,
`Page`/`TotalPages`/`TotalItems` on the response) — **not** cursor-based, **not**
`total_pages`-only. Loop `page = 1, 2, … while page <= response.TotalPages`, incrementing `page` by 1
each call (the SDK exposes no auto-pager for this operation — map row says "Pagination: none (only
`page`, no `perPage`)", i.e. this is a plain parameter, not an SDK pagination helper). Do not stop
after page 1.

**Error case — the one operation in this whole SDK that is Case B:** `SdkException<RawError>`. There
is **no typed error type at all** for `SearchTransactions` (confirmed: `sdk-map.md` records "39 are
Case A, 1 is Case B" and this is the one). Catch `SdkException<RawError>` directly and read
`ex.Error.StatusCode`, `ex.Error.ReadAsString()`, or `ex.Error.ReadAsJson<T>()` — there are no
`TryGet…` typed accessors to reach for here.

⚠ Step 8 — every other operation in this plan is Case A; `SearchTransactions` alone is Case B, so a
shared catch ladder built for `Orders`/`Payments`/`Vault` will not compile against
`SdkException<RawError>` without its own `catch` clause. **MUST load `dotnet-error-handling`** and
**`dotnet-configuration-resilience`** (for the pagination loop and any retry interaction with a
long-running report) before writing this step.

Map refs: `map/operations/TransactionSearch.md`, `map/models/records-2-Pa-Ve.md` (`SearchResponse`,
`TransactionDetails`, `TransactionInformation`, `Money`).

---

### 2.9 Enum value tables actually needed

`PayPalServerSdk.Models.Enums.OrderStatus`: `Created (CREATED)`, `Saved (SAVED)`, `Approved (APPROVED)`,
`Voided (VOIDED)`, `Completed (COMPLETED)`, `PayerActionRequired (PAYER_ACTION_REQUIRED)`.

`AuthorizationStatus`: `Created (CREATED)`, `Captured (CAPTURED)`, `Denied (DENIED)`,
`PartiallyCaptured (PARTIALLY_CAPTURED)`, `Voided (VOIDED)`, `Pending (PENDING)`. (No `Expired` member.)

`CaptureStatus`: `Completed (COMPLETED)`, `Declined (DECLINED)`, `PartiallyRefunded (PARTIALLY_REFUNDED)`,
`Pending (PENDING)`, `Refunded (REFUNDED)`, `Failed (FAILED)`.

`RefundStatus`: `Cancelled (CANCELLED)`, `Failed (FAILED)`, `Pending (PENDING)`, `Completed (COMPLETED)`.

`CheckoutPaymentIntent`: `Capture (CAPTURE)`, `Authorize (AUTHORIZE)`.

`CardBrand` (29 members incl. `Visa (VISA)`, `Mastercard (MASTERCARD)`, `Amex (AMEX)`, `Discover (DISCOVER)`, … `Unknown (UNKNOWN)`) — full list at `map/models/enums.md` if a value beyond Visa is needed.

`CardType`: `Credit (CREDIT)`, `Debit (DEBIT)`, `Prepaid (PREPAID)`, `Store (STORE)`, `Unknown (UNKNOWN)`.

---

### 2.10 Idempotency matrix (item 7 — mechanism is uniform, availability is not)

Mechanism: the `payPalRequestId` C# parameter → `PayPal-Request-Id` HTTP header on every operation
below (confirmed per-file in `Api/Orders.cs`, `Api/Payments.cs`, `Api/Vault.cs`). Pass a
caller-generated stable string per logical attempt.

| Operation | Has `payPalRequestId`? |
|---|---|
| `Orders.CreateOrder` | Yes |
| `Orders.AuthorizeOrder` | Yes |
| `Orders.CaptureOrder` | Yes |
| `Payments.CaptureAuthorizedPayment` | Yes |
| `Payments.ReauthorizePayment` | Yes |
| `Payments.VoidPayment` | Yes |
| `Payments.RefundCapturedPayment` | Yes |
| `Vault.CreatePaymentToken` | Yes |
| `Vault.CreateSetupToken` | Yes |
| `Orders.ConfirmOrder`, `Orders.GetOrder`, `Orders.PatchOrder`, `Orders.CreateOrderTracking`, `Orders.UpdateOrderTracking` | No such parameter |
| `Payments.GetAuthorizedPayment`, `Payments.GetCapturedPayment`, `Payments.GetRefund` | No such parameter (reads are naturally idempotent) |
| `Vault.DeletePaymentToken`, `Vault.GetPaymentToken`, `Vault.GetSetupToken`, `Vault.ListCustomerPaymentTokens` | No such parameter |
| `TransactionSearch.SearchTransactions`, `TransactionSearch.SearchBalances` | No such parameter (reads) |

⚠ Cross-cutting — `RetryOptions`'s transport-failure retry (see §2.1) fires on `POST` regardless of
`HttpMethodsToRetry`, so even with `PayPal-Request-Id` set, verify the SDK's automatic retry is not
itself the source of a "duplicate" call from PayPal's perspective — the header is what makes that
retry safe, but only if it is actually supplied on every write above. **MUST load
`dotnet-configuration-resilience`.**

---

## 3. Trap notes (consolidated — see also inline ⚠ notes above)

> ⚠ Step 1 (client & DI) — the `HttpClient`/handler pipeline must be long-lived via
> `IHttpClientFactory`, not rebuilt per request; the SDK client wrapper over it may be transient.
> **MUST load `dotnet-client-initialization`**.

> ⚠ Step 1 (auth) — set `Oauth2` before constructing the client or inside the DI callback; load
> secrets from configuration, never hardcode. **MUST load `dotnet-authentication`**.

> ⚠ Steps 2–7 (every write call) — `prefer` defaults to `"return=minimal"` on every operation that
> has it; the fields this plan reads (`PurchaseUnits`, `SellerReceivableBreakdown`, etc.) are only
> populated with `prefer: "return=representation"`. **MUST load `dotnet-calling-endpoints`**.

> ⚠ Steps 2–8 (building any request/reading any response) — enums are `StringEnum<T>`, not C#
> `enum`; unmodeled JSON fields are dropped on deserialize; nested `required` records need full
> object-initializer syntax. **MUST load `dotnet-models`**.

> ⚠ Steps 1–8 (all error handling) — Case A vs Case B differs **per operation** (confirm each one's
> row before writing its catch block — `Orders`/`Payments` use `Error`+`TryGetError`, `Vault` uses
> `Error1`+`TryGetError1`, `SearchTransactions` alone is Case B raw); `TryGetRawError` is not a
> catch-all on the typed errors; no operation in this SDK has a no-throw variant. **MUST load
> `dotnet-error-handling`**.

> ⚠ Steps 1, 6, 8 (config/resilience) — `HttpMethodsToRetry` gates only the status trigger, a
> transport failure retries every verb including `POST`; `Timeout` is per-attempt; base-URL override
> and the `SearchTransactions` pagination loop both live in this skill's territory. **MUST load
> `dotnet-configuration-resilience`**.

**Always include, verbatim** — `System.Text.Json.JsonException` reaches the boundary from two
directions and they need opposite handling:

- a drifted or malformed **2xx** body (a missing `required` member) surfaces as a
  `JsonException` from deserialization, **not** as an `SdkException` — so an
  SDK-exception-only catch ladder lets it escape the integration boundary;
- a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape
  throws `JsonException` *while the error object is being constructed*, so the `JsonException`
  **replaces** the `SdkException` and the HTTP status is destroyed with it — a boundary that
  maps every `JsonException` to a 5xx then reports a deterministic rejection as an outage,
  and a caller that retries 5xx retries something that can never succeed.

**MUST load `dotnet-error-handling`** before writing that boundary. These rows are in this FIRST
sheet, not a later revision: the boundary is written in Step 1/9, and a caveat that arrives
afterwards arrives too late to shape it.

> ⚠ Step 7 (testing the vault + payment flows) — the `HttpClient` constructor argument is the test
> seam for stubbing SDK responses. **MUST load `dotnet-testing`** before writing tests for any of
> these flows.

---

## 4. REQUIRED READING (load before implementation starts — this sheet does not carry their contents)

- `dotnet-client-initialization` — Step 1: client construction, DI registration, `HttpClient` lifetime.
- `dotnet-authentication` — Step 1: `Oauth2` credentials wiring, when/where to set them.
- `dotnet-calling-endpoints` — Steps 2–8: calling every operation above, named-argument discipline, the `prefer` header default trap.
- `dotnet-models` — Steps 2–8: building nested request records, `StringEnum<T>` construction/reading, JSON wire-name mapping.
- `dotnet-error-handling` — Steps 1–8 (mandatory for every step): Case A vs Case B per operation, the two `JsonException` hazards above, `TryGet…` accessor discipline.
- `dotnet-configuration-resilience` — Steps 1, 6, 8: retry/transport-failure semantics, base-URL override wiring, the `SearchTransactions` pagination loop, timeouts.
- `dotnet-testing` — Step 7 (and generally): the `HttpClient` seam for stubbing SDK calls in tests.

---

## 5. Assumptions & Blockers

**Assumptions:**
- The "direct/guest card, no redirect" authorize flow is implemented as the two-call sequence
  `Orders.CreateOrder` (no `PaymentSource`) → `Orders.AuthorizeOrder` (with `PaymentSource.Card`),
  because only `AuthorizeOrder`'s own map notes document accepting a `payment_source` in lieu of
  buyer-redirect approval; `CreateOrder`'s notes make no equivalent claim about a single-step
  card-at-creation flow, so this plan does not assert that path exists.
- `PayPal:BaseUrl`, `PayPal:ClientId`, `PayPal:ClientSecret` are read via the ASP.NET Core
  `IConfiguration` the PublicApi project already uses (JWT auth config lives there today);
  no new configuration provider is assumed.
- The in-memory EF Core dev database is out of scope for this contract sheet — persisting captured
  authorization/capture/refund/vault-token ids against eShopOnWeb's own `Order` entity is an
  implementation detail for the later coding session, not an SDK contract fact.
- `PayPal:Environment` / `PAYPAL_ENVIRONMENT` has no corresponding `ServerEnvironment` member to bind
  to beyond `Sandbox` (see GAP-1); this plan assumes the later coding session treats that
  configuration key as informational only, or as a second input alongside `PayPal:BaseUrl` for
  choosing which base URL to apply, and does not expect the SDK to expose a `Live` environment.

**Blockers:** none — every operation the brief asked about (items 1–11) is supported by this SDK.
The one hard gap is **GAP-1** in §2.1 (no live/production `ServerEnvironment` member — the
`PayPal:BaseUrl` override is the only lever for a non-sandbox host), which is documented above, not
worked around.
