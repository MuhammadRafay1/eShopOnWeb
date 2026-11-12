namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest credentials and endpoint, bound from the <c>Upvest</c> configuration section.
/// Values are supplied by configuration (user-secrets / environment variables) and are never hard-coded.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? SigningKeyId { get; set; }
    public string? SigningKeyPath { get; set; }

    /// <summary>Optional: the signing key may be unencrypted, in which case this is blank.</summary>
    public string? SigningKeyPassphrase { get; set; }

    public string? BaseUrl { get; set; }

    /// <summary>
    /// Optional. When set, every token is requested with exactly this OAuth scope instead of each operation's
    /// own scope set. Leave unset in a fully-scoped (production) deployment so each operation requests the
    /// scopes it needs; set it where the client is granted a single scope (e.g. the sandbox client, which has
    /// <c>users:admin</c> but not <c>users:read</c>).
    /// </summary>
    public string? Scope { get; set; }
}
