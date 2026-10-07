using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form. These are the shopper's personal details:
/// they are passed straight through to Upvest at enrolment and are never
/// persisted or written to logs by this application.
/// </summary>
public record InvestorDetails(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    InvestorAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>The shopper's residential address, part of <see cref="InvestorDetails"/>.</summary>
public record InvestorAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
