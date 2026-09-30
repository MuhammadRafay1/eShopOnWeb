using System;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class CancelOrderResponse : BaseResponse
{
    public CancelOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
}
