namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form. Carries personal details only as far as Upvest; it is
/// never persisted in the shop and never written to logs.
/// </summary>
public record EnrolmentDetails(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
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
