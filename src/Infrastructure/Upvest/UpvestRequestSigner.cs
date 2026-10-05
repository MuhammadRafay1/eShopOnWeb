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
/// Signs outbound requests to Upvest with HTTP Message Signatures v15 (ECDSA P-521 / SHA-512)
/// and attaches the <c>upvest-client-id</c>, version and digest headers. Used both by the
/// DelegatingHandler (for API calls) and the token provider (for the token request).
/// </summary>
public sealed class UpvestRequestSigner : IDisposable
{
    private readonly UpvestSettings _settings;
    private readonly object _signLock = new();
    private ECDsa? _signingKey;

    public UpvestRequestSigner(IOptions<UpvestSettings> settings)
    {
        _settings = settings.Value;
    }

    private ECDsa SigningKey
    {
        get
        {
            if (_signingKey is not null) return _signingKey;
            lock (_signLock)
            {
                if (_signingKey is null)
                {
                    var ecdsa = ECDsa.Create();
                    ecdsa.ImportFromEncryptedPem(File.ReadAllText(_settings.SigningKeyPath), _settings.SigningKeyPassphrase);
                    _signingKey = ecdsa;
                }
            }
            return _signingKey;
        }
    }

    /// <summary>
    /// Adds every header Upvest requires to authenticate the request: client id, api/signature
    /// versions, content-digest (for bodies), and the signature + signature-input headers. The
    /// <c>authorization</c> header, when present, is covered by the signature; this method never
    /// adds it (the caller/handler sets the bearer token first).
    /// </summary>
    public async Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI must be set before signing.");
        var method = request.Method.Method.ToUpperInvariant();
        var path = uri.AbsolutePath;
        var query = uri.Query; // includes leading '?' when present, else empty

        byte[]? body = null;
        string? contentType = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        // Ensure an Accept header exists and is covered by the signature.
        if (!request.Headers.Contains("accept"))
            request.Headers.TryAddWithoutValidation("accept", "application/json");
        var accept = request.Headers.TryGetValues("accept", out var acceptValues)
            ? string.Join(", ", acceptValues) : "application/json";

        // Client id header, covered by the signature.
        request.Headers.Remove("upvest-client-id");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);

        var authorization = request.Headers.Authorization?.ToString();
        var idempotencyKey = request.Headers.TryGetValues("idempotency-key", out var idemValues)
            ? idemValues.FirstOrDefault() : null;

        string? contentDigest = null;
        if (body is { Length: > 0 })
        {
            contentDigest = ComputeContentDigest(body);
            request.Content!.Headers.Remove("content-digest");
            request.Content.Headers.TryAddWithoutValidation("content-digest", contentDigest);
        }

        // Build the signature component list in canonical order, including only present components.
        var components = new List<(string Name, string Value)>
        {
            ("@method", method),
            ("@path", path)
        };
        if (!string.IsNullOrEmpty(query)) components.Add(("@query", query));
        components.Add(("accept", accept));
        if (!string.IsNullOrEmpty(authorization)) components.Add(("authorization", authorization));
        if (body is { Length: > 0 })
        {
            components.Add(("content-length", body.Length.ToString()));
            components.Add(("content-type", contentType ?? "application/octet-stream"));
            components.Add(("content-digest", contentDigest!));
        }
        if (!string.IsNullOrEmpty(idempotencyKey)) components.Add(("idempotency-key", idempotencyKey!));
        components.Add(("upvest-client-id", _settings.ClientId));

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = created + 60;
        var nonce = GenerateNonce();

        var componentList = "(" + string.Join(" ", components.Select(c => $"\"{c.Name}\"")) + ")";
        var signatureParams = $"{componentList};keyid=\"{_settings.SigningKeyId}\";created={created};expires={expires};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var (name, value) in components)
            baseBuilder.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        var signatureValue = Sign(Encoding.UTF8.GetBytes(baseBuilder.ToString()));

        request.Headers.Remove("upvest-api-version");
        request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        request.Headers.Remove("upvest-signature-version");
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");
        request.Headers.Remove("signature-input");
        request.Headers.TryAddWithoutValidation("signature-input", $"sig1={signatureParams}");
        request.Headers.Remove("signature");
        request.Headers.TryAddWithoutValidation("signature", $"sig1=:{signatureValue}:");
    }

    private string Sign(byte[] signatureBase)
    {
        lock (_signLock)
        {
            var der = SigningKey.SignData(signatureBase, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
            return Convert.ToBase64String(der);
        }
    }

    private static string ComputeContentDigest(byte[] body)
    {
        using var sha = SHA512.Create();
        return "sha-512=:" + Convert.ToBase64String(sha.ComputeHash(body)) + ":";
    }

    private static string GenerateNonce()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer).Replace("+", "").Replace("/", "").Replace("=", "").PadRight(16, '0')[..16];
    }

    public void Dispose() => _signingKey?.Dispose();
}
