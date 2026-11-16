using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.eShopWeb.PublicApi.Portfolio;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ----- Requests -----

/// <summary>Server-set context carried on every portfolio request (never bound from the body):
/// the caller's identity from the JWT, and the request cancellation token.</summary>
public abstract class PortfolioRequestBase
{
    [JsonIgnore] public string CallerId { get; set; } = string.Empty;
    [JsonIgnore] public CancellationToken Cancellation { get; set; }
}

public sealed class GetInvestorRequest : PortfolioRequestBase { }

public sealed class ListModelsRequest : PortfolioRequestBase { }

public sealed class ListPaymentsRequest : PortfolioRequestBase { }

public sealed class GetPortfolioRequest : PortfolioRequestBase { }

public sealed class TriggerRealignmentRequest : PortfolioRequestBase
{
    [JsonIgnore] public string ModelId { get; set; } = string.Empty;
}

public sealed class GetRealignmentRequest : PortfolioRequestBase
{
    [JsonIgnore] public Guid RealignmentId { get; set; }
}

public sealed class RegisterInvestorRequest : PortfolioRequestBase
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
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;
    public IdentityDocumentDto IdentityDocument { get; set; } = new();
}

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

public sealed class UpsertModelRequest : PortfolioRequestBase
{
    [JsonIgnore] public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public sealed class SelectModelRequest : PortfolioRequestBase
{
    public string ModelId { get; set; } = string.Empty;
}

public sealed class CreatePaymentRequest : PortfolioRequestBase
{
    public decimal Amount { get; set; }
}

// ----- Responses -----

public sealed class FundDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}

public sealed class InvestorResponse
{
    public Guid InvestorId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class ModelResponse
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public sealed class PaymentResponse
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class RealignmentResponse
{
    public Guid RealignmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class HoldingDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
}

public sealed class PortfolioResponse
{
    public string? ModelId { get; set; }
    public List<HoldingDto> Holdings { get; set; } = new();
    public decimal TotalValue { get; set; }
    public decimal PaidIn { get; set; }
}

/// <summary>Maps the service lifecycle states onto the fixed wire status strings.</summary>
public static class StatusText
{
    public static string For(InvestorState s) => s switch
    {
        InvestorState.Active => "active",
        InvestorState.Rejected => "rejected",
        _ => "pending",
    };

    public static string For(PaymentState s) => s switch
    {
        PaymentState.Invested => "invested",
        PaymentState.Failed => "failed",
        _ => "pending",
    };

    public static string For(RealignmentState s) => s switch
    {
        RealignmentState.Completed => "completed",
        RealignmentState.Failed => "failed",
        _ => "pending",
    };
}
