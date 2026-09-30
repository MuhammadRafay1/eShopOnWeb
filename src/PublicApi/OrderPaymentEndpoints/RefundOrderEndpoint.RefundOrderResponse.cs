using System;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class RefundOrderResponse : BaseResponse
{
    public RefundOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public RefundOrderResponse()
    {
    }

    public int RefundId { get; set; }
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalRefunded { get; set; }
    public decimal RefundableRemaining { get; set; }
}
