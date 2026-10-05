using System;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class CreateSubscriptionResponse : BaseResponse
{
    public CreateSubscriptionResponse(Guid correlationId) : base(correlationId)
    {
    }

    public CreateSubscriptionResponse()
    {
    }

    /// <summary>
    /// "Subscribed" when a new subscription was created, "AlreadySubscribed"
    /// when an existing live subscription for the same plan was returned.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    public bool AlreadySubscribed { get; set; }

    public SubscriptionDto? Subscription { get; set; }
}