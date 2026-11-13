using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Base for per-shopper requests. <see cref="BuyerId"/> and <see cref="Cancellation"/> are set server-side
/// from the JWT and the request — they have no public setter, so a caller can never bind them from the body
/// (which would let one shopper act as another).
/// </summary>
public abstract class AuthenticatedRequest : BaseRequest
{
    public string BuyerId { get; private set; } = string.Empty;
    public CancellationToken Cancellation { get; private set; }

    public void Bind(string buyerId, CancellationToken cancellation)
    {
        BuyerId = buyerId;
        Cancellation = cancellation;
    }
}

// ---- Request bodies ----

public class SignUpInvestorRequest : AuthenticatedRequest
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

public class PutModelRequest
{
    public string? Name { get; set; }
    public List<FundDto>? Funds { get; set; }

    // Set server-side from the route and request (never body-bound).
    public string ModelId { get; private set; } = string.Empty;
    public CancellationToken Cancellation { get; private set; }
    public void Bind(string modelId, CancellationToken cancellation)
    {
        ModelId = modelId;
        Cancellation = cancellation;
    }
}

public class FundDto
{
    public string? InstrumentId { get; set; }
    public decimal Weight { get; set; }
}

public class ChooseModelRequest : AuthenticatedRequest
{
    public string? ModelId { get; set; }
}

public class CreatePaymentRequest : AuthenticatedRequest
{
    public decimal Amount { get; set; }
}

/// <summary>A per-shopper request with no body (the caller is identified by the JWT).</summary>
public class CallerRequest : AuthenticatedRequest
{
}

public class GetRealignmentRequest : AuthenticatedRequest
{
    public Guid RealignmentId { get; private set; }
    public void SetRealignmentId(Guid id) => RealignmentId = id;
}

// ---- Responses (field names below are fixed by the task) ----

public class InvestorResponse
{
    public Guid InvestorId { get; set; }
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
    public string Status { get; set; } = string.Empty;
}

public class PaymentListItem
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RealignmentResponse
{
    public Guid RealignmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class HoldingDto
{
    public string InstrumentId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
}

public class PortfolioResponse
{
    public string? ModelId { get; set; }
    public List<HoldingDto> Holdings { get; set; } = new();
    public decimal TotalValue { get; set; }
    public decimal PaidIn { get; set; }
}

/// <summary>Maps the app's status enums to the fixed lowercase status strings the API returns.</summary>
public static class StatusText
{
    public static string For(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending",
    };

    public static string For(PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending",
    };

    public static string For(RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending",
    };
}
