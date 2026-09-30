using System;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class PayOrderResponse : BaseResponse
{
    public PayOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AuthorizationId { get; set; } = string.Empty;
    public decimal HeldAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
}
