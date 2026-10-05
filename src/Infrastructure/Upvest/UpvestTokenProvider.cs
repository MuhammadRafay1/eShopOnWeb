using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Obtains and caches the OAuth2 client-credentials access token. The token request itself goes
/// through the same authenticating handler (it is signed, but carries no bearer token).
/// </summary>
public sealed class UpvestTokenProvider : IDisposable
{
    public const string HttpClientName = "Upvest";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public UpvestTokenProvider(IHttpClientFactory httpClientFactory, IOptions<UpvestOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        // Refresh a little before expiry to avoid using a token that dies mid-flight.
        if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt - TimeSpan.FromSeconds(30))
            return _accessToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt - TimeSpan.FromSeconds(30))
                return _accessToken;

            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["grant_type"] = "client_credentials",
                    ["scope"] = _options.Scopes,
                })
            };

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Upvest returned an empty access-token response.");

            _accessToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(token.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
