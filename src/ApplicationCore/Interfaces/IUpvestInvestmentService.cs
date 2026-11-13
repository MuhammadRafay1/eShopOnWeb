using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The shop's gateway to Upvest for managed investment portfolios. Every method talks to Upvest through the
/// SDK and reports what actually happened there. All failures surface as
/// <see cref="Exceptions.UpvestApiException"/>; personal data is never logged.
/// </summary>
public interface IUpvestInvestmentService
{
    /// <summary>
    /// Provision the shopper as an investor at Upvest: create the user, record their identity document and tax
    /// residency, and open a PERSONAL account group with a PORTFOLIO account. Each step that Upvest's current
    /// scopes do not permit is skipped gracefully, leaving the investor <see cref="InvestorStatus.Pending"/>.
    /// Deterministic idempotency keys (derived from <paramref name="buyerId"/>) make the whole chain safe to retry.
    /// </summary>
    Task<InvestorProvisioningResult> ProvisionInvestorAsync(string buyerId, InvestorSignUp signUp, CancellationToken cancellationToken);

    /// <summary>Re-derive the investor's current status from Upvest (user + account readiness).</summary>
    Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountGroupId, Guid? accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Create the Upvest portfolio allocation for a model, or replace an existing one in place. Returns the
    /// allocation id. Idempotency key is derived from <paramref name="modelId"/>.
    /// </summary>
    Task<Guid> SaveModelAllocationAsync(string modelId, Guid? existingAllocationId, string name, IReadOnlyList<ModelFundInput> funds, CancellationToken cancellationToken);

    /// <summary>Point the investor's account at the given allocation (create the configuration, or replace it if one exists).</summary>
    Task ConfigureFollowedModelAsync(string buyerId, Guid accountId, Guid allocationId, bool configurationExists, CancellationToken cancellationToken);

    /// <summary>Whether the investor's account already has a portfolio configuration (a model chosen) at Upvest.</summary>
    Task<bool> HasConfigurationAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Pay an amount in: credit the account group (sandbox cash-in) then place a BUY portfolio order that invests
    /// the cash across the followed model's weights. Returns the Upvest ids and the derived status. Idempotency
    /// keys are derived from <paramref name="paymentId"/>.
    /// </summary>
    Task<PaymentPlacement> PayInAsync(string paymentId, Guid userId, Guid accountGroupId, Guid accountId, decimal amount, CancellationToken cancellationToken);

    /// <summary>Re-derive a payment's status from its Upvest portfolio order.</summary>
    Task<PaymentStatus> GetPaymentStatusAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Trigger a realignment (rebalancing) of every portfolio following the allocation. Returns the execution id.</summary>
    Task<Guid> TriggerRealignmentAsync(string realignmentId, Guid allocationId, CancellationToken cancellationToken);

    /// <summary>Re-derive a realignment's status from its Upvest execution orders.</summary>
    Task<RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken);

    /// <summary>Read the investor's portfolio from Upvest: the allocation followed, holdings and total value.</summary>
    Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken cancellationToken);
}
