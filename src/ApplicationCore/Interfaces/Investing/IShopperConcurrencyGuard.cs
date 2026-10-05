using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;

/// <summary>
/// Serializes mutations to a single shopper's enrolment/ledger so a round-up being set aside on a request
/// thread cannot race the background processor taking the balance for an investment. In-process and
/// single-host — adequate for this deployment (one PublicApi host; see the plan's concurrency notes).
/// </summary>
public interface IShopperConcurrencyGuard
{
    Task<IDisposable> LockAsync(string shopperId, CancellationToken cancellationToken);
}
