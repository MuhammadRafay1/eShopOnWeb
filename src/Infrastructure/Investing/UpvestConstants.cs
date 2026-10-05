namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>Fixed values for the Upvest integration that are not environment configuration.</summary>
internal static class UpvestConstants
{
    /// <summary>Named <see cref="System.Net.Http.HttpClient"/> that carries the authenticating handler.</summary>
    public const string HttpClientName = "Upvest";

    /// <summary>All OAuth scopes the integration needs, requested in a single token call.</summary>
    public const string Scopes =
        "users:admin users:read checks:admin accounts:admin accounts:read taxes:admin " +
        "webhooks:admin webhooks:read orders:admin orders:read virtual_cash_balances:admin";

    /// <summary>The HTTP message signature version this integration speaks.</summary>
    public const string SignatureVersion = "15";

    /// <summary>Upvest API version header value.</summary>
    public const string ApiVersion = "1";

    // Sandbox consent-document ids for the Take-Our-License onboarding flow (not secrets;
    // published fixed sandbox values from the Upvest onboarding workflow package).
    public const string TermsAndConditionsDocumentId = "d0b83880-3809-4eee-b0df-ca50db84c15a";
    public const string DataPrivacyDocumentId = "7ab0fc5c-8157-4acd-b02d-6ccda0e81dec";

    /// <summary>
    /// CONCAT nationalities for which Upvest generates the regulatory reporting identifier
    /// automatically — no explicit identifier call is required (or possible without a document).
    /// </summary>
    public static readonly string[] ConcatNationalities = { "AT", "DE", "FR", "HU", "IE", "LU" };
}
