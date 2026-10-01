using System;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class FulfilOrderResponse : BaseResponse
{
    public FulfilOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public FulfilOrderResponse()
    {
    }

    public int OrderId { get; set; }
    public OrderPaymentDto Payment { get; set; } = null!;
}
