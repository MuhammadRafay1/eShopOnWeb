using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Process-wide per-shopper locking. Registered as a singleton so one shopper's enrolment and investment
/// operations are serialized across concurrent requests within the host.
/// </summary>
public sealed class InvestingLocks : IInvestingLocks
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public async Task<IDisposable> AcquireAsync(string buyerId, CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd(buyerId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
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
