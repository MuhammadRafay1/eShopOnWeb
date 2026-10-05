using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Produces HTTP Message Signatures (draft-ietf-httpbis-message-signatures style, as the Upvest request
/// records reference) for every outgoing call. The signing key is the client's ECDSA P-521 private key,
/// loaded once from the configured encrypted PEM; signatures are SHA-512 over the signature base and
/// encoded as an ASN.1 DER sequence. The passphrase and key are never logged.
/// </summary>
public sealed class UpvestRequestSigner : IUpvestRequestSigner, IDisposable
{
    private const string TokenPath = "/auth/token";

    private readonly ECDsa _key;
    private readonly string _keyId;
    private readonly object _signLock = new();

    public UpvestRequestSigner(IOptions<UpvestOptions> options)
    {
        var o = options.Value;
        var errors = o.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException("Upvest configuration is invalid: " + string.Join(" ", errors));

        _keyId = o.SigningKeyIdGuid.ToString();
        _key = ECDsa.Create();
        try
        {
            var pem = File.ReadAllText(o.SigningKeyPath);
            _key.ImportFromEncryptedPem(pem, o.SigningKeyPassphrase);
        }
        catch (Exception ex)
        {
            _key.Dispose();
            // Do not echo the passphrase or key material; name only the config key at fault.
            throw new InvalidOperationException(
                $"Upvest signing key could not be loaded from the path in {UpvestOptions.SectionName}:SigningKeyPath " +
                $"(check the file exists and {UpvestOptions.SectionName}:SigningKeyPassphrase is correct).", ex);
        }
    }

    public async Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null) throw new InvalidOperationException("Cannot sign a request with no URI.");

        var path = request.RequestUri.AbsolutePath;
        var query = request.RequestUri.Query;              // includes leading '?'
        var isToken = string.Equals(path, TokenPath, StringComparison.Ordinal);

        // Buffer the body and compute its digest; set content-length and the digest header.
        byte[]? body = null;
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync();
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            request.Content.Headers.ContentLength = body.Length;
            var digest = "SHA-256=" + Convert.ToBase64String(SHA256.HashData(body));
            request.Headers.Remove("digest");
            request.Headers.TryAddWithoutValidation("digest", digest);
        }

        // Covered components, exactly as Upvest requires them.
        var components = new List<string> { "@method", "@path", "upvest-client-id" };
        if (!isToken) components.Add("authorization");
        if (!string.IsNullOrEmpty(query) && query != "?") components.Add("@query");
        if (body is not null) { components.Add("content-length"); components.Add("content-type"); components.Add("digest"); }
        if (HasHeader(request, "idempotency-key")) components.Add("idempotency-key");

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = created + 60;
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var componentList = "(" + string.Join(" ", components.Select(c => $"\"{c}\"")) + ")";
        var paramString = componentList +
            $";created={created.ToString(CultureInfo.InvariantCulture)}" +
            $";keyid=\"{_keyId}\"" +
            $";alg=\"ecdsa-p521-sha512\"" +
            $";nonce=\"{nonce}\"" +
            $";expires={expires.ToString(CultureInfo.InvariantCulture)}";

        var lines = new List<string>(components.Count + 1);
        foreach (var component in components)
            lines.Add($"\"{component}\": {ComponentValue(request, component, path, query)}");
        lines.Add($"\"@signature-params\": {paramString}");
        var signatureBase = string.Join("\n", lines);

        byte[] signature;
        lock (_signLock)
        {
            signature = _key.SignData(Encoding.UTF8.GetBytes(signatureBase), HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }

        request.Headers.Remove("signature-input");
        request.Headers.Remove("signature");
        request.Headers.TryAddWithoutValidation("signature-input", "sig1=" + paramString);
        request.Headers.TryAddWithoutValidation("signature", "sig1=:" + Convert.ToBase64String(signature) + ":");
    }

    private static string ComponentValue(HttpRequestMessage request, string component, string path, string query) => component switch
    {
        "@method" => request.Method.Method,
        "@path" => path,
        "@query" => query,
        "upvest-client-id" => FirstHeader(request, "upvest-client-id"),
        "authorization" => request.Headers.Authorization?.ToString() ?? FirstHeader(request, "authorization"),
        "content-length" => request.Content!.Headers.ContentLength!.Value.ToString(CultureInfo.InvariantCulture),
        "content-type" => request.Content!.Headers.ContentType?.ToString() ?? string.Empty,
        "digest" => FirstHeader(request, "digest"),
        "idempotency-key" => FirstHeader(request, "idempotency-key"),
        _ => throw new InvalidOperationException($"Unsupported signature component '{component}'.")
    };

    private static bool HasHeader(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out _) ||
        (request.Content is not null && request.Content.Headers.TryGetValues(name, out _));

    private static string FirstHeader(HttpRequestMessage request, string name)
    {
        if (request.Headers.TryGetValues(name, out var values)) return values.First();
        if (request.Content is not null && request.Content.Headers.TryGetValues(name, out var cvalues)) return cvalues.First();
        return string.Empty;
    }

    public void Dispose() => _key.Dispose();
}
