using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Fails host startup (via <c>ValidateOnStart</c>) when any required Upvest credential is missing or blank,
/// rather than discovering it as an auth failure on the first call in production. Each part of the two-part
/// OAuth credential and the three-part signing credential is checked separately — a blank part is not a
/// missing one. Messages name the config key only; a secret's value is never echoed.
/// </summary>
public sealed class UpvestSettingsValidator : IValidateOptions<UpvestSettings>
{
    public ValidateOptionsResult Validate(string? name, UpvestSettings options)
    {
        var failures = new List<string>();

        void Require(string? value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                failures.Add($"'{UpvestSettings.SectionName}:{key}' is not configured. Set it via user-secrets or an environment variable before starting the app.");
            }
        }

        Require(options.ClientId, nameof(UpvestSettings.ClientId));
        Require(options.ClientSecret, nameof(UpvestSettings.ClientSecret));
        Require(options.SigningKeyId, nameof(UpvestSettings.SigningKeyId));
        Require(options.SigningKeyPath, nameof(UpvestSettings.SigningKeyPath));
        Require(options.BaseUrl, nameof(UpvestSettings.BaseUrl));
        // SigningKeyPassphrase may legitimately be blank (unencrypted key) — not required.

        if (!string.IsNullOrWhiteSpace(options.SigningKeyPath) && !File.Exists(options.SigningKeyPath))
        {
            failures.Add($"The signing key file configured at '{UpvestSettings.SectionName}:{nameof(UpvestSettings.SigningKeyPath)}' does not exist or is not readable.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
