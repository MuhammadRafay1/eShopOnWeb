using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.ManagedPortfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The whole Upvest surface the managed-portfolio feature needs, expressed in application terms (no SDK
/// types leak out). The implementation lives in the Infrastructure layer and owns every call to Upvest,
/// translating SDK exceptions into <see cref="UpvestIntegrationException"/>. Every method bounds its own call
/// budget and never logs personal data.
/// </summary>
public interface IManagedPortfolioService
{
    /// <summary>
    /// Runs the Upvest onboarding chain for a shopper (create user → identity document → tax residency →
    /// account group → portfolio account), best-effort through the steps the client is authorised for, and
    /// returns the identifiers obtained plus the derived status. Throws only when the user itself could not
    /// be created.
    /// </summary>
    Task<InvestorOnboardingResult> OnboardInvestorAsync(string shopperKey, InvestorSignup signup, CancellationToken cancellationToken);

    /// <summary>
    /// Re-reads the investor's state at Upvest and recomputes the sign-up status. The known identifiers come
    /// from what onboarding persisted.
    /// </summary>
    Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken);

    /// <summary>Creates a model portfolio allocation at Upvest; returns the Upvest allocation id.</summary>
    Task<Guid> CreateAllocationAsync(string name, IReadOnlyList<ModelFundSpec> funds, CancellationToken cancellationToken);

    /// <summary>Replaces an existing model portfolio allocation at Upvest.</summary>
    Task UpdateAllocationAsync(Guid allocationId, string name, IReadOnlyList<ModelFundSpec> funds, CancellationToken cancellationToken);

    /// <summary>
    /// Links the investor's account to the chosen model's allocation (creating the configuration on first
    /// choice, updating it on a switch).
    /// </summary>
    Task SetModelChoiceAsync(Guid accountId, Guid allocationId, bool alreadyConfigured, CancellationToken cancellationToken);

    /// <summary>
    /// Credits cash into the investor's sandbox account and places a BUY order that invests it across the
    /// chosen model's weights. <paramref name="idempotencyKey"/> is the stable key reused on retries.
    /// </summary>
    Task<PayInResult> PayInAsync(Guid userId, Guid accountGroupId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Re-reads a portfolio order at Upvest and maps it to a payment status.</summary>
    Task<PaymentStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Triggers a rebalancing of every account following an allocation; returns the execution id.</summary>
    Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Derives a realignment status by aggregating the statuses of the execution's orders.</summary>
    Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken);

    /// <summary>Reads the investor's holdings and total value as Upvest currently records them.</summary>
    Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken cancellationToken);
}
