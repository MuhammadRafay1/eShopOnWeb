using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Upvest credentials and endpoint, bound from the <c>Upvest:</c> configuration section. The two
/// secrets (<see cref="ClientSecret"/>, <see cref="SigningKeyPassphrase"/>) are never logged,
/// returned by an endpoint, or written into source — they are loaded from configuration only
/// (user-secrets / environment / secret store).
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    public string SigningKeyId { get; set; } = string.Empty;

    [Required]
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Optional: the key may legitimately have no passphrase. Empty is treated as "none".</summary>
    public string? SigningKeyPassphrase { get; set; }

    /// <summary>The base address used verbatim for every call to Upvest.</summary>
    [Required]
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// Fails host start-up (via <c>ValidateOnStart</c>) when a required credential is missing/blank or
/// the signing key file is absent — rather than surfacing it as an exception on the first call in
/// production. Never echoes a secret value.
/// </summary>
public class UpvestSettingsValidator : IValidateOptions<UpvestSettings>
{
    public ValidateOptionsResult Validate(string? name, UpvestSettings options)
    {
        var failures = new System.Collections.Generic.List<string>();

        void Require(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                failures.Add($"Upvest:{key} is not configured. Set it via environment variable, user-secrets, or your secret store before starting the app.");
            }
        }

        Require(options.ClientId, nameof(UpvestSettings.ClientId));
        Require(options.ClientSecret, nameof(UpvestSettings.ClientSecret));
        Require(options.SigningKeyId, nameof(UpvestSettings.SigningKeyId));
        Require(options.SigningKeyPath, nameof(UpvestSettings.SigningKeyPath));
        Require(options.BaseUrl, nameof(UpvestSettings.BaseUrl));

        if (!string.IsNullOrWhiteSpace(options.SigningKeyPath) && !File.Exists(options.SigningKeyPath))
        {
            failures.Add("Upvest:SigningKeyPath does not point at an existing file.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
