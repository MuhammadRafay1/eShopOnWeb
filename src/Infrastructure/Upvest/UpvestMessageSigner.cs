using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs outgoing HTTP requests to Upvest using HTTP Message Signatures v15
/// (RFC 9421), with an ECDSA P-521 / SHA-512 key, producing the <c>content-digest</c>,
/// <c>signature-input</c> and <c>signature</c> headers Upvest requires.
/// </summary>
public sealed class UpvestMessageSigner : IDisposable
{
    private const string SignatureVersion = "15";
    private static readonly char[] NonceAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private readonly UpvestSettings _settings;
    private readonly ECDsa _key;
    private readonly object _signLock = new();

    public UpvestMessageSigner(UpvestSettings settings)
    {
        _settings = settings;
        _key = ECDsa.Create();
        _key.ImportFromEncryptedPem(File.ReadAllText(settings.SigningKeyPath), settings.SigningKeyPassphrase);
    }

    /// <summary>Adds all authentication-signature headers to <paramref name="request"/> in place.</summary>
    public async Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Covered components must appear in the signature base in the same order as declared.
        byte[]? body = null;
        string? contentType = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = request.Content.Headers.ContentType?.ToString();
            var digest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            request.Content.Headers.Remove("content-digest");
            request.Content.Headers.TryAddWithoutValidation("content-digest", digest);
        }

        // Ensure a deterministic Accept header we can cover.
        request.Headers.Remove("accept");
        request.Headers.TryAddWithoutValidation("accept", "application/json");

        request.Headers.Remove("upvest-client-id");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);
        request.Headers.Remove("upvest-signature-version");
        request.Headers.TryAddWithoutValidation("upvest-signature-version", SignatureVersion);

        string method = request.Method.Method.ToUpperInvariant();
        string path = request.RequestUri!.AbsolutePath;
        string query = request.RequestUri.Query; // includes leading '?' when present
        string? authorization = request.Headers.Authorization?.ToString();
        string? idempotencyKey = request.Headers.TryGetValues("idempotency-key", out var ik) ? ik.FirstOrDefault() : null;
        string contentDigest = body is not null
            ? request.Content!.Headers.GetValues("content-digest").First()
            : string.Empty;

        var components = new List<(string Name, string Value)>
        {
            ("@method", method),
            ("@path", path),
        };
        if (!string.IsNullOrEmpty(query)) components.Add(("@query", query));
        components.Add(("accept", "application/json"));
        if (!string.IsNullOrEmpty(authorization)) components.Add(("authorization", authorization));
        if (body is not null)
        {
            components.Add(("content-length", body.Length.ToString()));
            components.Add(("content-type", contentType ?? "application/json"));
            components.Add(("content-digest", contentDigest));
        }
        if (!string.IsNullOrEmpty(idempotencyKey)) components.Add(("idempotency-key", idempotencyKey!));
        components.Add(("upvest-client-id", _settings.ClientId));

        long created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long expires = created + 30;
        string nonce = RandomNonce(16);
        string componentList = "(" + string.Join(" ", components.Select(c => $"\"{c.Name}\"")) + ")";
        string signatureParams = $"{componentList};keyid=\"{_settings.SigningKeyId}\";created={created};expires={expires};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var (name, value) in components)
            baseBuilder.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        var signatureBase = Encoding.UTF8.GetBytes(baseBuilder.ToString());
        byte[] signature;
        lock (_signLock)
        {
            signature = _key.SignData(signatureBase, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }

        request.Headers.Remove("signature-input");
        request.Headers.TryAddWithoutValidation("signature-input", $"sig1={signatureParams}");
        request.Headers.Remove("signature");
        request.Headers.TryAddWithoutValidation("signature", $"sig1=:{Convert.ToBase64String(signature)}:");
    }

    private static string RandomNonce(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = NonceAlphabet[bytes[i] % NonceAlphabet.Length];
        return new string(chars);
    }

    public void Dispose() => _key.Dispose();
}
