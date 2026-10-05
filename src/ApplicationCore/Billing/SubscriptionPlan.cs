namespace Microsoft.eShopWeb.ApplicationCore.Billing;

/// <summary>
/// A recurring subscription plan available for purchase (e.g. a monthly membership).
/// Prices are expressed in the site's base currency.
/// </summary>
public sealed record SubscriptionPlan(
    string Handle,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int IntervalCount,
    string IntervalUnit);