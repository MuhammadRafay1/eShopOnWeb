namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>Bound from the "PayPal" configuration section. No default carries a real secret.</summary>
public class PayPalSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Environment { get; set; } = "sandbox";
    public string Currency { get; set; } = "USD";

    /// <summary>Optional override — when set, used verbatim as the API base address for every PayPal call, including the OAuth token request.</summary>
    public string? BaseUrl { get; set; }
}
