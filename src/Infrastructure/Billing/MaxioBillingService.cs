using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Billing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Billing.MaxioApi;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Billing;

/// <summary>
/// Recurring-subscription billing backed by the Maxio Advanced Billing API.
///
/// Every eShopOnWeb user is mapped to exactly one Maxio customer, keyed by the
/// customer "reference" field (<c>eShopWeb-user-&lt;userId&gt;</c>). Because the
/// reference is unique per site and stored on the Maxio side, customer and
/// subscription lookups are idempotent even across application restarts and
/// with no local persistence.
/// </summary>
public sealed class MaxioBillingService : IBillingService
{
    private const string CustomerReferencePrefix = "eShopWeb-user-";
    private const string RemittanceCollectionMethod = "remittance";

    /// <summary>Subscription states that mean the subscription has ended.</summary>
    private static readonly HashSet<string> EndedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "canceled",
        "expired",
        "expired_cards",
        "trial_ended"
    };

    private readonly MaxioApiClient _client;
    private readonly MaxioBillingOptions _options;

    public MaxioBillingService(MaxioApiClient client, IOptions<MaxioBillingOptions> options)
    {
        _client = client;
        _options = options.Value;
        _options.Validate();
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default)
    {
        var currency = await GetCurrencyAsync(cancellationToken);
        var products = await _client.ListProductsForFamilyAsync(_options.ProductFamilyHandle!, cancellationToken);

        return products
            .Where(p => p.ArchivedAt is null)
            .OrderBy(p => p.PriceInCents ?? 0)
            .Select(p => MapPlan(p, currency))
            .ToList();
    }

    public async Task<SubscriptionEnrollment> EnrollAsync(
        SubscriberInfo subscriber, string planHandle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planHandle))
        {
            throw new BillingException("A plan handle is required.");
        }

        var product = await _client.LookupProductByHandleAsync(planHandle.Trim(), cancellationToken)
            ?? throw new BillingException($"No subscription plan with handle '{planHandle}' exists.", 404);

        if (!string.Equals(product.ProductFamily?.Handle, _options.ProductFamilyHandle, StringComparison.OrdinalIgnoreCase))
        {
            throw new BillingException($"Plan '{planHandle}' is not part of the subscribable catalog.");
        }

        var customer = await GetOrCreateCustomerAsync(subscriber, cancellationToken);

        var existing = await FindLiveSubscriptionAsync(customer.Id, planHandle, cancellationToken);
        if (existing is not null)
        {
            return new SubscriptionEnrollment(MapSubscription(existing, subscriber.UserId), AlreadySubscribed: true);
        }

        // Deterministic token: a retried or double-clicked enroll hits the
        // billing API's duplicate-prevention (409) instead of creating a second
        // subscription for the same user+plan.
        var token = DeterministicToken("subscription", subscriber.UserId, planHandle);
        var payload = new MaxioCreateSubscriptionPayload(
            new MaxioCreateSubscriptionBody(
                product.Handle!,
                customer.Id,
                RemittanceCollectionMethod),
            token);

        var result = await _client.TryCreateSubscriptionAsync(payload, cancellationToken);
        if (result.IsSuccess && result.Value?.Subscription is not null)
        {
            return new SubscriptionEnrollment(MapSubscription(result.Value.Subscription, subscriber.UserId), AlreadySubscribed: false);
        }

        if (result.StatusCode == 409)
        {
            // Duplicate-prevention: an identical enrollment is already in
            // flight or completed. Resolve it and return the live subscription.
            var live = await FindLiveSubscriptionAsync(customer.Id, planHandle, cancellationToken);
            if (live is not null)
            {
                return new SubscriptionEnrollment(MapSubscription(live, subscriber.UserId), AlreadySubscribed: true);
            }

            // No live subscription (e.g. re-subscribing after a cancellation
            // within the token window): retry once with a fresh token.
            payload = payload with { UniquenessToken = Guid.NewGuid().ToString("N") };
            result = await _client.TryCreateSubscriptionAsync(payload, cancellationToken);
            if (result.IsSuccess && result.Value?.Subscription is not null)
            {
                return new SubscriptionEnrollment(MapSubscription(result.Value.Subscription, subscriber.UserId), AlreadySubscribed: false);
            }
        }

        throw new BillingException(
            $"Enrollment in plan '{planHandle}' failed: {result.ErrorBody}", MapClientError(result.StatusCode));
    }

    public async Task<IReadOnlyList<SubscriptionSummary>> ListSubscriptionsAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var customer = await _client.LookupCustomerByReferenceAsync(CustomerReference(userId), cancellationToken);
        if (customer is null)
        {
            return Array.Empty<SubscriptionSummary>();
        }

        var subscriptions = await _client.ListCustomerSubscriptionsAsync(customer.Id, cancellationToken);
        return subscriptions
            .OrderByDescending(s => s.CreatedAt ?? DateTimeOffset.MinValue)
            .Select(s => MapSubscription(s, userId))
            .ToList();
    }

    private async Task<MaxioCustomer> GetOrCreateCustomerAsync(
        SubscriberInfo subscriber, CancellationToken cancellationToken)
    {
        var reference = CustomerReference(subscriber.UserId);
        var existing = await _client.LookupCustomerByReferenceAsync(reference, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var (firstName, lastName) = SplitEmailIntoNames(subscriber.Email);
        var result = await _client.TryCreateCustomerAsync(
            new MaxioCreateCustomerBody(firstName, lastName, subscriber.Email, reference),
            DeterministicToken("customer", subscriber.UserId),
            cancellationToken);

        if (result.IsSuccess && result.Value?.Customer is not null)
        {
            return result.Value.Customer;
        }

        // A concurrent request (e.g. double-click) may have created the
        // customer first — the reference is unique, so re-lookup resolves it.
        existing = await _client.LookupCustomerByReferenceAsync(reference, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        throw new BillingException(
            $"Could not create a billing customer for '{subscriber.Email}': {result.ErrorBody}",
            MapClientError(result.StatusCode));
    }

    private async Task<MaxioSubscription?> FindLiveSubscriptionAsync(
        long customerId, string planHandle, CancellationToken cancellationToken)
    {
        var subscriptions = await _client.ListCustomerSubscriptionsAsync(customerId, cancellationToken);
        return subscriptions.FirstOrDefault(s =>
            string.Equals(s.Product?.Handle, planHandle, StringComparison.OrdinalIgnoreCase) &&
            !EndedStates.Contains(s.State ?? string.Empty));
    }

    private async Task<string> GetCurrencyAsync(CancellationToken cancellationToken)
    {
        return await _client.GetSiteCurrencyAsync(cancellationToken) ?? "USD";
    }

    private static string CustomerReference(string userId) => $"{CustomerReferencePrefix}{userId}";

    private static (string FirstName, string LastName) SplitEmailIntoNames(string email)
    {
        var localPart = email.Split('@')[0];
        return (localPart, "eShopOnWeb");
    }

    private static int MapClientError(int statusCode) => statusCode switch
    {
        404 => 404,
        422 => 400,
        _ => 502
    };

    private static string DeterministicToken(params string[] parts)
    {
        var joined = string.Join("|", parts);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static SubscriptionPlan MapPlan(MaxioProduct product, string currency)
    {
        return new SubscriptionPlan(
            product.Handle ?? string.Empty,
            product.Name ?? string.Empty,
            product.Description,
            (product.PriceInCents ?? 0) / 100m,
            currency,
            product.Interval ?? 1,
            product.IntervalUnit ?? "month");
    }

    private static SubscriptionSummary MapSubscription(MaxioSubscription subscription, string userId)
    {
        var priceInCents = subscription.ProductPriceInCents ?? subscription.Product?.PriceInCents ?? 0;
        return new SubscriptionSummary(
            subscription.Id.ToString(),
            subscription.Product?.Handle ?? string.Empty,
            subscription.Product?.Name ?? string.Empty,
            priceInCents / 100m,
            subscription.Currency ?? "USD",
            subscription.State ?? string.Empty,
            subscription.NextAssessmentAt ?? subscription.CurrentPeriodEndsAt,
            subscription.CurrentPeriodEndsAt,
            subscription.ActivatedAt,
            subscription.CanceledAt);
    }
}