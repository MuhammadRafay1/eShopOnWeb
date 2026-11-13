using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Issues and caches OAuth2 client-credentials access tokens for Upvest, one per scope set.
/// The token request is itself HTTP-message-signed (via <see cref="UpvestRequestExecutor"/>).
/// </summary>
public class UpvestTokenProvider
{
    private readonly UpvestRequestExecutor _executor;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestTokenProvider> _logger;

    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UpvestTokenProvider(
        UpvestRequestExecutor executor,
        IOptions<UpvestSettings> settings,
        ILogger<UpvestTokenProvider> logger)
    {
        _executor = executor;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns a valid access token for the given space-delimited <paramref name="scope"/>.
    /// Throws <see cref="UpvestApiException"/> if Upvest refuses to issue one (e.g. the client
    /// is not authorised for the requested scopes).
    /// </summary>
    public async Task<string> GetTokenAsync(string scope, CancellationToken cancellationToken, bool forceRefresh = false)
    {
        if (!forceRefresh && _cache.TryGetValue(scope, out var cached) && cached.IsValid)
        {
            return cached.AccessToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _cache.TryGetValue(scope, out cached) && cached.IsValid)
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

    /// <summary>Invalidate the cached token for a scope (e.g. after a 401 at usage time).</summary>
    public void Invalidate(string scope) => _cache.TryRemove(scope, out _);

    private async Task<CachedToken> RequestTokenAsync(string scope, CancellationToken cancellationToken)
    {
        var form =
            "client_id=" + Uri.EscapeDataString(_settings.ClientId) +
            "&client_secret=" + Uri.EscapeDataString(_settings.ClientSecret) +
            "&grant_type=client_credentials" +
            "&scope=" + Uri.EscapeDataString(scope);

        var body = Encoding.UTF8.GetBytes(form);

        var response = await _executor.SendAsync(
            HttpMethod.Post,
            "/auth/token",
            query: null,
            body: body,
            contentType: "application/x-www-form-urlencoded",
            accessToken: null,
            idempotencyKey: null,
            cancellationToken);

        if (!response.IsSuccess)
        {
            var summary = UpvestProblem.Summarize(response.Body, response.StatusCode);
            _logger.LogWarning("Upvest token request for scopes '{Scope}' failed: {Status}.", scope, response.StatusCode);
            throw new UpvestApiException($"Could not obtain an Upvest access token ({summary}).", response.StatusCode);
        }

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new UpvestApiException("Upvest token response had no access_token.", response.StatusCode);
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;

        // Refresh a minute early to avoid races near expiry.
        var expiry = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
        return new CachedToken(accessToken, expiry);
    }

    private readonly record struct CachedToken(string AccessToken, DateTimeOffset ExpiresAt)
    {
        public bool IsValid => DateTimeOffset.UtcNow < ExpiresAt;
    }
}
