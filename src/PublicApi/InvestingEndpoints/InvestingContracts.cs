using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Helper for reading the signed-in shopper's identity from the JWT.</summary>
public static class Caller
{
    public static string? BuyerId(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;
}

// ---- Requests ----

/// <summary>The shop's investor sign-up form (POST /api/investing/enrolment).</summary>
public class EnrolInvestorRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date, e.g. "1990-05-17".</summary>
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 nationality, e.g. "DE".</summary>
    public string Nationality { get; set; } = string.Empty;

    public EnrolAddress Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 tax country, e.g. "DE".</summary>
    public string TaxCountry { get; set; } = string.Empty;
}

public class EnrolAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country, e.g. "DE".</summary>
    public string Country { get; set; } = string.Empty;
}

/// <summary>Place an order from catalog items (POST /api/orders).</summary>
public class PlaceOrderRequest
{
    public List<OrderLineRequest> Items { get; set; } = new();
}

public class OrderLineRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

// ---- Responses (fixed field names) ----

public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class BalanceResponse
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}

public class InvestmentResponse
{
    public int InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class PlaceOrderResponse
{
    public int OrderId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
