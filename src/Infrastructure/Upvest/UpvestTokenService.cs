using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Obtains and caches OAuth2 client-credentials access tokens from Upvest, one per requested scope
/// set. The token request is itself signed (via the signing handler on the named HTTP client).
/// </summary>
public sealed class UpvestTokenService
{
    public const string HttpClientName = "upvest-auth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UpvestTokenService(IHttpClientFactory httpClientFactory, IOptions<UpvestSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<string> GetTokenAsync(string scope, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(scope, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Token;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(scope, out cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return cached.Token;
            }

            var token = await RequestTokenAsync(scope, ct);
            _cache[scope] = token;
            return token.Token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<CachedToken> RequestTokenAsync(string scope, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _settings.ClientId),
            new KeyValuePair<string, string>("client_secret", _settings.ClientSecret),
            new KeyValuePair<string, string>("scope", scope),
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/token") { Content = form };

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is not UpvestApiException)
        {
            throw new UpvestApiException("auth/token", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await UpvestProblem.ReadDetailAsync(response, ct);
            throw new UpvestApiException("auth/token", (int)response.StatusCode, detail);
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            ?? throw new UpvestApiException("auth/token", (int)response.StatusCode, "Empty token response.");

        var lifetime = TimeSpan.FromSeconds(Math.Max(0, payload.ExpiresIn - 60));
        return new CachedToken(payload.AccessToken, DateTimeOffset.UtcNow.Add(lifetime));
    }

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("scope")] string Scope);
}
