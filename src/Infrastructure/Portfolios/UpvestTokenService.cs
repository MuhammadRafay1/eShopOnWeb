using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolios;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Portfolios;

/// <summary>
/// Obtains and caches scoped OAuth2 access tokens (client-credentials grant). One token is cached per
/// distinct scope string; expired tokens are refreshed. The client secret is read from configuration
/// and never logged. The token request is itself HTTP-message-signed, as Upvest requires.
/// </summary>
public sealed class UpvestTokenService
{
    private readonly UpvestHttpClient _http;
    private readonly UpvestSettings _settings;
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UpvestTokenService(UpvestHttpClient http, IOptions<UpvestSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
    }

    /// <summary>Returns a valid access token carrying exactly <paramref name="scope"/>.</summary>
    public async Task<string> GetTokenAsync(string scope, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(scope, out var cached) && cached.IsValid)
            return cached.AccessToken;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(scope, out cached) && cached.IsValid)
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
        var form = new StringBuilder();
        form.Append("client_id=").Append(Uri.EscapeDataString(_settings.ClientId));
        form.Append("&client_secret=").Append(Uri.EscapeDataString(_settings.ClientSecret));
        form.Append("&grant_type=client_credentials");
        form.Append("&scope=").Append(Uri.EscapeDataString(scope));

        byte[] body = Encoding.UTF8.GetBytes(form.ToString());

        var response = await _http.SendAsync(
            HttpMethod.Post, "/auth/token", query: null, body: body,
            contentType: "application/x-www-form-urlencoded",
            bearerToken: null, withIdempotencyKey: false, ct);

        if (!response.IsSuccess)
            throw new UpvestApiException(response.Status, UpvestErrors.Describe(response, $"token request for scope '{scope}'"));

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;
        string accessToken = root.GetProperty("access_token").GetString()
            ?? throw new UpvestApiException(response.Status, "Upvest token response did not contain an access_token.");
        int expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var secs) ? secs : 1800;

        // Refresh a minute early to avoid using a token that expires mid-flight.
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
        return new CachedToken(accessToken, expiresAt);
    }

    private readonly record struct CachedToken(string AccessToken, DateTimeOffset ExpiresAt)
    {
        public bool IsValid => DateTimeOffset.UtcNow < ExpiresAt;
    }
}
