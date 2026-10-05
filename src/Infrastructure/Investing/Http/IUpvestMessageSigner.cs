namespace Microsoft.eShopWeb.Infrastructure.Investing.Http;

/// <summary>
/// Produces the cryptographic signature used in Upvest HTTP message signatures (v15):
/// ECDSA over the P-521 curve with SHA-512, DER-encoded and Base64-encoded.
/// </summary>
public interface IUpvestMessageSigner
{
    /// <summary>The id of the signing key, used as the <c>keyid</c> signature parameter.</summary>
    string KeyId { get; }

    /// <summary>Sign the given signature base string and return the Base64-encoded signature.</summary>
    string SignToBase64(string signatureBase);
}
