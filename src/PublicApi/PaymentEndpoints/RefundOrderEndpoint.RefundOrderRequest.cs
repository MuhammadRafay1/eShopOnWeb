namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>
/// Refunds in full when <see cref="Amount"/> is omitted, or partially for the given amount.
/// <see cref="IdempotencyKey"/> is caller-supplied: repeating the same key returns the original refund
/// rather than refunding twice; two distinct keys are two legitimate partial refunds.
/// </summary>
public class RefundOrderRequest : BaseRequest
{
    public int OrderId { get; set; }
    public decimal? Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
