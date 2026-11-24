using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Signing;

/// <summary>
/// Signs outgoing HTTP requests to Upvest using HTTP Message Signatures "v15"
/// (RFC 9421 style): ECDSA on curve P-521 with SHA-512, DER-encoded signature, base64.
/// Adds the <c>content-digest</c> (SHA-512), <c>signature-input</c> and <c>signature</c>
/// headers and the <c>upvest-signature-version</c> marker.
/// </summary>
public sealed class UpvestRequestSigner
{
    private const string SignatureLabel = "sig1";

    private readonly ECParameters _keyParameters;
    private readonly string _keyId;
    private readonly string _clientId;

    public UpvestRequestSigner(IOptions<UpvestSettings> options)
    {
        var settings = options.Value;
        _keyId = settings.SigningKeyId;
        _clientId = settings.ClientId;

        var pem = System.IO.File.ReadAllText(settings.SigningKeyPath);
        using var ecdsa = EcPrivateKeyLoader.Load(pem, settings.SigningKeyPassphrase);
        // Capture the private key parameters once; a fresh ECDsa is created per signature so
        // concurrent requests never share mutable crypto state.
        _keyParameters = ecdsa.ExportParameters(includePrivateParameters: true);
    }

    public async Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        // Ensure the client-id header is present and part of the signature.
        if (!request.Headers.Contains("upvest-client-id"))
            request.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);

        request.Headers.Remove("upvest-signature-version");
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        byte[]? body = null;
        string? contentType = null;
        if (request.Content != null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            contentType = request.Content.Headers.ContentType?.ToString();

            // Re-materialise the content so Content-Length is deterministic and matches what we sign.
            var rebuilt = new ByteArrayContent(body);
            if (contentType != null)
                rebuilt.Headers.TryAddWithoutValidation("Content-Type", contentType);

            var digest = Convert.ToBase64String(SHA512.HashData(body));
            var contentDigest = $"sha-512=:{digest}:";
            rebuilt.Headers.TryAddWithoutValidation("Content-Digest", contentDigest);
            request.Content = rebuilt;
        }

        var createdUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = GenerateNonce();

        // Build the ordered list of covered components, including only those present.
        var components = new List<(string name, string value)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", request.RequestUri!.AbsolutePath),
        };

        var query = request.RequestUri!.Query; // includes leading '?' or empty
        if (!string.IsNullOrEmpty(query))
            components.Add(("@query", query));

        AddHeaderComponent(components, "accept", request.Headers.Accept.Count > 0 ? string.Join(", ", request.Headers.Accept) : null);
        AddHeaderComponent(components, "authorization", request.Headers.Authorization?.ToString());

        if (body != null)
        {
            components.Add(("content-length", body.Length.ToString(CultureInfo.InvariantCulture)));
            AddHeaderComponent(components, "content-type", contentType);
            AddHeaderComponent(components, "content-digest", SingleHeader(request.Content!, "Content-Digest"));
        }

        AddHeaderComponent(components, "idempotency-key", SingleHeader(request, "idempotency-key"));
        components.Add(("upvest-client-id", _clientId));

        var covered = string.Join(" ", components.Select(c => $"\"{c.name}\""));
        var signatureParams =
            $"({covered});keyid=\"{_keyId}\";created={createdUnix};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var (name, value) in components)
            baseBuilder.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        var signatureBytes = SignBase(baseBuilder.ToString());
        var signature = Convert.ToBase64String(signatureBytes);

        request.Headers.Remove("signature-input");
        request.Headers.Remove("signature");
        request.Headers.TryAddWithoutValidation("signature-input", $"{SignatureLabel}={signatureParams}");
        request.Headers.TryAddWithoutValidation("signature", $"{SignatureLabel}=:{signature}:");
    }

    private byte[] SignBase(string signatureBase)
    {
        using var ecdsa = ECDsa.Create(_keyParameters);
        return ecdsa.SignData(
            Encoding.UTF8.GetBytes(signatureBase),
            HashAlgorithmName.SHA512,
            DSASignatureFormat.Rfc3279DerSequence);
    }

    private static void AddHeaderComponent(List<(string, string)> components, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            components.Add((name, value));
    }

    private static string? SingleHeader(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(", ", values) : null;

    private static string? SingleHeader(HttpContent content, string name) =>
        content.Headers.TryGetValues(name, out var values) ? string.Join(", ", values) : null;

    private static string GenerateNonce()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Span<char> chars = stackalloc char[16];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }
}
