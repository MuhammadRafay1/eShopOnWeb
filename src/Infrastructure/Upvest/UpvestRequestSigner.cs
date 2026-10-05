using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs outgoing Upvest requests with the IETF HTTP Message Signatures scheme the SDK's request records
/// cite (<c>signature</c> + <c>signature-input</c> headers). The SDK carries these as opaque strings and
/// never computes them; this class does, from the configured asymmetric signing key.
///
/// The covered components follow Upvest's v6 (default) scheme: <c>@method</c>, <c>@path</c>,
/// <c>upvest-client-id</c>, plus <c>authorization</c> (off the token endpoint), <c>@query</c> (when the
/// request has a query), <c>content-length</c>/<c>content-type</c>/<c>digest</c> (when it has a body, with a
/// SHA-256 <c>digest</c> header), and <c>idempotency-key</c> (when present). The signature parameters carry
/// <c>created</c>, <c>keyid</c> and <c>alg</c>. ECDSA signatures are SHA-512 and ASN.1 DER encoded.
/// </summary>
public sealed class UpvestRequestSigner : IDisposable
{
    private const string Label = "sig1";

    private readonly string _keyId;
    private readonly RSA? _rsa;
    private readonly ECDsa? _ecdsa;
    private readonly string _algorithm;
    private readonly TimeProvider _clock;

    public UpvestRequestSigner(UpvestSettings settings, TimeProvider? clock = null)
    {
        _keyId = settings.SigningKeyId;
        _clock = clock ?? TimeProvider.System;

        var pem = File.ReadAllText(settings.SigningKeyPath);
        (_rsa, _ecdsa, _algorithm) = LoadKey(pem, settings.SigningKeyPassphrase);
    }

    /// <summary>
    /// Sets the <c>digest</c> (for a body), <c>signature-input</c> and <c>signature</c> headers on
    /// <paramref name="request"/>. Any header the signature covers (<c>upvest-client-id</c>,
    /// <c>authorization</c>, <c>idempotency-key</c>, content headers) must already be set on the request.
    /// <paramref name="body"/> is the exact request body bytes (null/empty for a bodyless request).
    /// </summary>
    public void Sign(HttpRequestMessage request, byte[]? body)
    {
        var uri = request.RequestUri
            ?? throw new InvalidOperationException("Cannot sign a request with no URI.");
        var isTokenEndpoint = uri.AbsolutePath.EndsWith("/auth/token", StringComparison.Ordinal);
        var hasBody = body is { Length: > 0 };

        if (hasBody)
        {
            using var sha256 = SHA256.Create();
            var digest = "SHA-256=" + Convert.ToBase64String(sha256.ComputeHash(body!));
            Replace(request, "digest", digest);
            if (request.Content is not null)
            {
                request.Content.Headers.ContentLength = body!.Length;
            }
        }

        // Covered components, in the order they will be declared and signed.
        var components = new List<string> { "@method", "@path", "upvest-client-id" };
        if (!isTokenEndpoint && request.Headers.Authorization is not null) components.Add("authorization");
        if (!string.IsNullOrEmpty(uri.Query) && uri.Query != "?") components.Add("@query");
        if (hasBody) components.AddRange(new[] { "content-length", "content-type", "digest" });
        if (request.Headers.Contains("Idempotency-Key")) components.Add("idempotency-key");

        var created = _clock.GetUtcNow().ToUnixTimeSeconds();
        var componentList = string.Join(" ", components.Select(c => $"\"{c}\""));
        var signatureParams = $"({componentList});created={created};keyid=\"{_keyId}\";alg=\"{_algorithm}\"";

        var baseString = BuildSignatureBase(request, body, components, signatureParams);
        var signature = SignBytes(Encoding.ASCII.GetBytes(baseString));

        Replace(request, "signature-input", $"{Label}={signatureParams}");
        Replace(request, "signature", $"{Label}=:{Convert.ToBase64String(signature)}:");
    }

    private static string BuildSignatureBase(
        HttpRequestMessage request, byte[]? body, List<string> components, string signatureParams)
    {
        var uri = request.RequestUri!;
        var sb = new StringBuilder();

        foreach (var component in components)
        {
            var value = component switch
            {
                "@method" => request.Method.Method.ToUpperInvariant(),
                "@path" => uri.AbsolutePath,
                "@query" => uri.Query,                               // includes the leading '?'
                "@authority" => uri.Authority.ToLowerInvariant(),
                "upvest-client-id" => FirstHeader(request, "upvest-client-id"),
                "authorization" => request.Headers.Authorization?.ToString() ?? string.Empty,
                "content-length" => (body?.Length ?? 0).ToString(),
                "content-type" => request.Content?.Headers.ContentType?.ToString() ?? string.Empty,
                "digest" => FirstHeader(request, "digest"),
                "idempotency-key" => FirstHeader(request, "Idempotency-Key"),
                _ => throw new InvalidOperationException($"Unsupported signature component '{component}'.")
            };
            sb.Append('"').Append(component).Append("\": ").Append(value).Append('\n');
        }

        sb.Append("\"@signature-params\": ").Append(signatureParams);
        return sb.ToString();
    }

    private byte[] SignBytes(byte[] data)
    {
        if (_ecdsa is not null)
        {
            // Upvest verifies ECDSA signatures as SHA-512 over an ASN.1 DER (r,s) sequence.
            return _ecdsa.SignData(data, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }

        return _rsa!.SignData(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pss);
    }

    private static string FirstHeader(HttpRequestMessage request, string name)
    {
        if (request.Headers.TryGetValues(name, out var values))
            return values.FirstOrDefault() ?? string.Empty;
        if (request.Content is not null && request.Content.Headers.TryGetValues(name, out var contentValues))
            return contentValues.FirstOrDefault() ?? string.Empty;
        return string.Empty;
    }

    private static (RSA? rsa, ECDsa? ecdsa, string alg) LoadKey(string pem, string passphrase)
    {
        var hasPassphrase = !string.IsNullOrEmpty(passphrase);

        var ecdsa = ECDsa.Create();
        if (TryLoad(ecdsa, pem, passphrase, hasPassphrase))
        {
            return (null, ecdsa, "ecdsa-p521-sha512");
        }
        ecdsa.Dispose();

        var rsa = RSA.Create();
        if (TryLoad(rsa, pem, passphrase, hasPassphrase))
        {
            return (rsa, null, "rsa-pss-sha512");
        }
        rsa.Dispose();

        throw new InvalidOperationException(
            "The Upvest signing key could not be loaded as an EC or RSA private key (check Upvest:SigningKeyPath and Upvest:SigningKeyPassphrase).");
    }

    private static bool TryLoad(AsymmetricAlgorithm key, string pem, string passphrase, bool hasPassphrase)
    {
        if (hasPassphrase && Try(() => key.ImportFromEncryptedPem(pem, passphrase))) return true;
        if (Try(() => key.ImportFromPem(pem))) return true;
        return false;
    }

    private static bool Try(Action import)
    {
        try { import(); return true; }
        catch (Exception) { return false; }
    }

    private static void Replace(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Content?.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    public void Dispose()
    {
        _rsa?.Dispose();
        _ecdsa?.Dispose();
    }
}
