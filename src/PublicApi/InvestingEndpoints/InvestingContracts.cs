using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.Infrastructure.Investing;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form (Flow 1).</summary>
public class EnrolmentRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>ISO-8601 date (YYYY-MM-DD).</summary>
    public string BirthDate { get; set; } = string.Empty;
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Nationality { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new();
    public string? PhoneNumber { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;
}

public class AddressDto
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Country { get; set; } = string.Empty;
}

/// <summary>Response for both enrolment endpoints. Field names <c>enrolmentId</c>/<c>status</c> are fixed.</summary>
public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Request for POST /api/orders.</summary>
public class PlaceOrderRequest
{
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Response for POST /api/orders. Field names <c>orderId</c>/<c>roundUpAmount</c> are fixed.</summary>
public class PlaceOrderResponse
{
    public int OrderId { get; set; }

    [System.Text.Json.Serialization.JsonConverter(typeof(TwoDecimalMoneyConverter))]
    public decimal RoundUpAmount { get; set; }
}

/// <summary>An investment entry. Field names <c>investmentId</c>/<c>amount</c>/<c>status</c> are fixed.</summary>
public class InvestmentResponse
{
    public int InvestmentId { get; set; }

    [System.Text.Json.Serialization.JsonConverter(typeof(TwoDecimalMoneyConverter))]
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Response for GET /api/investing/balance. Field names <c>pendingAmount</c>/<c>investedAmount</c> are fixed.</summary>
public class BalanceResponse
{
    [System.Text.Json.Serialization.JsonConverter(typeof(TwoDecimalMoneyConverter))]
    public decimal PendingAmount { get; set; }

    [System.Text.Json.Serialization.JsonConverter(typeof(TwoDecimalMoneyConverter))]
    public decimal InvestedAmount { get; set; }
}

/// <summary>Shared mapping helpers for the investing endpoints.</summary>
internal static class InvestingMapping
{
    public static decimal Euros(long cents) => Math.Round(cents / 100m, 2);

    public static string ToWire(EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending",
    };

    public static string ToWire(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending",
    };

    public static string? BuyerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;

    /// <summary>
    /// Maps a provider failure to a caller-facing result. A provider rejection the caller caused keeps its
    /// 4xx; our own credential/quota problems and transport failures become 502/503 so the caller is not
    /// told they did something wrong when they did not.
    /// </summary>
    public static IResult MapProviderError(UpvestProviderException ex)
    {
        var status = (int?)ex.StatusCode switch
        {
            401 or 403 => 502,
            429 => 503,
            >= 400 and < 500 => (int)ex.StatusCode!,
            _ => 502,
        };
        return Results.Problem(statusCode: status, title: "Investment provider error", detail: ex.Message);
    }
}
