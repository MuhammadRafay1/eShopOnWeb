using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;

/// <summary>
/// Orchestrates the "invest your change" capability for a single shopper, identified by their token.
/// All operations are scoped to <paramref name="buyerId"/>; one shopper can never see another's data.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opts the shopper in (idempotent per shopper) and returns the enrolment view.</summary>
    Task<EnrolmentView> EnrolAsync(string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken);

    /// <summary>Returns the shopper's enrolment view, reconciling its status, or null if not enrolled.</summary>
    Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>Returns the shopper's set-aside and invested balances.</summary>
    Task<BalanceView> GetBalanceAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>Returns the shopper's investments, newest first.</summary>
    Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>
    /// Called when one of the shopper's orders is paid. Sets aside the round-up to the next whole euro for
    /// an accepted investor and invests the balance once it reaches the threshold. Returns the amount this
    /// order set aside, in cents (0 if nothing was set aside). Never throws — investing must never fail an
    /// order.
    /// </summary>
    Task<long> OnOrderPaidAsync(string buyerId, decimal orderTotalEuros, CancellationToken cancellationToken);
}

public sealed record EnrolmentView(int EnrolmentId, EnrolmentStatus Status);
public sealed record BalanceView(long PendingAmountCents, long InvestedAmountCents);
public sealed record InvestmentView(int InvestmentId, long AmountCents, InvestmentStatus Status);
