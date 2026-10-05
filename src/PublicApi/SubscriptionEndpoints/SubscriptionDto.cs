using System;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// A shopper's subscription as recorded in Maxio Advanced Billing.
/// </summary>
public class SubscriptionDto
{
    public int Id { get; set; }

    public string PlanHandle { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    /// <summary>Recurring plan price in dollars.</summary>
    public decimal Price { get; set; }

    /// <summary>Subscription state (e.g. active, trialing, canceled, past_due).</summary>
    public string State { get; set; } = string.Empty;

    public DateTimeOffset? CurrentPeriodStartedAt { get; set; }

    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }

    /// <summary>
    /// When the next billing attempt occurs (next_assessment_at, falling back to
    /// current_period_ends_at when the API does not provide it).
    /// </summary>
    public DateTimeOffset? NextBillingAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CanceledAt { get; set; }

    /// <summary>Outstanding balance in cents.</summary>
    public long BalanceInCents { get; set; }

    public string? PaymentCollectionMethod { get; set; }
}