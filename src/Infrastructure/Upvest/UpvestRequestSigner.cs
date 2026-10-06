using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public interface IUpvestRequestSigner
{
    /// <summary>
    /// Applies Upvest's mandatory headers and the v15 HTTP message signature to the request.
    /// Pass the bearer token for API calls; pass null for the access-token request itself.
    /// </summary>
    Task SignAsync(HttpRequestMessage request, string? bearerToken, CancellationToken cancellationToken);
}

/// <summary>
/// Implements Upvest's HTTP Message Signatures, protocol version 15
/// (ECDSA P-521 / SHA-512, DER-encoded signature, standard Base64), as documented in the
/// Upvest Investment API. This is the single place credentials and signatures are applied.
/// </summary>
public sealed class UpvestRequestSigner : IUpvestRequestSigner, IDisposable
{
    private readonly UpvestSettings _settings;
    private readonly ECDsa _signingKey;
    private readonly object _signLock = new();

    public UpvestRequestSigner(IOptions<UpvestSettings> settings)
    {
        _settings = settings.Value;

        if (string.IsNullOrWhiteSpace(_settings.SigningKeyPath) || !File.Exists(_settings.SigningKeyPath))
        {
            throw new InvalidOperationException(
                $"Upvest signing key not found at the configured path (Upvest:SigningKeyPath).");
        }

        var pem = File.ReadAllText(_settings.SigningKeyPath);
        _signingKey = ECDsa.Create();
        _signingKey.ImportFromEncryptedPem(pem, _settings.SigningKeyPassphrase);
    }

    public async Task SignAsync(HttpRequestMessage request, string? bearerToken, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null || !request.RequestUri.IsAbsoluteUri)
        {
            throw new InvalidOperationException("The request URI must be absolute before signing.");
        }

        // Mandatory headers.
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        SetHeader(request, "upvest-client-id", _settings.ClientId);
        SetHeader(request, "upvest-signature-version", "15");
        if (!string.IsNullOrEmpty(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        // Body-dependent components.
        byte[]? body = null;
        string? mediaType = null;
        string? contentDigest = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            mediaType = request.Content.Headers.ContentType?.MediaType ?? "application/json";
            // Normalise the content-type so the value we sign is exactly the value we send.
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            contentDigest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            SetHeader(request, "content-digest", contentDigest);
        }

        // Build the ordered component set. Order here must match the signature base and the header.
        var components = new List<(string Name, string Value)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", request.RequestUri.AbsolutePath)
        };
        if (!string.IsNullOrEmpty(request.RequestUri.Query))
        {
            components.Add(("@query", request.RequestUri.Query));
        }
        components.Add(("accept", "application/json"));
        if (!string.IsNullOrEmpty(bearerToken))
        {
            components.Add(("authorization", $"Bearer {bearerToken}"));
        }
        if (body is not null)
        {
            components.Add(("content-length", body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            components.Add(("content-type", mediaType!));
            components.Add(("content-digest", contentDigest!));
        }
        if (TryGetHeader(request, "idempotency-key", out var idemKey))
        {
            components.Add(("idempotency-key", idemKey));
        }
        components.Add(("upvest-client-id", _settings.ClientId));

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = created + 30;
        var nonce = GenerateNonce();

        var componentKeys = string.Join(" ", components.Select(c => $"\"{c.Name}\""));
        var signatureParams =
            $"({componentKeys});keyid=\"{_settings.SigningKeyId}\";created={created};expires={expires};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var (name, value) in components)
        {
            baseBuilder.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        }
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        var signatureBytes = SignData(Encoding.UTF8.GetBytes(baseBuilder.ToString()));
        var signature = Convert.ToBase64String(signatureBytes);

        SetHeader(request, "signature-input", $"sig1={signatureParams}");
        SetHeader(request, "signature", $"sig1=:{signature}:");
    }

    private byte[] SignData(byte[] data)
    {
        lock (_signLock)
        {
            return _signingKey.SignData(data, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }
    }

    private static string GenerateNonce()
    {
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
    }

    private static void SetHeader(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    private static bool TryGetHeader(HttpRequestMessage request, string name, out string value)
    {
        if (request.Headers.TryGetValues(name, out var values))
        {
            value = values.FirstOrDefault() ?? string.Empty;
            return !string.IsNullOrEmpty(value);
        }
        value = string.Empty;
        return false;
    }

    public void Dispose() => _signingKey.Dispose();
}
