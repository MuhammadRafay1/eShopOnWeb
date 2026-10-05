using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class SubscriptionPlanDto
{
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? PriceInCents { get; set; }
    public decimal? Price { get; set; }
    public int? Interval { get; set; }
    public string? IntervalUnit { get; set; }

    public static SubscriptionPlanDto From(SubscriptionPlan plan) => new()
    {
        Handle = plan.Handle,
        Name = plan.Name,
        Description = plan.Description,
        PriceInCents = plan.PriceInCents,
        Price = plan.PriceInCents / 100m,
        Interval = plan.Interval,
        IntervalUnit = plan.IntervalUnit,
    };
}

public class SubscriptionDto
{
    public int Id { get; set; }
    public string? PlanHandle { get; set; }
    public string? PlanName { get; set; }
    public string? State { get; set; }
    public long? PriceInCents { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    /// <summary>When the next regularly scheduled charge occurs (end of the current billing period).</summary>
    public DateTimeOffset? NextBillingAt { get; set; }
    public DateTimeOffset? NextAssessmentAt { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }

    public static SubscriptionDto From(BillingSubscription subscription, SubscriptionPlan? plan = null) => new()
    {
        Id = subscription.Id,
        PlanHandle = subscription.PlanHandle ?? plan?.Handle,
        PlanName = subscription.PlanName ?? plan?.Name,
        State = subscription.State,
        PriceInCents = subscription.PriceInCents ?? plan?.PriceInCents,
        Price = (subscription.PriceInCents ?? plan?.PriceInCents) / 100m,
        Currency = subscription.Currency,
        NextBillingAt = subscription.NextBillingAt,
        NextAssessmentAt = subscription.NextAssessmentAt,
        CreatedAt = subscription.CreatedAt,
        ActivatedAt = subscription.ActivatedAt,
    };
}
