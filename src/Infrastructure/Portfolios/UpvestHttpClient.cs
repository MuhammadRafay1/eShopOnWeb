using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Portfolios;

/// <summary>A raw Upvest HTTP response: the status code and the body, with no interpretation.</summary>
public readonly record struct UpvestResponse(int Status, string Body)
{
    public bool IsSuccess => Status is >= 200 and < 300;
}

/// <summary>
/// Low-level Upvest transport. Every request is signed with an HTTP message signature and carries the
/// mandatory Upvest headers (<c>upvest-client-id</c>, <c>accept</c>, <c>date</c>, and — for writes —
/// <c>idempotency-key</c> and <c>content-digest</c>). It does not add personal data to any log.
/// </summary>
public sealed class UpvestHttpClient
{
    private readonly HttpClient _http;
    private readonly UpvestRequestSigner _signer;
    private readonly UpvestSettings _settings;

    public UpvestHttpClient(HttpClient http, UpvestRequestSigner signer, IOptions<UpvestSettings> settings)
    {
        _http = http;
        _signer = signer;
        _settings = settings.Value;
    }

    /// <summary>
    /// Sends a signed request. <paramref name="path"/> is the URL path (starting with '/').
    /// <paramref name="query"/> is the raw query string without the leading '?', or null.
    /// </summary>
    public async Task<UpvestResponse> SendAsync(
        HttpMethod method,
        string path,
        string? query,
        byte[]? body,
        string? contentType,
        string? bearerToken,
        bool withIdempotencyKey,
        CancellationToken ct)
    {
        string url = query is { Length: > 0 } ? $"{path}?{query}" : path;
        using var request = new HttpRequestMessage(method, url);

        // Mandatory headers.
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Date = DateTimeOffset.UtcNow;

        string? idempotencyKey = withIdempotencyKey ? Guid.NewGuid().ToString() : null;
        if (idempotencyKey is not null)
            request.Headers.TryAddWithoutValidation("idempotency-key", idempotencyKey);

        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        string? digest = null;
        if (body is not null)
        {
            var content = new ByteArrayContent(body);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType ?? "application/json");
            content.Headers.ContentLength = body.Length;
            request.Content = content;
            digest = UpvestRequestSigner.ComputeDigest(body);
            request.Headers.TryAddWithoutValidation("digest", digest);
        }

        // Build the covered components in Upvest's canonical v6 order, including only those present.
        // @query is excluded entirely when the request has no query string.
        var components = new List<SignatureComponent>
        {
            new("@method", method.Method.ToUpperInvariant()),
            new("@path", path),
        };
        if (query is { Length: > 0 })
            components.Add(new("@query", $"?{query}"));
        components.Add(new("accept", "application/json"));
        if (bearerToken is not null)
            components.Add(new("authorization", $"Bearer {bearerToken}"));
        if (body is not null)
        {
            components.Add(new("content-length", body.Length.ToString(CultureInfo.InvariantCulture)));
            components.Add(new("content-type", contentType ?? "application/json"));
            components.Add(new("digest", digest!));
        }
        if (idempotencyKey is not null)
            components.Add(new("idempotency-key", idempotencyKey));
        components.Add(new("upvest-client-id", _settings.ClientId));

        var sig = _signer.Sign(components);
        request.Headers.TryAddWithoutValidation("signature", sig.Signature);
        request.Headers.TryAddWithoutValidation("signature-input", sig.SignatureInput);

        using var response = await _http.SendAsync(request, ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);
        return new UpvestResponse((int)response.StatusCode, responseBody);
    }
}
