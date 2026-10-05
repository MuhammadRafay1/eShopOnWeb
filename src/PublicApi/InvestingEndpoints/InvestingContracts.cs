using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

// ---- requests ----

public sealed class EnrolmentRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    /// <summary>ISO-8601 date, e.g. 1990-05-17.</summary>
    public string? BirthDate { get; set; }
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? Nationality { get; set; }
    public AddressRequest? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? TaxId { get; set; }
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? TaxCountry { get; set; }
}

public sealed class AddressRequest
{
    public string? Line1 { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? Country { get; set; }
}

public sealed class CreateOrderRequest
{
    public List<OrderItemRequest>? Items { get; set; }
}

public sealed class OrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

// ---- responses (fixed field names required by the task) ----

public sealed class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class CreateOrderResponse
{
    public int OrderId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}

public sealed class BalanceResponse
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}

public sealed class InvestmentResponse
{
    public int InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}
