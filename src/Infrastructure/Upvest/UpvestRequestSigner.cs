using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Computes Upvest HTTP message signatures (v6 scheme: SHA-256 <c>digest</c>, unquoted signature-base
/// component keys, ECDSA P-521/SHA-512 DER signature). Produces the <c>digest</c>, <c>signature</c>
/// and <c>signature-input</c> header values for a request.
/// </summary>
public sealed class UpvestRequestSigner
{
    private const string Accept = "application/json";
    private readonly UpvestSigningKeyProvider _keyProvider;
    private readonly UpvestSettings _settings;

    public UpvestRequestSigner(UpvestSigningKeyProvider keyProvider, IOptions<UpvestSettings> settings)
    {
        _keyProvider = keyProvider;
        _settings = settings.Value;
    }

    public SignedHeaders Sign(
        string method,
        string path,
        string? query,
        byte[]? body,
        string? contentType,
        string? bearerToken,
        string? idempotencyKey)
    {
        var hasBody = body is { Length: > 0 };
        var hasQuery = !string.IsNullOrEmpty(query);

        string? digest = null;
        if (hasBody)
        {
            using var sha = SHA256.Create();
            digest = "SHA-256=" + Convert.ToBase64String(sha.ComputeHash(body!));
        }

        // Component order must match between the signature base and the @signature-params list.
        var components = new List<(string Key, string Value)>
        {
            ("@method", method.ToUpperInvariant()),
            ("@path", path),
        };
        if (hasQuery) components.Add(("@query", query!));
        components.Add(("accept", Accept));
        if (bearerToken is not null) components.Add(("authorization", $"Bearer {bearerToken}"));
        if (hasBody)
        {
            components.Add(("content-length", body!.Length.ToString()));
            components.Add(("content-type", contentType ?? "application/json"));
            components.Add(("digest", digest!));
        }
        if (idempotencyKey is not null) components.Add(("idempotency-key", idempotencyKey));
        components.Add(("upvest-client-id", _settings.ClientId));

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = created + 60;
        // Hex (not base64) so the nonce never contains '=' / '+' / '/', which Upvest's
        // signature-input parser rejects inside the structured-field parameters.
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

        var quotedKeys = string.Join(" ", components.ConvertAll(c => $"\"{c.Key}\""));
        var signatureParams =
            $"({quotedKeys});keyid=\"{_settings.SigningKeyId}\";created={created};expires={expires};nonce=\"{nonce}\"";

        // v6: base component keys are NOT quoted; "key: value" lines joined by \n, then @signature-params.
        var sb = new StringBuilder();
        foreach (var (key, value) in components)
        {
            sb.Append(key).Append(": ").Append(value).Append('\n');
        }
        sb.Append("@signature-params: ").Append(signatureParams);

        var signature = _keyProvider.SignBase64(sb.ToString());

        return new SignedHeaders(
            Digest: digest,
            Signature: $"sig1=:{signature}:",
            SignatureInput: $"sig1={signatureParams}");
    }
}

public record SignedHeaders(string? Digest, string Signature, string SignatureInput);
