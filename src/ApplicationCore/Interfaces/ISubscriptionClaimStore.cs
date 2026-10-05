using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Local claims that keep a double-submitted request from reaching the billing system twice.
/// The <c>TryClaim…</c> methods insert a row keyed by its natural key and return false when the store refuses it
/// because the key is already taken.
/// </summary>
public interface ISubscriptionClaimStore
{
    Task<BillingCustomer?> GetCustomerAsync(string buyerId, CancellationToken cancellationToken = default);

    Task<bool> TryClaimCustomerAsync(BillingCustomer customer, CancellationToken cancellationToken = default);

    Task SaveCustomerAsync(BillingCustomer customer, CancellationToken cancellationToken = default);

    Task ReleaseCustomerAsync(BillingCustomer customer, CancellationToken cancellationToken = default);

    Task<SubscriptionEnrollment?> GetEnrollmentAsync(string buyerId, string planHandle, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionEnrollment>> ListEnrollmentsAsync(string buyerId, CancellationToken cancellationToken = default);

    Task<bool> TryClaimEnrollmentAsync(SubscriptionEnrollment enrollment, CancellationToken cancellationToken = default);

    Task SaveEnrollmentAsync(SubscriptionEnrollment enrollment, CancellationToken cancellationToken = default);

    /// <summary>Deletes the claim. Returns false when it was already gone (another request released it first).</summary>
    Task<bool> ReleaseEnrollmentAsync(SubscriptionEnrollment enrollment, CancellationToken cancellationToken = default);
}
