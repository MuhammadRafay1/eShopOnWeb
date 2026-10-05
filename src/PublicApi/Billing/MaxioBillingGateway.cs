using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.ErrorResponse;
using MaxioAdvancedBilling.Core.Exceptions;
using MaxioAdvancedBilling.Errors;
using MaxioAdvancedBilling.Models;
using MaxioAdvancedBilling.Models.AnyOf;
using MaxioAdvancedBilling.Models.Enums;
using MaxioAdvancedBilling.Requests.Customers;
using MaxioAdvancedBilling.Requests.ProductFamilies;
using MaxioAdvancedBilling.Requests.Subscriptions;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Billing;

/// <summary>
/// The only class that talks to the Maxio SDK. Scoped per HTTP request: it owns that request's single deadline
/// (<see cref="MaxioSettings.RequestBudget"/>), so however many Maxio calls one API request makes, the caller never
/// waits longer than the budget. Every SDK failure is translated here into <see cref="BillingProviderException"/>.
/// </summary>
public sealed class MaxioBillingGateway : ISubscriptionBillingGateway, IDisposable
{
    private const int PlansPerPage = 200;   // the provider's maximum page size
    private const int MaxPlanPages = 5;     // backstop: never walk more than 1,000 plans

    private readonly MaxioAdvancedBillingClient _client;
    private readonly MaxioSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MaxioBillingGateway> _logger;
    private readonly CancellationTokenSource _deadline;
    private readonly CancellationTokenSource _writeDeadline;

    public MaxioBillingGateway(MaxioAdvancedBillingClient client,
        IOptions<MaxioSettings> settings,
        IMemoryCache cache,
        TimeProvider timeProvider,
        ILogger<MaxioBillingGateway> logger)
    {
        _client = client;
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;
        _deadline = new CancellationTokenSource(_settings.RequestBudget, timeProvider);
        // A write stops early enough to leave time for looking up its outcome if it goes unanswered.
        _writeDeadline = new CancellationTokenSource(_settings.RequestBudget - _settings.WriteReconciliationReserve, timeProvider);
    }

    public async Task<PlanCatalog> ListPlansAsync(CancellationToken cancellationToken = default)
    {
        var family = _settings.ProductFamilyHandle!.Trim();
        var cacheKey = $"maxio:plans:{family}";
        if (_settings.PlanCacheSeconds > 0 && _cache.TryGetValue(cacheKey, out PlanCatalog? cached) && cached is not null)
        {
            return cached;
        }

        var plans = new List<SubscriptionPlan>();
        var truncated = false;
        for (var page = 1; ; page++)
        {
            IReadOnlyList<ProductResponse> products;
            try
            {
                var currentPage = page;
                products = await CallAsync<IReadOnlyList<ProductResponse>, ListProductsForProductFamilyError>(
                    "list plans",
                    token => _client.ProductFamilies.ListProductsForProductFamily(
                        new ListProductsForProductFamilyRequest
                        {
                            ProductFamilyId = "handle:" + family,
                            Page = currentPage,
                            PerPage = PlansPerPage,
                        },
                        cancellationToken: token),
                    TranslateListPlansError,
                    isWrite: false,
                    cancellationToken);
            }
            catch (BillingProviderException ex) when (ex.ProviderStatusCode == 404 && ex.Kind != BillingFailureKind.Misconfigured)
            {
                throw ProductFamilyNotFound(ex);
            }

            foreach (var product in products.Select(p => p.Product))
            {
                if (string.IsNullOrWhiteSpace(product.Handle) || product.ArchivedAt is not null)
                {
                    continue;
                }

                plans.Add(new SubscriptionPlan(product.Id, product.Handle, product.Name ?? product.Handle,
                    product.Description, product.PriceInCents, product.Interval, product.IntervalUnit?.Value));
            }

            if (products.Count < PlansPerPage)
            {
                break; // the provider has no further page
            }

            if (page >= MaxPlanPages)
            {
                truncated = true;
                _logger.LogWarning("Maxio plan listing for family {Family} stopped at the {MaxPages}-page cap; result is partial.",
                    family, MaxPlanPages);
                break;
            }
        }

        var catalog = new PlanCatalog(family, plans, truncated);
        if (_settings.PlanCacheSeconds > 0)
        {
            _cache.Set(cacheKey, catalog, TimeSpan.FromSeconds(_settings.PlanCacheSeconds));
        }

        return catalog;
    }

    public async Task<int?> FindCustomerIdByReferenceAsync(string customerReference, CancellationToken cancellationToken = default)
    {
        var response = await CallAsync<CustomerResponse?, RawError>(
            "find customer",
            async token =>
            {
                try
                {
                    return await _client.Customers.ReadCustomerByReference(
                        new ReadCustomerByReferenceRequest { Reference = customerReference },
                        cancellationToken: token);
                }
                catch (ApiException<RawError> ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return null; // no customer holds this reference
                }
            },
            ex => FromStatus((int)ex.StatusCode),
            isWrite: false,
            cancellationToken);

        if (response is null)
        {
            return null;
        }

        return response.Customer.Id
            ?? throw new BillingProviderException(BillingFailureKind.ProviderError,
                "Maxio returned a customer without an id.", 200);
    }

    public async Task<int> CreateCustomerAsync(NewBillingCustomer customer, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CallAsync<CustomerResponse, CreateCustomerError>(
                "create customer",
                token => _client.Customers.CreateCustomer(
                    new CreateCustomerOperationRequest
                    {
                        Body = new CreateCustomerRequest
                        {
                            Customer = new CreateCustomer
                            {
                                FirstName = customer.FirstName,
                                LastName = customer.LastName,
                                Email = customer.Email,
                                Reference = customer.Reference,
                            },
                        },
                    },
                    cancellationToken: token),
                TranslateCreateCustomerError,
                isWrite: true,
                cancellationToken);

            return response.Customer.Id
                ?? throw new BillingProviderException(BillingFailureKind.ProviderError,
                    "Maxio returned a customer without an id.", 200);
        }
        catch (BillingProviderException ex) when (ex.IsOutcomeUncertain || ex.Kind == BillingFailureKind.Rejected)
        {
            // Uncertain: the customer may have been created. Rejected: possibly because a concurrent request already
            // created a customer with this (provider-unique) reference. Either way the reference settles it.
            var existing = await TryFindCustomerAfterWriteAsync(customer.Reference, cancellationToken);
            if (existing is int id)
            {
                _logger.LogInformation("Maxio customer {CustomerId} found by reference after an inconclusive create.", id);
                return id;
            }

            if (!ex.IsOutcomeUncertain)
            {
                throw;
            }

            throw new BillingOutcomeUnknownException(
                ex.Kind == BillingFailureKind.Timeout
                    ? "Maxio did not respond in time while creating your billing account. It is safe to retry; the request will be reconciled."
                    : "Maxio did not confirm whether your billing account was created. It is safe to retry; the request will be reconciled.",
                ex);
        }
    }

    public async Task<BillingSubscription> CreateSubscriptionAsync(int customerId, string planHandle, string subscriptionReference,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CallAsync<SubscriptionResponse, CreateSubscriptionError>(
                "create subscription",
                token => _client.Subscriptions.CreateSubscription(
                    new CreateSubscriptionOperationRequest
                    {
                        Body = new CreateSubscriptionRequest
                        {
                            Subscription = new CreateSubscription
                            {
                                ProductHandle = planHandle,
                                CustomerId = customerId,
                                Reference = subscriptionReference,
                                PaymentCollectionMethod = CollectionMethod.TryGetKnownValue(
                                    _settings.PaymentCollectionMethod.Trim().ToLowerInvariant(), out var method) ? method : null,
                            },
                        },
                    },
                    cancellationToken: token),
                TranslateCreateSubscriptionError,
                isWrite: true,
                cancellationToken);

            if (response.Subscription is { Id: not null } subscription)
            {
                return Map(subscription);
            }

            throw new BillingProviderException(BillingFailureKind.ProviderError,
                "Maxio returned a subscription without an id.", 200);
        }
        catch (BillingProviderException ex) when (ex.IsOutcomeUncertain && ex is not BillingOutcomeUnknownException)
        {
            // The subscription may exist even though we got no usable answer: look it up by the reference we sent.
            var found = await TryFindSubscriptionAfterWriteAsync(customerId, subscriptionReference, cancellationToken);
            if (found is not null)
            {
                _logger.LogInformation("Maxio subscription {SubscriptionId} found by reference after an inconclusive create.", found.Id);
                return found;
            }

            throw new BillingOutcomeUnknownException(
                ex.Kind == BillingFailureKind.Timeout
                    ? "Maxio did not respond in time while creating your subscription. It is safe to retry; the request will be reconciled."
                    : "Maxio did not confirm whether your subscription was created. It is safe to retry; the request will be reconciled.",
                ex);
        }
    }

    public async Task<IReadOnlyList<BillingSubscription>> ListCustomerSubscriptionsAsync(int customerId,
        CancellationToken cancellationToken = default)
    {
        var responses = await CallAsync<IReadOnlyList<SubscriptionResponse>, RawError>(
            "list customer subscriptions",
            token => _client.Customers.ListCustomerSubscriptions(
                new ListCustomerSubscriptionsRequest { CustomerId = customerId },
                cancellationToken: token),
            ex => FromStatus((int)ex.StatusCode),
            isWrite: false,
            cancellationToken);

        return responses
            .Select(r => r.Subscription)
            .Where(s => s?.Id is not null)
            .Select(s => Map(s!))
            .ToList();
    }

    public void Dispose()
    {
        _deadline.Dispose();
        _writeDeadline.Dispose();
    }

    private async Task<int?> TryFindCustomerAfterWriteAsync(string reference, CancellationToken cancellationToken)
    {
        try
        {
            return await FindCustomerIdByReferenceAsync(reference, cancellationToken);
        }
        catch (BillingProviderException ex)
        {
            _logger.LogWarning("Maxio customer lookup after an inconclusive create failed: {Kind}.", ex.Kind);
            return null;
        }
    }

    private async Task<BillingSubscription?> TryFindSubscriptionAfterWriteAsync(int customerId, string reference,
        CancellationToken cancellationToken)
    {
        try
        {
            var subscriptions = await ListCustomerSubscriptionsAsync(customerId, cancellationToken);
            return subscriptions.FirstOrDefault(s => s.Reference == reference);
        }
        catch (BillingProviderException ex)
        {
            _logger.LogWarning("Maxio subscription lookup after an inconclusive create failed: {Kind}.", ex.Kind);
            return null;
        }
    }

    /// <summary>
    /// Runs one SDK call under the request deadline and converts every SDK failure into a caller-safe
    /// <see cref="BillingProviderException"/>. <typeparamref name="TError"/> is the operation's error type —
    /// its generated <c>{Operation}Error</c> (typed) or <see cref="RawError"/> (untyped).
    /// </summary>
    private async Task<T> CallAsync<T, TError>(string operation, Func<CancellationToken, Task<T>> call,
        Func<ApiException<TError>, BillingProviderException> translateApiError, bool isWrite,
        CancellationToken cancellationToken)
    {
        var deadline = isWrite ? _writeDeadline : _deadline;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            return await call(linked.Token);
        }
        catch (ApiException<TError> ex)
        {
            throw Logged(operation, translateApiError(ex), ex);
        }
        catch (ResponseDeserializationException ex)
        {
            var status = (int)ex.StatusCode;
            throw Logged(operation, status is >= 200 and < 300
                ? new BillingProviderException(BillingFailureKind.ProviderError,
                    "Maxio returned a response that could not be processed.", status, innerException: ex)
                : FromStatus(status, innerException: ex), ex);
        }
        catch (SdkTimeoutException ex)
        {
            throw Logged(operation, DidNotRespond(ex), ex);
        }
        catch (SdkConnectionException ex)
        {
            throw Logged(operation, new BillingProviderException(BillingFailureKind.Unreachable,
                "Maxio could not be reached.", innerException: ex), ex);
        }
        catch (AuthSchemeException ex)
        {
            throw Logged(operation, new BillingProviderException(BillingFailureKind.Misconfigured,
                "The billing integration's Maxio credentials could not be applied.", innerException: ex), ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            // Our own budget ran out (the caller's cancellation is not translated — it propagates as-is).
            throw Logged(operation, DidNotRespond(ex), ex);
        }
    }

    private BillingProviderException DidNotRespond(Exception inner) =>
        new(BillingFailureKind.Timeout,
            $"Maxio did not respond within {_settings.RequestBudgetSeconds:0.#} seconds.", innerException: inner);

    private BillingProviderException Logged(string operation, BillingProviderException translated, Exception cause)
    {
        // Never log request bodies or SDK messages verbatim here: the method + path is enough to correlate.
        _logger.LogWarning("Maxio {Operation} failed: {Kind} (HTTP {Status}) {Messages}",
            operation, translated.Kind, translated.ProviderStatusCode?.ToString() ?? "none",
            string.Join("; ", translated.ProviderMessages));
        if (cause is SdkException sdk)
        {
            _logger.LogDebug("Maxio {Operation} failed call: {Method} {Path}", operation, sdk.Method, sdk.RequestUri?.AbsolutePath);
        }
        return translated;
    }

    private static BillingProviderException FromStatus(int status, IReadOnlyList<string>? messages = null,
        Exception? innerException = null) => status switch
    {
        401 or 403 => new(BillingFailureKind.Misconfigured,
            "Maxio rejected the billing integration's credentials.", status, innerException: innerException),
        400 or 422 => new(BillingFailureKind.Rejected,
            messages is { Count: > 0 } ? "Maxio rejected the request: " + string.Join(" ", messages) : "Maxio rejected the request.",
            status, messages, innerException),
        429 => new(BillingFailureKind.RateLimited,
            "Maxio is rate-limiting requests. Try again shortly.", status, innerException: innerException),
        404 => new(BillingFailureKind.ProviderError,
            "Maxio could not find the requested billing record.", status, innerException: innerException),
        _ => new(BillingFailureKind.ProviderError,
            $"Maxio returned an unexpected error (HTTP {status}).", status, innerException: innerException),
    };

    private BillingProviderException ProductFamilyNotFound(Exception inner) =>
        new(BillingFailureKind.Misconfigured,
            "The product family configured in Maxio:ProductFamilyHandle was not found in Maxio.", 404, innerException: inner);

    private BillingProviderException TranslateListPlansError(ApiException<ListProductsForProductFamilyError> ex)
    {
        if (ex.Error.TryGetString(out _))
        {
            return ProductFamilyNotFound(ex);
        }

        return FromStatus((int)ex.StatusCode, innerException: ex);
    }

    private static BillingProviderException TranslateCreateCustomerError(ApiException<CreateCustomerError> ex)
    {
        if (ex.Error.TryGetCustomerErrorResponse1(out var body))
        {
            return FromStatus((int)ex.StatusCode, ErrorMessages(body.Errors), ex);
        }

        return FromStatus((int)ex.StatusCode, innerException: ex);
    }

    private static BillingProviderException TranslateCreateSubscriptionError(ApiException<CreateSubscriptionError> ex)
    {
        if (ex.Error.TryGetErrorListResponse1(out var body))
        {
            return FromStatus((int)ex.StatusCode, body.Errors, ex);
        }

        return FromStatus((int)ex.StatusCode, innerException: ex);
    }

    private static IReadOnlyList<string> ErrorMessages(Errors1? errors)
    {
        if (errors is null)
        {
            return Array.Empty<string>();
        }

        if (errors.TryGetCustomerError(out var customerError) && !string.IsNullOrWhiteSpace(customerError.Customer))
        {
            return new[] { customerError.Customer };
        }

        return errors.TryGetListOfString(out var list) ? list : Array.Empty<string>();
    }

    private static BillingSubscription Map(Subscription s) =>
        new(
            Id: s.Id!.Value,
            Reference: s.Reference,
            PlanHandle: s.Product?.Handle,
            PlanName: s.Product?.Name,
            State: s.State?.Value,
            IsTerminal: s.State is not null
                && (s.State == SubscriptionState.Canceled || s.State == SubscriptionState.Expired
                    || s.State == SubscriptionState.FailedToCreate),
            PriceInCents: s.ProductPriceInCents ?? s.Product?.PriceInCents,
            Currency: s.Currency,
            NextBillingAt: s.CurrentPeriodEndsAt ?? s.NextAssessmentAt,
            NextAssessmentAt: s.NextAssessmentAt,
            CreatedAt: s.CreatedAt,
            ActivatedAt: s.ActivatedAt);
}
