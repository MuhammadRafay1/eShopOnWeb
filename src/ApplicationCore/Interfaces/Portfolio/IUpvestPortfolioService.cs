using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

/// <summary>
/// The facade the API endpoints drive. Every method acts for exactly one caller where a shopper identity is
/// passed, resolving all Upvest ids from that caller's own record — a request never supplies another
/// shopper's ids. Operator actions (model upsert, realignment) take no shopper. Every status returned
/// reflects what actually happened at Upvest, learned by this application's own re-reads.
/// </summary>
public interface IUpvestPortfolioService
{
    // Flow 1 — becoming an investor
    Task<InvestorStatusResult> SignUpInvestorAsync(string shopper, InvestorSignupForm form, CancellationToken cancellationToken);
    Task<InvestorStatusResult?> GetInvestorStatusAsync(string shopper, CancellationToken cancellationToken);

    // Flow 2 — model portfolios
    Task<ModelPortfolioResult> UpsertModelAsync(string modelId, ModelPortfolioDefinition definition, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelPortfolioResult>> ListModelsAsync(CancellationToken cancellationToken);
    Task ChooseModelAsync(string shopper, string modelId, CancellationToken cancellationToken);

    // Flow 3 — paying in
    Task<PaymentResult> PayInAsync(string shopper, decimal amount, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentResult>> ListPaymentsAsync(string shopper, CancellationToken cancellationToken);

    // Flow 4 — realignment
    Task<RealignmentResult> RealignAsync(string modelId, CancellationToken cancellationToken);
    Task<RealignmentResult> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken);

    // Flow 5 — the investor's portfolio
    Task<PortfolioResult> GetPortfolioAsync(string shopper, CancellationToken cancellationToken);
}
