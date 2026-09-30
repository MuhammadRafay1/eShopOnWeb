using System;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CancelOrderResponse : BaseResponse
{
    public CancelOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public CancelOrderResponse()
    {
    }

    public OrderDto Order { get; set; } = default!;
}
