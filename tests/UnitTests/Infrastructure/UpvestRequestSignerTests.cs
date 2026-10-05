using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure;

public class UpvestRequestSignerTests : IDisposable
{
    private const string ClientId = "11111111-1111-1111-1111-111111111111";
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521); // matches Upvest's EC key
    private readonly string _keyPath = Path.Combine(Path.GetTempPath(), $"upvest-key-{Guid.NewGuid():N}.pem");

    private UpvestSettings EncryptedKeySettings(string passphrase)
    {
        var pem = _ecdsa.ExportEncryptedPkcs8PrivateKeyPem(
            passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000));
        File.WriteAllText(_keyPath, pem);
        return new UpvestSettings { SigningKeyId = "key-1", SigningKeyPath = _keyPath, SigningKeyPassphrase = passphrase };
    }

    private static void WithClientId(HttpRequestMessage request) =>
        request.Headers.TryAddWithoutValidation("upvest-client-id", ClientId);

    [Fact]
    public void SignsGetRequest_CoversMethodPathClientId_WithoutDigest_AndVerifies()
    {
        using var signer = new UpvestRequestSigner(EncryptedKeySettings("s3cr3t"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://sandbox.upvest.co/users?limit=10");
        WithClientId(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "abc123");

        signer.Sign(request, body: null);

        Assert.False(request.Headers.Contains("digest")); // no body ⇒ no digest
        var components = Components(request);
        Assert.Contains("@method", components);
        Assert.Contains("@path", components);
        Assert.Contains("upvest-client-id", components);
        Assert.Contains("authorization", components); // off the token endpoint
        Assert.Contains("@query", components);         // request has a query
        AssertVerifies(request, null);
    }

    [Fact]
    public void SignsPostRequest_CoversBodyComponents_WithSha256Digest_AndVerifies()
    {
        using var signer = new UpvestRequestSigner(EncryptedKeySettings("s3cr3t"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://sandbox.upvest.co/users");
        WithClientId(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "abc123");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        var body = Encoding.UTF8.GetBytes("{\"first_name\":\"Ada\"}");
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        signer.Sign(request, body);

        var digest = request.Headers.GetValues("digest").Single();
        using var sha = SHA256.Create();
        Assert.Equal("SHA-256=" + Convert.ToBase64String(sha.ComputeHash(body)), digest);

        var components = Components(request);
        foreach (var required in new[] { "@method", "@path", "upvest-client-id", "authorization", "content-length", "content-type", "digest", "idempotency-key" })
            Assert.Contains(required, components);

        AssertVerifies(request, body);
    }

    private static string[] Components(HttpRequestMessage request)
    {
        var si = request.Headers.GetValues("signature-input").Single();
        var inner = Regex.Match(si, @"\(([^)]*)\)").Groups[1].Value;
        return Regex.Matches(inner, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
    }

    // Reconstruct the signature base exactly as the provider does and verify (ECDSA SHA-512, DER).
    private void AssertVerifies(HttpRequestMessage request, byte[]? body)
    {
        var si = request.Headers.GetValues("signature-input").Single();
        var paramsValue = si.Substring("sig1=".Length);
        var uri = request.RequestUri!;

        var sb = new StringBuilder();
        foreach (var component in Components(request))
        {
            var value = component switch
            {
                "@method" => request.Method.Method.ToUpperInvariant(),
                "@path" => uri.AbsolutePath,
                "@query" => uri.Query,
                "upvest-client-id" => request.Headers.GetValues("upvest-client-id").First(),
                "authorization" => request.Headers.Authorization!.ToString(),
                "content-length" => (body?.Length ?? 0).ToString(),
                "content-type" => request.Content!.Headers.ContentType!.ToString(),
                "digest" => request.Headers.GetValues("digest").First(),
                "idempotency-key" => request.Headers.GetValues("Idempotency-Key").First(),
                _ => throw new InvalidOperationException(component)
            };
            sb.Append('"').Append(component).Append("\": ").Append(value).Append('\n');
        }
        sb.Append("\"@signature-params\": ").Append(paramsValue);

        var signatureHeader = request.Headers.GetValues("signature").Single();
        var base64 = signatureHeader.Substring(signatureHeader.IndexOf("=:", StringComparison.Ordinal) + 2).TrimEnd(':');
        var verified = _ecdsa.VerifyData(
            Encoding.ASCII.GetBytes(sb.ToString()),
            Convert.FromBase64String(base64),
            HashAlgorithmName.SHA512,
            DSASignatureFormat.Rfc3279DerSequence);

        Assert.True(verified, "The HTTP Message Signature did not verify against the signing key.");
    }

    public void Dispose()
    {
        _ecdsa.Dispose();
        if (File.Exists(_keyPath)) File.Delete(_keyPath);
    }
}
