using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form — the personal details a shopper provides to become an
/// investor with Upvest. This is passed straight to the onboarding process and is never
/// persisted in the shop or written to logs.
/// </summary>
public record InvestorRegistration(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    RegistrationAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>Residential address captured on the sign-up form.</summary>
public record RegistrationAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
