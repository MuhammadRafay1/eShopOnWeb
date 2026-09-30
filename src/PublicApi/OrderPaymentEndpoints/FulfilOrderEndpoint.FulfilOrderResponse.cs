using System;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class FulfilOrderResponse : BaseResponse
{
    public FulfilOrderResponse(Guid correlationId) : base(correlationId)
    {
    }

    public FulfilOrderResponse()
    {
    }

    public int OrderId { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? CaptureId { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
}
