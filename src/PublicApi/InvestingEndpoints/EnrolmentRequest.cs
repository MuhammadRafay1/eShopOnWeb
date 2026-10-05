namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form, posted by a shopper opting in.</summary>
public class EnrolmentRequest : BaseRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>ISO-8601 date, e.g. 1990-05-21.</summary>
    public string BirthDate { get; set; } = string.Empty;
    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string Nationality { get; set; } = string.Empty;
    public EnrolmentAddressDto Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    /// <summary>ISO 3166-1 alpha-2 country code of tax residency.</summary>
    public string TaxCountry { get; set; } = string.Empty;
}

public class EnrolmentAddressDto
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
