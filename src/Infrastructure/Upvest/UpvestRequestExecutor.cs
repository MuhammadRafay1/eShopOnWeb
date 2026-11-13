using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Upvest.Signing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The raw outcome of a signed HTTP exchange with Upvest.
/// </summary>
public readonly record struct UpvestRawResponse(int StatusCode, bool IsSuccess, string Body);

/// <summary>
/// Sends signed HTTP requests to Upvest. Owns content-digest computation, HTTP message signing,
/// the mandatory headers, and the documented retry policy (429/500/503). It carries no knowledge
/// of individual endpoints — the token provider and the API client build on it.
/// </summary>
public class UpvestRequestExecutor
{
    public const string HttpClientName = "upvest";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUpvestMessageSigner _signer;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestRequestExecutor> _logger;
    private readonly Uri _baseAddress;

    public UpvestRequestExecutor(
        IHttpClientFactory httpClientFactory,
        IUpvestMessageSigner signer,
        IOptions<UpvestSettings> settings,
        ILogger<UpvestRequestExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _signer = signer;
        _settings = settings.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            throw new InvalidOperationException("Upvest:BaseUrl is not configured.");
        }

        _baseAddress = new Uri(_settings.BaseUrl, UriKind.Absolute);
    }

    public async Task<UpvestRawResponse> SendAsync(
        HttpMethod method,
        string path,
        string? query,
        byte[]? body,
        string? contentType,
        string? accessToken,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        // Retry policy per Upvest workflow docs: 429 -> 3x/5s, 500 -> 2x/10s, 503 -> 3x/30s.
        var attempt = 0;
        while (true)
        {
            attempt++;
            var response = await SendOnceAsync(method, path, query, body, contentType, accessToken, idempotencyKey, cancellationToken);

            var retryDelay = response.StatusCode switch
            {
                429 when attempt <= 3 => TimeSpan.FromSeconds(5),
                500 when attempt <= 2 => TimeSpan.FromSeconds(10),
                503 when attempt <= 3 => TimeSpan.FromSeconds(30),
                _ => (TimeSpan?)null
            };

            if (retryDelay is null)
            {
                return response;
            }

            _logger.LogWarning("Upvest {Method} {Path} returned {Status}; retry {Attempt} after {Delay}s.",
                method.Method, path, response.StatusCode, attempt, (int)retryDelay.Value.TotalSeconds);
            await Task.Delay(retryDelay.Value, cancellationToken);
        }
    }

    private async Task<UpvestRawResponse> SendOnceAsync(
        HttpMethod method,
        string path,
        string? query,
        byte[]? body,
        string? contentType,
        string? accessToken,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var relative = string.IsNullOrEmpty(query) ? path : path + (query.StartsWith('?') ? query : "?" + query);
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        httpClient.BaseAddress = _baseAddress;
        using var request = new HttpRequestMessage(method, new Uri(_baseAddress, relative));

        const string accept = "application/json";
        string? contentDigest = null;

        if (body is not null && contentType is not null)
        {
            contentDigest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            request.Content.Headers.ContentLength = body.Length;
            request.Content.Headers.TryAddWithoutValidation("content-digest", contentDigest);
        }

        var authorization = string.IsNullOrEmpty(accessToken) ? null : "Bearer " + accessToken;

        var headers = _signer.Sign(
            method: method.Method,
            path: path,
            query: query,
            accept: accept,
            authorization: authorization,
            contentType: body is not null ? contentType : null,
            contentLength: body?.Length,
            contentDigest: contentDigest,
            idempotencyKey: idempotencyKey,
            upvestClientId: _settings.ClientId);

        request.Headers.TryAddWithoutValidation("accept", accept);
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);
        request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        // Select v15 of the signing protocol (content-digest). Not covered by the signature,
        // and not included in the content-digest computation.
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("authorization", authorization);
        }
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("idempotency-key", idempotencyKey);
        }
        request.Headers.TryAddWithoutValidation("signature", headers.Signature);
        request.Headers.TryAddWithoutValidation("signature-input", headers.SignatureInput);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            // Note: response bodies may contain personal data — never logged.
            _logger.LogInformation("Upvest {Method} {Path} -> {Status}.", method.Method, path, (int)response.StatusCode);
            return new UpvestRawResponse((int)response.StatusCode, response.IsSuccessStatusCode, responseBody);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Upvest {Method} {Path} transport error: {Message}", method.Method, path, ex.Message);
            throw new UpvestApiException($"Transport error calling Upvest {method.Method} {path}.", 0, ex);
        }
    }
}
