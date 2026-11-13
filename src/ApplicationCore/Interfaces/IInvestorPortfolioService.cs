using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's boundary to the managed-investment-portfolio capability backed by Upvest.
/// Every method acts for one signed-in shopper (identified by <c>shopperUserName</c>) or, for the
/// operator actions, on the shared set of model portfolios. Implementations translate every Upvest
/// SDK failure into a <see cref="Microsoft.eShopWeb.ApplicationCore.Exceptions.UpvestException"/>
/// and never leak SDK types or personal data.
/// </summary>
public interface IInvestorPortfolioService
{
    // Flow 1 — becoming an investor
    Task<InvestorStatusResult> SignUpInvestorAsync(string shopperUserName, InvestorSignupInput signup, CancellationToken cancellationToken = default);
    Task<InvestorStatusResult?> GetInvestorAsync(string shopperUserName, CancellationToken cancellationToken = default);

    // Flow 2 — model portfolios
    Task<ModelPortfolioResult> UpsertModelAsync(string modelId, ModelPortfolioInput model, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelPortfolioResult>> ListModelsAsync(CancellationToken cancellationToken = default);
    Task SelectModelAsync(string shopperUserName, string modelId, CancellationToken cancellationToken = default);

    // Flow 3 — paying in
    Task<PaymentResult> PayInAsync(string shopperUserName, decimal amount, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentResult>> ListPaymentsAsync(string shopperUserName, CancellationToken cancellationToken = default);

    // Flow 4 — realignment
    Task<RealignmentResult> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken = default);
    Task<RealignmentResult?> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken = default);

    // Flow 5 — the investor's portfolio
    Task<PortfolioResult> GetPortfolioAsync(string shopperUserName, CancellationToken cancellationToken = default);
}
