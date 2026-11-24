using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ---- Requests ----

public class AddressDto
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class IdentityDocumentDto
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
    public string BirthCity { get; set; } = string.Empty;
    public string BirthCountry { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;
    public IdentityDocumentDto IdentityDocument { get; set; } = new();
}

public class ModelFundDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public class UpsertModelRequest
{
    public string Name { get; set; } = string.Empty;
    public List<ModelFundDto> Funds { get; set; } = new();
}

public class ChooseModelRequest
{
    public string ModelId { get; set; } = string.Empty;
}

public class CreatePaymentRequest
{
    public decimal Amount { get; set; }
}

// ---- Responses ----

public class InvestorResponse
{
    public string InvestorId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ModelResponse
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ModelFundDto> Funds { get; set; } = new();
}

public class CreatePaymentResponse
{
    public string PaymentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class PaymentResponse
{
    public string PaymentId { get; set; } = string.Empty;

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class RealignmentResponse
{
    public string RealignmentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class HoldingResponse
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Value { get; set; }
}

public class PortfolioResponse
{
    public string? ModelId { get; set; }
    public List<HoldingResponse> Holdings { get; set; } = new();

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal TotalValue { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PaidIn { get; set; }
}
