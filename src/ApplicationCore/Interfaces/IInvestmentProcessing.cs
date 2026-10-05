using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Hands a newly started investment to the background processor. Placing the order must not wait on it.</summary>
public interface IInvestmentQueue
{
    void Enqueue(int investmentId);
}

/// <summary>
/// Reconciles a shopper's enrolment against Upvest: user activation, account-group/account creation once the
/// user is active, and account activation. Idempotent; safe to call repeatedly. Mutates and persists the investor.
/// </summary>
public interface IEnrolmentReconciler
{
    Task ReconcileAsync(Investor investor, CancellationToken cancellationToken);
}
