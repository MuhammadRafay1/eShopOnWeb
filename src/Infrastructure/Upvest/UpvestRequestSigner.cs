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

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs an outgoing Upvest request with HTTP Message Signatures (ECDSA P-521 / SHA-512, ASN.1-DER
/// signature) and sets the <c>upvest-client-id</c> and body <c>digest</c> headers. This is the single place
/// that applies request credentials; no call site does. The signing key (loaded once, encrypted PKCS#8) and
/// its passphrase never leave this process and are never logged.
/// </summary>
public sealed class UpvestRequestSigner : IDisposable
{
    private readonly ECDsa _key;
    private readonly string _keyId;
    private readonly string _clientId;
    private readonly object _signLock = new();

    public UpvestRequestSigner(UpvestOptions options)
    {
        _keyId = options.SigningKeyId;
        _clientId = options.ClientId;

        var pem = File.ReadAllText(options.SigningKeyPath);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromEncryptedPem(pem, options.SigningKeyPassphrase);
        _key = ecdsa;
    }

    /// <summary>
    /// Adds <c>upvest-client-id</c>, <c>digest</c> (for a body), and the <c>signature</c>/<c>signature-input</c>
    /// headers. An <c>authorization</c> header already set by the caller is covered by the signature;
    /// <c>idempotency-key</c> is covered when present.
    /// </summary>
    public async Task SignAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Remove("upvest-client-id");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);

        byte[]? body = null;
        string? digest = null;
        string? contentType = null;
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync().ConfigureAwait(false);
            body = await request.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            digest = "SHA-256=" + Convert.ToBase64String(SHA256.HashData(body));
            request.Headers.Remove("digest");
            request.Headers.TryAddWithoutValidation("digest", digest);
            request.Content.Headers.ContentLength = body.Length;
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        // Covered components, in the exact order written into the signature base.
        var comps = new List<(string Name, string Value)>
        {
            ("@method", request.Method.Method),
            ("@path", request.RequestUri!.AbsolutePath),
        };
        if (request.RequestUri.Query.Length > 0)
            comps.Add(("@query", request.RequestUri.Query));
        if (body is not null)
        {
            comps.Add(("content-type", contentType ?? "application/json"));
            comps.Add(("content-length", body.Length.ToString(CultureInfo.InvariantCulture)));
            comps.Add(("digest", digest!));
        }
        if (request.Headers.Authorization is not null)
            comps.Add(("authorization", request.Headers.Authorization.ToString()));
        comps.Add(("upvest-client-id", _clientId));
        if (request.Headers.TryGetValues("idempotency-key", out var idem))
            comps.Add(("idempotency-key", idem.First()));

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var inner = "(" + string.Join(" ", comps.Select(c => $"\"{c.Name}\"")) + ")";
        var paramString = $"{inner};created={created};keyid=\"{_keyId}\"";
        var baseString = string.Join("\n", comps.Select(c => $"\"{c.Name}\": {c.Value}"))
                         + $"\n\"@signature-params\": {paramString}";

        byte[] signature;
        lock (_signLock)
        {
            signature = _key.SignData(
                Encoding.UTF8.GetBytes(baseString),
                HashAlgorithmName.SHA512,
                DSASignatureFormat.Rfc3279DerSequence);
        }

        request.Headers.Remove("signature-input");
        request.Headers.Remove("signature");
        request.Headers.TryAddWithoutValidation("signature-input", $"sig1={paramString}");
        request.Headers.TryAddWithoutValidation("signature", $"sig1=:{Convert.ToBase64String(signature)}:");
    }

    public void Dispose() => _key.Dispose();
}
