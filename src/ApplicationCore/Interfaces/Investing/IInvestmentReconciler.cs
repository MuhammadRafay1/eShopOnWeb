using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;

/// <summary>
/// Settles pending investments and advances pending enrolments against the provider, so the application
/// reflects what actually happened at Upvest. Driven both on a timer and on inbound webhook notifications.
/// </summary>
public interface IInvestmentReconciler
{
    Task ReconcileAllAsync(CancellationToken cancellationToken);
}
