using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Billing;

/// <summary>
/// Recurring-subscription billing capability backed by Maxio Advanced Billing.
/// The caller's identity comes from the JWT bearer token; Maxio is the system of record.
/// </summary>
public interface ISubscriptionBillingService
{
    /// <summary>
    /// Lists the subscription plans (products of the configured Maxio product family) available for signup.
    /// </summary>
    Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enrolls the given shopper in the plan identified by <paramref name="planHandle"/>.
    /// Idempotent: subscribing twice (or concurrently) to the same plan returns the existing subscription,
    /// and never creates a second Maxio customer or subscription.
    /// </summary>
    Task<SubscriptionSummary> SubscribeAsync(Subscriber subscriber, string planHandle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the Maxio subscriptions belonging to the given shopper.
    /// Returns an empty list when the shopper has no Maxio customer yet.
    /// </summary>
    Task<IReadOnlyList<SubscriptionSummary>> ListSubscriptionsAsync(Subscriber subscriber, CancellationToken cancellationToken = default);
}