using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The shop's investor sign-up form.</summary>
public class EnrolRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO-8601 date of birth, e.g. <c>1990-05-17</c>.</summary>
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 nationality code.</summary>
    public string Nationality { get; set; } = string.Empty;

    public EnrolAddress Address { get; set; } = new();

    public string? PhoneNumber { get; set; }

    public string TaxId { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 tax residence country code.</summary>
    public string TaxCountry { get; set; } = string.Empty;
}

public class EnrolAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class EnrolmentResponse
{
    [JsonPropertyName("enrolmentId")]
    public Guid EnrolmentId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class BalanceResponse
{
    [JsonPropertyName("pendingAmount")]
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonPropertyName("investedAmount")]
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}

public class InvestmentDto
{
    [JsonPropertyName("investmentId")]
    public Guid InvestmentId { get; set; }

    [JsonPropertyName("amount")]
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class InvestmentsResponse
{
    [JsonPropertyName("investments")]
    public List<InvestmentDto> Investments { get; set; } = new();
}

/// <summary>Maps domain statuses to the fixed lowercase strings the API surface uses.</summary>
public static class InvestingStatusText
{
    public static string ToText(this EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string ToText(this InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending"
    };
}
