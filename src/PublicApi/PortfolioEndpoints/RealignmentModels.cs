using System;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public class RealignmentResponse : BaseResponse
{
    public RealignmentResponse(Guid correlationId) : base(correlationId) { }
    public RealignmentResponse() { }

    public int RealignmentId { get; set; }
    /// <summary>pending | completed | failed.</summary>
    public string Status { get; set; } = string.Empty;
    public string? Detail { get; set; }
}
