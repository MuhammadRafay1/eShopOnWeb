namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class MyOrdersRequest : BaseRequest
{
    public string BuyerId { get; init; }

    public MyOrdersRequest(string buyerId)
    {
        BuyerId = buyerId;
    }
}
