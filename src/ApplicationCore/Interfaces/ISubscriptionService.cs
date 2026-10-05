using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface ISubscriptionService
{
    Task<PlanCatalog> GetPlansAsync(CancellationToken cancellationToken = default);

    Task<SubscribeResult> SubscribeAsync(Subscriber subscriber, string planHandle, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingSubscription>> GetSubscriptionsAsync(string buyerId, CancellationToken cancellationToken = default);
}
