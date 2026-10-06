namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form, as supplied by the shopper when opting in.
/// This carries personal data: it is passed straight to Upvest and never logged or persisted.
/// </summary>
public record InvestorSignup(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string Nationality,
    SignupAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

public record SignupAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);
