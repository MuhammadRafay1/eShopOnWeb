namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Billing;

/// <summary>
/// A recurring-subscription plan offered for signup (a Maxio product in the configured product family).
/// </summary>
public record SubscriptionPlan(
    string Handle,
    string Name,
    string? Description,
    long? PriceInCents,
    int? Interval,
    string? IntervalUnit,
    bool? RequiresPaymentMethod);