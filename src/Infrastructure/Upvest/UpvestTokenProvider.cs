using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Requests.AccessTokens;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Supplies (and caches) the OAuth bearer token for Upvest calls.</summary>
public interface IUpvestTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Fetches the client-credentials token through the SDK's <c>AccessTokens.IssueToken</c> operation (which is
/// itself signed by <see cref="UpvestSigningHandler"/>) and caches it until shortly before expiry. The SDK
/// client is resolved lazily to break the handler&lt;-&gt;client construction cycle; the token call re-enters the
/// handler, which detects the token endpoint and signs it without requiring a bearer.
/// </summary>
public sealed class UpvestTokenProvider : IUpvestTokenProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UpvestOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public UpvestTokenProvider(IServiceProvider serviceProvider, IOptions<UpvestOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _accessToken;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _accessToken;

            var client = _serviceProvider.GetRequiredService<UpvestInvestmentApiClient>();
            var clientGuid = Guid.Parse(_options.ClientId);

            try
            {
                var response = await client.AccessTokens.IssueToken(new IssueTokenRequest
                {
                    UpvestClientId = clientGuid,
                    Signature = SigningPlaceholder,
                    SignatureInput = SigningPlaceholder,
                    ClientId = clientGuid,
                    ClientSecret = _options.ClientSecret,
                    Scope = _options.Scopes,
                }, cancellationToken: cancellationToken);

                _accessToken = response.AccessToken;
                _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, response.ExpiresIn - 60));
                return _accessToken;
            }
            catch (ApiException ex)
            {
                throw new UpvestApiException("Could not obtain an Upvest access token.", (int)ex.StatusCode, innerException: ex);
            }
            catch (SdkException ex)
            {
                throw new UpvestApiException("Could not reach Upvest to obtain an access token.", innerException: ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // Placeholder header values; the signing handler computes and overwrites the real signature headers.
    internal const string SigningPlaceholder = "AAAA";
}
