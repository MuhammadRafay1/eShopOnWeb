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

    public string RefundId { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
    public OrderDto Order { get; set; } = default!;
}
