namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class CancelOrderRequest : BaseRequest
{
    public int OrderId { get; }

    public CancelOrderRequest(int orderId)
    {
        OrderId = orderId;
    }
}
