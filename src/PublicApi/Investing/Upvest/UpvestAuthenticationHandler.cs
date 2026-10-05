using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Investing.Upvest;

/// <summary>
/// The single, reusable <see cref="DelegatingHandler"/> through which every call to Upvest passes.
/// It authenticates each call end to end: it signs the request (v15 HTTP message signatures) and
/// attaches a cached OAuth2 bearer token, fetching a new token (itself a signed request) when needed.
/// No call site attaches credentials itself.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    // Exactly the scopes this integration uses: onboarding, funding and trading.
    private const string Scopes =
        "users:admin users:read checks:admin accounts:admin accounts:read taxes:admin " +
        "webhooks:admin webhooks:read orders:admin orders:read virtual_cash_balances:admin";

    private static readonly JsonSerializerOptions TokenJson = new(JsonSerializerDefaults.Web);

    private readonly UpvestMessageSigner _signer;
    private readonly UpvestTokenStore _tokenStore;
    private readonly UpvestOptions _options;

    public UpvestAuthenticationHandler(
        UpvestMessageSigner signer,
        UpvestTokenStore tokenStore,
        IOptions<UpvestOptions> options)
    {
        _signer = signer;
        _tokenStore = tokenStore;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Accept.Count == 0)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        var isTokenRequest = request.RequestUri?.AbsolutePath.EndsWith("/auth/token", StringComparison.Ordinal) == true;

        if (!isTokenRequest)
        {
            if (request.Method != HttpMethod.Get && !request.Headers.Contains("idempotency-key"))
            {
                request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
            }

            var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        await _signer.SignAsync(request, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokenStore.TryGet(out var cached))
        {
            return cached;
        }

        await _tokenStore.RefreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tokenStore.TryGet(out cached))
            {
                return cached;
            }

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post,
                new Uri(new Uri(_options.BaseUrl, UriKind.Absolute), "/auth/token"));
            tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // The token endpoint expects an application/x-www-form-urlencoded body.
            var form = new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["grant_type"] = "client_credentials",
                ["scope"] = Scopes
            };
            tokenRequest.Content = new FormUrlEncodedContent(form);

            // The token request must itself be signed (but carries no bearer token).
            await _signer.SignAsync(tokenRequest, cancellationToken).ConfigureAwait(false);

            using var response = await base.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpvestException(
                    $"Upvest token request failed with status {(int)response.StatusCode}.", response.StatusCode);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<TokenResponse>(TokenJson, cancellationToken).ConfigureAwait(false);
            if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
            {
                throw new UpvestException("Upvest token response did not contain an access token.");
            }

            _tokenStore.Set(payload.AccessToken, payload.ExpiresIn);
            return payload.AccessToken;
        }
        finally
        {
            _tokenStore.RefreshLock.Release();
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
        [JsonPropertyName("scope")] public string? Scope { get; set; }
    }
}
