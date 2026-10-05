using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's whole surface onto Upvest. Every method goes through the Upvest SDK (and the one
/// signing <c>DelegatingHandler</c>); call sites never touch credentials or the SDK directly.
/// Identifiers returned are Upvest's own (opaque strings/UUIDs).
/// </summary>
public interface IUpvestGateway
{
    /// <summary>Creates the Upvest investor (user) for a shopper. Returns the Upvest user id.</summary>
    Task<string> CreateInvestorAsync(InvestorSignupForm form, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Submits the KYC check required before Upvest activates the user.</summary>
    Task SubmitKycCheckAsync(string upvestUserId, CancellationToken cancellationToken);

    /// <summary>Records the shopper's tax residency, also required before activation.</summary>
    Task SetTaxResidencyAsync(string upvestUserId, string taxCountry, string taxId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Current Upvest status of the user (e.g. INACTIVE / ACTIVE).</summary>
    Task<string> GetUserStatusAsync(string upvestUserId, CancellationToken cancellationToken);

    /// <summary>Creates the holding account group for the user. Returns the account group id.</summary>
    Task<string> CreateAccountGroupAsync(string upvestUserId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Creates the trading account inside the group. Returns the account id.</summary>
    Task<string> CreateAccountAsync(string upvestUserId, string accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Current Upvest status of the account (e.g. PENDING_APPROVAL / ACTIVE).</summary>
    Task<string> GetAccountStatusAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>Moves the set-aside cash into the account group so a buy order can fill.</summary>
    Task FundAccountGroupAsync(string accountGroupId, long amountCents, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Places a nominal BUY order for the configured fund. Returns the Upvest order id.</summary>
    Task<string> PlaceBuyOrderAsync(string upvestUserId, string accountId, long amountCents, string clientReference, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Current Upvest status of the order (e.g. NEW / PROCESSING / FILLED / CANCELLED).</summary>
    Task<string> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Re-reads the account's orders to find the one carrying <paramref name="clientReference"/>, used to
    /// settle a placement whose outcome was left unknown by a connection failure. Null if none is found.
    /// </summary>
    Task<UpvestOrderInfo?> FindOrderByReferenceAsync(string accountId, string clientReference, CancellationToken cancellationToken);
}

/// <summary>An order's Upvest id and status, as read back during reconciliation.</summary>
public record UpvestOrderInfo(string OrderId, string Status);
