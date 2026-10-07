using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form. These details are passed to Upvest to create the investor and
/// are never persisted by the shop (see <c>Investor</c>).
/// </summary>
public record InvestorSignup(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    InvestorAddress Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>A residential address as captured on the sign-up form.</summary>
public record InvestorAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
