using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The sole boundary between the managed-portfolio capability and Upvest. Every Upvest interaction goes
/// through this gateway; its implementation (Infrastructure) is the only code that references the Upvest SDK.
/// All methods translate SDK failures into <see cref="Exceptions.UpvestGatewayException"/>.
/// </summary>
public interface IUpvestInvestmentGateway
{
    // ----- Flow 1: becoming an investor -------------------------------------------------------------

    /// <summary>
    /// Create the user at Upvest and submit the KYC the shop collected (identity document, tax residency),
    /// then provision the managed-portfolio account group and account. Returns the ids obtained and the
    /// mapped status. Steps that fail because the sandbox client lacks the scope are tolerated: the user id
    /// is still returned and the status reflects how far provisioning got.
    /// </summary>
    Task<InvestorProvisioning> CreateInvestorAsync(InvestorSignup signup, InvestorProvisioningKeys keys, CancellationToken ct);

    /// <summary>
    /// Re-attempt provisioning of the account group and/or account for an already-created user, using the
    /// same idempotency keys (self-heal). Returns the ids now present and the mapped status.
    /// </summary>
    Task<InvestorProvisioning> ProvisionAccountAsync(Guid userId, Guid? accountGroupId, Guid? accountId, InvestorProvisioningKeys keys, CancellationToken ct);

    /// <summary>Re-read the investor's status as Upvest currently records it.</summary>
    Task<InvestorStatusInfo> GetInvestorStatusAsync(Guid userId, Guid? accountGroupId, Guid? accountId, CancellationToken ct);

    // ----- Flow 2: model portfolios -----------------------------------------------------------------

    /// <summary>The set of tradeable instrument ISINs Upvest offers, for validating a model's funds.</summary>
    Task<IReadOnlySet<string>> ListTradeableIsinsAsync(CancellationToken ct);

    /// <summary>Create the Upvest allocation for a model and return its id.</summary>
    Task<Guid> CreateModelAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken ct);

    /// <summary>Replace an existing Upvest allocation's name and funds.</summary>
    Task UpdateModelAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct);

    /// <summary>Point the investor's portfolio at the given allocation (create the configuration, or update it to switch).</summary>
    Task SetFollowedModelAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct);

    // ----- Flow 3: paying in ------------------------------------------------------------------------

    /// <summary>
    /// Credit money to the investor's account group (top-up + sandbox settlement) and invest it across the
    /// followed model (portfolio BUY order). Returns the Upvest ids and mapped status.
    /// </summary>
    Task<PayInResult> PayInAsync(PayInCommand command, CancellationToken ct);

    /// <summary>Re-read a payment's outcome at Upvest from the top-up and order status.</summary>
    Task<Entities.PortfolioAggregate.PaymentStatus> GetPaymentStatusAsync(Guid? topupId, Guid? orderId, CancellationToken ct);

    // ----- Flow 4: realignment ----------------------------------------------------------------------

    /// <summary>Trigger a rebalancing of every portfolio following the given allocation; return the execution id.</summary>
    Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct);

    /// <summary>Re-read a realignment's outcome at Upvest.</summary>
    Task<Entities.PortfolioAggregate.RealignmentStatus> GetRealignmentStatusAsync(Guid executionId, CancellationToken ct);

    // ----- Flow 5: the investor's portfolio ---------------------------------------------------------

    /// <summary>Read the caller's portfolio (followed model, holdings and values) as Upvest records it.</summary>
    Task<PortfolioSnapshot> GetPortfolioAsync(Guid accountId, CancellationToken ct);
}
