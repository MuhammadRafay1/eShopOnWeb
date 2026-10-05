using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Billing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Recurring-subscription billing capability, backed by an external billing
/// system of record. All operations are idempotent with respect to the
/// subscriber's identity.
/// </summary>
public interface IBillingService
{
    /// <summary>
    /// Lists the subscription plans available for purchase.
    /// </summary>
    Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a customer exists in the billing system for the given subscriber
    /// and enrolls them in the specified plan. Safe to call repeatedly: a
    /// subscriber who already has a live subscription to the plan gets it back
    /// instead of a duplicate.
    /// </summary>
    /// <exception cref="Exceptions.BillingException">Thrown when the plan handle is unknown.</exception>
    Task<SubscriptionEnrollment> EnrollAsync(SubscriberInfo subscriber, string planHandle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all subscriptions (active and historical) that belong to the
    /// given subscriber, or an empty list when the subscriber was never
    /// enrolled.
    /// </summary>
    Task<IReadOnlyList<SubscriptionSummary>> ListSubscriptionsAsync(string userId, CancellationToken cancellationToken = default);
}