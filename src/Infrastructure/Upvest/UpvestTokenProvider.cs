using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Acquires and caches OAuth2 client-credentials access tokens from Upvest, one per requested
/// scope string. Tokens are reused until shortly before they expire. The token request itself is
/// HTTP-message-signed (no bearer, form-encoded body).
/// </summary>
public sealed class UpvestTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly UpvestMessageSigner _signer;
    private readonly UpvestOptions _options;
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public UpvestTokenProvider(HttpClient httpClient, UpvestMessageSigner signer, IOptions<UpvestOptions> options)
    {
        _httpClient = httpClient;
        _signer = signer;
        _options = options.Value;
    }

    public async Task<string> GetTokenAsync(string scope, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(scope, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
            return cached.Token;

        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(scope, out cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
                return cached.Token;

            var fresh = await RequestTokenAsync(scope, ct).ConfigureAwait(false);
            _cache[scope] = fresh;
            return fresh.Token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<CachedToken> RequestTokenAsync(string scope, CancellationToken ct)
    {
        if (_httpClient.BaseAddress is null)
            throw new UpvestApiException(500, null, "Upvest base URL is not configured (Upvest:BaseUrl).");

        string body =
            "grant_type=client_credentials" +
            "&client_id=" + Uri.EscapeDataString(_options.ClientId) +
            "&client_secret=" + Uri.EscapeDataString(_options.ClientSecret) +
            "&scope=" + Uri.EscapeDataString(scope);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_httpClient.BaseAddress!, "/auth/token"));
        request.Content = new ByteArrayContent(bodyBytes);
        request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
        _signer.Sign(request, bodyBytes, "application/x-www-form-urlencoded", bearerToken: null, idempotencyKey: null);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw UpvestErrorMapper.ToException((int)response.StatusCode, responseBody);

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        string token = root.GetProperty("access_token").GetString()
            ?? throw new UpvestApiException(502, null, "Upvest token response did not contain an access token.");
        int expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;
        return new CachedToken(token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    private readonly record struct CachedToken(string Token, DateTimeOffset ExpiresAt);
}
