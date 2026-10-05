using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Investing;

/// <summary>Per-shopper in-process mutual exclusion backed by one <see cref="SemaphoreSlim"/> per shopper.</summary>
public sealed class ShopperConcurrencyGuard : IShopperConcurrencyGuard
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> LockAsync(string shopperId, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(shopperId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _semaphore;
        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;
        public void Dispose()
        {
            _semaphore?.Release();
            _semaphore = null;
        }
    }
}
