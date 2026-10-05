using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The shop's gateway to Upvest. Every call is authenticated by a single delegating handler,
/// so no method here deals with credentials or signatures.
/// </summary>
public interface IUpvestClient
{
    /// <summary>Create the shopper as an investor (Upvest user). Returns the user's id and status.</summary>
    Task<UpvestUser> CreateUserAsync(EnrolmentDetails details, CancellationToken cancellationToken);

    /// <summary>Fetch the current state of an Upvest user.</summary>
    Task<UpvestUser?> GetUserAsync(string upvestUserId, CancellationToken cancellationToken);

    /// <summary>Best-effort: record the investor's tax residency.</summary>
    Task SetTaxResidencyAsync(string upvestUserId, string taxCountry, string taxId, CancellationToken cancellationToken);

    /// <summary>Submit the KYC check that lets Upvest accept the shopper as an investor.</summary>
    Task CreateKycCheckAsync(string upvestUserId, string nationality, CancellationToken cancellationToken);

    /// <summary>Create a personal account group to hold the investor's assets. Returns its id.</summary>
    Task<string> CreateAccountGroupAsync(string upvestUserId, CancellationToken cancellationToken);

    /// <summary>Create a trading account in the account group. Returns its id.</summary>
    Task<string> CreateAccountAsync(string upvestUserId, string accountGroupId, CancellationToken cancellationToken);

    /// <summary>Fund the account group with cash so an order can be placed (sandbox virtual cash).</summary>
    Task IncreaseVirtualCashAsync(string accountGroupId, decimal amountEuros, CancellationToken cancellationToken);

    /// <summary>Place a market BUY order for the configured instrument. Returns the order id and status.</summary>
    Task<UpvestOrder> PlaceOrderAsync(string upvestUserId, string accountId, decimal amountEuros, CancellationToken cancellationToken);

    /// <summary>Fetch the current state of an Upvest order.</summary>
    Task<UpvestOrder?> GetOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>Best-effort: make sure a webhook subscription exists for the given callback url.</summary>
    Task EnsureWebhookAsync(string callbackUrl, CancellationToken cancellationToken);
}

/// <summary>An Upvest user as the shop cares about it.</summary>
public record UpvestUser(string Id, string Status);

/// <summary>An Upvest order as the shop cares about it.</summary>
public record UpvestOrder(string Id, string Status);
