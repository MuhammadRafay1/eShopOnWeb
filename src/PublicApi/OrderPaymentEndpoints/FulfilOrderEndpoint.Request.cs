namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class FulfilOrderRequest : BaseRequest
{
    public int OrderId { get; }

    public FulfilOrderRequest(int orderId)
    {
        OrderId = orderId;
    }
}
