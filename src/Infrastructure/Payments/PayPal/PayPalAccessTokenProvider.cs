using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Payments.PayPal;

public interface IPayPalAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct);
}

/// <summary>
/// Obtains and caches an OAuth2 client-credentials access token from PayPal. There is no refresh
/// flow - the token is simply re-requested before it expires. A lock serialises concurrent
/// refreshes so a burst of first requests makes a single token call.
/// </summary>
public class PayPalAccessTokenProvider : IPayPalAccessTokenProvider
{
    private const string CacheKey = "PayPal_AccessToken";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<PayPalOptions> _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PayPalAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public PayPalAccessTokenProvider(IHttpClientFactory httpClientFactory,
        IOptions<PayPalOptions> options, IMemoryCache cache,
        ILogger<PayPalAccessTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached!;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(CacheKey, out string? cachedAfterWait) && !string.IsNullOrEmpty(cachedAfterWait))
            {
                return cachedAfterWait!;
            }

            return await RequestNewTokenAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> RequestNewTokenAsync(CancellationToken ct)
    {
        var options = _options.Value;
        var client = _httpClientFactory.CreateClient(PayPalHttpClientNames.PayPal);

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token");
        var basic = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PayPalIntegrationException("Failed to reach PayPal to obtain an access token.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal token request failed with status {Status}", (int)response.StatusCode);
            throw new PayPalIntegrationException(
                $"PayPal token request failed with status {(int)response.StatusCode}.");
        }

        TokenResponse? token;
        try
        {
            token = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(body);
        }
        catch (Exception ex)
        {
            throw new PayPalIntegrationException("Could not parse PayPal token response.", ex);
        }

        if (token is null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new PayPalIntegrationException("PayPal token response did not contain an access token.");
        }

        // Refresh a minute before actual expiry to avoid using a token that expires mid-flight.
        var lifetime = TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn - 60));
        _cache.Set(CacheKey, token.AccessToken, lifetime);
        return token.AccessToken;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
