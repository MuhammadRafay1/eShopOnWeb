using System;
using System.Threading;

namespace Microsoft.eShopWeb.PublicApi.Investing.Upvest;

/// <summary>
/// Singleton cache for the current Upvest OAuth2 access token, shared across all calls.
/// </summary>
public sealed class UpvestTokenStore
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAtUtc = DateTimeOffset.MinValue;

    public SemaphoreSlim RefreshLock => _refreshLock;

    public bool TryGet(out string token)
    {
        token = _accessToken ?? string.Empty;
        return _accessToken is not null && DateTimeOffset.UtcNow < _expiresAtUtc;
    }

    public void Set(string token, int expiresInSeconds)
    {
        _accessToken = token;
        // Refresh a minute early to avoid using a token that expires mid-flight.
        var buffer = Math.Min(60, Math.Max(0, expiresInSeconds - 5));
        _expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds - buffer);
    }
}
