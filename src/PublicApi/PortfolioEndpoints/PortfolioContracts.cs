using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// Request contracts for the managed-investment-portfolio API. Response shapes are built inline
// by each endpoint so the fixed field names (investorId, status, modelId, funds, paymentId,
// amount, realignmentId, holdings, totalValue, paidIn) are exactly as specified.

public class AddressRequest
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class IdentityDocumentRequest
{
    public string Type { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string IssuingCountry { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
}

public class SignUpInvestorRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string? BirthCity { get; set; }
    public string? BirthCountry { get; set; }
    public string Nationality { get; set; } = string.Empty;
    public AddressRequest? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }
    public string? TaxCountry { get; set; }
    public IdentityDocumentRequest? IdentityDocument { get; set; }
}

public class FundRequest
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public class PutModelRequest
{
    public string Name { get; set; } = string.Empty;
    public List<FundRequest> Funds { get; set; } = new();
}

public class ChooseModelRequest
{
    public int ModelId { get; set; }
}

public class CreatePaymentRequest
{
    public decimal Amount { get; set; }
}
