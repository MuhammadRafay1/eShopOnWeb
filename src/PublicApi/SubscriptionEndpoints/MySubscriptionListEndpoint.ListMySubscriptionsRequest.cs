namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class ListMySubscriptionsRequest : BaseRequest
{
    public ListMySubscriptionsRequest(string buyerId)
    {
        BuyerId = buyerId;
    }

    /// <summary>The caller's identity, taken from the JWT — never from the request.</summary>
    public string BuyerId { get; }
}
