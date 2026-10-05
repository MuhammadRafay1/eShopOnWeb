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

    public Guid EnrolmentId { get; set; }

    /// <summary>"pending" until Upvest has accepted the shopper, then "active" — or "rejected".</summary>
    public string Status { get; set; } = string.Empty;
}
