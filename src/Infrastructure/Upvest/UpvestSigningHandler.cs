using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs every outgoing Upvest request per "HTTP Message Signatures v15": it derives the
/// <c>content-digest</c> (SHA-512) for requests with a body, builds the signature base from the
/// covered components, and adds the <c>signature-input</c>, <c>signature</c> and
/// <c>content-digest</c> headers. It never logs request contents.
/// </summary>
public sealed class UpvestSigningHandler : DelegatingHandler
{
    private const string SignatureVersion = "15";
    private static readonly char[] NonceAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private readonly UpvestSigner _signer;

    public UpvestSigningHandler(UpvestSigner signer) => _signer = signer;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // The ordered set of components Upvest covers; each is included only when present.
        var components = new List<(string Name, string Value)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", request.RequestUri!.AbsolutePath)
        };

        string query = request.RequestUri.Query; // includes leading '?'
        if (!string.IsNullOrEmpty(query))
            components.Add(("@query", query));

        if (TryGetHeader(request, "accept", out var accept))
            components.Add(("accept", accept));

        if (request.Headers.Authorization is not null)
            components.Add(("authorization", request.Headers.Authorization.ToString()));

        if (request.Content is not null)
        {
            byte[] body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            request.Content.Headers.ContentLength = body.Length;

            string digest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            request.Content.Headers.Remove("content-digest");
            request.Content.Headers.TryAddWithoutValidation("content-digest", digest);

            components.Add(("content-length", body.Length.ToString()));
            components.Add(("content-type", request.Content.Headers.ContentType?.ToString() ?? string.Empty));
            components.Add(("content-digest", digest));
        }

        if (TryGetHeader(request, "idempotency-key", out var idem))
            components.Add(("idempotency-key", idem));

        if (TryGetHeader(request, "upvest-client-id", out var clientId))
            components.Add(("upvest-client-id", clientId));

        string nonce = CreateNonce();
        long created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string componentList = string.Join(" ", components.Select(c => $"\"{c.Name}\""));
        string signatureParams = $"({componentList});keyid=\"{_signer.KeyId}\";created={created};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var (name, value) in components)
            baseBuilder.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        string signature = _signer.SignBase(baseBuilder.ToString());

        request.Headers.TryAddWithoutValidation("upvest-signature-version", SignatureVersion);
        request.Headers.TryAddWithoutValidation("signature-input", $"sig1={signatureParams}");
        request.Headers.TryAddWithoutValidation("signature", $"sig1=:{signature}:");

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool TryGetHeader(HttpRequestMessage request, string name, out string value)
    {
        if (request.Headers.TryGetValues(name, out var values))
        {
            value = string.Join(", ", values);
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static string CreateNonce()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[16];
        for (int i = 0; i < 16; i++)
            chars[i] = NonceAlphabet[bytes[i] % NonceAlphabet.Length];
        return new string(chars);
    }
}
