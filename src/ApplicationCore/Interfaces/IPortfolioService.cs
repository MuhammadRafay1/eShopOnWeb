using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the managed-investment-portfolio capability on top of the Upvest gateway and local
/// bookkeeping. Every shopper-scoped method is keyed by <c>shopperId</c> (the authenticated caller)
/// so one shopper can never see another's investor record, payments, or portfolio.
/// </summary>
public interface IPortfolioService
{
    // Flow 1 — becoming an investor
    Task<InvestorView> SignUpInvestorAsync(string shopperId, InvestorSignUp form, CancellationToken ct = default);
    Task<InvestorView?> GetInvestorAsync(string shopperId, CancellationToken ct = default);

    // Flow 2 — model portfolios
    Task<ModelView> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFundInput> funds, CancellationToken ct = default);
    Task<IReadOnlyList<ModelView>> GetModelsAsync(CancellationToken ct = default);
    Task ChooseModelAsync(string shopperId, string modelId, CancellationToken ct = default);

    // Flow 3 — paying in
    Task<PaymentView> PayInAsync(string shopperId, decimal amount, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentView>> GetPaymentsAsync(string shopperId, CancellationToken ct = default);

    // Flow 4 — realignment
    Task<RealignmentView> StartRealignmentAsync(string modelId, CancellationToken ct = default);
    Task<RealignmentView?> GetRealignmentAsync(string realignmentId, CancellationToken ct = default);

    // Flow 5 — the investor's portfolio
    Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken ct = default);
}
