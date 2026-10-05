namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up details, as collected from the shopper and forwarded to Upvest.
/// This data is never persisted or logged by the application.
/// </summary>
public sealed record UpvestInvestorRegistration(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string Nationality,
    string AddressLine1,
    string Postcode,
    string City,
    string Country,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);
