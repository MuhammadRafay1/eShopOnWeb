using System;

namespace Microsoft.eShopWeb.ApplicationCore.Billing;

/// <summary>
/// A customer's subscription as tracked by the billing system of record,
/// including the plan, the current price, the lifecycle state and the
/// next scheduled billing date.
/// </summary>
public sealed record SubscriptionSummary(
    string Id,
    string PlanHandle,
    string PlanName,
    decimal Price,
    string Currency,
    string State,
    DateTimeOffset? NextBillingAt,
    DateTimeOffset? CurrentPeriodEndsAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? CanceledAt);