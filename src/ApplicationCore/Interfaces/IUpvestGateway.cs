using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's entire boundary to Upvest. Every call to Upvest goes through
/// this gateway (and, beneath it, a single reusable signing handler). Call sites
/// never attach credentials or sign requests themselves.
/// </summary>
public interface IUpvestGateway
{
    /// <summary>
    /// Create the shopper as an investor at Upvest (user + regulatory checks +
    /// tax residency) and return the Upvest user id. The user begins inactive and
    /// is accepted asynchronously.
    /// </summary>
    Task<Guid> CreateInvestorAsync(InvestorDetails details, CancellationToken cancellationToken);

    /// <summary>The Upvest status of a user (e.g. INACTIVE, ACTIVE, OFFBOARDED).</summary>
    Task<string> GetUserStatusAsync(Guid upvestUserId, CancellationToken cancellationToken);

    /// <summary>Create the shopper's personal account group. Returns its id.</summary>
    Task<Guid> CreateAccountGroupAsync(Guid upvestUserId, CancellationToken cancellationToken);

    /// <summary>Create a trading account in the group. Returns its id and current status.</summary>
    Task<(Guid AccountId, string Status)> CreateAccountAsync(Guid upvestUserId, Guid accountGroupId, CancellationToken cancellationToken);

    /// <summary>The Upvest status of an account (e.g. PENDING_APPROVAL, ACTIVE).</summary>
    Task<string> GetAccountStatusAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Deliver the set-aside cash to the account group so an order can settle.</summary>
    Task FundAsync(Guid accountGroupId, decimal amount, CancellationToken cancellationToken);

    /// <summary>Place a market buy of <paramref name="amount"/> euros of the configured fund. Returns the order id.</summary>
    Task<Guid> PlaceInvestmentOrderAsync(Guid upvestUserId, Guid accountId, decimal amount, CancellationToken cancellationToken);

    /// <summary>The Upvest status of an order (e.g. NEW, PROCESSING, FILLED, CANCELLED).</summary>
    Task<string> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken);
}
