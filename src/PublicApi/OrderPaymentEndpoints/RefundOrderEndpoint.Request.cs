namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class RefundOrderRequest : BaseRequest
{
    public int OrderId { get; }
    public string IdempotencyKey { get; }
    public decimal? Amount { get; }

    public RefundOrderRequest(int orderId, string idempotencyKey, decimal? amount)
    {
        OrderId = orderId;
        IdempotencyKey = idempotencyKey;
        Amount = amount;
    }
}

/// <summary>The JSON body shape for POST /api/orders/{orderId}/refunds.</summary>
public class RefundOrderBody
{
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>Omit for a full refund of the remaining captured balance.</summary>
    public decimal? Amount { get; init; }
}
