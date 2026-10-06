using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Everything this application needs from Upvest, expressed in the shop's own terms.
/// The implementation lives in Infrastructure and is the only place that knows the Upvest wire
/// format; the single authenticating <c>DelegatingHandler</c> signs every call it makes.
/// </summary>
public interface IUpvestGateway
{
    /// <summary>
    /// Registers the shopper with Upvest as an investor: creates the Upvest user, the account
    /// that will hold their investments, and the KYC check whose outcome decides acceptance.
    /// </summary>
    Task<UpvestEnrolmentResult> EnrolInvestorAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken = default);

    /// <summary>Current enrolment status, derived from the KYC check's state at Upvest.</summary>
    Task<EnrolmentStatus> GetEnrolmentStatusAsync(string upvestUserId, string checkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the shopper has an account that can hold investments, creating it if needed. Used
    /// after acceptance when it could not be provisioned at enrolment time. Account ids are empty
    /// if the account is not available yet.
    /// </summary>
    Task<UpvestAccountResult> ProvisionAccountAsync(string upvestUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invests <paramref name="amount"/> euros in the configured fund for the shopper: funds the
    /// account with the cash and places a single buy order. Returns the order's id and state.
    /// </summary>
    Task<UpvestInvestmentResult> InvestAsync(string accountGroupId, string accountId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>The current state of a previously placed investment order at Upvest.</summary>
    Task<InvestmentStatus> GetInvestmentStatusAsync(string upvestOrderId, CancellationToken cancellationToken = default);
}

/// <summary>The shop's investor sign-up form. Personal data; never written to logs.</summary>
public record InvestorEnrolmentDetails(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    EnrolmentAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

public record EnrolmentAddress(string Line1, string Postcode, string City, string Country);

public record UpvestEnrolmentResult(string UserId, string AccountGroupId, string AccountId, string CheckId);

public record UpvestAccountResult(string AccountGroupId, string AccountId);

public record UpvestInvestmentResult(string OrderId, InvestmentStatus Status);
