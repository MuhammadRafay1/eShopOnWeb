using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.AccessTokens;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Acquires and caches the OAuth2 bearer token used to authenticate Upvest calls. The token request itself
/// is a signed call to the provider's token endpoint; signing is applied by the shared authentication
/// handler, so this provider attaches no credentials of its own beyond the client id/secret in the body.
/// </summary>
public sealed class UpvestAccessTokenProvider
{
    // Every scope the integration's operations need, requested in one token.
    private const string Scopes =
        "users:admin users:read checks:admin taxes:admin accounts:admin accounts:read " +
        "webhooks:admin virtual_cash_balances:admin orders:admin orders:read instruments:read";

    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly UpvestSettings _settings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public UpvestAccessTokenProvider(IServiceProvider serviceProvider, IOptions<UpvestSettings> settings)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var current = _cachedToken;
        if (current is not null && DateTimeOffset.UtcNow < _expiresAt - ExpirySkew)
        {
            return current;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt - ExpirySkew)
            {
                return _cachedToken;
            }

            // Resolve the client lazily — it depends (via its HttpClient handler) on this provider.
            var client = _serviceProvider.GetRequiredService<UpvestInvestmentApiClient>();
            var clientId = Guid.Parse(_settings.ClientId);

            var response = await client.AccessTokens.IssueToken(
                new IssueTokenRequest
                {
                    UpvestClientId = clientId,
                    ClientId = clientId,
                    ClientSecret = _settings.ClientSecret,
                    Scope = Scopes,
                    GrantType = "client_credentials",
                    UpvestApiVersion = UpvestApiVersion._1,
                    // Placeholders (non-empty/format-valid) — the authentication handler computes and sets the
                    // real signature headers before the request goes out.
                    Signature = "placeholder",
                    SignatureInput = "placeholder",
                },
                cancellationToken: cancellationToken);

            _cachedToken = response.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Drops the cached token so the next call re-acquires one (used on a 401).</summary>
    public void Invalidate()
    {
        _cachedToken = null;
        _expiresAt = DateTimeOffset.MinValue;
    }
}
