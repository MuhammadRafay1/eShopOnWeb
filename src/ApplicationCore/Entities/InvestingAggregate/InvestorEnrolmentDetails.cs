using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// The shop's investor sign-up form. These personal details are forwarded to Upvest when registering the
/// investor and are never persisted by this application nor written to logs.
/// </summary>
public sealed record InvestorEnrolmentDetails(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    EnrolmentAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>A postal address captured on the sign-up form.</summary>
public sealed record EnrolmentAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
