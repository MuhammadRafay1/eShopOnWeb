using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public interface IUpvestTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Obtains and caches an OAuth2 client-credentials access token from Upvest. The token request is
/// sent through the shared authenticating handler (which signs it without a bearer), so it is
/// authenticated by exactly the same pipeline as every other call.
/// </summary>
public sealed class UpvestTokenProvider : IUpvestTokenProvider
{
    public const string HttpClientName = "upvest-token";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public UpvestTokenProvider(IHttpClientFactory httpClientFactory, IOptions<UpvestSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _cachedToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _cachedToken;
            }

            var (token, expiresInSeconds) = await RequestTokenAsync(cancellationToken);
            _cachedToken = token;
            // Refresh a minute before the real expiry.
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresInSeconds - 60));
            return token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<(string Token, int ExpiresIn)> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var scope = string.Join(" ", UpvestSettings.Scopes);
        var body =
            $"client_id={Uri.EscapeDataString(_settings.ClientId)}" +
            $"&client_secret={Uri.EscapeDataString(_settings.ClientSecret)}" +
            $"&grant_type=client_credentials" +
            $"&scope={Uri.EscapeDataString(scope)}";

        // Relative path: the token client's BaseAddress + authenticating handler apply automatically.
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/token")
        {
            Content = new StringContent(body, Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Do not include the request body (it carries the client secret) in the error.
            throw new HttpRequestException($"Upvest token request failed with status {(int)response.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var token = root.GetProperty("access_token").GetString()
                    ?? throw new InvalidOperationException("Upvest token response had no access_token.");
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;
        return (token, expiresIn);
    }
}
