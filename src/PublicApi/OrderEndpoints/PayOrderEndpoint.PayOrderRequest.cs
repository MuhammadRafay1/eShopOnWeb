namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class PayOrderRequest : BaseRequest
{
    /// <summary>Set by the endpoint from the route - not part of the request body.</summary>
    public int OrderId { get; set; }

    /// <summary>Set by the endpoint from the caller's JWT identity.</summary>
    public string BuyerId { get; set; } = default!;

    /// <summary>Exactly one of Card / SavedPaymentMethodId must be set.</summary>
    public CardRequestDto? Card { get; set; }
    public int? SavedPaymentMethodId { get; set; }
}
