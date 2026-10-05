namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop's investor sign-up form, as collected from the shopper at enrolment.
/// These are personal details: they are passed straight to Upvest and are never
/// persisted or written to logs by this application.
/// </summary>
public class InvestorSignUp
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date (yyyy-MM-dd).</summary>
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string Nationality { get; set; } = string.Empty;

    public InvestorAddress Address { get; set; } = new();

    public string PhoneNumber { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code for the tax residency.</summary>
    public string TaxCountry { get; set; } = string.Empty;
}

public class InvestorAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string Country { get; set; } = string.Empty;
}
