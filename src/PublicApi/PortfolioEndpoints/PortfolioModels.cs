using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

// ---- Flow 1: becoming an investor ----

/// <summary>The shop's investor sign-up form. Dates are ISO-8601 (YYYY-MM-DD). Personal details; never logged.</summary>
public class InvestorSignupRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
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

public class InvestorResponse
{
    public string InvestorId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// ---- Flow 2: model portfolios ----

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

public class ModelDto
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public class ChooseModelRequest
{
    public string ModelId { get; set; } = string.Empty;
}

// ---- Flow 3: paying in ----

public class PayInRequest
{
    public decimal Amount { get; set; }
}

public class PaymentResponse
{
    public string PaymentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class PaymentListItem
{
    public string PaymentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ---- Flow 4: realignment ----

public class RealignmentResponse
{
    public string RealignmentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// ---- Flow 5: the portfolio ----

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

/// <summary>Shared helpers for the portfolio endpoints.</summary>
internal static class PortfolioEndpointHelpers
{
    /// <summary>The caller's own identity, from the JWT (ClaimTypes.Name). Scopes every record to one shopper.</summary>
    public static string? GetBuyerId(HttpContext http) => http.User?.Identity?.Name;

    /// <summary>Enum name lowercased — yields exactly the API's fixed status strings (pending/active/rejected/invested/failed/completed).</summary>
    public static string ToApiString(this InvestorLifecycleStatus status) => status.ToString().ToLowerInvariant();
    public static string ToApiString(this PaymentStatus status) => status.ToString().ToLowerInvariant();
    public static string ToApiString(this RealignmentStatus status) => status.ToString().ToLowerInvariant();

    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
