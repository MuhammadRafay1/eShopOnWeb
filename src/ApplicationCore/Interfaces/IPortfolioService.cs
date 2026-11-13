using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the managed-portfolio flows: persists the app's own records, drives Upvest through
/// <see cref="IUpvestPortfolioService"/>, and reconciles each status from Upvest reads. Every per-shopper
/// method is scoped to one shopper (<paramref name="buyerId"/>); a shopper can never reach another's data.
/// </summary>
public interface IPortfolioService
{
    // Flow 1
    Task<Investor> SignUpInvestorAsync(string buyerId, InvestorSignUpForm form, CancellationToken cancellationToken);
    Task<Investor?> GetInvestorAsync(string buyerId, CancellationToken cancellationToken);

    // Flow 2
    Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelPortfolio>> GetModelsAsync(CancellationToken cancellationToken);
    Task ChooseModelAsync(string buyerId, string modelId, CancellationToken cancellationToken);

    // Flow 3
    Task<Payment> PayInAsync(string buyerId, decimal amount, CancellationToken cancellationToken);
    Task<IReadOnlyList<Payment>> GetPaymentsAsync(string buyerId, CancellationToken cancellationToken);

    // Flow 4
    Task<Realignment> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken);
    Task<Realignment?> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken);

    // Flow 5
    Task<InvestorPortfolio> GetPortfolioAsync(string buyerId, CancellationToken cancellationToken);
}
