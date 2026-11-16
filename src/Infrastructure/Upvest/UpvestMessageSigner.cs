using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs outgoing Upvest requests with HTTP Message Signatures (v15 profile, RFC 9421 draft-15):
/// ECDSA over the P-521 curve with SHA-512, DER-encoded then Base64. Adds the required
/// <c>content-digest</c>, <c>signature</c>, <c>signature-input</c>, <c>upvest-client-id</c>,
/// <c>upvest-api-version</c> and <c>upvest-signature-version</c> headers to each request.
/// </summary>
public sealed class UpvestMessageSigner
{
    private const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private readonly UpvestOptions _options;
    private readonly Lazy<byte[]> _privateKeyDer; // decrypted SEC1 EC private key, loaded on first use

    public UpvestMessageSigner(IOptions<UpvestOptions> options)
    {
        _options = options.Value;
        // Load the key lazily so the host can start (and DI validation can run) even when Upvest
        // configuration is absent; a missing key only fails requests that actually sign.
        _privateKeyDer = new Lazy<byte[]>(LoadPrivateKey, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private byte[] LoadPrivateKey()
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKeyPath) || !File.Exists(_options.SigningKeyPath))
            throw new InvalidOperationException("Upvest signing key file was not found at the configured Upvest:SigningKeyPath.");
        return DecryptLegacyEcPrivateKey(File.ReadAllText(_options.SigningKeyPath), _options.SigningKeyPassphrase);
    }

    /// <summary>
    /// Computes the signature over the applicable components and applies all signing-related
    /// headers to <paramref name="request"/>. The caller must already have set
    /// <paramref name="request"/>.Content (if any) using the same <paramref name="bodyBytes"/>.
    /// </summary>
    public void Sign(HttpRequestMessage request, byte[]? bodyBytes, string? contentType, string? bearerToken, string? idempotencyKey)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI must be set before signing.");
        string path = uri.AbsolutePath;
        string query = uri.Query; // includes leading '?' when present, empty string otherwise

        // Build the ordered component list. Include a component only when it applies.
        var components = new List<(string Name, string Value)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", path),
        };
        if (!string.IsNullOrEmpty(query)) components.Add(("@query", query));
        components.Add(("accept", "application/json"));
        if (bearerToken is not null) components.Add(("authorization", "Bearer " + bearerToken));

        string? contentDigest = null;
        if (bodyBytes is not null)
        {
            contentDigest = ComputeContentDigest(bodyBytes);
            components.Add(("content-length", bodyBytes.Length.ToString()));
            components.Add(("content-type", contentType!));
            components.Add(("content-digest", contentDigest));
        }
        if (idempotencyKey is not null) components.Add(("idempotency-key", idempotencyKey));
        components.Add(("upvest-client-id", _options.ClientId));

        long created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long expires = created + 90;
        string nonce = GenerateNonce();
        string componentKeys = string.Join(" ", components.Select(c => $"\"{c.Name}\""));
        string signatureParams = $"({componentKeys});keyid=\"{_options.SigningKeyId}\";created={created};expires={expires};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var c in components) baseBuilder.Append($"\"{c.Name}\": {c.Value}\n");
        baseBuilder.Append($"\"@signature-params\": {signatureParams}");

        byte[] signature;
        using (var ecdsa = ECDsa.Create())
        {
            ecdsa.ImportECPrivateKey(_privateKeyDer.Value, out _);
            signature = ecdsa.SignData(
                Encoding.UTF8.GetBytes(baseBuilder.ToString()),
                HashAlgorithmName.SHA512,
                DSASignatureFormat.Rfc3279DerSequence);
        }

        // Apply headers (content-type/content-length travel on request.Content).
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        if (bearerToken is not null) request.Headers.TryAddWithoutValidation("authorization", "Bearer " + bearerToken);
        request.Headers.TryAddWithoutValidation("upvest-client-id", _options.ClientId);
        request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");
        if (contentDigest is not null) request.Headers.TryAddWithoutValidation("content-digest", contentDigest);
        if (idempotencyKey is not null) request.Headers.TryAddWithoutValidation("idempotency-key", idempotencyKey);
        request.Headers.TryAddWithoutValidation("signature", "sig1=:" + Convert.ToBase64String(signature) + ":");
        request.Headers.TryAddWithoutValidation("signature-input", "sig1=" + signatureParams);
    }

    private static string ComputeContentDigest(byte[] body)
    {
        using var sha = SHA512.Create();
        return "sha-512=:" + Convert.ToBase64String(sha.ComputeHash(body)) + ":";
    }

    private static string GenerateNonce()
    {
        var chars = new char[16];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = Alphanumeric[RandomNumberGenerator.GetInt32(Alphanumeric.Length)];
        return new string(chars);
    }

    /// <summary>
    /// Decrypts a legacy OpenSSL encrypted EC private key ("BEGIN EC PRIVATE KEY" with a
    /// <c>DEK-Info</c> header). .NET cannot import this format directly, so we derive the AES key
    /// via OpenSSL's EVP_BytesToKey (MD5) and decrypt to the SEC1 DER that <c>ImportECPrivateKey</c>
    /// accepts. Also tolerates an unencrypted PEM as a fallback.
    /// </summary>
    internal static byte[] DecryptLegacyEcPrivateKey(string pem, string passphrase)
    {
        var lines = pem.Replace("\r", string.Empty).Split('\n');
        string? dekInfo = lines.FirstOrDefault(l => l.StartsWith("DEK-Info", StringComparison.OrdinalIgnoreCase));

        if (dekInfo is null)
        {
            // Not encrypted in the legacy way — let .NET parse the PEM directly.
            using var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            return ec.ExportECPrivateKey();
        }

        var dekParts = dekInfo.Split(':', 2)[1].Trim().Split(',');
        string algorithm = dekParts[0].Trim();
        if (!algorithm.Equals("AES-256-CBC", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Unsupported signing key encryption algorithm '{algorithm}'.");
        byte[] iv = Convert.FromHexString(dekParts[1].Trim());

        var bodyBuilder = new StringBuilder();
        bool inBody = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("-----BEGIN", StringComparison.Ordinal)) continue;
            if (line.StartsWith("-----END", StringComparison.Ordinal)) break;
            if (!inBody) { if (line.Trim().Length == 0) inBody = true; continue; }
            bodyBuilder.Append(line.Trim());
        }
        byte[] encrypted = Convert.FromBase64String(bodyBuilder.ToString());

        byte[] salt = iv.AsSpan(0, 8).ToArray();
        byte[] key = EvpBytesToKey(Encoding.UTF8.GetBytes(passphrase), salt, 32);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Key = key;
        aes.IV = iv;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
    }

    /// <summary>OpenSSL EVP_BytesToKey with MD5 and a single iteration (as used by the legacy PEM format).</summary>
    private static byte[] EvpBytesToKey(byte[] passphrase, byte[] salt, int keyLength)
    {
        var output = new byte[keyLength];
        byte[] previous = Array.Empty<byte>();
        int filled = 0;
        while (filled < keyLength)
        {
            using var md5 = MD5.Create();
            md5.TransformBlock(previous, 0, previous.Length, null, 0);
            md5.TransformBlock(passphrase, 0, passphrase.Length, null, 0);
            md5.TransformFinalBlock(salt, 0, salt.Length);
            previous = md5.Hash!;
            int take = Math.Min(previous.Length, keyLength - filled);
            Array.Copy(previous, 0, output, filled, take);
            filled += take;
        }
        return output;
    }
}
