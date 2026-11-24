using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Holds the Upvest EC (P-521) signing key and produces ECDSA/SHA-512 signatures over a signature
/// base string, following Upvest's "HTTP Message Signatures v15" requirements.
/// <para>
/// The private key is loaded once and kept in memory; the passphrase is used only to decrypt it and
/// is never stored or logged. Upvest verifies <c>ecdsa</c> signatures using the <c>P-521</c> curve
/// with <c>SHA-512</c> hashing, DER-encoded.
/// </para>
/// </summary>
public sealed class UpvestSigner : IDisposable
{
    private readonly ECDsa _ecdsa;

    public string KeyId { get; }

    public UpvestSigner(IOptions<UpvestSettings> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SigningKeyPath))
            throw new InvalidOperationException("Upvest:SigningKeyPath is not configured.");
        if (string.IsNullOrWhiteSpace(settings.SigningKeyId))
            throw new InvalidOperationException("Upvest:SigningKeyId is not configured.");

        KeyId = settings.SigningKeyId;
        _ecdsa = LoadKey(settings.SigningKeyPath, settings.SigningKeyPassphrase);
    }

    /// <summary>
    /// Signs the UTF-8 bytes of <paramref name="signatureBase"/> and returns the base64-encoded,
    /// DER-formatted ECDSA signature for the <c>Signature</c> header.
    /// </summary>
    public string SignBase(string signatureBase)
    {
        byte[] signature = _ecdsa.SignData(
            Encoding.UTF8.GetBytes(signatureBase),
            HashAlgorithmName.SHA512,
            DSASignatureFormat.Rfc3279DerSequence);
        return Convert.ToBase64String(signature);
    }

    private static ECDsa LoadKey(string path, string passphrase)
    {
        using var reader = new StreamReader(path);
        var pemReader = new PemReader(reader, new StaticPasswordFinder(passphrase));

        object pemObject = pemReader.ReadObject()
            ?? throw new InvalidOperationException("Upvest signing key file did not contain a PEM object.");

        ECPrivateKeyParameters keyParams = pemObject switch
        {
            ECPrivateKeyParameters ec => ec,
            AsymmetricCipherKeyPair pair => (ECPrivateKeyParameters)pair.Private,
            _ => throw new InvalidOperationException(
                $"Upvest signing key is not an EC private key (got {pemObject.GetType().Name}).")
        };

        var q = keyParams.Parameters.G.Multiply(keyParams.D).Normalize();
        int fieldSize = (keyParams.Parameters.Curve.FieldSize + 7) / 8; // 66 bytes for P-521

        var parameters = new ECParameters
        {
            Curve = ECCurve.CreateFromValue(keyParams.PublicKeyParamSet.Id),
            D = PadLeft(keyParams.D.ToByteArrayUnsigned(), fieldSize),
            Q = new ECPoint
            {
                X = PadLeft(q.AffineXCoord.GetEncoded(), fieldSize),
                Y = PadLeft(q.AffineYCoord.GetEncoded(), fieldSize)
            }
        };

        return ECDsa.Create(parameters);
    }

    private static byte[] PadLeft(byte[] value, int size)
    {
        if (value.Length == size) return value;
        if (value.Length > size) throw new InvalidOperationException("Key component larger than field size.");
        var padded = new byte[size];
        Array.Copy(value, 0, padded, size - value.Length, value.Length);
        return padded;
    }

    public void Dispose() => _ecdsa.Dispose();

    private sealed class StaticPasswordFinder : IPasswordFinder
    {
        private readonly char[] _password;
        public StaticPasswordFinder(string password) => _password = (password ?? string.Empty).ToCharArray();
        public char[] GetPassword() => _password;
    }
}
