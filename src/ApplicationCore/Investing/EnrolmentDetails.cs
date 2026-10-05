namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form, as supplied by a shopper opting in.
/// These details are passed to Upvest and are never persisted or logged by the shop.
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

public record EnrolmentAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
