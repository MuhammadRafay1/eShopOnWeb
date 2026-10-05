using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.Configuration;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>One investment in the list returned by GET /api/investing/investments.</summary>
public class InvestmentResponse
{
    public int InvestmentId { get; set; }

    /// <summary>The amount invested, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    /// <summary>"pending" until the outcome is known at Upvest, then "settled" or "failed".</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>The list of a shopper's investments, newest first.</summary>
public class InvestmentsResponse
{
    public List<InvestmentResponse> Investments { get; set; } = new();
}

/// <summary>What a shopper has set aside and invested, from GET /api/investing/balance.</summary>
public class BalanceResponse
{
    /// <summary>Set aside but not yet invested, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    /// <summary>Total invested so far, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}
