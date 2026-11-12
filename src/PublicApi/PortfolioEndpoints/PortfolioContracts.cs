using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ----------------------------------------------------------------------- requests

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
    public DateOnly? ExpiryDate { get; set; }
}

/// <summary>The shop's investor sign-up form.</summary>
public class InvestorSignUpRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string? BirthCity { get; set; }
    public string? BirthCountry { get; set; }
    public string Nationality { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new();
    public string? PhoneNumber { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;
    public IdentityDocumentDto IdentityDocument { get; set; } = new();
}

public class FundInputDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public class ModelPutRequest
{
    public string Name { get; set; } = string.Empty;
    public List<FundInputDto> Funds { get; set; } = new();
}

public class ChooseModelRequest
{
    public string ModelId { get; set; } = string.Empty;
}

public class PayInRequest
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }
}

// ----------------------------------------------------------------------- responses

public class InvestorResponse
{
    public string InvestorId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public class FundDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public class ModelResponse
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public class PaymentResponse
{
    public string PaymentId { get; set; } = string.Empty;

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

/// <summary>Shape returned by POST /api/portfolio/payments (paymentId + status).</summary>
public class PaymentCreatedResponse
{
    public string PaymentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class RealignmentResponse
{
    public string RealignmentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public class HoldingDto
{
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Quantity of units held. Not a money amount; kept at full precision.</summary>
    public decimal Quantity { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Value { get; set; }
}

public class PortfolioResponse
{
    public string? ModelId { get; set; }
    public List<HoldingDto> Holdings { get; set; } = new();

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal TotalValue { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PaidIn { get; set; }
}
