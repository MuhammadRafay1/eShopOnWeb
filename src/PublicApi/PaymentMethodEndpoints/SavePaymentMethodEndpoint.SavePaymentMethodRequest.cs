using Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SavePaymentMethodRequest : BaseRequest
{
    public string BuyerId { get; set; } = string.Empty;
    public CardDto Card { get; set; } = new();
}
