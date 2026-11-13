using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>
/// Orchestrates the managed-portfolio flows: it owns the application's persisted state (investors,
/// models, payments, realignments) and drives Upvest through <see cref="IUpvestPortfolioGateway"/>.
/// Every method acts for one caller (<paramref name="buyerId"/>) except the operator actions, which
/// act on shared model state. Status always reflects what Upvest last told this application.
/// </summary>
public interface IPortfolioService
{
    // Flow 1 — becoming an investor
    Task<InvestorView> SignUpInvestorAsync(string buyerId, InvestorRegistration registration, CancellationToken ct);
    Task<InvestorView> GetInvestorAsync(string buyerId, CancellationToken ct);

    // Flow 2 — model portfolios
    Task<ModelView> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFund> funds, CancellationToken ct);
    Task<IReadOnlyList<ModelView>> GetModelsAsync(CancellationToken ct);
    Task ChooseModelAsync(string buyerId, string modelId, CancellationToken ct);

    // Flow 3 — paying in
    Task<PaymentView> PayInAsync(string buyerId, decimal amount, CancellationToken ct);
    Task<IReadOnlyList<PaymentView>> GetPaymentsAsync(string buyerId, CancellationToken ct);

    // Flow 4 — realignment
    Task<RealignmentView> TriggerRealignmentAsync(string modelId, CancellationToken ct);
    Task<RealignmentView> GetRealignmentAsync(Guid realignmentId, CancellationToken ct);

    // Flow 5 — the investor's portfolio
    Task<PortfolioView> GetPortfolioAsync(string buyerId, CancellationToken ct);
}
