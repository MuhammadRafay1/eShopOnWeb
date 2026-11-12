using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Microsoft.eShopWeb.Infrastructure.Portfolios;

/// <summary>One covered component of a signature base: a quoted key and its value.</summary>
public readonly record struct SignatureComponent(string Key, string Value);

/// <summary>The two headers that carry an HTTP message signature.</summary>
public readonly record struct SignatureHeaders(string Signature, string SignatureInput);

/// <summary>
/// Produces Upvest HTTP message signatures (v6 of their protocol, draft-ietf-httpbis-message-signatures-06).
/// The signing key is an EC P-521 private key; signatures use ECDSA with SHA-512 and are DER-encoded
/// then Base64-encoded, exactly as Upvest requires. The passphrase and key material never leave this type.
/// In v6 the signature base lines use unquoted component keys, and the request-body checksum is carried
/// in a <c>digest: SHA-256=...</c> header.
/// </summary>
public sealed class UpvestRequestSigner
{
    private readonly UpvestSettings _settings;
    private readonly object _gate = new();
    private AsymmetricKeyParameter? _privateKey;

    public UpvestRequestSigner(IOptions<UpvestSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    /// Builds the <c>signature</c> and <c>signature-input</c> header values covering the given ordered
    /// components. <paramref name="components"/> must already be in the intended signing order (the same
    /// order their keys appear in <c>@signature-params</c>).
    /// </summary>
    public SignatureHeaders Sign(IReadOnlyList<SignatureComponent> components)
    {
        long created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string nonce = GenerateNonce();

        // @signature-params value: ("k1" "k2" ...);keyid="...";created=...;nonce="..."
        var keyList = new StringBuilder("(");
        for (int i = 0; i < components.Count; i++)
        {
            if (i > 0) keyList.Append(' ');
            keyList.Append('"').Append(components[i].Key).Append('"');
        }
        keyList.Append(')');

        string paramsValue =
            $"{keyList};keyid=\"{_settings.SigningKeyId}\";created={created};nonce=\"{nonce}\"";

        // v6 signature base string: each component on its own line with an UNQUOTED key, then
        // @signature-params (also unquoted key), joined by \n.
        var baseBuilder = new StringBuilder();
        foreach (var c in components)
        {
            baseBuilder.Append(c.Key).Append(": ").Append(c.Value).Append('\n');
        }
        baseBuilder.Append("@signature-params: ").Append(paramsValue);

        byte[] baseBytes = Encoding.UTF8.GetBytes(baseBuilder.ToString());
        string signatureB64 = Convert.ToBase64String(ComputeEcdsaSha512(baseBytes));

        return new SignatureHeaders(
            Signature: $"sig1=:{signatureB64}:",
            SignatureInput: $"sig1={paramsValue}");
    }

    /// <summary>Computes the v6 <c>digest</c> header value for a request body: <c>SHA-256=base64</c>.</summary>
    public static string ComputeDigest(byte[] body)
    {
        using var sha = SHA256.Create();
        return $"SHA-256={Convert.ToBase64String(sha.ComputeHash(body))}";
    }

    private byte[] ComputeEcdsaSha512(byte[] data)
    {
        var key = GetPrivateKey();
        // "SHA-512withECDSA" yields an ASN.1 DER-encoded (r, s) signature, which is what Upvest expects.
        ISigner signer = SignerUtilities.GetSigner("SHA-512withECDSA");
        signer.Init(forSigning: true, key);
        signer.BlockUpdate(data, 0, data.Length);
        return signer.GenerateSignature();
    }

    private AsymmetricKeyParameter GetPrivateKey()
    {
        if (_privateKey is not null) return _privateKey;
        lock (_gate)
        {
            if (_privateKey is not null) return _privateKey;

            if (string.IsNullOrWhiteSpace(_settings.SigningKeyPath))
                throw new InvalidOperationException("Upvest signing key path is not configured (Upvest:SigningKeyPath).");
            if (!File.Exists(_settings.SigningKeyPath))
                throw new FileNotFoundException("Upvest signing key file was not found.", _settings.SigningKeyPath);

            string pem = File.ReadAllText(_settings.SigningKeyPath);
            using var reader = new StringReader(pem);
            var pemReader = new PemReader(reader, new StaticPassword(_settings.SigningKeyPassphrase));
            object obj = pemReader.ReadObject()
                ?? throw new InvalidOperationException("The Upvest signing key file did not contain a readable PEM object.");

            AsymmetricKeyParameter key = obj switch
            {
                AsymmetricCipherKeyPair pair => pair.Private,
                ECPrivateKeyParameters ec => ec,
                AsymmetricKeyParameter akp when akp.IsPrivate => akp,
                _ => throw new InvalidOperationException("The Upvest signing key is not a usable EC private key.")
            };

            if (!key.IsPrivate)
                throw new InvalidOperationException("The Upvest signing key did not resolve to a private key.");

            _privateKey = key;
            return _privateKey;
        }
    }

    private static string GenerateNonce()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(16);
        foreach (byte b in bytes) sb.Append(alphabet[b % alphabet.Length]);
        return sb.ToString();
    }

    private sealed class StaticPassword : IPasswordFinder
    {
        private readonly char[] _password;
        public StaticPassword(string password) => _password = (password ?? string.Empty).ToCharArray();
        public char[] GetPassword() => (char[])_password.Clone();
    }
}
