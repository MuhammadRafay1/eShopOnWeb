using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form, carried from the API request through to the Upvest gateway.
/// This is personal data: it is passed to Upvest and never persisted or logged.
/// </summary>
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

/// <summary>Postal address from the sign-up form.</summary>
public record EnrolmentAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
