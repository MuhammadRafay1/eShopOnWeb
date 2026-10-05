using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Requests.AccessTokens;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Supplies a valid OAuth bearer token for Upvest API calls, acquiring and caching it as needed.</summary>
public interface IUpvestTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Acquires the OAuth token via the SDK's <c>AccessTokens.IssueToken</c> operation (the documented token
/// endpoint) and caches it in-memory until shortly before expiry. The token request flows through the same
/// signing <see cref="UpvestAuthDelegatingHandler"/> as every other call; the client secret is passed from
/// configuration and never logged.
/// </summary>
public sealed class UpvestTokenProvider : IUpvestTokenProvider
{
    // The scopes this integration needs: onboard users, run checks, set tax residencies, manage accounts, place orders.
    private const string Scope = "users:admin accounts:admin orders:admin instruments:read checks:admin taxes:admin virtual_cash_balances:admin";
    private const string SigPlaceholder = "placeholder";
    private static readonly TimeSpan ExpiryGuard = TimeSpan.FromSeconds(30);

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public UpvestTokenProvider(UpvestInvestmentApiClient client, IOptions<UpvestSettings> options)
    {
        _client = client;
        _settings = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (IsFresh()) return _token!;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh()) return _token!;

            var response = await _client.AccessTokens.IssueToken(new IssueTokenRequest
            {
                UpvestClientId = Guid.Parse(_settings.ClientId),
                // The delegating handler fills the signature headers; placeholders satisfy the required members.
                Signature = SigPlaceholder,
                SignatureInput = SigPlaceholder,
                ClientId = Guid.Parse(_settings.ClientId),
                ClientSecret = _settings.ClientSecret,
                Scope = Scope,
            }, cancellationToken: cancellationToken);

            _token = response.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsFresh() => _token is not null && DateTimeOffset.UtcNow < _expiresAt - ExpiryGuard;
}
