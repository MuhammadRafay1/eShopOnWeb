using System;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class EnrolmentResponse : BaseResponse
{
    public EnrolmentResponse(Guid correlationId) : base(correlationId)
    {
    }

    public EnrolmentResponse()
    {
    }

    public int EnrolmentId { get; set; }

    /// <summary>"pending" until Upvest accepts the shopper, then "active", or "rejected".</summary>
    public string Status { get; set; } = string.Empty;
}
