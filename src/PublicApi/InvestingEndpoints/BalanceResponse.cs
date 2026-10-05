using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.Investing;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class BalanceResponse : BaseResponse
{
    /// <summary>Set aside and not yet invested, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    /// <summary>Total invested so far, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}
