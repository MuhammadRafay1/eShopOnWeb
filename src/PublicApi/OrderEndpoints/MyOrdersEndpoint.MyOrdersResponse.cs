using System;
using Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class MyOrderDto
{
    public int OrderId { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public decimal Total { get; set; }
    public OrderPaymentDto? Payment { get; set; }
}

public class MyOrdersResponse : BaseResponse
{
    public MyOrdersResponse(Guid correlationId) : base(correlationId)
    {
    }

    public MyOrdersResponse()
    {
    }

    public MyOrderDto[] Orders { get; set; } = Array.Empty<MyOrderDto>();
}
