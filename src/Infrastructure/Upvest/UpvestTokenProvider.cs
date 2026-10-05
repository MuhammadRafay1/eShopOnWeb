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
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Obtains and caches an OAuth2 client-credentials access token from Upvest. The token request
/// is itself signed (via <see cref="UpvestRequestSigner"/>) but carries no bearer token, which is
/// why it bypasses the DelegatingHandler and uses its own client.
/// </summary>
public sealed class UpvestTokenProvider : IUpvestTokenProvider, IDisposable
{
    // Every scope this integration uses across onboarding and investing.
    private const string Scopes =
        "users:admin users:read accounts:admin accounts:read orders:admin orders:read " +
        "virtual_cash_balances:admin webhooks:admin webhooks:read checks:admin checks:read " +
        "taxes:admin taxes:read instruments:read";

    private readonly UpvestSettings _settings;
    private readonly UpvestRequestSigner _signer;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public UpvestTokenProvider(IOptions<UpvestSettings> settings, UpvestRequestSigner signer)
    {
        _settings = settings.Value;
        _signer = signer;
        _httpClient = new HttpClient();
        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _cachedToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _cachedToken;

            var form =
                $"client_id={Uri.EscapeDataString(_settings.ClientId)}" +
                $"&client_secret={Uri.EscapeDataString(_settings.ClientSecret)}" +
                "&grant_type=client_credentials" +
                $"&scope={Uri.EscapeDataString(Scopes)}";
            var body = Encoding.UTF8.GetBytes(form);

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_httpClient.BaseAddress!, "/auth/token"))
            {
                Content = new ByteArrayContent(body)
            };
            request.Content.Headers.TryAddWithoutValidation("content-type", "application/x-www-form-urlencoded");
            await _signer.SignAsync(request, cancellationToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            var token = root.GetProperty("access_token").GetString()!;
            var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;

            _cachedToken = token;
            // Refresh a minute before actual expiry.
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
            return token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _lock.Dispose();
    }
}
