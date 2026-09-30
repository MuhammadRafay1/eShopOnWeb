using Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class PayOrderRequest : BaseRequest
{
    /// <summary>Set by the endpoint from the route value, not bound from the JSON body.</summary>
    public int OrderId { get; set; }

    public CardRequestDto? Card { get; set; }
    public int? PaymentMethodId { get; set; }
    public bool SaveCard { get; set; }
}
