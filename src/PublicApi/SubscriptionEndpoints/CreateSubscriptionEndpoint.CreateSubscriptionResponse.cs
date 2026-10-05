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

    /// <summary>True when this call created the subscription; false when an existing one was returned.</summary>
    public bool Created { get; set; }

    public SubscriptionDto? Subscription { get; set; }
}
