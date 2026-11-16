using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

/// <summary>
/// Persistence for the application's own portfolio state. "Claim" writes
/// (<see cref="TryClaimInvestorAsync"/>, <see cref="TryClaimPaymentAsync"/>, <see cref="AddRealignmentAsync"/>)
/// insert a primary-keyed row and must fail — not overwrite — when the key already exists, so a duplicate
/// caller is rejected at the store before any Upvest call is made.
/// </summary>
public interface IPortfolioRepository
{
    // Investors
    Task<InvestorRecord?> GetInvestorAsync(string shopper, CancellationToken cancellationToken);

    /// <summary>Insert a new investor claim. Returns false if one already exists for the shopper
    /// (primary-key conflict), without overwriting it.</summary>
    Task<bool> TryClaimInvestorAsync(InvestorRecord investor, CancellationToken cancellationToken);

    Task UpdateInvestorAsync(InvestorRecord investor, CancellationToken cancellationToken);

    // Models
    Task<ModelPortfolioRecord?> GetModelAsync(string modelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelPortfolioRecord>> ListModelsAsync(CancellationToken cancellationToken);
    Task UpsertModelAsync(ModelPortfolioRecord model, CancellationToken cancellationToken);

    // Payments
    Task<PaymentRecord?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>Insert a new payment claim. Returns false if the id already exists (primary-key conflict).</summary>
    Task<bool> TryClaimPaymentAsync(PaymentRecord payment, CancellationToken cancellationToken);

    Task UpdatePaymentAsync(PaymentRecord payment, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentRecord>> ListPaymentsAsync(string shopper, CancellationToken cancellationToken);

    // Realignments
    Task<RealignmentRecord?> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken);
    Task AddRealignmentAsync(RealignmentRecord realignment, CancellationToken cancellationToken);
}
