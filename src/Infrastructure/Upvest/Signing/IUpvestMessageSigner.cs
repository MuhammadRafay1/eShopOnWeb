using System.Collections.Generic;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Signing;

/// <summary>
/// Builds the <c>signature</c> and <c>signature-input</c> headers for an Upvest request,
/// per Upvest HTTP message signatures v15 (ECDSA P-521 / SHA-512, DER-encoded signature).
/// </summary>
public interface IUpvestMessageSigner
{
    /// <summary>
    /// Computes the signature headers to add to a request.
    /// </summary>
    /// <param name="method">HTTP method (will be upper-cased).</param>
    /// <param name="path">Request path, e.g. <c>/users</c>.</param>
    /// <param name="query">Raw query string including the leading <c>?</c>, or null/empty when absent.</param>
    /// <param name="accept">The <c>accept</c> header value sent on the request.</param>
    /// <param name="authorization">The <c>authorization</c> header value, or null for the token request.</param>
    /// <param name="contentType">The body media type, or null when there is no body.</param>
    /// <param name="contentLength">The body length in bytes, or null when there is no body.</param>
    /// <param name="contentDigest">The <c>content-digest</c> value, or null when there is no body.</param>
    /// <param name="idempotencyKey">The idempotency key, or null when not sent.</param>
    /// <param name="upvestClientId">The <c>upvest-client-id</c> header value.</param>
    SignatureHeaders Sign(
        string method,
        string path,
        string? query,
        string accept,
        string? authorization,
        string? contentType,
        long? contentLength,
        string? contentDigest,
        string? idempotencyKey,
        string upvestClientId);
}

/// <summary>The two headers that carry an HTTP message signature.</summary>
public readonly record struct SignatureHeaders(string Signature, string SignatureInput);
