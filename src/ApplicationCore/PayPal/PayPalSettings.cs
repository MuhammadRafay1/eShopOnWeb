namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

/// <summary>Bound from the "PayPal" configuration section. No value here is ever hard-coded - see Program.cs.</summary>
public class PayPalSettings
{
    public const string CONFIG_NAME = "PayPal";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>"sandbox" or "live"/"production". Used only when <see cref="BaseUrl"/> is not set.</summary>
    public string? Environment { get; set; }

    /// <summary>ISO 4217 currency code, e.g. "USD".</summary>
    public string? Currency { get; set; }

    /// <summary>Optional verbatim override for the API base address (including the OAuth token call).</summary>
    public string? BaseUrl { get; set; }
}
