using System;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.Investing;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderResponse : BaseResponse
{
    public CreateOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public CreateOrderResponse()
    {
    }

    /// <summary>The id of the order that was placed.</summary>
    public int OrderId { get; set; }

    /// <summary>The spare change this order set aside, in euros (0 when it set aside nothing).</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
