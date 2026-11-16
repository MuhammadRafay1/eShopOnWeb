using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Application service for the managed investment portfolio feature. Orchestrates the local
/// store and the Upvest client, and derives every reported status from Upvest's actual state.
/// All per-shopper operations are scoped by <paramref name="shopperId"/> so one shopper can
/// never see another's investor record, payments or portfolio.
/// </summary>
public interface IPortfolioService
{
    // Flow 1 — Becoming an investor
    Task<InvestorResult> SignUpInvestorAsync(string shopperId, InvestorSignUp signUp, CancellationToken ct = default);
    Task<InvestorResult?> GetInvestorAsync(string shopperId, CancellationToken ct = default);

    // Flow 2 — Model portfolios
    Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFund> funds, CancellationToken ct = default);
    Task<IReadOnlyList<ModelPortfolio>> GetModelsAsync(CancellationToken ct = default);
    Task ChooseModelAsync(string shopperId, string modelId, CancellationToken ct = default);

    // Flow 3 — Paying in
    Task<PaymentResult> PayInAsync(string shopperId, decimal amount, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentResult>> GetPaymentsAsync(string shopperId, CancellationToken ct = default);

    // Flow 4 — Realignment
    Task<RealignmentResult> RealignAsync(string modelId, CancellationToken ct = default);
    Task<RealignmentResult?> GetRealignmentAsync(string realignmentId, CancellationToken ct = default);

    // Flow 5 — The investor's portfolio
    Task<PortfolioView> GetPortfolioAsync(string shopperId, CancellationToken ct = default);
}
