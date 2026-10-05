using System;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Settings bound from the <c>Upvest:</c> configuration section. Secret values come from .NET user-secrets
/// (never the repository). Key names here match the mandated configuration keys exactly.
/// </summary>
public sealed class UpvestOptions
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string SigningKeyId { get; set; } = "";
    public string SigningKeyPath { get; set; } = "";
    public string SigningKeyPassphrase { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string InstrumentId { get; set; } = "";
    public string CallbackBaseUrl { get; set; } = "";

    /// <summary>Space-delimited OAuth scopes requested for the access token (not a secret).</summary>
    public string Scopes { get; set; } =
        "users:admin checks:admin taxes:admin accounts:admin orders:admin instruments:read payments:admin";

    /// <summary>The set-aside balance, in euros, at which a shopper's change is invested.</summary>
    public decimal InvestmentThresholdEuros { get; set; } = 10m;

    /// <summary>
    /// Throws if any required setting is missing or blank, naming the configuration key (never the value),
    /// so the host refuses to start rather than surfacing a 401 on the first call.
    /// </summary>
    public void Validate()
    {
        void Require(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"{SectionName}:{key} is not configured. Set it via user-secrets or the environment before starting the app.");
        }

        Require(ClientId, nameof(ClientId));
        Require(ClientSecret, nameof(ClientSecret));
        Require(SigningKeyId, nameof(SigningKeyId));
        Require(SigningKeyPath, nameof(SigningKeyPath));
        Require(SigningKeyPassphrase, nameof(SigningKeyPassphrase));
        Require(BaseUrl, nameof(BaseUrl));
        Require(InstrumentId, nameof(InstrumentId));
        Require(CallbackBaseUrl, nameof(CallbackBaseUrl));
    }
}
