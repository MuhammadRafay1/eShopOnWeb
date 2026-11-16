using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Serialises a decimal as a JSON number with exactly two decimal places (euros).</summary>
public class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteRawValue(value.ToString("0.00", CultureInfo.InvariantCulture));
}

// ---- Flow 1: investor ----

public class SignUpInvestorRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? BirthDate { get; set; }
    public string? BirthCity { get; set; }
    public string? BirthCountry { get; set; }
    public string? Nationality { get; set; }
    public AddressDto? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }
    public string? TaxCountry { get; set; }
    public IdentityDocumentDto? IdentityDocument { get; set; }
}

public class AddressDto
{
    public string? Line1 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

public class IdentityDocumentDto
{
    public string? Type { get; set; }
    public string? Number { get; set; }
    public string? IssuingCountry { get; set; }
    public string? ExpiryDate { get; set; }
}

public class InvestorResponse
{
    public string InvestorId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// ---- Flow 2: models ----

public class PutModelRequest
{
    public string? Name { get; set; }
    public List<FundDto>? Funds { get; set; }
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

public class ChooseModelRequest
{
    public string? ModelId { get; set; }
}

// ---- Flow 3: payments ----

public class PayInRequest
{
    public decimal Amount { get; set; }
}

public class PaymentResponse
{
    public int PaymentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

// ---- Flow 4: realignment ----

public class RealignmentResponse
{
    public int RealignmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ---- Flow 5: portfolio ----

public class PortfolioResponse
{
    public string? ModelId { get; set; }
    public List<HoldingDto> Holdings { get; set; } = new();

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal TotalValue { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PaidIn { get; set; }
}

public class HoldingDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Value { get; set; }
}

/// <summary>Maps domain status enums to the fixed lowercase status strings the API exposes.</summary>
public static class PortfolioStatusText
{
    public static string Of(InvestorStatus s) => s switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string Of(PortfolioPaymentStatus s) => s switch
    {
        PortfolioPaymentStatus.Invested => "invested",
        PortfolioPaymentStatus.Failed => "failed",
        _ => "pending"
    };

    public static string Of(RealignmentStatus s) => s switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending"
    };
}
