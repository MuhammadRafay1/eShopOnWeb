using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Requests.AccessTokens;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Acquires the OAuth client-credentials token via the SDK's <c>AccessTokens.IssueToken</c> operation (which
/// is signed by the same DelegatingHandler) and caches it in-memory until shortly before it expires. The
/// client is resolved lazily to avoid a construction-time cycle (client → handler → token provider).
/// </summary>
public sealed class UpvestTokenProvider : IUpvestTokenProvider, IDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _services;
    private readonly UpvestOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public UpvestTokenProvider(IServiceProvider services, IOptions<UpvestOptions> options)
    {
        _services = services;
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt - RefreshSkew)
            return _token;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt - RefreshSkew)
                return _token;

            var client = _services.GetRequiredService<UpvestInvestmentApiClient>();
            var response = await client.AccessTokens.IssueToken(new IssueTokenRequest
            {
                UpvestClientId = _options.ClientIdGuid,
                Signature = string.Empty,       // the DelegatingHandler supplies the real signature
                SignatureInput = string.Empty,
                ClientId = _options.ClientIdGuid,
                ClientSecret = _options.ClientSecret,
                Scope = _options.Scopes,
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

    public void Dispose() => _gate.Dispose();
}
