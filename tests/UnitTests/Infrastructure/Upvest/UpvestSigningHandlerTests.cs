using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Upvest;

public class UpvestSigningHandlerTests
{
    private const string ClientId = "client-abc";
    private const string KeyId = "key-123";

    [Fact]
    public async Task AddsMandatoryHeadersAndAValidSignatureForABodyRequest()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var capture = new CapturingHandler();
        var handler = new UpvestSigningHandler(new UpvestSigningKey(KeyId, ecdsa), Options.Create(new UpvestSettings { ClientId = ClientId }))
        {
            InnerHandler = capture
        };
        using var invoker = new HttpMessageInvoker(handler);

        var body = """{"hello":"world"}""";
        var request = new HttpRequestMessage(HttpMethod.Post, "https://sandbox.upvest.co/users?x=1")
        {
            Content = new StringContent(body, Encoding.UTF8),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "the-token");
        request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());

        await invoker.SendAsync(request, CancellationToken.None);
        var sent = capture.Request!;

        Assert.Equal(ClientId, Single(sent, "upvest-client-id"));
        Assert.Equal("15", Single(sent, "upvest-signature-version"));

        var expectedDigest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(body))) + ":";
        Assert.Equal(expectedDigest, Single(sent, "content-digest"));

        var signatureInput = Single(sent, "signature-input");
        var signature = Single(sent, "signature");
        Assert.StartsWith("sig1=(", signatureInput);
        Assert.Contains($"keyid=\"{KeyId}\"", signatureInput);

        // The signature must verify against the reconstructed signature base.
        var baseString = ReconstructBase(sent, signatureInput);
        var sigBytes = Convert.FromBase64String(Between(signature, "sig1=:", ":"));
        Assert.True(ecdsa.VerifyData(Encoding.ASCII.GetBytes(baseString), sigBytes, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence));
    }

    [Fact]
    public async Task OmitsContentDigestForBodilessRequest()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var capture = new CapturingHandler();
        var handler = new UpvestSigningHandler(new UpvestSigningKey(KeyId, ecdsa), Options.Create(new UpvestSettings { ClientId = ClientId }))
        {
            InnerHandler = capture
        };
        using var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://sandbox.upvest.co/users/abc");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "the-token");

        await invoker.SendAsync(request, CancellationToken.None);
        var sent = capture.Request!;

        Assert.False(sent.Headers.Contains("content-digest"));
        var signatureInput = Single(sent, "signature-input");
        Assert.DoesNotContain("content-digest", signatureInput);

        var baseString = ReconstructBase(sent, signatureInput);
        var sigBytes = Convert.FromBase64String(Between(Single(sent, "signature"), "sig1=:", ":"));
        Assert.True(ecdsa.VerifyData(Encoding.ASCII.GetBytes(baseString), sigBytes, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence));
    }

    // Rebuilds the RFC 9421 signature base from the sent request + its signature-input header.
    private static string ReconstructBase(HttpRequestMessage request, string signatureInput)
    {
        var paren = Between(signatureInput, "(", ")");
        var names = Regex.Matches(paren, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        // Everything from the first ')' onwards is ";keyid=...;created=...;nonce=...".
        var paramsPart = signatureInput.Substring(signatureInput.IndexOf(')') + 1);

        var sb = new StringBuilder();
        foreach (var name in names)
        {
            sb.Append('"').Append(name).Append("\": ").Append(ComponentValue(request, name)).Append('\n');
        }
        sb.Append("\"@signature-params\": (").Append(paren).Append(')').Append(paramsPart);
        return sb.ToString();
    }

    private static string ComponentValue(HttpRequestMessage request, string name) => name switch
    {
        "@method" => request.Method.Method.ToUpperInvariant(),
        "@path" => request.RequestUri!.AbsolutePath,
        "@query" => request.RequestUri!.Query,
        "authorization" => request.Headers.Authorization!.ToString(),
        "content-type" => request.Content!.Headers.ContentType!.ToString(),
        "content-length" => request.Content!.Headers.ContentLength!.Value.ToString(),
        _ => Single(request, name),
    };

    private static string Single(HttpRequestMessage request, string name)
    {
        if (request.Headers.TryGetValues(name, out var values) ||
            (request.Content != null && request.Content.Headers.TryGetValues(name, out values)))
        {
            return values!.First();
        }
        return string.Empty;
    }

    private static string Between(string value, string start, string end)
    {
        var s = value.IndexOf(start, StringComparison.Ordinal) + start.Length;
        var e = value.IndexOf(end, s, StringComparison.Ordinal);
        return value.Substring(s, e - s);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
