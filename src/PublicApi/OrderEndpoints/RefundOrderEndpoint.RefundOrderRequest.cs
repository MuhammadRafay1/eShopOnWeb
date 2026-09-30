using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class RefundOrderRequest : BaseRequest
{
    [JsonIgnore]
    public int OrderId { get; set; }

    /// <summary>Omit for a full refund of whatever remains captured.</summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Required. Repeating a request under the same key returns the original refund rather than
    /// refunding twice; distinct keys allow legitimate split refunds.
    /// </summary>
    public string IdempotencyKey { get; set; } = "";
}
