using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Acquires and caches the Upvest OAuth2 access token. The token request itself goes through the same
/// authenticated HTTP pipeline (the <see cref="UpvestAuthenticationHandler"/> signs it but does not try to
/// attach a bearer to the token endpoint), so every call to Upvest — token included — is signed by the one
/// handler. The client id/secret are read here only; the secret is never logged.
/// </summary>
public sealed class UpvestTokenProvider
{
    private const string ClientName = UpvestAuthenticationHandler.HttpClientName;
    private static readonly TimeSpan ExpiryGuard = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestOptions _options;
    private readonly ILogger<UpvestTokenProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public UpvestTokenProvider(IHttpClientFactory httpClientFactory, UpvestOptions options, ILogger<UpvestTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt - ExpiryGuard)
        {
            return _cachedToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt - ExpiryGuard)
            {
                return _cachedToken;
            }

            var (token, expiresIn) = await RequestTokenAsync(cancellationToken);
            _cachedToken = token;
            _expiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(expiresIn);
            _logger.LogInformation("Acquired Upvest access token (expires in {Seconds}s).", expiresIn);
            return token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<(string token, int expiresIn)> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(ClientName);
        var baseUrl = _options.BaseUrl.TrimEnd('/');

        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/auth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["grant_type"] = "client_credentials",
                ["scope"] = _options.Scopes,
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new UpvestUnavailableException("issue_token", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpvestApiException("issue_token", (int)response.StatusCode, SafeDetail(body));
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var token = root.GetProperty("access_token").GetString()
                        ?? throw new UpvestApiException("issue_token", (int)response.StatusCode, "no access_token in response");
            var expiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds) ? seconds : 300;
            return (token, expiresIn);
        }
    }

    private static string SafeDetail(string body)
    {
        // The token error body carries no secret (it is Upvest's own error envelope), but keep it short.
        return body.Length > 200 ? body[..200] : body;
    }
}
