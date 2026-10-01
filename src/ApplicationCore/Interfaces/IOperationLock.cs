using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// A per-key async mutex that serializes money-moving operations on the same order/payment within this process,
/// so a double-click cannot run two authorize/capture/refund attempts concurrently. The authoritative
/// cross-process guarantee against double money movement is PayPal's own request-id dedup (the gateway sends a
/// deterministic key); this lock plus a status check makes the local effect idempotent.
/// </summary>
public interface IOperationLock
{
    Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken);
}
