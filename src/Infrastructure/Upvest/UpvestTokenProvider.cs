using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Acquires and caches the Upvest OAuth2 access token. The token endpoint itself must be HTTP-signed and
/// expects the client credentials in the form body (not Basic auth), so the SDK's built-in OAuth cannot be
/// used here — this provider builds and signs the token request directly. Uses its own HttpClient so it does
/// not recurse through the authentication handler.
/// </summary>
public sealed class UpvestTokenProvider
{
    public const string HttpClientName = "upvest-token";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestRequestSigner _signer;
    private readonly UpvestOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public UpvestTokenProvider(IHttpClientFactory httpClientFactory, UpvestRequestSigner signer, UpvestOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _signer = signer;
        _options = options;
    }

    public void Invalidate()
    {
        _lock.Wait();
        try { _token = null; }
        finally { _lock.Release(); }
    }

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        var cached = _token;
        if (cached is not null && DateTimeOffset.UtcNow < _expiresAt)
            return cached;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _token;

            var (token, expiresIn) = await FetchAsync(ct).ConfigureAwait(false);
            _token = token;
            // Refresh a minute before expiry to avoid using a token that lapses mid-flight.
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, expiresIn - 60));
            return token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<(string Token, int ExpiresIn)> FetchAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/auth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = _options.Scopes,
            })
        };

        await _signer.SignAsync(request, ct).ConfigureAwait(false);

        var http = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Upvest token request failed with status {(int)response.StatusCode}.");

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var token = root.GetProperty("access_token").GetString()
                    ?? throw new InvalidOperationException("Upvest token response had no access_token.");
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;
        return (token, expiresIn);
    }
}
