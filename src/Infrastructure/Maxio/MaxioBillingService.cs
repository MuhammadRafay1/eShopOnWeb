using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Maxio.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Maxio;

/// <summary>
/// Implementation of the subscription billing capability on top of Maxio Advanced Billing.
///
/// Maxio is the system of record: billing customers are keyed by a stable reference derived
/// from the eShop user id (reference = "eshop-user-{userId}"), and subscription state is always
/// read back from Maxio. That makes the flow idempotent across restarts even without local
/// persistence, and safe against double-clicks:
///  1. Customer creation is guarded by the unique customer reference (Maxio answers 422 when
///     the reference is taken; the service then re-reads the existing customer).
///  2. Subscription creation is serialized per user with an in-process gate, and an already
///     existing open subscription for the same plan is returned instead of creating a duplicate.
/// </summary>
public class MaxioBillingService : ISubscriptionBillingService
{
    private const string CustomerReferencePrefix = "eshop-user-";
    private const string PlanCacheKeyPrefix = "maxio-plans:";
    private static readonly TimeSpan PlanCacheDuration = TimeSpan.FromMinutes(2);

    private static readonly string[] ClosedSubscriptionStates = { "canceled", "expired" };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _subscribeGates = new();

    private readonly IMaxioClient _client;
    private readonly MaxioOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IAppLogger<MaxioBillingService> _logger;

    public MaxioBillingService(IMaxioClient client,
        IOptions<MaxioOptions> options,
        IMemoryCache cache,
        IAppLogger<MaxioBillingService> logger)
    {
        Guard.Against.Null(client, nameof(client));
        Guard.Against.Null(options, nameof(options));
        Guard.Against.Null(cache, nameof(cache));
        Guard.Against.Null(logger, nameof(logger));

        _client = client;
        _options = options.Value;
        _cache = cache;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("The Maxio integration is not configured. Provide the 'Maxio:ApiKey' setting (e.g. via user secrets or the MAXIO_API_KEY environment variable).");
        }

        if (string.IsNullOrWhiteSpace(_options.BaseUrl) && string.IsNullOrWhiteSpace(_options.Subdomain))
        {
            throw new InvalidOperationException("The Maxio integration is not configured. Provide either 'Maxio:Subdomain' (from MAXIO_SITE_SUBDOMAIN) or 'Maxio:BaseUrl'.");
        }

        if (string.IsNullOrWhiteSpace(_options.ProductFamilyHandle))
        {
            throw new InvalidOperationException("The Maxio integration is not configured. Provide the 'Maxio:ProductFamilyHandle' setting (from MAXIO_DEFAULT_PRODUCT_FAMILY).");
        }
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(PlanCacheKeyPrefix + _options.ProductFamilyHandle, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = PlanCacheDuration;

            var family = await _client.GetProductFamilyByHandleAsync(_options.ProductFamilyHandle, cancellationToken);
            if (family is null)
            {
                throw new BillingProviderException($"The Maxio product family '{_options.ProductFamilyHandle}' does not exist on this site.");
            }

            var products = await _client.ListProductsAsync(family.Id, cancellationToken);

            return (IReadOnlyList<SubscriptionPlan>)products
                .Where(p => p.ArchivedAt is null)
                .Select(MapPlan)
                .ToList();
        }) ?? Array.Empty<SubscriptionPlan>();
    }

    public async Task<SubscribeResult> SubscribeAsync(Subscriber subscriber, string productHandle, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(subscriber, nameof(subscriber));
        Guard.Against.NullOrEmpty(subscriber.UserId, nameof(subscriber.UserId));
        Guard.Against.NullOrEmpty(productHandle, nameof(productHandle));

        var plans = await GetPlansAsync(cancellationToken);
        var plan = plans.FirstOrDefault(p => string.Equals(p.Handle, productHandle, StringComparison.OrdinalIgnoreCase));
        if (plan is null)
        {
            throw new SubscriptionPlanNotFoundException(productHandle);
        }

        var gate = _subscribeGates.GetOrAdd(subscriber.UserId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var customer = await EnsureCustomerAsync(subscriber, cancellationToken);

            var subscriptions = await _client.ListCustomerSubscriptionsAsync(customer.Id, cancellationToken);
            var existing = subscriptions.FirstOrDefault(s =>
                string.Equals(s.Product?.Handle, plan.Handle, StringComparison.OrdinalIgnoreCase) &&
                IsOpenState(s.State));

            if (existing is not null)
            {
                _logger.LogInformation("User {UserId} already subscribed to plan {PlanHandle}; returning existing subscription {SubscriptionId}.",
                    subscriber.UserId, plan.Handle, existing.Id);
                return new SubscribeResult(MapDetail(existing), alreadySubscribed: true);
            }

            var created = await _client.CreateSubscriptionAsync(customer.Id, plan.Handle, cancellationToken);
            _logger.LogInformation("Created Maxio subscription {SubscriptionId} for user {UserId} on plan {PlanHandle}.",
                created.Id, subscriber.UserId, plan.Handle);

            return new SubscribeResult(MapDetail(created), alreadySubscribed: false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<SubscriptionDetail>> GetSubscriptionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(userId, nameof(userId));

        var customer = await _client.GetCustomerByReferenceAsync(CustomerReferenceFor(userId), cancellationToken);
        if (customer is null)
        {
            return Array.Empty<SubscriptionDetail>();
        }

        var subscriptions = await _client.ListCustomerSubscriptionsAsync(customer.Id, cancellationToken);

        return subscriptions
            .OrderByDescending(s => s.CreatedAt)
            .Select(MapDetail)
            .ToList();
    }

    private async Task<MaxioCustomer> EnsureCustomerAsync(Subscriber subscriber, CancellationToken cancellationToken)
    {
        var reference = CustomerReferenceFor(subscriber.UserId);

        var existing = await _client.GetCustomerByReferenceAsync(reference, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        try
        {
            var created = await _client.CreateCustomerAsync(
                subscriber.FirstName, subscriber.LastName, subscriber.Email, reference, cancellationToken);
            _logger.LogInformation("Created Maxio customer {CustomerId} with reference {Reference}.", created.Id, reference);
            return created;
        }
        catch (MaxioApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            // The reference is unique per site; a concurrent request may have created the
            // customer first. Re-read it instead of failing.
            var raced = await _client.GetCustomerByReferenceAsync(reference, cancellationToken);
            if (raced is not null)
            {
                _logger.LogInformation("Maxio customer for reference {Reference} was created concurrently; using customer {CustomerId}.",
                    reference, raced.Id);
                return raced;
            }
            throw;
        }
    }

    private static string CustomerReferenceFor(string userId) => CustomerReferencePrefix + userId;

    private static bool IsOpenState(string state) => !ClosedSubscriptionStates.Contains(state, StringComparer.OrdinalIgnoreCase);

    private static SubscriptionPlan MapPlan(MaxioProduct product) => new()
    {
        Handle = product.Handle,
        Name = product.Name,
        Description = product.Description,
        PriceInCents = product.PriceInCents,
        FormattedPrice = FormatPrice(product.PriceInCents),
        Interval = product.Interval,
        IntervalUnit = product.IntervalUnit
    };

    private static SubscriptionDetail MapDetail(MaxioSubscription subscription) => new()
    {
        Id = subscription.Id,
        PlanHandle = subscription.Product?.Handle ?? string.Empty,
        PlanName = subscription.Product?.Name ?? string.Empty,
        State = subscription.State,
        PriceInCents = subscription.ProductPriceInCents,
        FormattedPrice = FormatPrice(subscription.ProductPriceInCents),
        NextBillingAt = subscription.NextAssessmentAt ?? subscription.CurrentPeriodEndsAt,
        CurrentPeriodEndsAt = subscription.CurrentPeriodEndsAt,
        ActivatedAt = subscription.ActivatedAt,
        CreatedAt = subscription.CreatedAt,
        CustomerId = subscription.Customer?.Id ?? 0,
        CustomerReference = subscription.Customer?.Reference ?? string.Empty
    };

    private static string FormatPrice(int cents)
    {
        var amount = cents / 100m;
        return string.Format(CultureInfo.GetCultureInfo("en-US"), "{0:C}", amount);
    }
}