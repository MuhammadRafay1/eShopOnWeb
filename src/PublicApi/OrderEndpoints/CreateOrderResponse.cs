using System;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.Investing;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderResponse : BaseResponse
{
    public CreateOrderResponse(Guid correlationId) : base(correlationId) { }

    public CreateOrderResponse() { }

    public int OrderId { get; set; }

    /// <summary>Amount this order set aside, in euros (0 when it set aside nothing).</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
