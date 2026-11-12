using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's gateway to Upvest. Every method speaks in the app's own SDK-free contracts and
/// translates Upvest's call surface behind it; failures surface as
/// <see cref="Exceptions.UpvestIntegrationException"/>. Statuses returned here reflect what actually
/// happened at Upvest (learned through the gateway's own re-read calls — no webhook is exposed).
/// </summary>
public interface IUpvestInvestmentService
{
    /// <summary>
    /// Sign a shopper up as an investor at Upvest and provision an account able to hold a managed portfolio.
    /// <paramref name="idempotencyKey"/> is reused verbatim on a reconciling retry of the same sign-up.
    /// </summary>
    Task<InvestorProvisionResult> ProvisionInvestorAsync(InvestorSignup signup, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Re-read the current sign-up status from Upvest and map it to the app lifecycle.</summary>
    Task<InvestorLifecycleStatus> GetInvestorStatusAsync(Guid upvestUserId, bool accountReady, CancellationToken cancellationToken);

    /// <summary>
    /// Create or replace the Upvest allocation that backs a model portfolio (full-replace semantics).
    /// Pass the existing allocation id to replace it, or <c>null</c> to create a new one. Returns the allocation id.
    /// </summary>
    Task<Guid> UpsertModelAllocationAsync(string name, IReadOnlyList<ModelFundInput> funds, Guid? existingAllocationId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Validate a set of fund ISINs against Upvest's tradable instruments. Returns the ISINs Upvest does not recognise (empty when all are valid).</summary>
    Task<IReadOnlyList<string>> FindUnknownInstrumentsAsync(IReadOnlyList<string> isins, CancellationToken cancellationToken);

    /// <summary>Point the investor's account at an allocation (create a configuration, or update it when switching).</summary>
    Task FollowModelAsync(Guid accountId, Guid allocationId, bool configurationExists, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Credit the investor's account in the sandbox and place a BUY order that invests the cash across the
    /// followed model's weights. Returns the placed order, its status, and the virtual bank account used
    /// (created on first use).
    /// </summary>
    Task<PaymentPlacementResult> PayInAsync(InvestorPayIn payIn, CancellationToken cancellationToken);

    /// <summary>Re-read the status of a placed invest order from Upvest.</summary>
    Task<PaymentStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Trigger a realignment of every account following an allocation. Returns the rebalancing-execution id.</summary>
    Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Re-read the status of a rebalancing execution from Upvest (synthesised from its orders).</summary>
    Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken);

    /// <summary>Read the investor's portfolio as Upvest currently records it: the followed allocation, holdings and total value.</summary>
    Task<RemotePortfolio> GetPortfolioAsync(Guid accountId, CancellationToken cancellationToken);
}
