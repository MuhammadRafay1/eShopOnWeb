using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs outgoing Upvest requests with an HTTP Message Signature. The registered key is an EC P-521 private
/// key signed over SHA-512; Upvest requires the signature as an ASN.1 DER sequence (not the IEEE-P1363
/// concatenation). The covered component set follows Upvest's "v15" scheme: it always covers
/// <c>@method</c>, <c>@path</c> and <c>upvest-client-id</c>, adds <c>authorization</c> for authenticated
/// calls, <c>@query</c> when there is a query, the body's <c>content-length</c>/<c>content-type</c>/
/// <c>content-digest</c> when there is a body, and <c>idempotency-key</c> when that header is present.
/// </summary>
public sealed class UpvestRequestSigner : IDisposable
{
    private const string Algorithm = "ecdsa-p521-sha512";
    private const string SignatureLabel = "sig1";

    private readonly ECDsa _key;
    private readonly string _keyId;
    private readonly object _signLock = new();

    public UpvestRequestSigner(IOptions<UpvestSettings> options)
    {
        var settings = options.Value;
        _keyId = settings.SigningKeyId;

        string pem;
        try
        {
            pem = File.ReadAllText(settings.SigningKeyPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Upvest:SigningKeyPath could not be read ('{settings.SigningKeyPath}').", ex);
        }

        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromEncryptedPem(pem, settings.SigningKeyPassphrase);
        }
        catch (Exception ex)
        {
            ecdsa.Dispose();
            // Do not echo the passphrase or key material in the message.
            throw new InvalidOperationException(
                "Upvest signing key could not be loaded. Check Upvest:SigningKeyPath and Upvest:SigningKeyPassphrase.",
                ex);
        }

        _key = ecdsa;
    }

    /// <summary>
    /// Adds the <c>content-digest</c> (when there is a body), <c>signature-input</c> and <c>signature</c>
    /// headers to <paramref name="request"/>, computed over the final request. The call site supplies only
    /// placeholders for signature material; this replaces them.
    /// </summary>
    public void Sign(HttpRequestMessage request, byte[] body)
    {
        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("Cannot sign a request without a URI.");
        }

        var uri = request.RequestUri;
        var path = uri.AbsolutePath;
        var hasBody = body.Length > 0;
        var hasQuery = !string.IsNullOrEmpty(uri.Query) && uri.Query != "?";
        var isToken = path.EndsWith("/auth/token", StringComparison.Ordinal);

        string? contentDigest = null;
        if (hasBody && request.Content is not null)
        {
            contentDigest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            request.Content.Headers.Remove("content-digest");
            request.Content.Headers.TryAddWithoutValidation("content-digest", contentDigest);
        }

        // Covered components, in the order they will appear in signature-input and the signature base.
        var components = new List<string> { "@method", "@path", "upvest-client-id" };
        if (!isToken)
        {
            components.Add("authorization");
        }

        if (hasQuery)
        {
            components.Add("@query");
        }

        if (hasBody)
        {
            components.Add("content-length");
            components.Add("content-type");
            components.Add("content-digest");
        }

        if (request.Headers.Contains("idempotency-key"))
        {
            components.Add("idempotency-key");
        }

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signatureParams = BuildSignatureParams(components, created);

        var baseString = BuildSignatureBase(request, uri, path, body, contentDigest, components, signatureParams);

        byte[] signature;
        lock (_signLock)
        {
            signature = _key.SignData(
                Encoding.UTF8.GetBytes(baseString),
                HashAlgorithmName.SHA512,
                DSASignatureFormat.Rfc3279DerSequence);
        }

        request.Headers.Remove("signature-input");
        request.Headers.TryAddWithoutValidation("signature-input", $"{SignatureLabel}={signatureParams}");
        request.Headers.Remove("signature");
        request.Headers.TryAddWithoutValidation(
            "signature", $"{SignatureLabel}=:{Convert.ToBase64String(signature)}:");
    }

    private string BuildSignatureParams(IReadOnlyList<string> components, long created)
    {
        var sb = new StringBuilder();
        sb.Append('(');
        for (var i = 0; i < components.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append('"').Append(components[i]).Append('"');
        }

        sb.Append(')');
        sb.Append(";created=").Append(created.ToString(CultureInfo.InvariantCulture));
        sb.Append(";keyid=\"").Append(_keyId).Append('"');
        sb.Append(";alg=\"").Append(Algorithm).Append('"');
        return sb.ToString();
    }

    private static string BuildSignatureBase(
        HttpRequestMessage request,
        Uri uri,
        string path,
        byte[] body,
        string? contentDigest,
        IReadOnlyList<string> components,
        string signatureParams)
    {
        var sb = new StringBuilder();
        foreach (var component in components)
        {
            var value = component switch
            {
                "@method" => request.Method.Method,
                "@path" => path,
                // uri.Query already includes the leading '?', matching the verifier's "?" + query.
                "@query" => uri.Query,
                "content-length" => body.Length.ToString(CultureInfo.InvariantCulture),
                "content-type" => request.Content?.Headers.ContentType?.ToString() ?? string.Empty,
                "content-digest" => contentDigest ?? string.Empty,
                _ => GetHeaderValue(request, component)
            };

            sb.Append('"').Append(component).Append("\": ").Append(value).Append('\n');
        }

        sb.Append("\"@signature-params\": ").Append(signatureParams);
        return sb.ToString();
    }

    private static string GetHeaderValue(HttpRequestMessage request, string name)
    {
        if (request.Headers.TryGetValues(name, out var values))
        {
            return string.Join(", ", values);
        }

        if (request.Content is not null && request.Content.Headers.TryGetValues(name, out var contentValues))
        {
            return string.Join(", ", contentValues);
        }

        return string.Empty;
    }

    public void Dispose() => _key.Dispose();
}
