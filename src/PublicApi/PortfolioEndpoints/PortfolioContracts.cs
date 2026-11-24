using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ----- Requests -----

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

public class FundDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public class UpsertModelRequest
{
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public class SelectModelRequest
{
    public string ModelId { get; set; } = string.Empty;
}

public class PayInRequest
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }
}

// ----- Responses (field names are fixed by the task spec) -----

public class InvestorResponse
{
    public string InvestorId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ModelResponse
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public class PaymentResponse
{
    public Guid PaymentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class PaymentCreatedResponse
{
    public Guid PaymentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RealignmentResponse
{
    public Guid RealignmentId { get; set; }
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

/// <summary>Mapping helpers from service results to API responses.</summary>
public static class PortfolioResponseMapper
{
    public static InvestorResponse ToResponse(this InvestorResult r) =>
        new() { InvestorId = r.InvestorId, Status = r.Status.ToString().ToLowerInvariant() };

    public static ModelResponse ToResponse(this ModelResult r) =>
        new()
        {
            ModelId = r.ModelId,
            Name = r.Name,
            Funds = r.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList(),
        };

    public static PaymentResponse ToResponse(this PaymentResult r) =>
        new() { PaymentId = r.PaymentId, Amount = r.Amount, Status = r.Status.ToString().ToLowerInvariant() };

    public static RealignmentResponse ToResponse(this RealignmentResult r) =>
        new() { RealignmentId = r.RealignmentId, Status = r.Status.ToString().ToLowerInvariant() };

    public static PortfolioResponse ToResponse(this PortfolioResult r) =>
        new()
        {
            ModelId = r.ModelId,
            Holdings = r.Holdings.Select(h => new HoldingResponse
            {
                InstrumentId = h.InstrumentId,
                Quantity = h.Quantity,
                Value = h.Value,
            }).ToList(),
            TotalValue = r.TotalValue,
            PaidIn = r.PaidIn,
        };
}
