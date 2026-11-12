using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolios;

/// <summary>
/// Persistence for investor records. Each shopper owns at most one investor record.
/// </summary>
public interface IInvestorStore
{
    Task<Investor?> GetByShopperAsync(string shopperId, CancellationToken ct = default);
    Task<Investor> AddAsync(Investor investor, CancellationToken ct = default);
    Task UpdateAsync(Investor investor, CancellationToken ct = default);

    /// <summary>All investors whose chosen model is <paramref name="modelId"/>. Used by realignment.</summary>
    Task<IReadOnlyList<Investor>> ListByModelAsync(string modelId, CancellationToken ct = default);
}

/// <summary>Persistence for operator-defined model portfolios.</summary>
public interface IModelPortfolioStore
{
    Task<ModelPortfolio?> GetAsync(string modelId, CancellationToken ct = default);
    Task<IReadOnlyList<ModelPortfolio>> ListAsync(CancellationToken ct = default);
    Task UpsertAsync(ModelPortfolio model, CancellationToken ct = default);
}

/// <summary>Persistence for pay-ins.</summary>
public interface IPaymentStore
{
    Task<Payment> AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);

    /// <summary>The shopper's payments, newest first.</summary>
    Task<IReadOnlyList<Payment>> ListByShopperAsync(string shopperId, CancellationToken ct = default);
}

/// <summary>Persistence for realignment runs.</summary>
public interface IRealignmentStore
{
    Task<Realignment> AddAsync(Realignment realignment, CancellationToken ct = default);
    Task UpdateAsync(Realignment realignment, CancellationToken ct = default);
    Task<Realignment?> GetAsync(System.Guid id, CancellationToken ct = default);
}
