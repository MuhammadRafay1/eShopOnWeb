using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form.</summary>
public class EnrolmentRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }

    /// <summary>ISO-8601 date, e.g. 1990-05-21.</summary>
    public string? BirthDate { get; set; }

    /// <summary>ISO 3166-1 alpha-2, e.g. DE.</summary>
    public string? Nationality { get; set; }

    public EnrolmentAddressModel? Address { get; set; }

    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }

    /// <summary>ISO 3166-1 alpha-2, e.g. DE.</summary>
    public string? TaxCountry { get; set; }
}

public class EnrolmentAddressModel
{
    public string? Line1 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

/// <summary>Enrolment state returned to the caller.</summary>
public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }

    /// <summary>pending | active | rejected.</summary>
    public string Status { get; set; } = string.Empty;
}
