using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Serialises the quick read-modify-write sections that touch the shared in-memory ledger and enrolment
/// state, so order round-ups, enrolment creation and the reconciliation worker never race each other.
/// Upvest network calls are made outside the lock; only the state mutations are guarded.
/// </summary>
public sealed class InvestingCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T> RunLockedAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { _gate.Release(); }
    }

    public async Task RunLockedAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await action(); }
        finally { _gate.Release(); }
    }
}
