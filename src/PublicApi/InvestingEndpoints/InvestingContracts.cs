using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

// ---- Enrolment ----

public class EnrolmentAddress
{
    [Required] public string Line1 { get; set; } = string.Empty;
    [Required] public string Postcode { get; set; } = string.Empty;
    [Required] public string City { get; set; } = string.Empty;
    [Required] public string Country { get; set; } = string.Empty;
}

/// <summary>The shop's investor sign-up form.</summary>
public class EnrolmentRequest
{
    [Required] public string FirstName { get; set; } = string.Empty;
    [Required] public string LastName { get; set; } = string.Empty;
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string BirthDate { get; set; } = string.Empty;
    [Required] public string Nationality { get; set; } = string.Empty;
    [Required] public EnrolmentAddress Address { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    [Required] public string TaxId { get; set; } = string.Empty;
    [Required] public string TaxCountry { get; set; } = string.Empty;
}

public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ---- Orders ----

public class CreateOrderItem
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderRequest
{
    [Required] public List<CreateOrderItem> Items { get; set; } = new();
}

public class CreateOrderResponse
{
    public int OrderId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}

// ---- Balance ----

public class BalanceResponse
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}

// ---- Investments ----

public class InvestmentResponse
{
    public int InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}
