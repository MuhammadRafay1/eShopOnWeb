using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Upvest;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Obtains and caches OAuth client-credentials access tokens from Upvest, one per scope set. Tokens
/// are reused until shortly before they expire. The token request is itself signed (by the shared
/// signing handler on the named HttpClient). The client secret is sent only in the token request
/// body and is never logged.
/// </summary>
public sealed class UpvestTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UpvestTokenProvider(IHttpClientFactory httpClientFactory, IOptions<UpvestSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<string> GetTokenAsync(string scope, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(scope, out var cached) && cached.ExpiresAtUtc > DateTimeOffset.UtcNow)
            return cached.AccessToken;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(scope, out cached) && cached.ExpiresAtUtc > DateTimeOffset.UtcNow)
                return cached.AccessToken;

            var token = await RequestTokenAsync(scope, ct);
            _cache[scope] = token;
            return token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<CachedToken> RequestTokenAsync(string scope, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(UpvestHttpClient.Name);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret,
                ["scope"] = scope
            })
        };
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);

        using var response = await client.SendAsync(request, ct);
        string payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw UpvestErrors.FromResponse((int)response.StatusCode, payload, $"Upvest token request for scope '{scope}' failed");

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        string accessToken = root.GetProperty("access_token").GetString()!;
        int expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;

        // Refresh a minute early to avoid races near expiry.
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
        return new CachedToken(accessToken, expiresAt);
    }

    private readonly record struct CachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
}
