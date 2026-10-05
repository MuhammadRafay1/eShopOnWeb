using System;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class EnrolInvestingResponse : BaseResponse
{
    public EnrolInvestingResponse(Guid correlationId) : base(correlationId) { }
    public EnrolInvestingResponse() { }

    public Guid EnrolmentId { get; set; }

    /// <summary>"pending", "active" or "rejected".</summary>
    public string Status { get; set; } = string.Empty;
}
