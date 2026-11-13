using System;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Binds the <c>Upvest:</c> configuration section. Values come from configuration only
/// (user-secrets / environment) — never hard-coded. The secret members (ClientSecret,
/// SigningKeyPassphrase) are never logged or returned by any endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? SigningKeyId { get; set; }
    public string? SigningKeyPath { get; set; }
    public string? SigningKeyPassphrase { get; set; }
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Consent-document ids the user-create call must reference (Upvest marks them optional in the schema but
    /// enforces them). Defaulted to the Upvest sandbox ids documented in the onboarding workflow reference;
    /// override per environment via <c>Upvest:TermsConsentDocumentId</c> / <c>Upvest:DataPrivacyConsentDocumentId</c>.
    /// These are public sandbox reference ids, not secrets.
    /// </summary>
    public Guid TermsConsentDocumentId { get; set; } = Guid.Parse("d0b83880-3809-4eee-b0df-ca50db84c15a");
    public Guid DataPrivacyConsentDocumentId { get; set; } = Guid.Parse("7ab0fc5c-8157-4acd-b02d-6ccda0e81dec");
}
