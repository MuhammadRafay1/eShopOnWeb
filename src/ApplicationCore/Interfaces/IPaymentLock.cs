using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// A per-key async mutex that serializes concurrent payment operations for the same order (or
/// shopper), so a double-click cannot authorize, capture, or refund twice. With the in-memory store
/// the app runs single-host per run, so an in-process lock plus the persisted status/claim is the
/// duplicate guard; PayPal's own idempotency keys are the cross-process backstop.
/// </summary>
public interface IPaymentLock
{
    /// <summary>Acquires the lock for <paramref name="key"/>; dispose the result to release it.</summary>
    Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default);
}
