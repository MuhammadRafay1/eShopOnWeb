using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs every outgoing Upvest request with an HTTP Message Signature (RFC 9421, Upvest "v15"):
/// ECDSA P-521 / SHA-512, DER-encoded. Adds the mandatory <c>upvest-client-id</c>,
/// <c>upvest-signature-version</c>, <c>accept</c> and (for requests with a body) <c>content-digest</c>
/// headers, then the <c>signature</c> / <c>signature-input</c> pair over the components that are present.
/// </summary>
public sealed class UpvestSigningHandler : DelegatingHandler
{
    private const string SignatureLabel = "sig1";

    private readonly UpvestSigningKey _signingKey;
    private readonly string _clientId;

    public UpvestSigningHandler(UpvestSigningKey signingKey, IOptions<UpvestSettings> settings)
    {
        _signingKey = signingKey;
        _clientId = settings.Value.ClientId;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Mandatory headers present on every request.
        if (request.Headers.Accept.Count == 0)
        {
            request.Headers.TryAddWithoutValidation("accept", "application/json");
        }
        request.Headers.Remove("upvest-client-id");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);
        request.Headers.Remove("upvest-signature-version");
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        byte[]? body = null;
        if (request.Content != null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            request.Content.Headers.ContentLength = body.Length;

            var digest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            request.Headers.Remove("content-digest");
            request.Headers.TryAddWithoutValidation("content-digest", digest);
        }

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = GenerateNonce();

        var components = BuildComponents(request, body);
        var componentList = BuildComponentList(components);
        var signatureParams = $"({componentList});keyid=\"{_signingKey.KeyId}\";created={created};nonce=\"{nonce}\"";

        var signatureBase = BuildSignatureBase(components, signatureParams);
        var signature = _signingKey.SignData(Encoding.ASCII.GetBytes(signatureBase));

        request.Headers.TryAddWithoutValidation("signature", $"{SignatureLabel}=:{Convert.ToBase64String(signature)}:");
        request.Headers.TryAddWithoutValidation("signature-input", $"{SignatureLabel}={signatureParams}");

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>The ordered list of covered components and their values, including only those present.</summary>
    private List<(string Name, string Value)> BuildComponents(HttpRequestMessage request, byte[]? body)
    {
        var components = new List<(string, string)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", request.RequestUri!.AbsolutePath),
        };

        var query = request.RequestUri!.Query;
        if (!string.IsNullOrEmpty(query))
        {
            components.Add(("@query", query));
        }

        components.Add(("accept", FirstHeaderValue(request, "accept")));

        if (request.Headers.Authorization != null)
        {
            components.Add(("authorization", request.Headers.Authorization.ToString()));
        }

        if (body != null)
        {
            components.Add(("content-length", body.Length.ToString()));
            components.Add(("content-type", request.Content!.Headers.ContentType?.ToString() ?? "application/json"));
            components.Add(("content-digest", FirstHeaderValue(request, "content-digest")));
        }

        if (request.Headers.TryGetValues("idempotency-key", out var idem))
        {
            foreach (var value in idem)
            {
                components.Add(("idempotency-key", value));
                break;
            }
        }

        components.Add(("upvest-client-id", _clientId));
        return components;
    }

    private static string BuildComponentList(List<(string Name, string Value)> components)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < components.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }
            sb.Append('"').Append(components[i].Name).Append('"');
        }
        return sb.ToString();
    }

    private static string BuildSignatureBase(List<(string Name, string Value)> components, string signatureParams)
    {
        var sb = new StringBuilder();
        foreach (var (name, value) in components)
        {
            sb.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        }
        sb.Append("\"@signature-params\": ").Append(signatureParams);
        return sb.ToString();
    }

    private static string FirstHeaderValue(HttpRequestMessage request, string name)
    {
        if (request.Headers.TryGetValues(name, out var values))
        {
            foreach (var value in values)
            {
                return value;
            }
        }
        return string.Empty;
    }

    private static string GenerateNonce()
    {
        Span<byte> buffer = stackalloc byte[12];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer).Replace("+", "A").Replace("/", "B").Replace("=", string.Empty);
    }
}
