using System;
using Microsoft.eShopWeb.ApplicationCore.Billing;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Maps ApplicationCore billing models to the API contract
/// </summary>
public static class SubscriptionDtoMapper
{
    public static SubscriptionDto ToDto(SubscriptionSummary subscription) => new()
    {
        Id = subscription.Id,
        PlanHandle = subscription.PlanHandle,
        PlanName = subscription.PlanName,
        Price = subscription.Price,
        Currency = subscription.Currency,
        State = subscription.State,
        NextBillingAt = Format(subscription.NextBillingAt),
        CurrentPeriodEndsAt = Format(subscription.CurrentPeriodEndsAt),
        ActivatedAt = Format(subscription.ActivatedAt),
        CanceledAt = Format(subscription.CanceledAt)
    };

    private static string? Format(DateTimeOffset? value) => value?.ToString("O");
}