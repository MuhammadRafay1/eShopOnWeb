using System;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderResponse : BaseResponse
{
    public CreateOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public CreateOrderResponse()
    {
    }

    public int OrderId { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public string PaymentStatus { get; set; } = "AwaitingPayment";
}
