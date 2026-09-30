namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class FulfilOrderRequest : BaseRequest
{
    public FulfilOrderRequest(int orderId)
    {
        OrderId = orderId;
    }

    public int OrderId { get; set; }
}
