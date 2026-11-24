using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.eShopWeb.Infrastructure.Upvest.Signing;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Upvest;

public class UpvestKeyLoaderTests
{
    [Fact]
    public void Loads_pkcs8_unencrypted_pem()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var pem = key.ExportPkcs8PrivateKeyPem();

        using var loaded = UpvestKeyLoader.Load(pem, null);
        Assert.Equal(521, loaded.KeySize);
    }

    [Fact]
    public void Empty_pem_throws()
    {
        Assert.Throws<ArgumentException>(() => UpvestKeyLoader.Load("", "x"));
    }

    [Fact]
    public void Loads_openssl_legacy_encrypted_sec1_pem()
    {
        // The real Upvest signing key is an OpenSSL legacy (Proc-Type/DEK-Info, AES-256-CBC) SEC1 key.
        // Use openssl as an independent oracle to produce that exact format; soft-skip if unavailable.
        if (!TryFindOpenSsl(out var openssl))
        {
            return;
        }

        const string passphrase = "legacy-pass";
        var dir = Directory.CreateTempSubdirectory("upvest-key-test");
        try
        {
            var plainPath = Path.Combine(dir.FullName, "plain.pem");
            var encPath = Path.Combine(dir.FullName, "enc.pem");

            using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP521))
            {
                File.WriteAllText(plainPath, key.ExportECPrivateKeyPem());
            }

            // Re-encrypt into the legacy encrypted SEC1 form.
            Run(openssl, $"ec -in \"{plainPath}\" -aes-256-cbc -passout pass:{passphrase} -out \"{encPath}\"");

            var pem = File.ReadAllText(encPath);
            Assert.Contains("ENCRYPTED", pem);

            using var loaded = UpvestKeyLoader.Load(pem, passphrase);
            Assert.Equal(521, loaded.KeySize);

            // Sanity: the loaded key can sign and verify.
            var data = Encoding.UTF8.GetBytes("hello");
            var sig = loaded.SignData(data, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
            Assert.True(loaded.VerifyData(data, sig, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static void Run(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit(30_000);
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"openssl failed: {p.StandardError.ReadToEnd()}");
        }
    }

    private static bool TryFindOpenSsl(out string path)
    {
        foreach (var candidate in new[] { "openssl", "/usr/bin/openssl", "/mingw64/bin/openssl", "C:\\Program Files\\Git\\usr\\bin\\openssl.exe" })
        {
            try
            {
                var psi = new ProcessStartInfo(candidate, "version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(psi);
                if (p is null)
                {
                    continue;
                }
                p.WaitForExit(10_000);
                if (p.ExitCode == 0)
                {
                    path = candidate;
                    return true;
                }
            }
            catch
            {
                // try next candidate
            }
        }
        path = string.Empty;
        return false;
    }
}
