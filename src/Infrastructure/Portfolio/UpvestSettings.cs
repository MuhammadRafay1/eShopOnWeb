namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. Secret values
/// (<see cref="ClientSecret"/>, <see cref="SigningKeyPassphrase"/>) are never logged or returned.
/// </summary>
public sealed class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Whole-call budget (across retries) the caller is bounded by, in seconds.</summary>
    public int CallBudgetSeconds { get; set; } = 30;

    /// <summary>Per-attempt timeout, in seconds.</summary>
    public int AttemptTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Set to true only when the signing key is unencrypted and the passphrase is intentionally blank.
    /// Otherwise a blank passphrase is treated as a misconfiguration at startup.
    /// </summary>
    public bool SigningKeyIsUnencrypted { get; set; } = false;
}
