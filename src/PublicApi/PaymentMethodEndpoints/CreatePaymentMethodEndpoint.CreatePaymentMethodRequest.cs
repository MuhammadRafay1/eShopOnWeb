namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class CreatePaymentMethodRequest : BaseRequest
{
    /// <summary>Set by the endpoint from the caller's JWT identity.</summary>
    public string BuyerId { get; set; } = default!;

    public CardRequestDto Card { get; set; } = default!;

    /// <summary>Optional shopper-friendly name, e.g. "Personal Visa".</summary>
    public string? Label { get; set; }
}
