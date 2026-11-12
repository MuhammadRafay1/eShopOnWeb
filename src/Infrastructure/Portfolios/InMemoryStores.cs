using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolios;

namespace Microsoft.eShopWeb.Infrastructure.Portfolios;

// The task mandates the in-memory database in this environment, where data lives only for one run.
// These thread-safe in-memory stores keep the portfolio domain self-contained and clearly swappable
// for a durable implementation (EF Core, etc.) behind the same interfaces, without touching the
// catalog/identity contexts. They are registered as singletons.

public class InMemoryInvestorStore : IInvestorStore
{
    // Keyed by shopper id (one investor per shopper).
    private readonly ConcurrentDictionary<string, Investor> _byShopper = new(StringComparer.Ordinal);

    public Task<Investor?> GetByShopperAsync(string shopperId, CancellationToken ct = default)
        => Task.FromResult(_byShopper.TryGetValue(shopperId, out var investor) ? investor : null);

    public Task<Investor> AddAsync(Investor investor, CancellationToken ct = default)
    {
        if (!_byShopper.TryAdd(investor.ShopperId, investor))
            throw new PortfolioConflictException("This shopper is already registered as an investor.");
        return Task.FromResult(investor);
    }

    public Task UpdateAsync(Investor investor, CancellationToken ct = default)
    {
        _byShopper[investor.ShopperId] = investor;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Investor>> ListByModelAsync(string modelId, CancellationToken ct = default)
    {
        IReadOnlyList<Investor> result = _byShopper.Values
            .Where(i => string.Equals(i.ModelId, modelId, StringComparison.Ordinal))
            .ToList();
        return Task.FromResult(result);
    }
}

public class InMemoryModelPortfolioStore : IModelPortfolioStore
{
    private readonly ConcurrentDictionary<string, ModelPortfolio> _byId = new(StringComparer.Ordinal);

    public Task<ModelPortfolio?> GetAsync(string modelId, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(modelId, out var model) ? model : null);

    public Task<IReadOnlyList<ModelPortfolio>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ModelPortfolio> result = _byId.Values.OrderBy(m => m.CreatedAt).ToList();
        return Task.FromResult(result);
    }

    public Task UpsertAsync(ModelPortfolio model, CancellationToken ct = default)
    {
        _byId[model.ModelId] = model;
        return Task.CompletedTask;
    }
}

public class InMemoryPaymentStore : IPaymentStore
{
    private readonly ConcurrentDictionary<Guid, Payment> _byId = new();

    public Task<Payment> AddAsync(Payment payment, CancellationToken ct = default)
    {
        _byId[payment.Id] = payment;
        return Task.FromResult(payment);
    }

    public Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _byId[payment.Id] = payment;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Payment>> ListByShopperAsync(string shopperId, CancellationToken ct = default)
    {
        IReadOnlyList<Payment> result = _byId.Values
            .Where(p => string.Equals(p.ShopperId, shopperId, StringComparison.Ordinal))
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
        return Task.FromResult(result);
    }
}

public class InMemoryRealignmentStore : IRealignmentStore
{
    private readonly ConcurrentDictionary<Guid, Realignment> _byId = new();

    public Task<Realignment> AddAsync(Realignment realignment, CancellationToken ct = default)
    {
        _byId[realignment.Id] = realignment;
        return Task.FromResult(realignment);
    }

    public Task UpdateAsync(Realignment realignment, CancellationToken ct = default)
    {
        _byId[realignment.Id] = realignment;
        return Task.CompletedTask;
    }

    public Task<Realignment?> GetAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(id, out var realignment) ? realignment : null);
}
