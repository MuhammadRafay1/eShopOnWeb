using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// -------------------- Requests --------------------

public class RegisterInvestorRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string BirthDate { get; set; } = string.Empty;
    public string? BirthCity { get; set; }
    public string? BirthCountry { get; set; }
    public string Nationality { get; set; } = string.Empty;
    public AddressRequest Address { get; set; } = new();
    public string? PhoneNumber { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;
    public IdentityDocumentRequest IdentityDocument { get; set; } = new();
}

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

public class UpsertModelRequest
{
    public string Name { get; set; } = string.Empty;
    public List<ModelFundRequest> Funds { get; set; } = new();
}

public class ModelFundRequest
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public class SelectModelRequest
{
    public string ModelId { get; set; } = string.Empty;
}

public class CreatePaymentRequest
{
    public decimal Amount { get; set; }
}

// -------------------- Responses --------------------

public record InvestorResponse(Guid InvestorId, string Status);

public record FundResponse(string InstrumentId, decimal Weight);
public record ModelResponse(string ModelId, string Name, IReadOnlyList<FundResponse> Funds);

public record CreatePaymentResponse(Guid PaymentId, string Status);
public record PaymentResponse(
    Guid PaymentId,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal Amount,
    string Status);

public record RealignmentResponse(Guid RealignmentId, string Status);

public record HoldingResponse(
    string InstrumentId,
    decimal Quantity,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal Value);
public record PortfolioResponse(
    string? ModelId,
    IReadOnlyList<HoldingResponse> Holdings,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal TotalValue,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal PaidIn);
