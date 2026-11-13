using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Builds the Upvest HTTP message-signature headers for a request (the Upvest "v6" scheme that the
/// Investment API verifies): a <c>digest</c> header plus <c>signature</c> / <c>signature-input</c>
/// headers computed over a canonical signature base using an ECDSA P-521 / SHA-512 key.
/// </summary>
public class UpvestMessageSigner
{
    private readonly UpvestOptions _options;
    private readonly ECPrivateKeyParameters _privateKey;

    public UpvestMessageSigner(IOptions<UpvestOptions> options)
    {
        _options = options.Value;
        _privateKey = LoadPrivateKey(_options.SigningKeyPath, _options.SigningKeyPassphrase);
    }

    public readonly record struct SignatureHeaders(string Signature, string SignatureInput, string? Digest);

    /// <summary>
    /// Computes the signature headers for a request.
    /// </summary>
    /// <param name="method">HTTP method, e.g. POST.</param>
    /// <param name="path">Request path without query, e.g. <c>/users</c>.</param>
    /// <param name="query">Raw query string without a leading '?', or empty when there is none.</param>
    /// <param name="body">Request body bytes, or null for a body-less request (e.g. GET).</param>
    /// <param name="bearerToken">The OAuth2 access token, or null for the token request itself.</param>
    /// <param name="contentType">Content type of the body, when a body is present.</param>
    /// <param name="idempotencyKey">The idempotency key header value, when present.</param>
    public SignatureHeaders Build(
        string method,
        string path,
        string? query,
        byte[]? body,
        string? bearerToken,
        string? contentType,
        string? idempotencyKey)
    {
        var components = new List<(string Key, string Value)>
        {
            ("@method", method.ToUpperInvariant()),
            ("@path", path),
        };

        if (!string.IsNullOrEmpty(query))
        {
            components.Add(("@query", "?" + query));
        }

        components.Add(("accept", "application/json"));

        if (bearerToken is not null)
        {
            components.Add(("authorization", "Bearer " + bearerToken));
        }

        string? digest = null;
        if (body is not null)
        {
            digest = "SHA-256=" + Convert.ToBase64String(SHA256.HashData(body));
            components.Add(("content-length", body.Length.ToString()));
            components.Add(("content-type", contentType ?? "application/json"));
            components.Add(("digest", digest));
        }

        if (idempotencyKey is not null)
        {
            components.Add(("idempotency-key", idempotencyKey));
        }

        components.Add(("upvest-client-id", _options.ClientId));

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = created + 60;
        var nonce = CreateNonce();

        var paramList = string.Join(' ', ComponentNames(components));
        var signatureParams =
            $"({paramList});keyid=\"{_options.SigningKeyId}\";created={created};expires={expires};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var (key, value) in components)
        {
            baseBuilder.Append(key).Append(": ").Append(value).Append('\n');
        }
        baseBuilder.Append("@signature-params: ").Append(signatureParams);

        var signature = Sign(Encoding.ASCII.GetBytes(baseBuilder.ToString()));

        return new SignatureHeaders(
            Signature: $"sig1=:{signature}:",
            SignatureInput: $"sig1={signatureParams}",
            Digest: digest);
    }

    private static IEnumerable<string> ComponentNames(IEnumerable<(string Key, string Value)> components)
    {
        foreach (var (key, _) in components)
        {
            yield return $"\"{key}\"";
        }
    }

    private string Sign(byte[] data)
    {
        var signer = SignerUtilities.GetSigner("SHA-512withECDSA");
        signer.Init(forSigning: true, _privateKey);
        signer.BlockUpdate(data, 0, data.Length);
        var der = signer.GenerateSignature();
        return Convert.ToBase64String(der);
    }

    private static string CreateNonce()
    {
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
    }

    private static ECPrivateKeyParameters LoadPrivateKey(string path, string passphrase)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("Upvest:SigningKeyPath is not configured.");
        }

        using var reader = new System.IO.StreamReader(path);
        var pemReader = new PemReader(reader, new StaticPasswordFinder(passphrase));
        var keyObject = pemReader.ReadObject();

        var keyParameters = keyObject switch
        {
            AsymmetricCipherKeyPair pair => pair.Private,
            ECPrivateKeyParameters ecPrivate => ecPrivate,
            AsymmetricKeyParameter asymmetric => asymmetric,
            _ => throw new InvalidOperationException(
                $"The Upvest signing key is not a supported private key (got {keyObject?.GetType().Name ?? "null"}).")
        };

        if (keyParameters is not ECPrivateKeyParameters ecKey)
        {
            throw new InvalidOperationException("The Upvest signing key must be an EC private key.");
        }

        return ecKey;
    }

    private sealed class StaticPasswordFinder : IPasswordFinder
    {
        private readonly char[] _password;
        public StaticPasswordFinder(string password) => _password = (password ?? string.Empty).ToCharArray();
        public char[] GetPassword() => (char[])_password.Clone();
    }
}
