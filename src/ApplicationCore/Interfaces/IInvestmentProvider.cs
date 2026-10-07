using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The shop's port onto an external investment provider (Upvest). It is the
/// only seam through which the application holds investments on a shopper's
/// behalf; the concrete adapter lives in Infrastructure and owns every detail
/// of talking to the provider.
/// </summary>
public interface IInvestmentProvider
{
    /// <summary>
    /// Registers the shopper as an investor with the provider and provisions
    /// an account able to hold investments for them.
    /// </summary>
    Task<ProviderEnrolment> EnrolAsync(InvestorRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-reads where the shopper's enrolment has got to at the provider,
    /// provisioning the holding account if acceptance has since completed.
    /// </summary>
    Task<ProviderEnrolment> RefreshEnrolmentAsync(string providerUserId, string? providerAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invests the given euro amount for the shopper into the configured fund,
    /// returning the provider order id that carries it out.
    /// </summary>
    Task<string> InvestAsync(string providerUserId, string providerAccountId, decimal amountEuros, CancellationToken cancellationToken = default);

    /// <summary>Reads the current outcome of a previously placed investment order.</summary>
    Task<ProviderInvestmentOutcome> GetInvestmentOutcomeAsync(string providerOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Best-effort: asks the provider to deliver order/execution updates to the given callback
    /// URL. Settlement is also reconciled on read, so this never throws.
    /// </summary>
    Task RegisterCallbackAsync(string callbackUrl, CancellationToken cancellationToken = default);
}

/// <summary>The shop's investor sign-up form, as carried into the provider.</summary>
public record InvestorRegistration(
    string FirstName,
    string LastName,
    string Email,
    DateTime BirthDate,
    string Nationality,
    RegistrationAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

public record RegistrationAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);

/// <summary>The outcome of an enrolment at the provider.</summary>
public record ProviderEnrolment(
    string ProviderUserId,
    string? ProviderAccountId,
    EnrolmentStatus Status);

/// <summary>Where a placed investment order has got to at the provider.</summary>
public enum ProviderInvestmentOutcome
{
    Pending,
    Settled,
    Failed
}
