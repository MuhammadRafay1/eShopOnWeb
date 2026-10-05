using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Serializes a shopper's enrolment and investment operations within the process, so a double-submit cannot
/// enrol twice or cross the investment threshold twice. A process-wide (singleton) guard; in a multi-host
/// deployment it would be backed by a distributed lock or a unique store constraint instead.
/// </summary>
public interface IInvestingLocks
{
    Task<IDisposable> AcquireAsync(string buyerId, CancellationToken cancellationToken);
}
