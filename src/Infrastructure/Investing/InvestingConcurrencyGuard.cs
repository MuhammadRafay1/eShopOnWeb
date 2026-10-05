using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Serialises all read-modify-write work on investing accounts. The background sweep, inbound
/// webhooks and the set-aside path share one process-wide lock so a shopper's balance can never
/// be corrupted by two operations racing (e.g. investing a balance while change is being added).
/// Held only around the short database critical sections, never around slow Upvest network calls.
/// </summary>
public sealed class InvestingConcurrencyGuard
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose()
        {
            if (!_released)
            {
                _released = true;
                _semaphore.Release();
            }
        }
    }
}
