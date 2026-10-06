using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's gateway to Upvest. Every method maps to one Upvest Investment API
/// operation. Authentication (OAuth token + HTTP message signature) is applied uniformly by
/// the single delegating handler behind this client — no caller attaches credentials itself.
/// </summary>
public interface IUpvestClient
{
    /// <summary>Creates an Upvest user from the shopper's sign-up form and returns its id and status.</summary>
    Task<UpvestUserResult> CreateUserAsync(InvestorSignup signup, CancellationToken cancellationToken);

    /// <summary>Submits the KYC/compliance check that triggers user activation.</summary>
    Task SubmitKycCheckAsync(string upvestUserId, InvestorSignup signup, CancellationToken cancellationToken);

    /// <summary>Declares the shopper's tax residency.</summary>
    Task SetTaxResidencyAsync(string upvestUserId, InvestorSignup signup, CancellationToken cancellationToken);

    /// <summary>Retrieves the current state of an Upvest user.</summary>
    Task<UpvestUserResult> GetUserAsync(string upvestUserId, CancellationToken cancellationToken);

    /// <summary>Creates a PERSONAL account group for the user.</summary>
    Task<UpvestAccountGroupResult> CreateAccountGroupAsync(string upvestUserId, CancellationToken cancellationToken);

    /// <summary>Creates a TRADING account within the given account group.</summary>
    Task<UpvestAccountResult> CreateAccountAsync(string upvestUserId, string accountGroupId, CancellationToken cancellationToken);

    /// <summary>Retrieves the current state of an Upvest account.</summary>
    Task<UpvestAccountResult> GetAccountAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>Funds the account group with virtual cash (the sandbox mechanism for paying in).</summary>
    Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, string currency, CancellationToken cancellationToken);

    /// <summary>Places a nominal (cash-amount) MARKET buy order for the configured instrument.</summary>
    Task<UpvestOrderResult> PlaceBuyOrderAsync(string upvestUserId, string accountId, string instrumentId, decimal cashAmount, string currency, CancellationToken cancellationToken);

    /// <summary>Retrieves the current state of an Upvest order.</summary>
    Task<UpvestOrderResult> GetOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>Ensures a webhook subscription exists for the given callback url and event types.</summary>
    Task EnsureWebhookAsync(string callbackUrl, IEnumerable<string> eventTypes, CancellationToken cancellationToken);
}
