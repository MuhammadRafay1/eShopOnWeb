namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CancelOrderRequest : BaseRequest
{
    public int OrderId { get; }

    public CancelOrderRequest(int orderId)
    {
        OrderId = orderId;
    }
}
