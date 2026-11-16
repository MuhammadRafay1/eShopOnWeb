using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's boundary to Upvest for managed portfolios. The implementation wraps the Upvest SDK,
/// signs and authenticates every call, and translates every failure into <see cref="UpvestGatewayException"/>.
/// All methods accept a cancellation token bound to a per-call budget by the implementation.
/// </summary>
public interface IUpvestPortfolioGateway
{
    // Flow 1 — becoming an investor.
    Task<InvestorOnboardingResult> OnboardInvestorAsync(InvestorSignUp form, InvestorOnboardingKeys keys, CancellationToken cancellationToken);
    Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken);

    // Flow 2 — model portfolios.
    Task<ModelPortfolioResult> CreateModelAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<ModelPortfolioResult> ReplaceModelAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelPortfolioResult>> ListModelsAsync(CancellationToken cancellationToken);
    Task SetInvestorConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, bool alreadyConfigured, CancellationToken cancellationToken);

    // Flow 3 — paying in. Runs the full sandbox credit + invest sequence.
    Task<PayInResult> PayInAsync(
        Guid userId,
        Guid accountId,
        Guid accountGroupId,
        Guid? existingVirtualBankAccountId,
        Guid virtualBankAccountIdempotencyKey,
        decimal amount,
        Guid orderIdempotencyKey,
        string clientReference,
        CancellationToken cancellationToken);
    Task<PaymentStatus> GetPaymentStatusAsync(Guid portfolioOrderId, CancellationToken cancellationToken);

    // Flow 4 — realignment.
    Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken);

    // Flow 5 — the investor's portfolio.
    Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken cancellationToken);
}
