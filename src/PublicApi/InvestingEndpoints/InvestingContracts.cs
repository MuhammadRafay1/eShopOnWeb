using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

// ---- requests -------------------------------------------------------------------------------------------

/// <summary>The shop's investor sign-up form for POST /api/investing/enrolment.</summary>
public sealed class EnrolmentRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;     // ISO-8601 date
    public string Nationality { get; set; } = string.Empty;   // ISO 3166-1 alpha-2
    public EnrolmentAddress Address { get; set; } = new();
    public string? PhoneNumber { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;    // ISO 3166-1 alpha-2
}

public sealed class EnrolmentAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public sealed class PlaceOrderRequest
{
    public List<OrderLine> Items { get; set; } = new();
}

public sealed class OrderLine
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

// ---- responses ------------------------------------------------------------------------------------------

public sealed class EnrolmentResponse
{
    public Guid EnrolmentId { get; init; }
    public string Status { get; init; } = "pending";
}

public sealed class PlaceOrderResponse
{
    public int OrderId { get; init; }

    [JsonConverter(typeof(MoneyNumberJsonConverter))]
    public decimal RoundUpAmount { get; init; }
}

public sealed class BalanceResponse
{
    [JsonConverter(typeof(MoneyNumberJsonConverter))]
    public decimal PendingAmount { get; init; }

    [JsonConverter(typeof(MoneyNumberJsonConverter))]
    public decimal InvestedAmount { get; init; }
}

public sealed class InvestmentItem
{
    public Guid InvestmentId { get; init; }

    [JsonConverter(typeof(MoneyNumberJsonConverter))]
    public decimal Amount { get; init; }

    public string Status { get; init; } = "pending";
}

public sealed class InvestmentsResponse
{
    public IReadOnlyList<InvestmentItem> Investments { get; init; } = Array.Empty<InvestmentItem>();
}

// ---- helpers --------------------------------------------------------------------------------------------

internal static class InvestingMappings
{
    public static string ToWire(this EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending",
    };

    public static string ToWire(this InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending",
    };

    public static decimal ToEuros(long cents) => cents / 100m;

    /// <summary>The shopper's identity from the JWT (the <see cref="ClaimTypes.Name"/> claim).</summary>
    public static string? ShopperId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;
}
