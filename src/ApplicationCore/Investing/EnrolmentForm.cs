using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form, as collected from a shopper opting in. Personal data; never logged.
/// </summary>
public record EnrolmentForm(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    EnrolmentAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>Residential address from the sign-up form.</summary>
public record EnrolmentAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
