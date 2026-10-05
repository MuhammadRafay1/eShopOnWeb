using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form, as collected from the shopper. The gateway maps this onto Upvest's
/// user-onboarding payload. This value is passed to the gateway transiently and is never logged.
/// </summary>
public record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string Nationality,
    InvestorAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>Residential address from the sign-up form.</summary>
public record InvestorAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
