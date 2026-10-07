using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Where a shopper's acceptance as an investor stands at Upvest.</summary>
public enum UpvestAcceptanceStatus
{
    Pending,
    Active,
    Rejected
}

/// <summary>Where a single investment stands at Upvest.</summary>
public enum UpvestInvestmentState
{
    Pending,
    Settled,
    Failed
}

/// <summary>A shopper's postal address, as captured by the sign-up form.</summary>
public record InvestorAddress(string Line1, string Postcode, string City, string Country);

/// <summary>The sign-up form a shopper fills in to become an investor.</summary>
public record InvestorEnrolmentDetails(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    InvestorAddress Address,
    string? PhoneNumber,
    string? TaxId,
    string? TaxCountry);

/// <summary>The handles Upvest hands back for a shopper, plus where their acceptance stands.</summary>
public record UpvestEnrolmentResult(string UpvestUserId, string? UpvestAccountId, UpvestAcceptanceStatus Status);

/// <summary>The handle Upvest hands back for a placed investment, the fund it bought, and where it stands.</summary>
public record UpvestInvestmentResult(string UpvestOrderId, string InstrumentId, UpvestInvestmentState State);

/// <summary>
/// The application's gateway to Upvest. Hides the SDK and the multi-step Upvest flows behind the
/// handful of operations the investing feature needs. Every call made through an implementation of
/// this interface goes through the single reusable signing <see cref="System.Net.Http.DelegatingHandler"/>.
/// </summary>
public interface IUpvestInvestingGateway
{
    /// <summary>
    /// Registers the shopper as an investor at Upvest (creating whatever Upvest needs to hold
    /// investments on their behalf) and reports where their acceptance stands.
    /// </summary>
    Task<UpvestEnrolmentResult> EnrolAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-reads where a shopper's acceptance stands at Upvest, provisioning anything still
    /// outstanding (such as their investing account) once Upvest has accepted them.
    /// </summary>
    Task<UpvestEnrolmentResult> RefreshAcceptanceAsync(string upvestUserId, string? upvestAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invests <paramref name="amount"/> euros of accrued spare change into the configured fund for
    /// the shopper, returning the Upvest order and where it currently stands.
    /// </summary>
    Task<UpvestInvestmentResult> PlaceInvestmentAsync(string upvestUserId, string upvestAccountId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>Re-reads where a placed investment stands at Upvest.</summary>
    Task<UpvestInvestmentState> GetInvestmentStateAsync(string upvestOrderId, CancellationToken cancellationToken = default);
}
