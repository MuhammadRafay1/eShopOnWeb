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

    public int RefundId { get; set; }

    public string PayPalRefundId { get; set; } = "";
    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = "";
}
