using System;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form, submitted to opt the signed-in shopper in.</summary>
public class EnrolmentRequest : BaseRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date.</summary>
    public DateTime BirthDate { get; set; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Nationality { get; set; } = string.Empty;

    public AddressRequest Address { get; set; } = new();

    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string TaxCountry { get; set; } = string.Empty;
}

public class AddressRequest
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Country { get; set; } = string.Empty;
}
