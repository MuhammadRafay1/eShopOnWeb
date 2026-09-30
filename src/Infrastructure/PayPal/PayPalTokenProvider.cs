using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.Infrastructure.PayPal.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Obtains and caches an OAuth2 client-credentials bearer token from PayPal's token endpoint
/// (declared as the Oauth2 security scheme's tokenUrl in every PayPal spec we consume).
/// Uses a dedicated HttpClient with no auth handler attached, to avoid recursing into itself.
/// </summary>
public class PayPalTokenProvider : IPayPalTokenProvider
{
    private const string CacheKey = "PayPal:AccessToken";

    private readonly HttpClient _httpClient;
    private readonly PayPalOptions _options;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public PayPalTokenProvider(HttpClient httpClient, IOptions<PayPalOptions> options, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
    }

    public void InvalidateCache() => _cache.Remove(CacheKey);

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<string>(CacheKey, out var cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue<string>(CacheKey, out cached) && !string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            var tokenUrl = _options.ResolveBaseUrl() + "/v1/oauth2/token";
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
            var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                // Never include request headers/body (contains the client secret) in the exception.
                throw new PayPalGatewayException(response.StatusCode, "oauth_error", "Failed to obtain a PayPal access token.", null);
            }

            var token = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(body)
                ?? throw new PayPalGatewayException(response.StatusCode, "oauth_error", "PayPal token response was empty.", null);

            var expiresIn = token.ExpiresIn > 90 ? token.ExpiresIn - 60 : token.ExpiresIn;
            _cache.Set(CacheKey, token.AccessToken, TimeSpan.FromSeconds(Math.Max(expiresIn, 30)));

            return token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}
