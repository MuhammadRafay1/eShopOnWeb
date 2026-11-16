using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

internal static class CurrentUser
{
    /// <summary>The signed-in shopper's identity, taken from the JWT (never from the request body).</summary>
    public static string Name(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name)
        ?? user.Identity?.Name
        ?? throw new UnauthorizedAccessException("The caller identity is missing from the token.");
}

// ── Flow 1: becoming an investor ──

public sealed class InvestorSignUpRequest : BaseRequest
{
    /// <summary>Set server-side from the JWT; never bound from the request body.</summary>
    [JsonIgnore] public string ShopUserId { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string BirthCity { get; set; } = string.Empty;
    public string BirthCountry { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
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
    public DateOnly ExpiryDate { get; set; }
}

public sealed class InvestorResponse
{
    public Guid InvestorId { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ── Flow 2: model portfolios ──

public sealed class UpsertModelRequest : BaseRequest
{
    /// <summary>Set server-side from the route; never bound from the request body.</summary>
    [JsonIgnore] public string ModelId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public sealed class GetModelsRequest : BaseRequest { }

public sealed class FundDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Weight { get; set; }
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

public sealed class SelectModelRequest : BaseRequest
{
    [JsonIgnore] public string ShopUserId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
}

public sealed class GetInvestorRequest : BaseRequest
{
    [JsonIgnore] public string ShopUserId { get; set; } = string.Empty;
}

public sealed class GetPortfolioRequest : BaseRequest
{
    [JsonIgnore] public string ShopUserId { get; set; } = string.Empty;
}

// ── Flow 3: paying in ──

public sealed class CreatePaymentRequest : BaseRequest
{
    [JsonIgnore] public string ShopUserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class GetPaymentsRequest : BaseRequest
{
    [JsonIgnore] public string ShopUserId { get; set; } = string.Empty;
}

public sealed class PaymentResponse
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class PaymentsResponse
{
    public List<PaymentResponse> Payments { get; set; } = new();
}

// ── Flow 4: realignment ──

public sealed class TriggerRealignmentRequest : BaseRequest
{
    [JsonIgnore] public string ModelId { get; set; } = string.Empty;
}

public sealed class GetRealignmentRequest : BaseRequest
{
    [JsonIgnore] public Guid RealignmentId { get; set; }
}

public sealed class RealignmentResponse
{
    public Guid RealignmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ── Flow 5: portfolio ──

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
