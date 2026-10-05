using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form submitted to opt a shopper in.</summary>
public class EnrolmentRequest
{
    [Required] public string FirstName { get; set; } = string.Empty;
    [Required] public string LastName { get; set; } = string.Empty;
    [Required] public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date, e.g. 1990-05-15.</summary>
    [Required] public string BirthDate { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    [Required] public string Nationality { get; set; } = string.Empty;

    [Required] public EnrolmentAddress Address { get; set; } = new();

    public string PhoneNumber { get; set; } = string.Empty;

    [Required] public string TaxId { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code of the tax residency.</summary>
    [Required] public string TaxCountry { get; set; } = string.Empty;
}

public class EnrolmentAddress
{
    [Required] public string Line1 { get; set; } = string.Empty;
    [Required] public string Postcode { get; set; } = string.Empty;
    [Required] public string City { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    [Required] public string Country { get; set; } = string.Empty;
}

/// <summary>Enrolment status response. <see cref="Status"/> is "pending", "active" or "rejected".</summary>
public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public string Status { get; set; } = "pending";
}
