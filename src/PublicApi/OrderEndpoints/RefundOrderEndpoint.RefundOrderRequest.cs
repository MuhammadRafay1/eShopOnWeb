namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class RefundOrderBody
{
    /// <summary>Omit for a full refund of whatever remains capturable; set for a partial refund.</summary>
    public decimal? Amount { get; set; }
    public string? Reason { get; set; }
}

public class RefundOrderRequest : BaseRequest
{
    public int OrderId { get; }
    public string BuyerId { get; }
    public string IdempotencyKey { get; }
    public RefundOrderBody Body { get; }

    public RefundOrderRequest(int orderId, string buyerId, string idempotencyKey, RefundOrderBody body)
    {
        OrderId = orderId;
        BuyerId = buyerId;
        IdempotencyKey = idempotencyKey;
        Body = body;
    }
}
