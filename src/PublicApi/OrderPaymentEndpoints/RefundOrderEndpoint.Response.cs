using System;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class RefundOrderResponse : BaseResponse
{
    public RefundOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public string RefundId { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalRefunded { get; set; }
    public decimal CapturedAmount { get; set; }
}
