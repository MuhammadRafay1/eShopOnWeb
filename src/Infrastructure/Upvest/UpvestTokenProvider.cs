using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Issues OAuth2 client-credentials access tokens for a given scope set, cached until expiry.</summary>
public interface IUpvestTokenProvider
{
    Task<string> GetTokenAsync(string scope, CancellationToken ct = default);
}

public sealed class UpvestTokenProvider : IUpvestTokenProvider
{
    /// <summary>Name of the dedicated HttpClient used to call the token endpoint (signed, no bearer).</summary>
    public const string HttpClientName = "upvest-auth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UpvestTokenProvider(IHttpClientFactory httpClientFactory, IOptions<UpvestSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<string> GetTokenAsync(string scope, CancellationToken ct = default)
    {
        if (TryGetFresh(scope, out var cached))
        {
            return cached;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (TryGetFresh(scope, out cached))
            {
                return cached;
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/token")
            {
                Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", _settings.ClientId),
                    new KeyValuePair<string, string>("client_secret", _settings.ClientSecret),
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", scope),
                })
            };

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw await UpvestError.FromResponseAsync(response, ct);
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var token = JsonSerializer.Deserialize<TokenResponse>(body)
                ?? throw new InvalidOperationException("Empty token response from Upvest.");

            var expiry = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, token.ExpiresIn));
            _cache[scope] = new CachedToken(token.AccessToken, expiry);
            return token.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryGetFresh(string scope, out string token)
    {
        if (_cache.TryGetValue(scope, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            token = entry.Token;
            return true;
        }
        token = string.Empty;
        return false;
    }

    private readonly record struct CachedToken(string Token, DateTimeOffset ExpiresAt);

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string TokenType { get; set; } = string.Empty;
    }
}
