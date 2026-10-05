using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Investing.Upvest;

/// <summary>
/// Signs outgoing requests to Upvest using v15 of the HTTP Message Signatures protocol:
/// ECDSA (P-521 / SHA-512) over a strictly-ordered signature base, plus a SHA-512
/// <c>content-digest</c> for requests with a body. See Upvest's HTTP signatures guide.
/// </summary>
public sealed class UpvestMessageSigner : IDisposable
{
    // The exact component order Upvest expects. A component is included only when present on the request.
    private static readonly string[] ComponentOrder =
    {
        "@method", "@path", "@query",
        "accept", "authorization",
        "content-length", "content-type", "content-digest",
        "idempotency-key", "upvest-client-id"
    };

    private readonly ECDsa _ecdsa;
    private readonly object _signLock = new();
    private readonly string _keyId;
    private readonly string _clientId;

    public UpvestMessageSigner(IOptions<UpvestOptions> options)
    {
        var o = options.Value;
        _keyId = o.SigningKeyId;
        _clientId = o.ClientId;

        if (string.IsNullOrWhiteSpace(o.SigningKeyPath) || !File.Exists(o.SigningKeyPath))
        {
            throw new InvalidOperationException(
                "Upvest signing key file was not found. Configure Upvest:SigningKeyPath.");
        }

        var pem = File.ReadAllText(o.SigningKeyPath);
        _ecdsa = ECDsa.Create();
        // The key is a passphrase-protected PKCS#8 (EC P-521) PEM.
        _ecdsa.ImportFromEncryptedPem(pem, o.SigningKeyPassphrase);
    }

    /// <summary>
    /// Adds the <c>content-digest</c>, <c>upvest-*</c>, <c>signature-input</c> and
    /// <c>signature</c> headers to the request. Call this last, after any Authorization,
    /// Accept and idempotency-key headers are already set.
    /// </summary>
    public async Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("Request URI must be set before signing.");
        }

        // Always-present signed/supporting headers.
        request.Headers.Remove("upvest-client-id");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);
        request.Headers.Remove("upvest-api-version");
        request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        request.Headers.Remove("upvest-signature-version");
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        byte[]? bodyBytes = null;
        if (request.Content is not null)
        {
            bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bodyBytes.Length > 0)
            {
                var digest = Convert.ToBase64String(SHA512.HashData(bodyBytes));
                request.Content.Headers.Remove("content-digest");
                request.Content.Headers.TryAddWithoutValidation("content-digest", $"sha-512=:{digest}:");
            }
            else
            {
                bodyBytes = null;
            }
        }

        var components = GatherComponents(request, bodyBytes);

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = created + 300;
        var nonce = GenerateNonce();

        var paramsList = string.Join(" ", components.ConvertAll(c => $"\"{c.Key}\""));
        var signatureParams =
            $"({paramsList});keyid=\"{_keyId}\";created={created};expires={expires};nonce=\"{nonce}\"";

        var sb = new StringBuilder();
        foreach (var (key, value) in components)
        {
            sb.Append('"').Append(key).Append("\": ").Append(value).Append('\n');
        }
        sb.Append("\"@signature-params\": ").Append(signatureParams);
        var baseString = sb.ToString();

        byte[] signatureBytes;
        var data = Encoding.UTF8.GetBytes(baseString);
        lock (_signLock)
        {
            signatureBytes = _ecdsa.SignData(data, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }
        var signature = Convert.ToBase64String(signatureBytes);

        request.Headers.Remove("signature-input");
        request.Headers.TryAddWithoutValidation("signature-input", $"sig1={signatureParams}");
        request.Headers.Remove("signature");
        request.Headers.TryAddWithoutValidation("signature", $"sig1=:{signature}:");
    }

    private List<KeyValuePair<string, string>> GatherComponents(HttpRequestMessage request, byte[]? bodyBytes)
    {
        var uri = request.RequestUri!;
        var query = uri.Query; // includes leading '?' when present
        if (string.IsNullOrEmpty(query))
        {
            query = "?";
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["@method"] = request.Method.Method.ToUpperInvariant(),
            ["@path"] = uri.AbsolutePath,
            ["@query"] = query,
            ["upvest-client-id"] = _clientId
        };

        if (request.Headers.Accept.Count > 0)
        {
            values["accept"] = request.Headers.Accept.ToString();
        }

        if (request.Headers.Authorization is not null)
        {
            values["authorization"] = request.Headers.Authorization.ToString();
        }

        if (request.Headers.TryGetValues("idempotency-key", out var idem))
        {
            values["idempotency-key"] = string.Join(", ", idem);
        }

        if (bodyBytes is not null && request.Content is not null)
        {
            values["content-length"] = bodyBytes.Length.ToString(CultureInfo.InvariantCulture);
            if (request.Content.Headers.ContentType is not null)
            {
                values["content-type"] = request.Content.Headers.ContentType.ToString();
            }
            if (request.Content.Headers.TryGetValues("content-digest", out var cd))
            {
                values["content-digest"] = string.Join(", ", cd);
            }
        }

        var ordered = new List<KeyValuePair<string, string>>();
        foreach (var name in ComponentOrder)
        {
            if (values.TryGetValue(name, out var v))
            {
                ordered.Add(new KeyValuePair<string, string>(name, v));
            }
        }
        return ordered;
    }

    private static string GenerateNonce()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[16];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }
        return new string(chars);
    }

    public void Dispose() => _ecdsa.Dispose();
}
