using System;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class CancelOrderResponse : BaseResponse
{
    public CancelOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public CancelOrderResponse()
    {
    }

    public int OrderId { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}
