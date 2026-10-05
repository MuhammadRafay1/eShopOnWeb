namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form, as supplied by a shopper opting in.
/// This is personal data and must never be written to logs.
/// </summary>
public sealed record EnrolmentDetails(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string Nationality,
    string AddressLine1,
    string Postcode,
    string City,
    string Country,
    string? PhoneNumber,
    string? TaxId,
    string TaxCountry);
