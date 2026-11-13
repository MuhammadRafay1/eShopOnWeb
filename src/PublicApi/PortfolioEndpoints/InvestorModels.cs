using System;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>The shop's investor sign-up form. These values are personal data and are never logged.</summary>
public class CreateInvestorRequest : BaseRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    /// <summary>ISO-8601 date (yyyy-MM-dd).</summary>
    public string? BirthDate { get; set; }
    public string? BirthCity { get; set; }
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? BirthCountry { get; set; }
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? Nationality { get; set; }
    public AddressDto? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? TaxCountry { get; set; }
    public IdentityDocumentDto? IdentityDocument { get; set; }
}

public class AddressDto
{
    public string? Line1 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? Country { get; set; }
}

public class IdentityDocumentDto
{
    public string? Type { get; set; }
    public string? Number { get; set; }
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? IssuingCountry { get; set; }
    /// <summary>ISO-8601 date (yyyy-MM-dd).</summary>
    public string? ExpiryDate { get; set; }
}

public class InvestorResponse : BaseResponse
{
    public InvestorResponse(Guid correlationId) : base(correlationId) { }
    public InvestorResponse() { }

    /// <summary>The investor's identity at Upvest.</summary>
    public string InvestorId { get; set; } = string.Empty;

    /// <summary>pending | active | rejected.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Non-personal explanation of the current status.</summary>
    public string? Detail { get; set; }
}
