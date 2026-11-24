using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.eShopWeb.Infrastructure.Upvest.Signing;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Upvest;

public class UpvestMessageSignerTests : IDisposable
{
    private readonly string _unencryptedPath = Path.GetTempFileName();
    private readonly string _encryptedPkcs8Path = Path.GetTempFileName();
    private const string Passphrase = "s3cr3t-passphrase";

    public UpvestMessageSignerTests()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        File.WriteAllText(_unencryptedPath, key.ExportECPrivateKeyPem());
        File.WriteAllText(_encryptedPkcs8Path, key.ExportEncryptedPkcs8PrivateKeyPem(
            Passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000)));
    }

    [Fact]
    public void Loads_unencrypted_sec1_key_and_produces_a_verifiable_der_sha512_signature()
    {
        var signer = new UpvestMessageSigner(Options.Create(new UpvestSettings
        {
            SigningKeyPath = _unencryptedPath,
            SigningKeyId = "key-123"
        }));

        const string baseString = "\"@method\": POST\n\"@path\": /users";
        var signatureBase64 = signer.Sign(baseString);
        var signature = Convert.FromBase64String(signatureBase64);

        Assert.Equal("key-123", signer.KeyId);

        // The signature must verify against the public key as a DER-encoded ECDSA/SHA-512 signature.
        using var publicKey = ECDsa.Create();
        publicKey.ImportFromPem(File.ReadAllText(_unencryptedPath));
        var verified = publicKey.VerifyData(
            Encoding.UTF8.GetBytes(baseString), signature, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        Assert.True(verified);
    }

    [Fact]
    public void Loads_encrypted_pkcs8_key_using_the_passphrase()
    {
        var signer = new UpvestMessageSigner(Options.Create(new UpvestSettings
        {
            SigningKeyPath = _encryptedPkcs8Path,
            SigningKeyPassphrase = Passphrase,
            SigningKeyId = "key-abc"
        }));

        var signature = signer.Sign("\"@method\": GET\n\"@path\": /users/1");
        Assert.False(string.IsNullOrEmpty(signature));
    }

    [Fact]
    public void Loading_a_missing_key_file_throws()
    {
        Assert.Throws<FileNotFoundException>(() => new UpvestMessageSigner(Options.Create(new UpvestSettings
        {
            SigningKeyPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pem")
        })));
    }

    public void Dispose()
    {
        File.Delete(_unencryptedPath);
        File.Delete(_encryptedPkcs8Path);
    }
}
