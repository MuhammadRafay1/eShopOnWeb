namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class RefundOrderRequest : BaseRequest
{
    /// <summary>Set by the endpoint from the route value, not bound from the JSON body.</summary>
    public int OrderId { get; set; }

    /// <summary>Omitted/null refunds the remaining refundable balance in full.</summary>
    public decimal? Amount { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;
}
