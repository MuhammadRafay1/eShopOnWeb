using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Obtains and caches an OAuth2 access token (client-credentials grant) from Upvest.
/// The token request is itself HTTP-signed by the signing handler on the named client.
/// Tokens are reused until shortly before expiry. The client secret is never logged.
/// </summary>
public sealed class UpvestTokenProvider
{
    public const string AuthHttpClientName = "upvest-auth";

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

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Refresh a minute before expiry to avoid using a token that lapses mid-flight.
        if (_cachedToken != null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-60))
            return _cachedToken;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedToken != null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-60))
                return _cachedToken;

            var client = _httpClientFactory.CreateClient(AuthHttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/token")
            {
                Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", _settings.ClientId),
                    new KeyValuePair<string, string>("client_secret", _settings.ClientSecret),
                    new KeyValuePair<string, string>("scope", _settings.Scopes),
                })
            };
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // The token endpoint's error body is OAuth metadata (error / error_description /
                // message) and carries no personal data, so it is safe to surface for diagnostics.
                var reason = ExtractTokenError(payload);
                throw new UpvestApiException(response.StatusCode,
                    $"Failed to obtain Upvest access token (HTTP {(int)response.StatusCode}){reason}.");
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var token = root.GetProperty("access_token").GetString()
                        ?? throw new UpvestApiException(response.StatusCode, "Upvest token response had no access_token.");
            var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;

            _cachedToken = token;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string ExtractTokenError(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            foreach (var key in new[] { "error_description", "message", "detail", "title", "error" })
            {
                if (root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                    return $": {v.GetString()}";
            }
        }
        catch
        {
            // Non-JSON body – ignore.
        }
        return string.Empty;
    }
}
