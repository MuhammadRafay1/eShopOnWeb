using System;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Shared by POST and GET of the enrolment resource.</summary>
public class EnrolmentResponse : BaseResponse
{
    public EnrolmentResponse(Guid correlationId) : base(correlationId) { }

    public EnrolmentResponse() { }

    public Guid EnrolmentId { get; set; }

    /// <summary>"pending" until Upvest accepts the shopper, then "active"; or "rejected".</summary>
    public string Status { get; set; } = "pending";
}
