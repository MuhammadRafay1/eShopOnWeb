using System;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class PayOrderResponse : BaseResponse
{
    public PayOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public PayOrderResponse()
    {
    }

    public int OrderId { get; set; }
    public OrderPaymentDto Payment { get; set; } = null!;
}
