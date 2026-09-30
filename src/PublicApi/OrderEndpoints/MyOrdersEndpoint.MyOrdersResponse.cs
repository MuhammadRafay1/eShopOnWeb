using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class MyOrdersResponse : BaseResponse
{
    public MyOrdersResponse(Guid correlationId) : base(correlationId)
    {
    }

    public MyOrdersResponse()
    {
    }

    public List<OrderSummaryDto> Orders { get; set; } = new();
}

public class OrderSummaryDto
{
    public int OrderId { get; set; }

    public string Status { get; set; } = "";
    public decimal Total { get; set; }

    public DateTimeOffset OrderDate { get; set; }

    public PaymentDto? Payment { get; set; }
}
