using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class ListSubscriptionPlansResponse : BaseResponse
{
    public ListSubscriptionPlansResponse(Guid correlationId) : base(correlationId)
    {
    }

    public ListSubscriptionPlansResponse()
    {
    }

    public string ProductFamilyHandle { get; set; } = string.Empty;

    public List<SubscriptionPlanDto> Plans { get; set; } = new List<SubscriptionPlanDto>();

    /// <summary>True when the listing stopped at its page cap: more plans may exist than are listed.</summary>
    public bool Truncated { get; set; }
}
