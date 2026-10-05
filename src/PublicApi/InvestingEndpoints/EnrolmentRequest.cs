using System;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form.</summary>
public class EnrolmentRequest : BaseRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Date of birth as an ISO-8601 date (yyyy-MM-dd).</summary>
    public DateOnly BirthDate { get; set; }

    /// <summary>Nationality as an ISO 3166-1 alpha-2 code.</summary>
    public string Nationality { get; set; } = string.Empty;

    public EnrolmentAddress Address { get; set; } = new();

    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;

    /// <summary>Tax country as an ISO 3166-1 alpha-2 code.</summary>
    public string TaxCountry { get; set; } = string.Empty;
}

public class EnrolmentAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>Country as an ISO 3166-1 alpha-2 code.</summary>
    public string Country { get; set; } = string.Empty;
}
