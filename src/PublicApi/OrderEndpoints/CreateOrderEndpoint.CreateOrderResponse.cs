using System;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderResponse : BaseResponse
{
    public CreateOrderResponse(Guid correlationId) : base(correlationId) { }
    public CreateOrderResponse() { }

    /// <summary>Identifier of the placed order.</summary>
    public int OrderId { get; set; }

    /// <summary>The amount this order set aside towards investing (0 when it set aside nothing).</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
