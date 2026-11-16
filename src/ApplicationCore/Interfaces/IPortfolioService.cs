using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the managed-portfolio capability: investor sign-up, model portfolios, paying in,
/// realignment and portfolio views. Every reported status reflects the state at Upvest, learned
/// through this application's own calls. All shopper-scoped operations are isolated per shopper.
/// </summary>
public interface IPortfolioService
{
    // Flow 1 — becoming an investor
    Task<InvestorView> SignUpInvestorAsync(string shopperId, InvestorSignUp signUp, CancellationToken ct = default);
    Task<InvestorView?> GetInvestorAsync(string shopperId, CancellationToken ct = default);

    // Flow 2 — model portfolios
    Task<ModelView> UpsertModelAsync(int modelId, string name, IReadOnlyList<FundWeightInput> funds, CancellationToken ct = default);
    Task<IReadOnlyList<ModelView>> GetModelsAsync(CancellationToken ct = default);
    Task ChooseModelAsync(string shopperId, int modelId, CancellationToken ct = default);

    // Flow 3 — paying in
    Task<PaymentView> PayInAsync(string shopperId, decimal amount, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentView>> GetPaymentsAsync(string shopperId, CancellationToken ct = default);

    // Flow 4 — realignment
    Task<RealignmentView> RealignAsync(int modelId, CancellationToken ct = default);
    Task<RealignmentView> GetRealignmentAsync(Guid realignmentId, CancellationToken ct = default);

    // Flow 5 — the investor's portfolio
    Task<PortfolioView> GetPortfolioAsync(string shopperId, CancellationToken ct = default);
}
