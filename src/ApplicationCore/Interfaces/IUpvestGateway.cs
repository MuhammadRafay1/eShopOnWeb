using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The shop's investor sign-up form, as submitted by a shopper opting in. These values are passed to Upvest
/// to create the investor; they are personal data and must never be written to logs.
/// </summary>
public record InvestorEnrolmentForm(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string Nationality,
    string AddressLine1,
    string Postcode,
    string City,
    string Country,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>Result of creating an investor at Upvest.</summary>
public record UpvestEnrolmentResult(string UpvestUserId, string Status);

/// <summary>Result of creating the account group + trading account at Upvest.</summary>
public record UpvestAccountSetupResult(string AccountGroupId, string AccountId, string AccountStatus);

/// <summary>The state of a buy order at Upvest.</summary>
public record UpvestOrderState(string UpvestOrderId, string Status);

/// <summary>
/// The single seam through which this application talks to Upvest. Every method goes through the SDK client
/// whose one <c>DelegatingHandler</c> authenticates (OAuth2 bearer + HTTP message signature) each call;
/// no caller attaches credentials. Statuses are returned verbatim from Upvest (e.g. "ACTIVE", "FILLED").
/// </summary>
public interface IUpvestGateway
{
    /// <summary>Create the shopper as an investor at Upvest (user + KYC check + tax residency).</summary>
    Task<UpvestEnrolmentResult> EnrolAsync(InvestorEnrolmentForm form, CancellationToken cancellationToken);

    /// <summary>Read the current status of an Upvest user (e.g. INACTIVE, ACTIVE).</summary>
    Task<string> GetUserStatusAsync(string upvestUserId, CancellationToken cancellationToken);

    /// <summary>Create the shopper's account group and trading account (requires the user to be ACTIVE).</summary>
    Task<UpvestAccountSetupResult> CreateAccountSetupAsync(
        string upvestUserId, Guid groupIdempotencyKey, Guid accountIdempotencyKey, CancellationToken cancellationToken);

    /// <summary>Read the current status of a trading account (e.g. PENDING_APPROVAL, ACTIVE).</summary>
    Task<string> GetAccountStatusAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>Fund the account group with the given amount of euros so a buy order can settle.</summary>
    Task FundAsync(string accountGroupId, decimal amountEuros, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Place a nominal buy order for the configured fund. Returns the Upvest order id.</summary>
    Task<string> PlaceInvestmentOrderAsync(
        string upvestUserId, string accountId, decimal amountEuros, string clientReference, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Read the current state of an order at Upvest.</summary>
    Task<UpvestOrderState> GetOrderAsync(string upvestOrderId, CancellationToken cancellationToken);

    /// <summary>Find an order previously placed for an account by its client reference (used to settle an uncertain write).</summary>
    Task<UpvestOrderState?> FindOrderByReferenceAsync(string accountId, string clientReference, CancellationToken cancellationToken);
}
