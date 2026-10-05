using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single delegating handler through which every Upvest call passes. It
/// authenticates each call: it lazily obtains and caches an OAuth access token
/// (itself a signed call to the token endpoint), attaches it as a bearer token,
/// and signs the request with an HTTP message signature. No call site attaches
/// credentials itself.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    // Every scope this integration needs, requested once per token.
    private const string Scopes =
        "users:read users:admin accounts:admin accounts:read checks:admin taxes:admin " +
        "webhooks:admin webhooks:read orders:admin orders:read instruments:read " +
        "positions:read virtual_cash_balances:admin";

    private readonly UpvestRequestSigner _signer;
    private readonly UpvestTokenStore _tokenStore;
    private readonly UpvestOptions _options;

    public UpvestAuthenticationHandler(UpvestRequestSigner signer, UpvestTokenStore tokenStore, IOptions<UpvestOptions> options)
    {
        _signer = signer;
        _tokenStore = tokenStore;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (request.Method != HttpMethod.Get && !request.Headers.Contains("idempotency-key"))
        {
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        }

        await _signer.SignAsync(request, ct);
        return await base.SendAsync(request, ct);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_tokenStore.TryGet(out var cached)) return cached;

        await _tokenStore.Gate.WaitAsync(ct);
        try
        {
            if (_tokenStore.TryGet(out cached)) return cached;

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.BaseUrl), "/auth/token"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["grant_type"] = "client_credentials",
                    ["scope"] = Scopes,
                })
            };
            // Signed, but no Authorization header — this *is* the token request.
            await _signer.SignAsync(request, ct);
            using var response = await base.SendAsync(request, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Upvest token request failed with status {(int)response.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(payload);
            var token = doc.RootElement.GetProperty("access_token").GetString()
                ?? throw new HttpRequestException("Upvest token response had no access_token.");
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 300;
            _tokenStore.Set(token, expiresIn);
            return token;
        }
        finally
        {
            _tokenStore.Gate.Release();
        }
    }
}
