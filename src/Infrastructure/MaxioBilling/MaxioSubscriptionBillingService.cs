using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.Exceptions;
using MaxioAdvancedBilling.Errors;
using MaxioAdvancedBilling.Models;
using MaxioAdvancedBilling.Requests.Customers;
using MaxioAdvancedBilling.Requests.ProductFamilies;
using MaxioAdvancedBilling.Requests.Subscriptions;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Billing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.MaxioBilling;

/// <summary>
/// <see cref="ISubscriptionBillingService"/> backed by Maxio Advanced Billing (the system of record).
///
/// Idempotency: every shopper gets a deterministic Maxio customer reference and every (shopper, plan)
/// pair a deterministic subscription reference, so a double-click — concurrent or repeated, in this
/// process or after a restart — finds the existing records instead of creating duplicates. An in-process
/// claim (per-reference semaphore) serialises concurrent attempts before they reach the provider.
/// </summary>
public sealed class MaxioSubscriptionBillingService : ISubscriptionBillingService
{
    /// <summary>Total deadline for one caller-facing operation (every SDK call inside it shares it).</summary>
    private static readonly TimeSpan RequestBudget = TimeSpan.FromSeconds(20);

    /// <summary>Page size and safety cap for the hand-driven plan-catalog page loop.</summary>
    private const int PlanPageSize = 200;
    private const int MaxPlanPages = 10;

    private readonly MaxioAdvancedBillingClient _client;
    private readonly MaxioBillingOptions _settings;
    private readonly ILogger<MaxioSubscriptionBillingService> _logger;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _claims = new();

    public MaxioSubscriptionBillingService(MaxioAdvancedBillingClient client,
        IOptions<MaxioBillingOptions> settings,
        ILogger<MaxioSubscriptionBillingService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default)
    {
        using var budget = Budget(cancellationToken);

        var products = await ListFamilyProductsAsync(budget.Token);
        return products
            .Where(p => p.ArchivedAt is null)
            .Select(ToPlan)
            .ToList();
    }

    public async Task<SubscriptionSummary> SubscribeAsync(Subscriber subscriber, string planHandle,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planHandle))
        {
            throw new SubscriptionBillingException("A planHandle is required.", 400, callerFault: true);
        }
        planHandle = planHandle.Trim();

        using var budget = Budget(cancellationToken);
        var token = budget.Token;
        var subscriptionReference = SubscriptionReference(subscriber.UserId, planHandle);

        var gate = _claims.GetOrAdd(subscriptionReference, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            // Idempotent pre-check: an earlier attempt (double-click, retry, previous run) already enrolled this user.
            var existing = await TryFindSubscriptionAsync(subscriptionReference, token);
            if (existing is not null)
            {
                _logger.LogInformation("Subscribe: user {UserId} is already subscribed to {PlanHandle}; returning subscription {SubscriptionId}.",
                    subscriber.UserId, planHandle, existing.Id);
                return ToSummary(existing);
            }

            // The plan handle must be one this application offers (the configured family's products).
            var products = await ListFamilyProductsAsync(token);
            var product = products.FirstOrDefault(p =>
                string.Equals(p.Handle, planHandle, StringComparison.OrdinalIgnoreCase));
            if (product is null)
            {
                throw new SubscriptionBillingException(
                    $"No subscription plan with handle '{planHandle}' is available.", 404, callerFault: true);
            }

            var customerId = await EnsureCustomerAsync(subscriber, token);

            var subscription = await CreateSubscriptionAsync(customerId, planHandle, subscriptionReference, token);
            _logger.LogInformation("Subscribe: user {UserId} enrolled in {PlanHandle}; subscription {SubscriptionId} is {State}.",
                subscriber.UserId, planHandle, subscription.Id, subscription.State?.Value);
            return ToSummary(subscription);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<SubscriptionSummary>> ListSubscriptionsAsync(Subscriber subscriber,
        CancellationToken cancellationToken = default)
    {
        using var budget = Budget(cancellationToken);
        var token = budget.Token;

        int customerId;
        try
        {
            customerId = await ReadCustomerByReferenceAsync(CustomerReference(subscriber.UserId), token);
        }
        catch (SubscriptionBillingException ex) when (ex.HttpStatusCode == 404)
        {
            // The shopper has never subscribed: no Maxio customer exists yet.
            return Array.Empty<SubscriptionSummary>();
        }

        var subscriptions = await Invoke(() => _client.Customers.ListCustomerSubscriptions(
            new ListCustomerSubscriptionsRequest { CustomerId = customerId }, cancellationToken: token));

        return subscriptions
            .Where(r => r.Subscription is not null)
            .Select(r => ToSummary(r.Subscription!))
            .ToList();
    }

    private async Task<Subscription> CreateSubscriptionAsync(int customerId, string planHandle,
        string subscriptionReference, CancellationToken token)
    {
        try
        {
            var response = await Invoke(() => _client.Subscriptions.CreateSubscription(
                new CreateSubscriptionOperationRequest
                {
                    Body = new CreateSubscriptionRequest
                    {
                        Subscription = new CreateSubscription
                        {
                            ProductHandle = planHandle,
                            CustomerId = customerId,
                            Reference = subscriptionReference
                        }
                    }
                }, cancellationToken: token),
                MapCreateSubscriptionError);

            return response.Subscription
                ?? throw new SubscriptionBillingException("Maxio returned no subscription payload.", 502);
        }
        catch (SubscriptionBillingException ex) when (ex.HttpStatusCode == 422)
        {
            // A duplicate reference under a race we lost (another process/instance): settle it.
            var settled = await TryFindSubscriptionAsync(subscriptionReference, token);
            if (settled is not null)
            {
                return settled;
            }
            throw;
        }
        catch (SubscriptionBillingException ex) when (ex.UnknownOutcome)
        {
            // The create may have landed even though the transport failed — settle it by re-reading.
            var settled = await TryFindSubscriptionAsync(subscriptionReference, token);
            if (settled is not null)
            {
                _logger.LogInformation("Subscribe: create outcome was unknown; settled to existing subscription {SubscriptionId}.",
                    settled.Id);
                return settled;
            }
            throw;
        }
    }

    private async Task<int> EnsureCustomerAsync(Subscriber subscriber, CancellationToken token)
    {
        var reference = CustomerReference(subscriber.UserId);
        try
        {
            return await ReadCustomerByReferenceAsync(reference, token);
        }
        catch (SubscriptionBillingException ex) when (ex.HttpStatusCode == 404)
        {
            // First subscription for this shopper — create the Maxio customer.
            var (firstName, lastName) = SplitName(subscriber);
            try
            {
                var response = await Invoke(() => _client.Customers.CreateCustomer(
                    new CreateCustomerOperationRequest
                    {
                        Body = new CreateCustomerRequest
                        {
                            Customer = new CreateCustomer
                            {
                                FirstName = firstName,
                                LastName = lastName,
                                Email = subscriber.Email,
                                Reference = reference
                            }
                        }
                    }, cancellationToken: token),
                    MapCreateCustomerError);

                _logger.LogInformation("Subscribe: created Maxio customer {CustomerId} for user {UserId}.",
                    response.Customer.Id, subscriber.UserId);
                return response.Customer.Id
                    ?? throw new SubscriptionBillingException("Maxio returned a customer without an id.", 502);
            }
            catch (SubscriptionBillingException createEx) when (createEx.HttpStatusCode == 422)
            {
                // Most plausibly a lost create race on the same reference — re-read and reuse.
                var raced = await ReadCustomerByReferenceAsync(reference, token);
                return raced;
            }
        }
    }

    private async Task<int> ReadCustomerByReferenceAsync(string reference, CancellationToken token)
    {
        var response = await Invoke(() => _client.Customers.ReadCustomerByReference(
            new ReadCustomerByReferenceRequest { Reference = reference }, cancellationToken: token));
        return response.Customer.Id
            ?? throw new SubscriptionBillingException("Maxio returned a customer without an id.", 502);
    }

    private async Task<Subscription?> TryFindSubscriptionAsync(string reference, CancellationToken token)
    {
        try
        {
            var response = await Invoke(() => _client.Subscriptions.FindSubscription(
                new FindSubscriptionRequest { Reference = reference }, cancellationToken: token),
                MapFindSubscriptionError);
            return response.Subscription;
        }
        catch (SubscriptionLookupNotFound)
        {
            return null;
        }
    }

    private async Task<List<Product>> ListFamilyProductsAsync(CancellationToken token)
    {
        var results = new List<Product>();
        for (var page = 1; ; page++)
        {
            var batch = await Invoke(() => _client.ProductFamilies.ListProductsForProductFamily(
                new ListProductsForProductFamilyRequest
                {
                    ProductFamilyId = "handle:" + _settings.ProductFamilyHandle,
                    Page = page,
                    PerPage = PlanPageSize
                }, cancellationToken: token),
                MapListProductsForFamilyError);

            results.AddRange(batch.Where(r => r.Product is not null).Select(r => r.Product!));
            if (batch.Count < PlanPageSize)
            {
                break;
            }
            if (page >= MaxPlanPages)
            {
                // Never truncate silently: the caller could not distinguish a complete list from a cut one.
                throw new SubscriptionBillingException(
                    $"The subscription plan catalog exceeded {MaxPlanPages} pages of {PlanPageSize} products; the list was not returned.",
                    503);
            }
        }
        return results;
    }

    // --- Error mappers: one per Case-A operation, enumerating every TryGet* that operation's
    //     error declares, in order, with TryGetRawError last (it is not a catch-all). ---

    private static SubscriptionBillingException? MapCreateSubscriptionError(ApiException ex)
    {
        if (ex is not ApiException<CreateSubscriptionError> typed)
        {
            return null;
        }
        if (typed.Error.TryGetErrorListResponse1(out var errors))
        {
            return new SubscriptionBillingException(
                $"Maxio rejected the subscription: {string.Join("; ", errors.Errors)}",
                (int)typed.StatusCode, callerFault: true, innerException: typed);
        }
        if (typed.Error.TryGetRawError(out _))
        {
            return new SubscriptionBillingException(
                $"Maxio rejected the subscription (HTTP {(int)typed.StatusCode}).",
                (int)typed.StatusCode, callerFault: IsCallerFault(typed.StatusCode), innerException: typed);
        }
        return new SubscriptionBillingException(
            $"Maxio rejected the subscription (HTTP {(int)typed.StatusCode}).",
            (int)typed.StatusCode, callerFault: IsCallerFault(typed.StatusCode), innerException: typed);
    }

    private static SubscriptionBillingException? MapCreateCustomerError(ApiException ex)
    {
        if (ex is not ApiException<CreateCustomerError> typed)
        {
            return null;
        }
        if (typed.Error.TryGetCustomerErrorResponse1(out _))
        {
            return new SubscriptionBillingException(
                $"Maxio rejected the customer (HTTP {(int)typed.StatusCode}).",
                (int)typed.StatusCode, callerFault: true, innerException: typed);
        }
        if (typed.Error.TryGetRawError(out _))
        {
            return new SubscriptionBillingException(
                $"Maxio rejected the customer (HTTP {(int)typed.StatusCode}).",
                (int)typed.StatusCode, callerFault: IsCallerFault(typed.StatusCode), innerException: typed);
        }
        return new SubscriptionBillingException(
            $"Maxio rejected the customer (HTTP {(int)typed.StatusCode}).",
            (int)typed.StatusCode, callerFault: IsCallerFault(typed.StatusCode), innerException: typed);
    }

    private static Exception? MapFindSubscriptionError(ApiException ex)
    {
        if (ex is not ApiException<FindSubscriptionError> typed)
        {
            return null;
        }
        if (typed.Error.TryGetNoContent(out _))
        {
            return new SubscriptionLookupNotFound();
        }
        if (typed.Error.TryGetRawError(out _))
        {
            return new SubscriptionBillingException(
                $"Maxio lookup failed (HTTP {(int)typed.StatusCode}).",
                (int)typed.StatusCode, callerFault: IsCallerFault(typed.StatusCode), innerException: typed);
        }
        return new SubscriptionBillingException(
            $"Maxio lookup failed (HTTP {(int)typed.StatusCode}).",
            (int)typed.StatusCode, callerFault: IsCallerFault(typed.StatusCode), innerException: typed);
    }

    private SubscriptionBillingException? MapListProductsForFamilyError(ApiException ex)
    {
        if (ex is not ApiException<ListProductsForProductFamilyError> typed)
        {
            return null;
        }
        if (typed.Error.TryGetString(out var message))
        {
            return new SubscriptionBillingException(
                $"The configured Maxio product family '{_settings.ProductFamilyHandle}' was not found: {message}",
                502, innerException: typed);
        }
        if (typed.Error.TryGetRawError(out _))
        {
            return new SubscriptionBillingException(
                $"Maxio plan catalog request failed (HTTP {(int)typed.StatusCode}).",
                (int)typed.StatusCode, callerFault: IsCallerFault(typed.StatusCode), innerException: typed);
        }
        return new SubscriptionBillingException(
            $"Maxio plan catalog request failed (HTTP {(int)typed.StatusCode}).",
            (int)typed.StatusCode, callerFault: IsCallerFault(typed.StatusCode), innerException: typed);
    }

    /// <summary>
    /// The one SDK error boundary: every SDK exception is converted to
    /// <see cref="SubscriptionBillingException"/> carrying the status (when the provider answered)
    /// and whether the failure is the caller's to fix. Catch order mirrors the SDK family:
    /// the deserialization leaf first (it IS an ApiException), then the ApiException base for
    /// everything typed, then the transport leaves. <paramref name="mapApiException"/> lets a
    /// Case-A call site substitute its own typed mapping for the exceptions it knows.
    /// </summary>
    private async Task<T> Invoke<T>(Func<Task<T>> call, Func<ApiException, Exception?>? mapApiException = null)
    {
        try
        {
            return await call();
        }
        catch (ResponseDeserializationException ex)
        {
            // A 2xx body that does not match its declared type leaves the outcome unknown;
            // an error-status body only lost its detail — keep the status.
            var unknown = (int)ex.StatusCode >= 200 && (int)ex.StatusCode < 300;
            throw new SubscriptionBillingException(
                "Maxio returned a response that could not be processed.",
                (int)ex.StatusCode, callerFault: !unknown && IsCallerFault(ex.StatusCode), unknownOutcome: unknown,
                innerException: ex);
        }
        catch (ApiException ex)
        {
            throw (mapApiException?.Invoke(ex)) ?? new SubscriptionBillingException(
                $"Maxio request failed (HTTP {(int)ex.StatusCode}).",
                (int)ex.StatusCode, callerFault: IsCallerFault(ex.StatusCode), innerException: ex);
        }
        catch (SdkTimeoutException ex)
        {
            throw new SubscriptionBillingException(
                "Maxio did not answer in time; the outcome may be unknown.",
                null, callerFault: false, unknownOutcome: true, innerException: ex);
        }
        catch (SdkConnectionException ex)
        {
            throw new SubscriptionBillingException(
                "Maxio is unreachable; the outcome may be unknown.",
                null, callerFault: false, unknownOutcome: true, innerException: ex);
        }
        catch (AuthSchemeException ex)
        {
            throw new SubscriptionBillingException(
                "Maxio credentials could not be applied.", null, callerFault: false, innerException: ex);
        }
        catch (SdkException ex)
        {
            throw new SubscriptionBillingException(
                "Maxio request failed.", null, callerFault: false, innerException: ex);
        }
    }

    /// <summary>Marks "FindSubscription answered 404: no subscription exists for this reference".</summary>
    private sealed class SubscriptionLookupNotFound : Exception
    {
    }

    private static CancellationTokenSource Budget(CancellationToken callerToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        cts.CancelAfter(RequestBudget);
        return cts;
    }

    private static bool IsCallerFault(HttpStatusCode? status) =>
        status is not null &&
        (int)status is >= 400 and < 500 &&
        (int)status is not (401 or 403 or 429);

    private static string CustomerReference(string userId) => $"eshop-user-{userId}";

    private static string SubscriptionReference(string userId, string planHandle) =>
        $"eshop-sub-{userId}-{planHandle}";

    private static (string FirstName, string LastName) SplitName(Subscriber subscriber)
    {
        var source = string.IsNullOrWhiteSpace(subscriber.UserName)
            ? subscriber.Email
            : subscriber.UserName;
        if (string.IsNullOrWhiteSpace(source))
        {
            return ("eShop", "Customer");
        }
        var localPart = source.Contains('@') ? source.Split('@')[0] : source;
        var tokens = localPart.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length switch
        {
            0 => ("eShop", "Customer"),
            1 => (tokens[0], "Customer"),
            _ => (tokens[0], string.Join(' ', tokens.Skip(1)))
        };
    }

    private static SubscriptionPlan ToPlan(Product product) => new(
        Handle: product.Handle ?? string.Empty,
        Name: product.Name ?? string.Empty,
        Description: product.Description,
        PriceInCents: product.PriceInCents,
        Interval: product.Interval,
        IntervalUnit: product.IntervalUnit?.Value,
        RequiresPaymentMethod: product.RequireCreditCard);

    private static SubscriptionSummary ToSummary(Subscription subscription) => new(
        Id: subscription.Id ?? 0,
        PlanHandle: subscription.Product?.Handle ?? string.Empty,
        PlanName: subscription.Product?.Name ?? string.Empty,
        State: subscription.State?.Value ?? "unknown",
        PriceInCents: subscription.ProductPriceInCents,
        NextBillingAt: subscription.NextAssessmentAt ?? subscription.CurrentPeriodEndsAt,
        CurrentPeriodEndsAt: subscription.CurrentPeriodEndsAt,
        ActivatedAt: subscription.ActivatedAt);
}