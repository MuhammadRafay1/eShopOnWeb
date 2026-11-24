using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Signing;

/// <summary>
/// A delegating handler that signs every outgoing Upvest request per the HTTP message signatures
/// v15 profile: it adds <c>upvest-client-id</c>, a <c>content-digest</c> (SHA-512) for requests
/// with a body, and the <c>signature-input</c>/<c>signature</c> headers computed over the agreed
/// component set. The components and their order match Upvest's specification exactly.
/// </summary>
public class UpvestSigningHandler : DelegatingHandler
{
    private const string NonceAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly UpvestMessageSigner _signer;
    private readonly string _clientId;

    public UpvestSigningHandler(UpvestMessageSigner signer, IOptions<UpvestSettings> options)
    {
        _signer = signer;
        _clientId = options.Value.ClientId;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        // Collect the signature components, in the required order, including only those present.
        var components = new List<(string name, string value)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", request.RequestUri!.AbsolutePath)
        };

        var query = request.RequestUri.Query;
        if (!string.IsNullOrEmpty(query))
        {
            components.Add(("@query", query));
        }

        if (request.Headers.Accept.Count > 0)
        {
            components.Add(("accept", request.Headers.Accept.ToString()));
        }

        if (request.Headers.Authorization is not null)
        {
            components.Add(("authorization", request.Headers.Authorization.ToString()));
        }

        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = request.Content.Headers.ContentType;

            var digest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";

            // Rebuild the content so the exact bytes, content-length and content-digest we signed are sent.
            var rebuilt = new ByteArrayContent(body);
            if (contentType is not null)
            {
                rebuilt.Headers.ContentType = contentType;
            }
            rebuilt.Headers.ContentLength = body.Length;
            rebuilt.Headers.TryAddWithoutValidation("content-digest", digest);
            request.Content = rebuilt;

            components.Add(("content-length", body.Length.ToString()));
            components.Add(("content-type", contentType?.ToString() ?? string.Empty));
            components.Add(("content-digest", digest));
        }

        if (TryGetHeader(request, "idempotency-key", out var idempotencyKey))
        {
            components.Add(("idempotency-key", idempotencyKey));
        }

        components.Add(("upvest-client-id", _clientId));

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = GenerateNonce();

        var componentList = string.Join(" ", components.ConvertAll(c => $"\"{c.name}\""));
        var signatureParams = $"({componentList});keyid=\"{_signer.KeyId}\";created={created};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var (name, value) in components)
        {
            baseBuilder.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        }
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        var signature = _signer.Sign(baseBuilder.ToString());

        request.Headers.TryAddWithoutValidation("signature-input", $"sig1={signatureParams}");
        request.Headers.TryAddWithoutValidation("signature", $"sig1=:{signature}:");

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool TryGetHeader(HttpRequestMessage request, string name, out string value)
    {
        if (request.Headers.TryGetValues(name, out var values))
        {
            foreach (var v in values)
            {
                value = v;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }

    private static string GenerateNonce()
    {
        var chars = new char[16];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = NonceAlphabet[RandomNumberGenerator.GetInt32(NonceAlphabet.Length)];
        }
        return new string(chars);
    }
}
