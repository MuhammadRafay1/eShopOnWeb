using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Thin, low-level HTTP client for the PayPal REST API: OAuth token caching, common headers
/// (idempotency, Prefer, Accept/Content-Type) and error translation into
/// <see cref="PayPalGatewayException"/>. Wire-format JSON only - never exposed outside Infrastructure.
/// </summary>
public class PayPalClient
{
    private const string TokenCacheKey = "PayPal:AccessToken";

    private readonly HttpClient _httpClient;
    private readonly PayPalOptions _options;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public PayPalClient(HttpClient httpClient, IOptions<PayPalOptions> options, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
    }

    public async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string path, object? body, string? idempotencyKey, CancellationToken cancellationToken)
        where TResponse : class
    {
        var response = await SendCoreAsync(method, path, body, idempotencyKey, cancellationToken);
        if (response.Content.Headers.ContentLength is 0 or null)
            return null!;

        var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
        return result ?? throw new PayPalGatewayException("PayPal returned an empty response body.");
    }

    public async Task SendAsync(HttpMethod method, string path, object? body, string? idempotencyKey, CancellationToken cancellationToken)
    {
        await SendCoreAsync(method, path, body, idempotencyKey, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var response = await SendOnceAsync(method, path, body, idempotencyKey, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _cache.Remove(TokenCacheKey);
            response = await SendOnceAsync(method, path, body, idempotencyKey, cancellationToken);
        }

        if (response.IsSuccessStatusCode)
            return response;

        var error = await response.Content.ReadFromJsonAsync<PpError>(cancellationToken: cancellationToken.CanBeCanceled ? cancellationToken : default)
                    ?? new PpError();
        var firstDetail = error.Details?.Count > 0 ? error.Details[0] : null;
        var issue = firstDetail?.Issue;
        var message = string.IsNullOrEmpty(error.Message)
            ? $"PayPal request failed with status {(int)response.StatusCode}."
            : $"{error.Name}: {error.Message}";
        if (firstDetail is not null)
            message += $" ({firstDetail.Issue}{(firstDetail.Field is null ? "" : $" on '{firstDetail.Field}'")}: {firstDetail.Description})";
        throw new PayPalGatewayException(message, error.DebugId, issue, (int)response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, object? body, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Prefer", "return=representation");
        if (!string.IsNullOrEmpty(idempotencyKey))
            request.Headers.Add("PayPal-Request-Id", idempotencyKey);

        if (body is not null)
            request.Content = JsonContent.Create(body);
        else if (method == HttpMethod.Post)
            request.Content = JsonContent.Create(new { });

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(TokenCacheKey, out string? cachedToken) && cachedToken is not null)
            return cachedToken;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(TokenCacheKey, out cachedToken) && cachedToken is not null)
                return cachedToken;

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new PayPalGatewayException("Failed to obtain a PayPal access token.", statusCode: (int)response.StatusCode);

            var token = await response.Content.ReadFromJsonAsync<PpTokenResponse>(cancellationToken: cancellationToken)
                        ?? throw new PayPalGatewayException("PayPal token response was empty.");

            _cache.Set(TokenCacheKey, token.AccessToken, TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn - 60)));
            return token.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
