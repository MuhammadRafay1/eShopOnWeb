using System;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Billing;

/// <summary>
/// A shopper's subscription as recorded in Maxio Advanced Billing.
/// </summary>
public record SubscriptionSummary(
    int Id,
    string PlanHandle,
    string PlanName,
    string State,
    long? PriceInCents,
    DateTimeOffset? NextBillingAt,
    DateTimeOffset? CurrentPeriodEndsAt,
    DateTimeOffset? ActivatedAt);