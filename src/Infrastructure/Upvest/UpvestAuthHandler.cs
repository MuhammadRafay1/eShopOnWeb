using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The one reusable handler through which every call to Upvest is authenticated. It is the only place
/// credentials are applied: no call site attaches any. For every outgoing request it adds the
/// <c>upvest-client-id</c> header and an HTTP Message Signature (via <see cref="UpvestRequestSigner"/>);
/// for every request other than the token endpoint it also attaches a cached OAuth bearer token, which it
/// obtains with its own signed call to <c>/auth/token</c>.
/// </summary>
public sealed class UpvestAuthHandler : DelegatingHandler
{
    private readonly UpvestRequestSigner _signer;
    private readonly UpvestSettings _settings;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiry = DateTimeOffset.MinValue;

    public UpvestAuthHandler(UpvestRequestSigner signer, UpvestSettings settings, TimeProvider? clock = null)
    {
        _signer = signer;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isTokenEndpoint = request.RequestUri?.AbsolutePath.EndsWith("/auth/token", StringComparison.Ordinal) == true;

        // The bearer token is a signed component off the token endpoint, so it must be attached before signing.
        if (!isTokenEndpoint)
        {
            var token = await GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        await PrepareAndSignAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task PrepareAndSignAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("upvest-client-id");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);

        byte[]? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        _signer.Sign(request, body);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (IsTokenFresh()) return _accessToken!;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (IsTokenFresh()) return _accessToken!;

            using var tokenRequest = BuildTokenRequest();
            await PrepareAndSignAsync(tokenRequest, cancellationToken);

            using var response = await base.SendAsync(tokenRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Do not echo the response body — it can carry sensitive detail.
                throw new HttpRequestException(
                    $"Upvest token request failed with HTTP {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            (_accessToken, _accessTokenExpiry) = ParseToken(payload);
            return _accessToken!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private HttpRequestMessage BuildTokenRequest()
    {
        var uri = _settings.BaseUrl.TrimEnd('/') + "/auth/token";
        return new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _settings.ClientId),
                new KeyValuePair<string, string>("client_secret", _settings.ClientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", _settings.Scope),
            })
        };
    }

    private (string token, DateTimeOffset expiry) ParseToken(byte[] payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("access_token", out var tokenElement)
            || tokenElement.GetString() is not { Length: > 0 } token)
        {
            throw new HttpRequestException("Upvest token response did not contain an access token.");
        }

        // RFC 6749 expires_in is RECOMMENDED but optional; default conservatively when absent.
        var lifetime = root.TryGetProperty("expires_in", out var expiresElement)
            && expiresElement.TryGetInt32(out var seconds)
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.FromMinutes(5);

        return (token, _clock.GetUtcNow().Add(lifetime));
    }

    private bool IsTokenFresh() =>
        _accessToken is not null && _clock.GetUtcNow() < _accessTokenExpiry - TimeSpan.FromSeconds(30);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tokenLock.Dispose();
        }
        base.Dispose(disposing);
    }
}
