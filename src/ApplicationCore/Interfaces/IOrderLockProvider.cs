using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Serializes payment operations (authorize/fulfil/cancel/refund) per order within this process, so two
/// concurrent requests for the same order never race between checking its state and acting on PayPal.
/// This is a single-host, in-process backstop; <c>PayPal-Request-Id</c> idempotency keys are the
/// cross-process/cross-restart backstop.
/// </summary>
public interface IOrderLockProvider
{
    Task<IDisposable> AcquireAsync(int orderId, CancellationToken ct = default);
}
