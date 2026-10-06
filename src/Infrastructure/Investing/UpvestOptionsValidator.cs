using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Startup validation for <see cref="UpvestOptions"/>: fails fast (host refuses to start) when a required
/// value is missing/blank or the signing key cannot be found or decrypted — rather than surfacing later as
/// a 401 on the first call. Error messages name the offending config key and never echo secret values.
/// </summary>
public sealed class UpvestOptionsValidator : IValidateOptions<UpvestOptions>
{
    public ValidateOptionsResult Validate(string? name, UpvestOptions options)
    {
        void Require(string value, string key, ref System.Collections.Generic.List<string>? errors)
        {
            if (string.IsNullOrWhiteSpace(value))
                (errors ??= new()).Add($"Upvest:{key} is not configured.");
        }

        System.Collections.Generic.List<string>? errors = null;
        Require(options.ClientId, nameof(UpvestOptions.ClientId), ref errors);
        Require(options.ClientSecret, nameof(UpvestOptions.ClientSecret), ref errors);
        Require(options.SigningKeyId, nameof(UpvestOptions.SigningKeyId), ref errors);
        Require(options.SigningKeyPath, nameof(UpvestOptions.SigningKeyPath), ref errors);
        Require(options.SigningKeyPassphrase, nameof(UpvestOptions.SigningKeyPassphrase), ref errors);
        Require(options.BaseUrl, nameof(UpvestOptions.BaseUrl), ref errors);
        Require(options.InstrumentId, nameof(UpvestOptions.InstrumentId), ref errors);
        Require(options.CallbackBaseUrl, nameof(UpvestOptions.CallbackBaseUrl), ref errors);

        if (errors is not null)
            return ValidateOptionsResult.Fail(errors);

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
            return ValidateOptionsResult.Fail("Upvest:BaseUrl is not a valid absolute URL.");

        if (!File.Exists(options.SigningKeyPath))
            return ValidateOptionsResult.Fail("Upvest:SigningKeyPath does not point to an existing file.");

        try
        {
            using var probe = ECDsa.Create();
            probe.ImportFromEncryptedPem(File.ReadAllText(options.SigningKeyPath), options.SigningKeyPassphrase);
        }
        catch (Exception)
        {
            // Do not echo the passphrase or key material.
            return ValidateOptionsResult.Fail(
                "Upvest signing key at Upvest:SigningKeyPath could not be decrypted with Upvest:SigningKeyPassphrase.");
        }

        return ValidateOptionsResult.Success;
    }
}
