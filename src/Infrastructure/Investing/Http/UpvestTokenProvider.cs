using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Http;

/// <summary>
/// Obtains and caches an OAuth2 client-credentials access token. The token request goes through
/// the same named <see cref="HttpClient"/> as every other Upvest call, so it is signed by the
/// authenticating handler; the handler special-cases the token endpoint and does not try to add a
/// bearer token to it, which is what stops this from recursing.
/// </summary>
public sealed class UpvestTokenProvider : IUpvestTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestTokenProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public UpvestTokenProvider(IHttpClientFactory httpClientFactory, IOptions<UpvestSettings> options, ILogger<UpvestTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (IsCurrent())
        {
            return _accessToken!;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsCurrent())
            {
                return _accessToken!;
            }

            var client = _httpClientFactory.CreateClient(UpvestConstants.HttpClientName);
            using var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _settings.ClientId),
                new KeyValuePair<string, string>("client_secret", _settings.ClientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", UpvestConstants.Scopes)
            });

            using var response = await client.PostAsync("/auth/token", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Upvest token request failed with {Status}: {Error}", (int)response.StatusCode, error);
                response.EnsureSuccessStatusCode();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            _accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;
            // Refresh a minute early to avoid using a token that expires mid-flight.
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));

            return _accessToken!;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate()
    {
        _accessToken = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    private bool IsCurrent() => _accessToken is not null && DateTimeOffset.UtcNow < _expiresAt;
}
