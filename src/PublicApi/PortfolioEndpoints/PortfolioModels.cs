using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ---- Requests ----------------------------------------------------------------------------------------------

/// <summary>The shop's investor sign-up form. Personal details here are forwarded to Upvest, never persisted/logged.</summary>
public class RegisterInvestorRequest
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
    public DateTimeOffset ExpiryDate { get; set; }
}

public class UpsertModelBody
{
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public class ChooseModelRequest
{
    public string ModelId { get; set; } = string.Empty;
}

public class PayInRequest
{
    public decimal Amount { get; set; }

    /// <summary>Optional caller idempotency key; when supplied, a re-submit returns the first payment.</summary>
    public Guid? IdempotencyKey { get; set; }
}

// ---- Responses (field names are fixed by the task) ---------------------------------------------------------

public class InvestorResponse
{
    public Guid? InvestorId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ModelId { get; set; }
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

/// <summary>Maps domain status enums to the fixed lowercase wire values.</summary>
public static class PortfolioStatusText
{
    public static string From(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string From(PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending"
    };

    public static string From(RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending"
    };
}
