namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class RefundOrderRequest : BaseRequest
{
    public int OrderId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
