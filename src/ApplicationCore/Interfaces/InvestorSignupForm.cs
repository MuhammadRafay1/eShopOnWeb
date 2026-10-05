using System;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The shop's investor sign-up form for a shopper opting in to investing their change. Carries personal
/// details that are passed to Upvest but never written to logs.
/// </summary>
public record InvestorSignupForm(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    InvestorAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>Residential address on the investor sign-up form.</summary>
public record InvestorAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
