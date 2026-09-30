namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class PayOrderRequest : BaseRequest
{
    public int OrderId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public CardDto? Card { get; set; }
    public int? SavedPaymentMethodId { get; set; }
}
