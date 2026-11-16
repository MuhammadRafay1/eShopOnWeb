using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the managed-investment-portfolio capability: signs shoppers up as investors with
/// Upvest, defines operator model portfolios, takes pay-ins, realigns portfolios, and reports
/// holdings. Every status it returns reflects what actually happened at Upvest. Implementations own
/// both the shop-side mapping records and the Upvest calls; callers (endpoints) pass only the
/// signed-in shopper's identity and never Upvest ids.
/// </summary>
public interface IManagedPortfolioService
{
    // Flow 1 — becoming an investor
    Task<InvestorView> SignUpInvestorAsync(string shopUserId, InvestorSignUp signUp, CancellationToken cancellationToken = default);
    Task<InvestorView?> GetInvestorAsync(string shopUserId, CancellationToken cancellationToken = default);

    // Flow 2 — model portfolios
    Task<ModelPortfolioView> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelPortfolioView>> ListModelsAsync(CancellationToken cancellationToken = default);
    Task SelectModelAsync(string shopUserId, string modelId, CancellationToken cancellationToken = default);

    // Flow 3 — paying in
    Task<PaymentView> PayInAsync(string shopUserId, decimal amount, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentView>> ListPaymentsAsync(string shopUserId, CancellationToken cancellationToken = default);

    // Flow 4 — realignment
    Task<RealignmentView> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken = default);
    Task<RealignmentView?> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken = default);

    // Flow 5 — the investor's portfolio
    Task<PortfolioView> GetPortfolioAsync(string shopUserId, CancellationToken cancellationToken = default);
}
