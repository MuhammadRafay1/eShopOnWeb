using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// In-process implementation of <see cref="IPaymentLock"/> backed by a dictionary of semaphores.
/// Registered as a singleton so the semaphores are shared across requests in the host.
/// </summary>
public sealed class PaymentLock : IPaymentLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(gate);
    }

    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _gate;
        public Releaser(SemaphoreSlim gate) => _gate = gate;

        public void Dispose()
        {
            _gate?.Release();
            _gate = null;
        }
    }
}
