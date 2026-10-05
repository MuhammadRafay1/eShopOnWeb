using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Converges local state with Upvest: completes pending enrolments as users and
/// accounts activate, invests balances that have reached the threshold, and
/// settles investments as their orders reach their outcome at Upvest.
/// Driven both on a timer and on inbound Upvest webhooks.
/// </summary>
public interface IInvestmentReconciliationService
{
    Task ReconcileAsync(CancellationToken ct);
}
