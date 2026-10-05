using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the five "Invest your change" flows on top of the domain and the Upvest gateway.
/// Every method is scoped to one shopper (<c>buyerId</c>) and never exposes another shopper's data.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt the shopper in. Idempotent: returns the existing enrolment if already enrolled.</summary>
    Task<EnrolmentView> EnrolAsync(string buyerId, InvestorSignUp signUp, CancellationToken cancellationToken);

    /// <summary>The shopper's enrolment, reconciling acceptance with Upvest. Null if not enrolled.</summary>
    Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>
    /// Place an order for the shopper from catalog items, mark it paid, and — for an accepted investor —
    /// set aside the round-up and invest when the threshold is reached. Never fails because of investing.
    /// </summary>
    Task<OrderPlacementResult> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken);

    /// <summary>The shopper's investments, newest first, reconciling pending ones with Upvest.</summary>
    Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>The shopper's current set-aside and total-invested amounts, reconciling pending investments.</summary>
    Task<BalanceView> GetBalanceAsync(string buyerId, CancellationToken cancellationToken);
}
