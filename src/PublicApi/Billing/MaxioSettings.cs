using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Billing;

/// <summary>
/// Settings bound from the <c>Maxio:</c> configuration section. Values come from user-secrets (development) or the
/// environment / a secret store (deployments) — never from a file in this repository.
/// </summary>
public sealed class MaxioSettings
{
    public const string SectionName = "Maxio";

    /// <summary><c>Maxio:ApiKey</c> — the site's API key (Basic-auth user name; the password is <c>x</c>).</summary>
    public string? ApiKey { get; set; }

    /// <summary><c>Maxio:Subdomain</c> — the site subdomain, used to derive the base address.</summary>
    public string? Subdomain { get; set; }

    /// <summary><c>Maxio:ProductFamilyHandle</c> — the product family whose products are offered as plans.</summary>
    public string? ProductFamilyHandle { get; set; }

    /// <summary><c>Maxio:BaseUrl</c> — optional; when set it is used verbatim instead of the subdomain-derived address.</summary>
    public string? BaseUrl { get; set; }

    /// <summary><c>Maxio:Environment</c> — <c>US</c> (default) or <c>EU</c> hosting.</summary>
    public string Environment { get; set; } = "US";

    /// <summary>
    /// <c>Maxio:RequestBudgetSeconds</c> — the most time one API request may spend waiting on Maxio, across every
    /// call it makes. Must stay under 30 s.
    /// </summary>
    public double RequestBudgetSeconds { get; set; } = 25;

    /// <summary><c>Maxio:AttemptTimeoutSeconds</c> — the bound on a single HTTP attempt.</summary>
    public double AttemptTimeoutSeconds { get; set; } = 8;

    /// <summary>
    /// <c>Maxio:PaymentCollectionMethod</c> — how subscriptions created here are collected. Defaults to
    /// <c>remittance</c> (invoiced; no card on file needed). Valid values per the SDK: <c>remittance</c>,
    /// <c>automatic</c>, <c>prepaid</c> (Relationship Invoicing sites) or <c>invoice</c>, <c>automatic</c> (legacy sites).
    /// </summary>
    public string PaymentCollectionMethod { get; set; } = "remittance";

    /// <summary><c>Maxio:PlanCacheSeconds</c> — how long the plan catalog is cached (0 disables caching).</summary>
    public double PlanCacheSeconds { get; set; } = 300;

    public TimeSpan RequestBudget => TimeSpan.FromSeconds(RequestBudgetSeconds);

    public TimeSpan AttemptTimeout => TimeSpan.FromSeconds(AttemptTimeoutSeconds);

    /// <summary>Part of the budget a write leaves unused so an ambiguous outcome can still be looked up.</summary>
    public TimeSpan WriteReconciliationReserve => TimeSpan.FromSeconds(Math.Min(5, RequestBudgetSeconds / 4));

    public bool IsEu => string.Equals(Environment, "EU", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps the provisioning environment variables (<c>MAXIO_API_KEY</c>, <c>MAXIO_SITE_SUBDOMAIN</c>,
    /// <c>MAXIO_DEFAULT_PRODUCT_FAMILY</c>, <c>MAXIO_ENVIRONMENT</c>) onto the <c>Maxio:</c> keys as the
    /// LOWEST-priority source, so user-secrets, appsettings and <c>Maxio__*</c> variables always win.
    /// </summary>
    public static void AddEnvironmentVariableFallback(IConfigurationBuilder configuration)
    {
        var map = new Dictionary<string, string>
        {
            ["MAXIO_API_KEY"] = $"{SectionName}:{nameof(ApiKey)}",
            ["MAXIO_SITE_SUBDOMAIN"] = $"{SectionName}:{nameof(Subdomain)}",
            ["MAXIO_DEFAULT_PRODUCT_FAMILY"] = $"{SectionName}:{nameof(ProductFamilyHandle)}",
            ["MAXIO_ENVIRONMENT"] = $"{SectionName}:{nameof(Environment)}",
        };

        var data = new Dictionary<string, string?>();
        foreach (var (variable, key) in map)
        {
            var value = System.Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                data[key] = value;
            }
        }

        if (data.Count > 0)
        {
            configuration.Sources.Insert(0, new MemoryConfigurationSource { InitialData = data });
        }
    }
}

/// <summary>
/// Refuses to start the host with an incomplete Maxio configuration. Messages name the key, never its value.
/// </summary>
public sealed class MaxioSettingsValidator : IValidateOptions<MaxioSettings>
{
    public ValidateOptionsResult Validate(string? name, MaxioSettings s)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(s.ApiKey))
            failures.Add("Maxio:ApiKey is not configured. Set it via user-secrets, the Maxio__ApiKey environment variable or MAXIO_API_KEY.");

        if (string.IsNullOrWhiteSpace(s.ProductFamilyHandle))
            failures.Add("Maxio:ProductFamilyHandle is not configured.");

        if (string.IsNullOrWhiteSpace(s.BaseUrl))
        {
            if (string.IsNullOrWhiteSpace(s.Subdomain))
                failures.Add("Maxio:Subdomain is not configured (required unless Maxio:BaseUrl is set).");
        }
        else if (!Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            failures.Add("Maxio:BaseUrl must be an absolute http(s) URL.");
        }

        if (!string.Equals(s.Environment, "US", StringComparison.OrdinalIgnoreCase) && !s.IsEu)
            failures.Add("Maxio:Environment must be 'US' or 'EU'.");

        if (s.RequestBudgetSeconds is <= 0 or >= 30)
            failures.Add("Maxio:RequestBudgetSeconds must be greater than 0 and less than 30.");

        if (s.AttemptTimeoutSeconds <= 0 || s.AttemptTimeoutSeconds > s.RequestBudgetSeconds)
            failures.Add("Maxio:AttemptTimeoutSeconds must be greater than 0 and no larger than Maxio:RequestBudgetSeconds.");

        if (!MaxioAdvancedBilling.Models.Enums.CollectionMethod.TryGetKnownValue(s.PaymentCollectionMethod?.Trim().ToLowerInvariant(), out _))
            failures.Add("Maxio:PaymentCollectionMethod must be one of: remittance, automatic, prepaid, invoice.");

        if (s.PlanCacheSeconds < 0)
            failures.Add("Maxio:PlanCacheSeconds must not be negative.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
