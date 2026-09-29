using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Fetches and caches a PayPal OAuth2 client-credentials access token. The token request goes to
/// {baseUrl}/v1/oauth2/token (the tokenUrl declared in every spec's securitySchemes), against the
/// same base URL as every other call — including any BaseUrl override.
/// </summary>
public class PayPalAccessTokenProvider : IPayPalAccessTokenProvider
{
    private const string CacheKeyPrefix = "paypal-access-token::";
    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly PayPalOptions _options;
    private readonly IMemoryCache _cache;

    public PayPalAccessTokenProvider(HttpClient httpClient, IOptions<PayPalOptions> options, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
    }

    public async Task<string> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyPrefix + _options.ClientId;
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached!;
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out string? cachedAfterWait) && !string.IsNullOrEmpty(cachedAfterWait))
            {
                return cachedAfterWait!;
            }

            var token = await RequestTokenAsync(cancellationToken);
            // Refresh a little before the reported expiry to avoid using a token mid-expiry.
            var lifetime = TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn - 60));
            _cache.Set(cacheKey, token.AccessToken, lifetime);
            return token.AccessToken;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Never surface the request (it carries the client secret in the Basic header). Report
            // only the status so credential misconfiguration is diagnosable without leaking secrets.
            throw new PayPalApiException((int)response.StatusCode, "AUTH_ERROR",
                "Failed to obtain a PayPal access token. Check PayPal:ClientId / PayPal:ClientSecret.",
                null, null);
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(body);
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new PayPalApiException((int)response.StatusCode, "AUTH_ERROR",
                "PayPal returned an empty access token.", null, null);
        }
        return token;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
