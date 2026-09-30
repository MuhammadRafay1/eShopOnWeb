namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class CancelOrderRequest : BaseRequest
{
    public CancelOrderRequest(int orderId)
    {
        OrderId = orderId;
    }

    public int OrderId { get; set; }
}
