namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>Shared constants for the inbound Upvest webhook callback.</summary>
public static class UpvestWebhooks
{
    /// <summary>
    /// The path Upvest posts events to. This is the one route Upvest itself calls, so it is the
    /// only route that is not authenticated with the shopper's token.
    /// </summary>
    public const string Path = "/api/investing/upvest/webhook";

    /// <summary>Human-readable title for the registered webhook subscription.</summary>
    public const string SubscriptionTitle = "eShopOnWeb invest-your-change";
}
