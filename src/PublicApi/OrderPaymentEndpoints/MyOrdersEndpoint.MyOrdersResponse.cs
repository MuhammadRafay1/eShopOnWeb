using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class OrderSummaryDto
{
    public int OrderId { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? AuthorizationId { get; set; }
    public string? CaptureId { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public List<RefundSummaryDto> Refunds { get; set; } = new();
    public List<OrderItemSummaryDto> Items { get; set; } = new();
}

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
