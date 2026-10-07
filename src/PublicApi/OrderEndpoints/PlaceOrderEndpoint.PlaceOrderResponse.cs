using System;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class PlaceOrderResponse : BaseResponse
{
    public PlaceOrderResponse(Guid correlationId) : base(correlationId) { }

    public PlaceOrderResponse() { }

    /// <summary>The id of the placed order.</summary>
    public int OrderId { get; set; }

    /// <summary>The change set aside by this order (0 when it set aside nothing), in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
