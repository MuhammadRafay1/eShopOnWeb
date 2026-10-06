using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Serialises read-modify-write access to investor state. The order endpoint, the reconciler and
/// the webhook handler all mutate the same investor aggregate from different request/background
/// contexts; without a single writer the in-memory provider (which has no concurrency control)
/// can lose updates. The gate must only ever be held around local database work — never around a
/// call to Upvest — so that an inbound webhook delivered mid-investment cannot deadlock against it.
/// </summary>
public interface IInvestorMutationGate
{
    Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task RunAsync(Func<Task> action, CancellationToken cancellationToken = default);
}
