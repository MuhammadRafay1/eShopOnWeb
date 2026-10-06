using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderResponse
{
    [JsonPropertyName("orderId")]
    public int OrderId { get; set; }

    /// <summary>The amount this order set aside, in euros (0 when it set aside nothing).</summary>
    [JsonPropertyName("roundUpAmount")]
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
