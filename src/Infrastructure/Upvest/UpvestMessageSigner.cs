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
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs outbound Upvest requests with HTTP Message Signatures (Upvest "v15"): ECDSA P-521 / SHA-512,
/// DER-encoded signature, base64. Adds the <c>signature</c>, <c>signature-input</c>, <c>content-digest</c>,
/// <c>upvest-client-id</c>, <c>accept</c>, <c>upvest-api-version</c> and <c>upvest-signature-version</c> headers.
/// The private key (encrypted SEC1 PEM) is loaded once and cached; the passphrase is never logged.
/// Registered as a singleton.
/// </summary>
public class UpvestMessageSigner
{
    private readonly UpvestSettings _settings;
    private readonly Lazy<AsymmetricKeyParameter> _privateKey;

    // Canonical component order expected by Upvest; only components actually present are signed.
    public UpvestMessageSigner(IOptions<UpvestSettings> settings)
    {
        _settings = settings.Value;
        _privateKey = new Lazy<AsymmetricKeyParameter>(LoadPrivateKey, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private AsymmetricKeyParameter LoadPrivateKey()
    {
        var pem = File.ReadAllText(_settings.SigningKeyPath);
        using var reader = new StringReader(pem);
        var pemReader = new PemReader(reader, new PassphraseProvider(_settings.SigningKeyPassphrase));
        var obj = pemReader.ReadObject();
        return obj switch
        {
            AsymmetricCipherKeyPair pair => pair.Private,
            ECPrivateKeyParameters ec => ec,
            AsymmetricKeyParameter key => key,
            _ => throw new InvalidOperationException("Could not load the Upvest signing key.")
        };
    }

    /// <summary>Signs the request in place, adding all required Upvest headers.</summary>
    public async Task SignAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is required for signing.");
        string method = request.Method.Method.ToUpperInvariant();
        string path = uri.AbsolutePath;
        string query = uri.Query; // "" or "?a=b"

        // Standard headers every signed request carries.
        SetHeader(request, "accept", "application/json");
        SetHeader(request, "upvest-client-id", _settings.ClientId);
        SetHeader(request, "upvest-api-version", "1");

        byte[]? body = null;
        string? contentType = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(ct);
            if (body.Length == 0) body = null;
            else contentType = request.Content.Headers.ContentType?.ToString();
        }

        string? contentDigest = null;
        if (body is not null)
        {
            using var sha = SHA512.Create();
            contentDigest = "sha-512=:" + Convert.ToBase64String(sha.ComputeHash(body)) + ":";
            SetHeader(request, "content-digest", contentDigest);
        }

        string? authorization = request.Headers.Authorization is { } a ? a.ToString() : null;
        string? idempotencyKey = request.Headers.TryGetValues("idempotency-key", out var ik) ? ik.FirstOrDefault() : null;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long created = now - 5;
        long expires = now + 300;
        string nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        // Build the ordered component list — include a component only when its value is actually present.
        var components = new List<(string Name, string Value)>
        {
            ("@method", method),
            ("@path", path),
        };
        if (query.Length > 1) components.Add(("@query", query)); // omit entirely when there is no query string
        components.Add(("accept", "application/json"));
        if (authorization is not null) components.Add(("authorization", authorization));
        if (body is not null)
        {
            components.Add(("content-length", body.Length.ToString()));
            components.Add(("content-type", contentType!));
            components.Add(("content-digest", contentDigest!));
        }
        if (idempotencyKey is not null) components.Add(("idempotency-key", idempotencyKey));
        components.Add(("upvest-client-id", _settings.ClientId));

        string inner = string.Join(" ", components.Select(c => $"\"{c.Name}\""));
        string signatureParams = $"({inner});keyid=\"{_settings.SigningKeyId}\";created={created};expires={expires};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var c in components)
            baseBuilder.Append('"').Append(c.Name).Append("\": ").Append(c.Value).Append('\n');
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        var signer = SignerUtilities.GetSigner("SHA-512withECDSA");
        signer.Init(true, _privateKey.Value);
        var baseBytes = Encoding.UTF8.GetBytes(baseBuilder.ToString());
        signer.BlockUpdate(baseBytes, 0, baseBytes.Length);
        string signature = Convert.ToBase64String(signer.GenerateSignature());

        SetHeader(request, "upvest-signature-version", "15");
        SetHeader(request, "signature", $"sig1=:{signature}:");
        SetHeader(request, "signature-input", $"sig1={signatureParams}");
    }

    private static void SetHeader(HttpRequestMessage request, string name, string value)
    {
        if (request.Headers.Contains(name)) return;
        request.Headers.TryAddWithoutValidation(name, value);
    }

    private sealed class PassphraseProvider : IPasswordFinder
    {
        private readonly char[] _passphrase;
        public PassphraseProvider(string passphrase) => _passphrase = passphrase.ToCharArray();
        public char[] GetPassword() => _passphrase;
    }
}
