using System;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class RefundOrderResponse : BaseResponse
{
    public RefundOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public RefundOrderResponse()
    {
    }

    public int OrderId { get; set; }
    public int RefundId { get; set; }
    public OrderPaymentDto Payment { get; set; } = null!;
}
