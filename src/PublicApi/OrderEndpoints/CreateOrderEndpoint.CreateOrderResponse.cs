using System;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderResponse : BaseResponse
{
    public CreateOrderResponse(Guid correlationId) : base(correlationId) { }

    public CreateOrderResponse() { }

    /// <summary>The id of the placed order.</summary>
    public int OrderId { get; set; }

    /// <summary>The euro amount this order set aside towards investing (0 when nothing was set aside).</summary>
    public decimal RoundUpAmount { get; set; }
}
