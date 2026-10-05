namespace Microsoft.eShopWeb.ApplicationCore.Billing;

/// <summary>
/// The result of enrolling a subscriber in a plan. Idempotent: enrolling the
/// same user in the same plan twice returns the existing subscription with
/// <see cref="AlreadySubscribed"/> set to <c>true</c> instead of creating a
/// second one.
/// </summary>
public sealed record SubscriptionEnrollment(
    SubscriptionSummary Subscription,
    bool AlreadySubscribed);