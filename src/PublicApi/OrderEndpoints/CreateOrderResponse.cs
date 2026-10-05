using System;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.Configuration;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderResponse : BaseResponse
{
    public CreateOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public CreateOrderResponse()
    {
    }

    /// <summary>The id of the placed order.</summary>
    public int OrderId { get; set; }

    /// <summary>The spare change this order set aside, in euros (0 if none).</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
