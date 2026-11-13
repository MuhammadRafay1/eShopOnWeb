using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// Request DTOs — field names match the task's JSON (bound case-insensitively from camelCase).

public sealed class InvestorSignupRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? BirthDate { get; set; }
    public string? BirthCity { get; set; }
    public string? BirthCountry { get; set; }
    public string? Nationality { get; set; }
    public AddressRequest? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }
    public string? TaxCountry { get; set; }
    public IdentityDocumentRequest? IdentityDocument { get; set; }
}

public sealed class AddressRequest
{
    public string? Line1 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

public sealed class IdentityDocumentRequest
{
    public string? Type { get; set; }
    public string? Number { get; set; }
    public string? IssuingCountry { get; set; }
    public string? ExpiryDate { get; set; }
}

public sealed class UpsertModelRequest
{
    public string? Name { get; set; }
    public List<ModelFundDto>? Funds { get; set; }
}

public sealed class ModelFundDto
{
    public string? InstrumentId { get; set; }
    public decimal Weight { get; set; }
}

public sealed class ChooseModelRequest
{
    public string? ModelId { get; set; }
}

public sealed class PayInRequest
{
    public decimal Amount { get; set; }
}

// Response DTOs — the fixed field names required by the task.

public sealed record InvestorResponse(Guid InvestorId, string Status);

public sealed record ModelResponse(string ModelId, string Name, IReadOnlyList<ModelFundDto> Funds);

public sealed record PaymentResponse(Guid PaymentId, decimal Amount, string Status);

public sealed record RealignmentResponse(Guid RealignmentId, string Status);

public sealed record HoldingResponse(string InstrumentId, decimal Quantity, decimal Value);

public sealed record PortfolioResponse(
    string? ModelId,
    IReadOnlyList<HoldingResponse> Holdings,
    decimal TotalValue,
    decimal PaidIn);
