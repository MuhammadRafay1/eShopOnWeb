namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// A subscription plan (Maxio product) available to shoppers.
/// </summary>
public class SubscriptionPlanDto
{
    /// <summary>Stable Maxio API handle - the value used to subscribe.</summary>
    public string Handle { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Recurring price in dollars.</summary>
    public decimal Price { get; set; }

    public int Interval { get; set; }

    /// <summary>Interval unit per the spec: "month" or "day".</summary>
    public string IntervalUnit { get; set; } = string.Empty;

    public bool RequiresPaymentMethod { get; set; }

    public bool HasTrial { get; set; }
}