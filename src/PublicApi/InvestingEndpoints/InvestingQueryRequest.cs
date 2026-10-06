namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Carries the caller's identity for the investing read endpoints (no request body).</summary>
public class InvestingQueryRequest : BaseRequest
{
    public string BuyerId { get; set; } = string.Empty;
}
