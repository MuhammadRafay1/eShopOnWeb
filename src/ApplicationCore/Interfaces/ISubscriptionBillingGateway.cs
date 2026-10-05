using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The billing system of record. Implementations translate every provider failure into
/// <see cref="Exceptions.BillingProviderException"/> and bound the whole request's provider work by one deadline.
/// </summary>
public interface ISubscriptionBillingGateway
{
    /// <summary>The plans offered by the configured product family.</summary>
    Task<PlanCatalog> ListPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>The provider customer id for <paramref name="customerReference"/>, or null when no such customer exists.</summary>
    Task<int?> FindCustomerIdByReferenceAsync(string customerReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the customer and returns its id. When the outcome is ambiguous (timeout, dropped connection, or the
    /// reference already exists) the customer is looked up by its reference before anything is reported.
    /// Throws <see cref="Exceptions.BillingOutcomeUnknownException"/> when that lookup cannot settle it.
    /// </summary>
    Task<int> CreateCustomerAsync(NewBillingCustomer customer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes the customer to the plan. An ambiguous outcome is settled by finding the subscription by
    /// <paramref name="subscriptionReference"/>; <see cref="Exceptions.BillingOutcomeUnknownException"/> when it cannot be.
    /// </summary>
    Task<BillingSubscription> CreateSubscriptionAsync(int customerId, string planHandle, string subscriptionReference,
        CancellationToken cancellationToken = default);

    /// <summary>All subscriptions the billing system holds for the customer.</summary>
    Task<IReadOnlyList<BillingSubscription>> ListCustomerSubscriptionsAsync(int customerId,
        CancellationToken cancellationToken = default);
}
