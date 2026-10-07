using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the "invest your change" capability: enrolment, setting aside
/// the rounding difference of paid orders, investing the balance once it is
/// large enough, and reflecting what happened to each investment at the
/// provider. Every method is scoped to a single shopper (<c>buyerId</c>).
/// </summary>
public interface IInvestingService
{
    /// <summary>Opts the shopper in, registering them as an investor with the provider.</summary>
    Task<EnrolmentView> EnrolAsync(string buyerId, InvestorRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>The shopper's current enrolment, or <c>null</c> if they have never opted in.</summary>
    Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets aside the rounding difference of a paid order and, if the balance
    /// has reached the threshold, invests it. Returns the amount set aside by
    /// this order (0 when nothing was set aside). Never throws — investing must
    /// never make an order fail.
    /// </summary>
    Task<decimal> ApplyPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken = default);

    /// <summary>What the shopper currently has set aside and has invested so far.</summary>
    Task<BalanceView?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>The shopper's investments, newest first.</summary>
    Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-reads the outcome of the investment carried by the given provider
    /// order and updates it. Used by the provider's own callback. Never throws.
    /// </summary>
    Task ReconcileByProviderOrderAsync(string providerOrderId, CancellationToken cancellationToken = default);
}

public record EnrolmentView(Guid EnrolmentId, EnrolmentStatus Status);

public record BalanceView(decimal PendingAmount, decimal InvestedAmount);

public record InvestmentView(Guid InvestmentId, decimal Amount, InvestmentStatus Status, DateTimeOffset CreatedAt);
