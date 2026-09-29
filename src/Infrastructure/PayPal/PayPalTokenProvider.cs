using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.Infrastructure.PayPal.Models;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Obtains and caches the PayPal OAuth client-credentials token. Cached behind a
/// <see cref="SemaphoreSlim"/> so concurrent callers don't all re-authenticate; refreshed ~60s
/// before it expires. Registered as a singleton; uses <see cref="IHttpClientFactory"/> so it does
/// not capture a pooled handler.
/// </summary>
public class PayPalTokenProvider : IPayPalTokenProvider
{
    public const string HttpClientName = "PayPalToken";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PayPalOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAtUtc = DateTimeOffset.MinValue;

    public PayPalTokenProvider(IHttpClientFactory httpClientFactory, IOptions<PayPalOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAtUtc)
            return _cachedToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAtUtc)
                return _cachedToken;

            var token = await RequestTokenAsync(cancellationToken);
            _cachedToken = token.AccessToken;
            // Refresh a minute early to avoid using a token that expires mid-request.
            _expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, token.ExpiresIn - 60));
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<PayPalTokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(PayPalUrlResolver.Resolve(_options));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new PayPalApiException((int)response.StatusCode, "oauth_error",
                "Failed to obtain a PayPal access token.", null, Redact(body));
        }

        var token = JsonSerializer.Deserialize<PayPalTokenResponse>(body);
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
            throw new PayPalApiException((int)response.StatusCode, "oauth_error",
                "PayPal returned an empty access token.", null, null);

        return token;
    }

    // Never let a raw token-endpoint body flow into an exception message unbounded.
    private static string Redact(string body) => body.Length > 500 ? body[..500] : body;
}
