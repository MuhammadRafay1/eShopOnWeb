namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class RefundOrderRequest : BaseRequest
{
    /// <summary>Set by the endpoint from the route.</summary>
    public int OrderId { get; set; }

    /// <summary>Set by the endpoint from the caller's JWT identity.</summary>
    public string BuyerId { get; set; } = default!;

    /// <summary>Omitted = refund whatever remains of the captured amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Required. Repeating a request with the same key returns the original refund rather than refunding again.</summary>
    public string IdempotencyKey { get; set; } = default!;
}
