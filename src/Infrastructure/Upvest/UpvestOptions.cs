using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Bound from the <c>Upvest:</c> configuration section. Every value comes from configuration
/// (environment / user-secrets) — none is hard-coded. <see cref="ClientSecret"/> and
/// <see cref="SigningKeyPassphrase"/> are secrets: never logged, never returned by an endpoint.
/// </summary>
public sealed class UpvestOptions
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string InstrumentId { get; set; } = string.Empty;
    public string CallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// OAuth scopes requested for the access token. Not a credential; a sensible default is used when
    /// unset. Covers the operations this integration makes (users/checks/taxes/accounts/orders/payments
    /// admin plus instrument reads).
    /// </summary>
    public string Scopes { get; set; } =
        "users:admin checks:admin taxes:admin accounts:admin orders:admin payments:admin instruments:read positions:read";

    public Guid ClientIdGuid => Guid.Parse(ClientId);
    public Guid SigningKeyIdGuid => Guid.Parse(SigningKeyId);

    /// <summary>
    /// Returns the configuration keys that are missing/blank or malformed, so the host can refuse to start
    /// with a message that names the key — never echoing a value. A blank part is treated as missing.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        void Require(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add($"{SectionName}:{key} is not configured.");
        }

        Require(ClientId, nameof(ClientId));
        Require(ClientSecret, nameof(ClientSecret));
        Require(SigningKeyId, nameof(SigningKeyId));
        Require(SigningKeyPath, nameof(SigningKeyPath));
        Require(SigningKeyPassphrase, nameof(SigningKeyPassphrase));
        Require(BaseUrl, nameof(BaseUrl));
        Require(InstrumentId, nameof(InstrumentId));
        Require(CallbackBaseUrl, nameof(CallbackBaseUrl));

        if (!string.IsNullOrWhiteSpace(ClientId) && !Guid.TryParse(ClientId, out _))
            errors.Add($"{SectionName}:{nameof(ClientId)} is not a valid GUID.");
        if (!string.IsNullOrWhiteSpace(SigningKeyId) && !Guid.TryParse(SigningKeyId, out _))
            errors.Add($"{SectionName}:{nameof(SigningKeyId)} is not a valid GUID.");
        if (!string.IsNullOrWhiteSpace(BaseUrl) && !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            errors.Add($"{SectionName}:{nameof(BaseUrl)} is not a valid absolute URL.");

        return errors;
    }
}
