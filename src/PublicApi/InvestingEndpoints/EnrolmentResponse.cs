using System;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class EnrolmentResponse : BaseResponse
{
    public EnrolmentResponse(Guid correlationId) : base(correlationId) { }
    public EnrolmentResponse() { }

    public Guid EnrolmentId { get; set; }

    /// <summary><c>pending</c>, <c>active</c>, or <c>rejected</c>.</summary>
    public string Status { get; set; } = "pending";
}
