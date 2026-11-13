using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Process-wide cache of OAuth2 access tokens, keyed by scope, with per-scope locking so concurrent
/// callers don't each mint a fresh token. Tokens only ever live in memory.
/// </summary>
public sealed class UpvestTokenCache
{
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expiry)> _tokens = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public bool TryGet(string scope, out string token)
    {
        if (_tokens.TryGetValue(scope, out var entry) && entry.Expiry > DateTimeOffset.UtcNow)
        {
            token = entry.Token;
            return true;
        }
        token = string.Empty;
        return false;
    }

    public void Set(string scope, string token, DateTimeOffset expiry) => _tokens[scope] = (token, expiry);

    public void Invalidate(string scope) => _tokens.TryRemove(scope, out _);

    public SemaphoreSlim LockFor(string scope) => _locks.GetOrAdd(scope, _ => new SemaphoreSlim(1, 1));
}
