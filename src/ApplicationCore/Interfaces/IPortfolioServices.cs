using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Flow 1 — becoming an investor.</summary>
public interface IInvestorService
{
    Task<Investor> SignUpAsync(string shopUserName, InvestorSignup signup, CancellationToken ct = default);
    /// <summary>Returns the caller's investor record, refreshing its status from Upvest, or null if they have not signed up.</summary>
    Task<Investor?> GetAsync(string shopUserName, CancellationToken ct = default);
}

/// <summary>Flow 2 — model portfolios.</summary>
public interface IModelPortfolioService
{
    Task<ModelPortfolio> UpsertAsync(string modelId, string name, IEnumerable<ModelFund> funds, CancellationToken ct = default);
    Task<IReadOnlyList<ModelPortfolio>> ListAsync(CancellationToken ct = default);
    /// <summary>The signed-in investor chooses which model their portfolio follows.</summary>
    Task ChooseModelAsync(string shopUserName, string modelId, CancellationToken ct = default);
}

/// <summary>Flow 3 — paying in.</summary>
public interface IPortfolioPaymentService
{
    Task<PortfolioPayment> PayInAsync(string shopUserName, decimal amount, CancellationToken ct = default);
    Task<IReadOnlyList<PortfolioPayment>> ListAsync(string shopUserName, CancellationToken ct = default);
}

/// <summary>Flow 4 — realignment.</summary>
public interface IRealignmentService
{
    Task<Realignment> TriggerAsync(string modelId, CancellationToken ct = default);
    Task<Realignment?> GetAsync(int realignmentId, CancellationToken ct = default);
}

/// <summary>Flow 5 — the investor's portfolio.</summary>
public interface IPortfolioViewService
{
    Task<PortfolioView?> GetAsync(string shopUserName, CancellationToken ct = default);
}

public record PortfolioHolding(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioView(
    string? ModelId,
    IReadOnlyList<PortfolioHolding> Holdings,
    decimal TotalValue,
    decimal PaidIn);
