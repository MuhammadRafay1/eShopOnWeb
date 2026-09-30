using Microsoft.eShopWeb.ApplicationCore.Models.Payments;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class PayOrderRequest : BaseRequest
{
    public int OrderId { get; }
    public CardDetails? Card { get; }
    public int? PaymentMethodId { get; }

    public PayOrderRequest(int orderId, CardDetails? card, int? paymentMethodId)
    {
        OrderId = orderId;
        Card = card;
        PaymentMethodId = paymentMethodId;
    }
}

/// <summary>The JSON body shape for POST /api/orders/{orderId}/pay — exactly one of Card or PaymentMethodId.</summary>
public class PayOrderBody
{
    public CardDetails? Card { get; init; }
    public int? PaymentMethodId { get; init; }
}
