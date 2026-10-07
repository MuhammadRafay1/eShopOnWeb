using System;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shopper's postal address on the investor sign-up form.</summary>
public class EnrolmentAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>The investor sign-up form. The caller's own identity comes from the token, not this body.</summary>
public class EnrolmentRequest : BaseRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date, e.g. 1990-05-17.</summary>
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string Nationality { get; set; } = string.Empty;

    public EnrolmentAddress Address { get; set; } = new();

    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }
    public string? TaxCountry { get; set; }
}

public class EnrolmentResponse : BaseResponse
{
    public EnrolmentResponse(Guid correlationId) : base(correlationId)
    {
    }

    public EnrolmentResponse()
    {
    }

    /// <summary>Stable identifier for this shopper's enrolment.</summary>
    public Guid EnrolmentId { get; set; }

    /// <summary><c>pending</c>, <c>active</c> or <c>rejected</c>.</summary>
    public string Status { get; set; } = "pending";
}
