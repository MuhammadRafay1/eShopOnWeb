using System;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class PlaceOrderResponse : BaseResponse
{
    public PlaceOrderResponse(Guid correlationId) : base(correlationId) { }
    public PlaceOrderResponse() { }

    public int OrderId { get; set; }

    /// <summary>The amount this order set aside, 0 when it set aside nothing.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
