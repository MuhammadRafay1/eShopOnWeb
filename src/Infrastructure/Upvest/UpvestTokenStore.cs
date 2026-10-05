using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Caches the Upvest OAuth access token and reuses it until shortly before it expires, fetching a
/// fresh one only when needed. Shared across requests as a singleton.
/// </summary>
public sealed class UpvestTokenStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetTokenAsync(Func<CancellationToken, Task<(string Token, int ExpiresInSeconds)>> fetch, CancellationToken cancellationToken)
    {
        if (IsValid())
            return _token!;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsValid())
                return _token!;

            var (token, expiresIn) = await fetch(cancellationToken);
            _token = token;
            // Refresh a minute early to avoid using a token that expires mid-flight.
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
            return _token;
        }
        finally { _gate.Release(); }
    }

    private bool IsValid() => _token is not null && DateTimeOffset.UtcNow < _expiresAt;
}
