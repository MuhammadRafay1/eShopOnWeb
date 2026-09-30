namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class RefundOrderRequest : BaseRequest
{
    /// <summary>Bound from the route.</summary>
    public int OrderId { get; set; }

    /// <summary>Set server-side from the caller's JWT after binding.</summary>
    public string BuyerId { get; set; } = string.Empty;

    /// <summary>Omit for a full refund of the remaining captured balance; specify for a partial refund.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Caller-supplied key: repeating a request under the same key returns the original refund instead of issuing a second one.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string? Note { get; set; }
}
