using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The operations the investing feature needs from Upvest. Every call made through the
/// implementation is authenticated (OAuth bearer token and HTTP message signature) by a single
/// shared <c>DelegatingHandler</c>; no caller attaches credentials.
/// </summary>
public interface IUpvestClient
{
    /// <summary>
    /// Registers the shopper as an investor (user, KYC check, tax residency) and returns the
    /// Upvest user id. The user is not immediately active — acceptance is asynchronous.
    /// </summary>
    Task<string> CreateInvestorAsync(UpvestInvestorRegistration registration, CancellationToken cancellationToken);

    /// <summary>The Upvest onboarding status of a user, e.g. <c>INACTIVE</c>, <c>ACTIVE</c>.</summary>
    Task<string> GetUserStatusAsync(string upvestUserId, CancellationToken cancellationToken);

    /// <summary>Opens a personal account group and a trading account for an active user.</summary>
    Task<UpvestAccount> CreateTradingAccountAsync(string upvestUserId, CancellationToken cancellationToken);

    /// <summary>The status of an account, e.g. <c>PENDING_APPROVAL</c>, <c>ACTIVE</c>.</summary>
    Task<string> GetAccountStatusAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>Makes cash available in the account group so a buy order can be funded.</summary>
    Task TopUpAsync(string accountGroupId, decimal amount, CancellationToken cancellationToken);

    /// <summary>Places a market buy order for the configured fund and returns the Upvest order id.</summary>
    Task<string> PlaceBuyOrderAsync(string upvestUserId, string accountId, decimal amount, CancellationToken cancellationToken);

    /// <summary>The status of an order, e.g. <c>NEW</c>, <c>PROCESSING</c>, <c>FILLED</c>, <c>CANCELLED</c>.</summary>
    Task<string> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>Ensures a webhook subscription exists and is enabled for order/execution events.</summary>
    Task EnsureOrderWebhookAsync(CancellationToken cancellationToken);
}

/// <summary>The Upvest account group and account opened for a shopper.</summary>
public sealed record UpvestAccount(string AccountGroupId, string AccountId, string Status);
