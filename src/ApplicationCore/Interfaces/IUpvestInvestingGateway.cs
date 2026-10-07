using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's view of everything it needs from Upvest, expressed in plain domain
/// terms. The implementation lives in Infrastructure and is the only place that talks to
/// the Upvest SDK. No Upvest SDK type crosses this boundary.
/// </summary>
public interface IUpvestInvestingGateway
{
    /// <summary>
    /// Onboards the shopper as an Upvest investor (user, checks, tax residency, account
    /// group and trading account). Returns the created identifiers and the account's
    /// current acceptance state.
    /// </summary>
    Task<UpvestEnrolmentResult> EnrolInvestorAsync(UpvestEnrolmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Current acceptance state of the investor's trading account.</summary>
    Task<UpvestAcceptanceState> GetAccountAcceptanceAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invests the whole set-aside balance: moves the cash to the shopper's Upvest account group
    /// (the shop transferring the set-aside money) and places a nominal market buy order for the
    /// configured fund. The instrument is read from configuration by the implementation.
    /// </summary>
    Task<UpvestPlacedOrder> PlaceInvestmentOrderAsync(Guid userId, Guid accountGroupId, Guid accountId, decimal amountEuros, CancellationToken cancellationToken = default);

    /// <summary>Reads the outcome of a previously placed investment order.</summary>
    Task<UpvestInvestmentOutcome> GetOrderOutcomeAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a webhook subscription exists pointing at this application's callback, so
    /// Upvest can notify us of status changes. Best-effort and idempotent.
    /// </summary>
    Task EnsureWebhookSubscriptionAsync(CancellationToken cancellationToken = default);
}

/// <summary>Investor sign-up details gathered by the shop's form. Carried, never logged.</summary>
public record UpvestEnrolmentRequest(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    string AddressLine1,
    string Postcode,
    string City,
    string Country,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

public record UpvestEnrolmentResult(
    Guid UserId,
    Guid AccountGroupId,
    Guid AccountId,
    UpvestAcceptanceState Acceptance);

/// <summary>Mapped acceptance state, independent of Upvest's own enum spellings.</summary>
public enum UpvestAcceptanceState
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}

public record UpvestPlacedOrder(Guid OrderId, string InstrumentId, string RawStatus);

/// <summary>Mapped order outcome, independent of Upvest's own enum spellings.</summary>
public enum UpvestInvestmentOutcome
{
    Pending = 0,
    Settled = 1,
    Failed = 2
}
