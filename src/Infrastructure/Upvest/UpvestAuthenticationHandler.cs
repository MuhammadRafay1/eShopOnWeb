using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single, reusable <see cref="DelegatingHandler"/> through which every call to Upvest passes.
/// It authenticates each call end to end: it obtains and caches an OAuth access token, attaches the
/// <c>Authorization</c> bearer header (except on the token request itself), and signs every request
/// with an HTTP message signature. No call site attaches any credentials.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    // Scopes required across enrolment, accounts, payments, orders, instruments and webhooks.
    private const string Scopes =
        "users:admin users:read accounts:admin accounts:read orders:admin orders:read " +
        "payments:admin payments:read instruments:read webhooks:admin checks:admin taxes:admin positions:read";

    private readonly UpvestSettings _settings;
    private readonly UpvestMessageSigner _signer;
    private readonly UpvestTokenStore _tokenStore;

    public UpvestAuthenticationHandler(UpvestSettings settings, UpvestMessageSigner signer, UpvestTokenStore tokenStore)
    {
        _settings = settings;
        _signer = signer;
        _tokenStore = tokenStore;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        bool isTokenRequest = request.RequestUri!.AbsolutePath.EndsWith("/auth/token", StringComparison.OrdinalIgnoreCase);

        if (!isTokenRequest)
        {
            var token = await _tokenStore.GetTokenAsync(FetchTokenAsync, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        await _signer.SignAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<(string Token, int ExpiresInSeconds)> FetchTokenAsync(CancellationToken cancellationToken)
    {
        var form =
            $"client_id={Uri.EscapeDataString(_settings.ClientId)}" +
            $"&client_secret={Uri.EscapeDataString(_settings.ClientSecret)}" +
            "&grant_type=client_credentials" +
            $"&scope={Uri.EscapeDataString(Scopes)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_settings.BaseUrl), "/auth/token"))
        {
            Content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded")
        };

        // Signed but intentionally without a bearer token (this is how the token is obtained).
        await _signer.SignAsync(request, cancellationToken);
        using var response = await base.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Upvest token request failed with status {(int)response.StatusCode}.");

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var token = root.GetProperty("access_token").GetString()
            ?? throw new HttpRequestException("Upvest token response did not contain an access_token.");
        var expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) ? seconds : 1800;
        return (token, expiresIn);
    }
}
