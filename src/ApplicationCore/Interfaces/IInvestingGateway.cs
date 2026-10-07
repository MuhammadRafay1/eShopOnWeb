using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The one way the application talks to Upvest. Implemented in Infrastructure over the Upvest SDK;
/// every call is authenticated centrally by the SDK client's reusable message-signing handler, so
/// no method here deals with credentials.
/// </summary>
public interface IInvestingGateway
{
    /// <summary>Register the shopper as an investor at Upvest and report their initial acceptance state.</summary>
    Task<CreateInvestorResult> CreateInvestorAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken = default);

    /// <summary>Read the shopper's current acceptance state at Upvest.</summary>
    Task<EnrolmentStatus> GetEnrolmentStatusAsync(Guid upvestUserId, CancellationToken cancellationToken = default);

    /// <summary>Ensure the shopper has a trading account (and its cash account group) to hold investments.</summary>
    Task<UpvestAccountRef> EnsureAccountAsync(Guid upvestUserId, CancellationToken cancellationToken = default);

    /// <summary>Make the given amount of cash available in the account group so an order can be placed.</summary>
    Task AddCashAsync(Guid accountGroupId, decimal amountEur, CancellationToken cancellationToken = default);

    /// <summary>Invest the given cash amount into the configured fund for the shopper.</summary>
    Task<PlaceInvestmentResult> PlaceInvestmentOrderAsync(Guid upvestUserId, Guid accountId, decimal amountEur, CancellationToken cancellationToken = default);

    /// <summary>Read where an investment's order has got to at Upvest.</summary>
    Task<InvestmentStatus> GetInvestmentStatusAsync(Guid upvestOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensure Upvest has a webhook subscription delivering user and order events to this host at the
    /// given callback URL, so settlement can be reflected by push as well as by polling.
    /// </summary>
    Task EnsureSettlementWebhookAsync(string callbackUrl, CancellationToken cancellationToken = default);
}
