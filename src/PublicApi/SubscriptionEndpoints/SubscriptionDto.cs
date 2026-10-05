namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// A subscription as tracked by the billing system of record
/// </summary>
public class SubscriptionDto
{
    public string Id { get; set; } = string.Empty;
    public string PlanHandle { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string State { get; set; } = string.Empty;
    public string? NextBillingAt { get; set; }
    public string? CurrentPeriodEndsAt { get; set; }
    public string? ActivatedAt { get; set; }
    public string? CanceledAt { get; set; }
}