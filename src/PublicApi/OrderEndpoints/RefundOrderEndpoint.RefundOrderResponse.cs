using System;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class RefundOrderResponse : BaseResponse
{
    public RefundOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public RefundOrderResponse()
    {
    }

    public int OrderId { get; set; }
    public string RefundId { get; set; } = string.Empty;
    public decimal RefundedAmount { get; set; }
    public string RefundStatus { get; set; } = string.Empty;
    public decimal RemainingRefundable { get; set; }
    public PaymentDto Payment { get; set; } = new();
}
