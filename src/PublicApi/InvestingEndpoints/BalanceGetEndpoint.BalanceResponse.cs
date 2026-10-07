using System;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class BalanceResponse : BaseResponse
{
    public BalanceResponse(Guid correlationId) : base(correlationId) { }

    public BalanceResponse() { }

    /// <summary>Change set aside and not yet invested, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    /// <summary>Total amount invested so far, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}
