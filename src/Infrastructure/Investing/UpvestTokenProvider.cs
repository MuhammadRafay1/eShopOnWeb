using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Requests.AccessTokens;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Acquires and caches the OAuth client-credentials access token via the SDK's AccessTokens.IssueToken
/// operation. The token endpoint call itself flows through the single authentication handler, which signs
/// it (the token endpoint takes no bearer). Cached in-memory with a safety skew before expiry.
/// </summary>
public sealed class UpvestTokenProvider : IUpvestTokenProvider
{
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly UpvestOptions _options;
    private readonly ILogger<UpvestTokenProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public UpvestTokenProvider(
        IServiceProvider serviceProvider,
        Microsoft.Extensions.Options.IOptions<UpvestOptions> options,
        ILogger<UpvestTokenProvider> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _cachedToken;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _cachedToken;

            // Resolved lazily to avoid a construction cycle (client → handler → token provider).
            var client = _serviceProvider.GetRequiredService<UpvestInvestmentApiClient>();

            try
            {
                var response = await client.AccessTokens.IssueToken(
                    new IssueTokenRequest
                    {
                        UpvestClientId = Guid.Parse(_options.ClientId),
                        // The single authentication handler fills these in on the wire.
                        Signature = string.Empty,
                        SignatureInput = string.Empty,
                        ClientId = Guid.Parse(_options.ClientId),
                        ClientSecret = _options.ClientSecret,
                        Scope = _options.Scope,
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                _cachedToken = response.AccessToken;
                var lifetime = response.ExpiresIn > 0 ? TimeSpan.FromSeconds(response.ExpiresIn) : TimeSpan.FromMinutes(5);
                _expiresAt = DateTimeOffset.UtcNow + lifetime - ExpirySkew;
                _logger.LogInformation("Acquired Upvest access token (expires in {Seconds}s).", response.ExpiresIn);
                return _cachedToken;
            }
            catch (ApiException ex)
            {
                _logger.LogError("Upvest token request failed with status {Status}.", (int)ex.StatusCode);
                throw new UpvestProviderException("Could not obtain a provider access token.", ex.StatusCode, ex);
            }
            catch (SdkException ex)
            {
                _logger.LogError("Upvest token request could not be completed: {Reason}.", ex.GetType().Name);
                throw new UpvestProviderException("Could not reach the provider to obtain an access token.", null, ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
