namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class MyOrdersRequest : BaseRequest
{
    public MyOrdersRequest(string buyerId)
    {
        BuyerId = buyerId;
    }

    public string BuyerId { get; set; }
}
