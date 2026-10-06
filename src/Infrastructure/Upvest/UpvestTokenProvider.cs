using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public interface IUpvestTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Obtains and caches an OAuth2 client-credentials access token from Upvest. The token request is
/// itself made through the shared, signing <see cref="UpvestAuthenticationHandler"/> (it carries
/// no bearer, only a signature), so no credentials are attached at this call site.
/// </summary>
public sealed class UpvestTokenProvider : IUpvestTokenProvider
{
    // Every scope this integration needs across all flows, requested on a single token.
    private const string Scopes =
        "users:admin users:read checks:admin checks:read accounts:admin accounts:read " +
        "orders:admin orders:read payments:admin payments:read virtual_cash_balances:admin " +
        "taxes:admin instruments:read webhooks:admin webhooks:read";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt;

    public UpvestTokenProvider(IHttpClientFactory httpClientFactory, IOptions<UpvestSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _cachedToken;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _cachedToken;
            }

            var client = _httpClientFactory.CreateClient(UpvestHttpClient.Name);
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _settings.ClientId,
                    ["client_secret"] = _settings.ClientSecret,
                    ["grant_type"] = "client_credentials",
                    ["scope"] = Scopes,
                })
            };

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Upvest returned an empty token response.");

            _cachedToken = token.AccessToken;
            // Refresh a minute early to avoid using a token that expires mid-request.
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, token.ExpiresIn - 60));
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
