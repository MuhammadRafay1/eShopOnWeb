using System;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form.</summary>
public class EnrolmentRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date, e.g. <c>1990-05-17</c>.</summary>
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 nationality, e.g. <c>DE</c>.</summary>
    public string Nationality { get; set; } = string.Empty;

    public EnrolmentAddress Address { get; set; } = new();

    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 tax country, e.g. <c>DE</c>.</summary>
    public string TaxCountry { get; set; } = string.Empty;
}

public class EnrolmentAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country, e.g. <c>DE</c>.</summary>
    public string Country { get; set; } = string.Empty;
}

/// <summary>Enrolment state returned to the caller.</summary>
public class EnrolmentResponse
{
    public Guid EnrolmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}
