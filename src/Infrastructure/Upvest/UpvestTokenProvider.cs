using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Upvest;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Obtains and caches OAuth client-credentials access tokens from Upvest, one per scope set.
/// Tokens are reused until they near expiry. A minimal scope is requested for each call so that
/// operations the sandbox client is authorised for keep working even when others are not.
/// </summary>
public class UpvestTokenProvider
{
    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UpvestTokenProvider(IHttpClientFactory httpClientFactory, IOptions<UpvestSettings> options)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(string scope, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(scope, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60))
        {
            return cached.AccessToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(scope, out cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60))
            {
                return cached.AccessToken;
            }

            var token = await RequestTokenAsync(scope, cancellationToken);
            _cache[scope] = token;
            return token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<CachedToken> RequestTokenAsync(string scope, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(UpvestHttp.ClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/token")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _settings.ClientId),
                new KeyValuePair<string, string>("client_secret", _settings.ClientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", scope)
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The client secret is in the request, never in the response; surface only the status.
            throw new UpvestApiException((int)response.StatusCode, $"issue-token (scope {scope})");
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new UpvestApiException((int)response.StatusCode, "issue-token", "no access_token in response");
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;

        return new CachedToken(accessToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }
}
