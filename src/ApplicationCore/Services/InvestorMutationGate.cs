using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>Process-wide single-writer gate for investor state. Registered as a singleton.</summary>
public sealed class InvestorMutationGate : IInvestorMutationGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RunAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
