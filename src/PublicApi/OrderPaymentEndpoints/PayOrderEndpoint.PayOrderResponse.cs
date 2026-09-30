using System;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class PayOrderResponse : BaseResponse
{
    public PayOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public PayOrderResponse()
    {
    }

    public int OrderId { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? AuthorizationId { get; set; }
    public decimal AmountHeld { get; set; }
    public string Currency { get; set; } = string.Empty;
}
