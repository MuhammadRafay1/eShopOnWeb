namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class DeletePaymentMethodRequest : BaseRequest
{
    public int PaymentMethodId { get; }

    public DeletePaymentMethodRequest(int paymentMethodId)
    {
        PaymentMethodId = paymentMethodId;
    }
}
