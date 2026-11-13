using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Obtains and caches OAuth2 access tokens (client-credentials grant) for a given scope set. The
/// token request itself is HTTP-message-signed. Tokens are reused until shortly before expiry.
/// </summary>
public class UpvestTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestMessageSigner _signer;
    private readonly UpvestOptions _options;

    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UpvestTokenProvider(
        IHttpClientFactory httpClientFactory,
        UpvestMessageSigner signer,
        IOptions<UpvestOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _signer = signer;
        _options = options.Value;
    }

    public async Task<string> GetTokenAsync(string scope, CancellationToken ct)
    {
        if (_cache.TryGetValue(scope, out var cached) && cached.IsValid)
        {
            return cached.AccessToken;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(scope, out cached) && cached.IsValid)
            {
                return cached.AccessToken;
            }

            var token = await RequestTokenAsync(scope, ct);
            _cache[scope] = token;
            return token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Drops any cached token for a scope so the next call re-authenticates.</summary>
    public void Invalidate(string scope) => _cache.TryRemove(scope, out _);

    private async Task<CachedToken> RequestTokenAsync(string scope, CancellationToken ct)
    {
        var form =
            $"client_id={Uri.EscapeDataString(_options.ClientId)}" +
            $"&client_secret={Uri.EscapeDataString(_options.ClientSecret)}" +
            $"&grant_type=client_credentials" +
            $"&scope={Uri.EscapeDataString(scope)}";
        var body = Encoding.UTF8.GetBytes(form);
        const string contentType = "application/x-www-form-urlencoded";

        var headers = _signer.Build(
            method: "POST",
            path: "/auth/token",
            query: null,
            body: body,
            bearerToken: null,
            contentType: contentType,
            idempotencyKey: null);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri(), "/auth/token"));
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _options.ClientId);
        request.Headers.TryAddWithoutValidation("digest", headers.Digest);
        request.Headers.TryAddWithoutValidation("signature", headers.Signature);
        request.Headers.TryAddWithoutValidation("signature-input", headers.SignatureInput);

        var client = _httpClientFactory.CreateClient(UpvestHttp.ClientName);
        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new UpvestApiException((int)response.StatusCode, "auth.token", UpvestHttp.ExtractDetail(payload));
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()!;
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;

        // Refresh a minute before the real expiry to avoid using a token mid-flight that just died.
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
        return new CachedToken(accessToken, expiresAt);
    }

    private Uri BaseUri() => new(_options.BaseUrl, UriKind.Absolute);

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt)
    {
        public bool IsValid => DateTimeOffset.UtcNow < ExpiresAt;
    }
}
