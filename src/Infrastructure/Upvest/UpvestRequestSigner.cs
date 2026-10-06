using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs outgoing Upvest requests using v15 of Upvest's HTTP Message Signatures protocol
/// (ECDSA P-521 / SHA-512). It is the only place the signing key is used. The private key material
/// and its passphrase never leave this type and are never logged.
/// </summary>
public sealed class UpvestRequestSigner
{
    private const string NonceAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly UpvestSettings _settings;
    private readonly Lazy<ECDsa> _signingKey;

    public UpvestRequestSigner(IOptions<UpvestSettings> settings)
    {
        _settings = settings.Value;
        _signingKey = new Lazy<ECDsa>(LoadSigningKey, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private ECDsa LoadSigningKey()
    {
        var pem = File.ReadAllText(_settings.SigningKeyPath);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromEncryptedPem(pem, _settings.SigningKeyPassphrase);
        return ecdsa;
    }

    /// <summary>
    /// Adds the <c>content-digest</c>, <c>signature-input</c> and <c>signature</c> headers to the
    /// request. The caller must have already set all other signed headers (authorization,
    /// accept, upvest-client-id, idempotency-key, content-type) and the request body.
    /// </summary>
    public async Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is required for signing.");

        byte[]? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            // content-digest covers the exact body bytes (SHA-512, base64), per v15.
            var digest = Convert.ToBase64String(SHA512.HashData(body));
            request.Content.Headers.Remove("content-digest");
            request.Content.Headers.TryAddWithoutValidation("content-digest", $"sha-512=:{digest}:");
            request.Content.Headers.ContentLength = body.Length;
        }

        // Build the ordered list of covered components, including only those actually present.
        var components = new List<(string Key, string Value)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", uri.AbsolutePath),
        };

        if (!string.IsNullOrEmpty(uri.Query))
        {
            components.Add(("@query", uri.Query));
        }

        AddHeaderComponent(components, "accept", GetHeaderValue(request, "accept"));
        AddHeaderComponent(components, "authorization", GetHeaderValue(request, "authorization"));

        if (body is not null)
        {
            AddHeaderComponent(components, "content-length", body.Length.ToString());
            AddHeaderComponent(components, "content-type", GetHeaderValue(request, "content-type"));
            AddHeaderComponent(components, "content-digest", GetHeaderValue(request, "content-digest"));
        }

        AddHeaderComponent(components, "idempotency-key", GetHeaderValue(request, "idempotency-key"));
        AddHeaderComponent(components, "upvest-client-id", GetHeaderValue(request, "upvest-client-id"));

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = GenerateNonce();

        var componentList = string.Join(" ", components.Select(c => $"\"{c.Key}\""));
        var signatureParams = $"({componentList});keyid=\"{_settings.SigningKeyId}\";created={created};nonce=\"{nonce}\"";

        var baseLines = components.Select(c => $"\"{c.Key}\": {c.Value}").ToList();
        baseLines.Add($"\"@signature-params\": {signatureParams}");
        var signatureBase = string.Join("\n", baseLines);

        var signatureBytes = _signingKey.Value.SignData(
            Encoding.UTF8.GetBytes(signatureBase),
            HashAlgorithmName.SHA512,
            DSASignatureFormat.Rfc3279DerSequence);
        var signature = Convert.ToBase64String(signatureBytes);

        request.Headers.TryAddWithoutValidation("signature-input", $"sig1={signatureParams}");
        request.Headers.TryAddWithoutValidation("signature", $"sig1=:{signature}:");
    }

    private static void AddHeaderComponent(List<(string, string)> components, string key, string? value)
    {
        if (value is not null) components.Add((key, value));
    }

    private static string? GetHeaderValue(HttpRequestMessage request, string name)
    {
        if (request.Headers.TryGetValues(name, out var values))
        {
            return string.Join(", ", values);
        }
        if (request.Content is not null && request.Content.Headers.TryGetValues(name, out var contentValues))
        {
            return string.Join(", ", contentValues);
        }
        return null;
    }

    private static string GenerateNonce()
    {
        var chars = new char[16];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = NonceAlphabet[RandomNumberGenerator.GetInt32(NonceAlphabet.Length)];
        }
        return new string(chars);
    }
}
