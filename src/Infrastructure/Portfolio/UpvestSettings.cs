using System;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Bound from the <c>Upvest:</c> configuration section. The six credential/connection keys are required and
/// never hard-coded; the nested sections carry non-secret onboarding and payment fixtures the Upvest sandbox
/// needs that the shop's sign-up form does not supply.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    // --- The six required keys (values come from configuration/user-secrets/env, never source) ---
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? SigningKeyId { get; set; }
    public string? SigningKeyPath { get; set; }

    /// <summary>Optional: only needed when the signing key file is encrypted.</summary>
    public string? SigningKeyPassphrase { get; set; }

    /// <summary>The base address used verbatim for every Upvest call.</summary>
    public string? BaseUrl { get; set; }

    public UpvestOnboardingSettings Onboarding { get; set; } = new();

    public UpvestPaymentSettings Payments { get; set; } = new();
}

/// <summary>Onboarding prerequisites the sandbox enforces but the shop's form does not carry.</summary>
public class UpvestOnboardingSettings
{
    /// <summary>Account-group type the investor's portfolio account is created under (e.g. PERSONAL).</summary>
    public string AccountGroupType { get; set; } = "PERSONAL";

    /// <summary>KYC provider name recorded on the identity check.</summary>
    public string KycProvider { get; set; } = "eShopOnWeb";

    /// <summary>KYC method recorded on the identity check (see Upvest DocumentType/Method enums).</summary>
    public string KycMethod { get; set; } = "ID_COLLECTION";

    /// <summary>A link to the KYC evidence file the shop holds for the verified shopper.</summary>
    public string KycEvidenceUrl { get; set; } = "https://files.example.com/kyc-evidence";

    /// <summary>Whether the shopper is subject to US (FATCA) taxation. Defaults to false.</summary>
    public bool Fatca { get; set; }

    /// <summary>Sandbox consent-document id for terms &amp; conditions; sent only when configured.</summary>
    public Guid? TermsConsentDocumentId { get; set; }

    /// <summary>Sandbox consent-document id for the data-privacy agreement; sent only when configured.</summary>
    public Guid? DataPrivacyConsentDocumentId { get; set; }
}

/// <summary>Pay-in fixtures for the sandbox credit mechanism.</summary>
public class UpvestPaymentSettings
{
    /// <summary>The sandbox counterparty IBAN used to simulate an incoming SEPA credit.</summary>
    public string SandboxCreditIban { get; set; } = "FR7617789000011051089906447";

    /// <summary>The currency every pay-in and order uses.</summary>
    public string Currency { get; set; } = "EUR";
}
