namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class ListPaymentMethodsRequest : BaseRequest
{
    public string OwnerId { get; init; }

    public ListPaymentMethodsRequest(string ownerId)
    {
        OwnerId = ownerId;
    }
}
