using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;

/// <summary>
/// The background work that drives investing forward independently of any request: advancing pending
/// enrolments to accepted (and provisioning their accounts), investing balances that have reached the
/// threshold, and settling placed investments against the provider. Each pass is idempotent and safe to
/// run on a timer.
/// </summary>
public interface IInvestingProcessor
{
    /// <summary>Poll pending enrolments for acceptance and provision accounts once accepted.</summary>
    Task ProcessEnrolmentsAsync(CancellationToken cancellationToken);

    /// <summary>Invest balances that have reached the threshold, and carry placed investments forward.</summary>
    Task ProcessInvestmentsAsync(CancellationToken cancellationToken);

    /// <summary>Settle placed investments by reading their provider order outcome.</summary>
    Task ReconcileInvestmentsAsync(CancellationToken cancellationToken);
}
