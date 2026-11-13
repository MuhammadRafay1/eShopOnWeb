using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.PortfolioEndpoints;

// ---- Requests --------------------------------------------------------------------------------

public sealed class AddressDto
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public sealed class IdentityDocumentDto
{
    public string Type { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string IssuingCountry { get; set; } = string.Empty;
    public DateTimeOffset ExpiryDate { get; set; }
}

public sealed class InvestorSignupRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset BirthDate { get; set; }
    public string? BirthCity { get; set; }
    public string? BirthCountry { get; set; }
    public string Nationality { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new();
    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }
    public string? TaxCountry { get; set; }
    public IdentityDocumentDto IdentityDocument { get; set; } = new();
}

public sealed class FundDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public sealed class PutModelRequest
{
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public sealed class ChooseModelRequest
{
    public string ModelId { get; set; } = string.Empty;
}

public sealed class PayInRequest
{
    public decimal Amount { get; set; }
}

// ---- Responses -------------------------------------------------------------------------------

public sealed class InvestorResponse
{
    public string? InvestorId { get; set; }
    public string Status { get; set; } = "pending";
}

public sealed class ModelResponse
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public sealed class ModelsResponse
{
    public List<ModelResponse> Models { get; set; } = new();
}

public sealed class PaymentResponse
{
    public string PaymentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "pending";
}

public sealed class PaymentsResponse
{
    public List<PaymentResponse> Payments { get; set; } = new();
}

public sealed class RealignmentResponse
{
    public string RealignmentId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
}

public sealed class HoldingResponse
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
}

public sealed class PortfolioResponse
{
    public string? ModelId { get; set; }
    public List<HoldingResponse> Holdings { get; set; } = new();
    public decimal TotalValue { get; set; }
    public decimal PaidIn { get; set; }
}
