using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

// ---- Enrolment ----

public class AddressDto
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class EnrolmentRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string TaxCountry { get; set; } = string.Empty;
}

public class EnrolmentResponse
{
    public Guid EnrolmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ---- Orders ----

public class PlaceOrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class PlaceOrderRequest
{
    public List<PlaceOrderItemRequest> Items { get; set; } = new();
}

public class PlaceOrderResponse
{
    public int OrderId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}

// ---- Investments ----

public class InvestmentResponse
{
    public Guid InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

// ---- Balance ----

public class BalanceResponse
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}

/// <summary>Maps domain values to their fixed public representations and resolves the caller.</summary>
public static class InvestingContractMapping
{
    public static string ToApi(EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending",
    };

    public static string ToApi(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending",
    };

    /// <summary>The authenticated shopper's stable identity, taken from the bearer token.</summary>
    public static string? ResolveShopper(HttpContext http)
    {
        var user = http.User;
        return user.Identity?.Name
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("unique_name")
            ?? user.FindFirstValue("name")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
    }
}
