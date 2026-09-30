using System;
using System.Threading;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Holds the cached OAuth2 access token shared across requests. Registered as a singleton so
/// the token (and the lock that guards refreshing it) outlives any single typed-HttpClient
/// instance handed out by IHttpClientFactory.
/// </summary>
public class PayPalAccessTokenCache
{
    public SemaphoreSlim Lock { get; } = new SemaphoreSlim(1, 1);
    public string? Token { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.MinValue;

    public bool IsValid => Token is not null && DateTimeOffset.UtcNow < ExpiresAt;
}
