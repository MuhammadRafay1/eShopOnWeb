using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public sealed record AcquiredClaim(PaymentOperationClaim Claim, bool Resumed, bool AlreadySucceeded);

/// <summary>
/// Takes the durable claim that must precede every provider write (claim → provider call → record result).
/// The store's primary key refuses a second claim for the same operation; an operation whose earlier attempt
/// ended in an unknown outcome (or whose holder died) is resumed by exactly one request, which must settle
/// the earlier attempt before re-issuing anything.
/// </summary>
public class PaymentClaimCoordinator
{
    /// <summary>An in-progress claim older than this is assumed abandoned (its request is long past its budget).</summary>
    public static readonly TimeSpan InProgressTimeout = TimeSpan.FromMinutes(2);

    private readonly IPaymentStore _store;
    private readonly TimeProvider _clock;

    public PaymentClaimCoordinator(IPaymentStore store, TimeProvider clock)
    {
        _store = store;
        _clock = clock;
    }

    public static string AuthorizeKey(int orderId) => $"authorize:{orderId}";
    public static string CaptureKey(int orderId) => $"capture:{orderId}";
    public static string VoidKey(int orderId) => $"void:{orderId}";
    public static string RefundKey(int orderId, string idempotencyKey) => $"refund:{orderId}:{idempotencyKey}";
    public static string SaveCardKey(string buyerId, string idempotencyKey) => $"vault:{Hash(buyerId)}:{idempotencyKey}";

    public async Task<AcquiredClaim> AcquireAsync(string key, string operation, string buyerId, int? orderId,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var claim = new PaymentOperationClaim(key, operation, buyerId, orderId, now);
        if (await _store.TryAddClaimAsync(claim, cancellationToken))
            return new AcquiredClaim(claim, Resumed: false, AlreadySucceeded: false);

        var existing = await _store.GetClaimAsync(key, cancellationToken)
            ?? throw new PaymentConflictException($"Another {operation} request just finished; retry the request.");

        if (existing.State == PaymentClaimState.Succeeded)
            return new AcquiredClaim(existing, Resumed: false, AlreadySucceeded: true);

        if (!existing.IsStale(now, InProgressTimeout))
            throw new PaymentConflictException($"A {operation} request for this payment is already in progress; retry shortly.");

        existing.Resume(now);
        if (!await _store.TrySaveClaimAsync(existing, cancellationToken))
            throw new PaymentConflictException($"Another request is already resuming this {operation}; retry shortly.");

        return new AcquiredClaim(existing, Resumed: true, AlreadySucceeded: false);
    }

    public async Task SucceedAsync(PaymentOperationClaim claim, string? resultReference, CancellationToken cancellationToken)
    {
        claim.Succeed(resultReference, _clock.GetUtcNow());
        await _store.TrySaveClaimAsync(claim, cancellationToken);
    }

    public async Task MarkUnknownAsync(PaymentOperationClaim claim, CancellationToken cancellationToken)
    {
        claim.MarkUnknown(_clock.GetUtcNow());
        await _store.TrySaveClaimAsync(claim, cancellationToken);
    }

    public async Task ReleaseAsync(PaymentOperationClaim claim, CancellationToken cancellationToken)
    {
        if (claim.IsReleased) return;
        await _store.ReleaseClaimAsync(claim, cancellationToken);
        claim.MarkReleased();
    }

    /// <summary>
    /// Called when a request fails while still holding a claim. If a provider write may have gone out, the claim is
    /// parked as Unknown so the next request for the same operation settles it from the provider first; if nothing
    /// reached the provider, the claim is released so the operation can simply be retried.
    /// </summary>
    public async Task AbandonAsync(PaymentOperationClaim claim)
    {
        if (!claim.IsHeld) return;
        if (claim.ProviderMayHaveActed)
            await MarkUnknownAsync(claim, CancellationToken.None);
        else
            await ReleaseAsync(claim, CancellationToken.None);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24].ToLowerInvariant();
}
