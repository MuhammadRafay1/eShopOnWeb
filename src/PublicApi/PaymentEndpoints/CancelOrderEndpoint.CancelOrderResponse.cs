using System;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class CancelOrderResponse : BaseResponse
{
    public CancelOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public CancelOrderResponse()
    {
    }

    public int OrderId { get; set; }
    public OrderPaymentDto Payment { get; set; } = null!;
}
