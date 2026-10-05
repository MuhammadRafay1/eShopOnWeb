using System;
using System.Threading;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Caches the current Upvest OAuth access token across requests. Registered as
/// a singleton so the token survives the (transient, pooled) delegating handler.
/// </summary>
public sealed class UpvestTokenStore
{
    public SemaphoreSlim Gate { get; } = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public bool TryGet(out string token)
    {
        token = _token ?? string.Empty;
        return _token is not null && DateTimeOffset.UtcNow < _expiresAt;
    }

    public void Set(string token, int expiresInSeconds)
    {
        _token = token;
        // Renew a minute early to avoid using a token that expires mid-flight.
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, expiresInSeconds - 60));
    }
}
