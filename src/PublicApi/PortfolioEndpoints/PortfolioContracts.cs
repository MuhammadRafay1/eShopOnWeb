using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ---- Request DTOs -------------------------------------------------------------------------------

public class InvestorSignupRequest
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public DateOnly BirthDate { get; set; }
    public string? BirthCity { get; set; }
    public string? BirthCountry { get; set; }
    public string Nationality { get; set; } = default!;
    public AddressDto Address { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string TaxId { get; set; } = default!;
    public string TaxCountry { get; set; } = default!;
    public IdentityDocumentDto IdentityDocument { get; set; } = default!;
}

public class AddressDto
{
    public string Line1 { get; set; } = default!;
    public string Postcode { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
}

public class IdentityDocumentDto
{
    public string Type { get; set; } = default!;
    public string Number { get; set; } = default!;
    public string IssuingCountry { get; set; } = default!;
    public DateOnly ExpiryDate { get; set; }
}

public class UpsertModelRequest
{
    public string Name { get; set; } = default!;
    public List<FundInput> Funds { get; set; } = new();
}

public class FundInput
{
    public string InstrumentId { get; set; } = default!;
    public decimal Weight { get; set; }
}

public class ChooseModelRequest
{
    public string ModelId { get; set; } = default!;
}

public class PayInRequest
{
    public decimal Amount { get; set; }
}

// ---- Response DTOs (field names fixed by the task) -----------------------------------------------

public class InvestorResponse
{
    public Guid InvestorId { get; set; }
    public string Status { get; set; } = default!;
}

public class FundDto
{
    public string InstrumentId { get; set; } = default!;
    public decimal Weight { get; set; }
}

public class ModelDto
{
    public string ModelId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public List<FundDto> Funds { get; set; } = new();
}

public class PaymentResponse
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
}

public class RealignmentResponse
{
    public Guid RealignmentId { get; set; }
    public string Status { get; set; } = default!;
}

public class HoldingDto
{
    public string InstrumentId { get; set; } = default!;
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

/// <summary>Shared helpers for the portfolio endpoints: caller identity, status strings, money rounding.</summary>
internal static class PortfolioHttp
{
    /// <summary>The authenticated shopper's user name from the JWT; null if unauthenticated.</summary>
    public static string? UserName(HttpContext? http) =>
        http?.User.FindFirstValue(ClaimTypes.Name) ?? http?.User.Identity?.Name;

    public static string Status(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string Status(PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending"
    };

    public static string Status(RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending"
    };

    /// <summary>Money in euros to two decimal places (serialized as a JSON number).</summary>
    public static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
