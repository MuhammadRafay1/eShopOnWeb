using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The sole boundary to Upvest. Every method is expressed in domain terms, translates every SDK failure into
/// <see cref="Microsoft.eShopWeb.ApplicationCore.Exceptions.UpvestIntegrationException"/>, and reports status by
/// reading it back from Upvest. Each write takes its caller-supplied idempotency key so a replay is safe.
/// </summary>
public interface IUpvestPortfolioGateway
{
    // --- Flow 1: becoming an investor ---------------------------------------------------------------------

    Task<Guid> CreateInvestorUserAsync(InvestorRegistration registration, Guid idempotencyKey, CancellationToken cancellationToken);

    Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken cancellationToken);

    Task<Guid> CreatePortfolioAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Re-reads the investor's state at Upvest and derives the current <see cref="InvestorStatus"/>.</summary>
    Task<InvestorStatus> GetInvestorStatusAsync(Guid userId, Guid? accountGroupId, Guid? accountId, CancellationToken cancellationToken);

    // --- Flow 2: model portfolios -------------------------------------------------------------------------

    Task<Guid> CreateModelAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken);

    Task UpdateModelAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<UpvestAllocation>> ListModelAllocationsAsync(CancellationToken cancellationToken);

    Task SetFollowedModelAsync(Guid accountId, Guid allocationId, bool alreadyFollowingAModel, Guid idempotencyKey, CancellationToken cancellationToken);

    Task<Guid?> GetFollowedAllocationIdAsync(Guid accountId, CancellationToken cancellationToken);

    // --- Flow 3: paying in --------------------------------------------------------------------------------

    /// <summary>Returns the account group's virtual-bank-account IBAN, creating one if none exists.</summary>
    Task<string> EnsureVirtualBankAccountIbanAsync(Guid accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Credits cash to the investor's account in the sandbox (the Upvest "money in" test facility).</summary>
    Task CreditCashAsync(string iban, decimal amount, string clientReference, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Places a BUY portfolio order that distributes the cash across the followed model's weights.</summary>
    Task<UpvestOrderResult> PlaceBuyOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken);

    Task<PaymentStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken);

    // --- Flow 4: realignment ------------------------------------------------------------------------------

    Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken cancellationToken);

    Task<RealignmentStatus> GetRealignmentStatusAsync(Guid realignmentId, CancellationToken cancellationToken);

    // --- Flow 5: the portfolio ----------------------------------------------------------------------------

    Task<PortfolioHoldings> GetPortfolioHoldingsAsync(Guid accountId, CancellationToken cancellationToken);
}
