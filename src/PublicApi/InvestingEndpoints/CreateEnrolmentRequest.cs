namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// The shop's investor sign-up form. These are personal details and are never
/// written to logs.
/// </summary>
public class CreateEnrolmentRequest : BaseRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date, e.g. 1990-01-31.</summary>
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string Nationality { get; set; } = string.Empty;

    public EnrolmentAddress Address { get; set; } = new();

    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code of tax residency.</summary>
    public string TaxCountry { get; set; } = string.Empty;
}

public class EnrolmentAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
