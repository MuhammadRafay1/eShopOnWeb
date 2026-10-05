using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities;

public enum PaymentClaimState
{
    InProgress = 0,
    Succeeded = 1,
    /// <summary>The provider call failed in a way that leaves its outcome unknown; settled before any re-issue.</summary>
    Unknown = 2
}

/// <summary>
/// A durable claim on one payment operation (authorize order 42, capture order 42, refund order 42 under key K…).
/// The <see cref="Key"/> is the primary key, so the store itself refuses a second claim for the same operation —
/// a double-click or a racing instance never reaches the provider twice. The claim also carries the
/// provider idempotency key (<see cref="ProviderRequestId"/>) so a re-issue after an unknown outcome is
/// de-duplicated by the provider too.
/// </summary>
public class PaymentOperationClaim : IAggregateRoot
{
    public const int MaxKeyLength = 200;

    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentOperationClaim() { }

    public PaymentOperationClaim(string key, string operation, string buyerId, int? orderId, DateTimeOffset now)
    {
        Guard.Against.NullOrEmpty(key, nameof(key));
        Guard.Against.OutOfRange(key.Length, nameof(key), 1, MaxKeyLength);

        Key = key;
        Operation = operation;
        BuyerId = buyerId;
        OrderId = orderId;
        ProviderRequestId = Guid.NewGuid().ToString("N");
        State = PaymentClaimState.InProgress;
        CreatedAt = now;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }

    public string Key { get; private set; }
    public string Operation { get; private set; }
    public string BuyerId { get; private set; }
    public int? OrderId { get; private set; }
    public string ProviderRequestId { get; private set; }
    public PaymentClaimState State { get; private set; }
    /// <summary>What the operation produced (e.g. the refund id or saved payment method id).</summary>
    public string? ResultReference { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    /// <summary>Concurrency token: taking over a stale/unknown claim succeeds for exactly one request.</summary>
    public Guid Version { get; private set; }

    /// <summary>Set (in memory only) once the claim row has been deleted so the operation can be retried.</summary>
    public bool IsReleased { get; private set; }

    /// <summary>The claim is still held by this request: neither completed, given up, nor parked as unknown.</summary>
    public bool IsHeld => State == PaymentClaimState.InProgress && !IsReleased;

    public void MarkReleased() => IsReleased = true;

    /// <summary>
    /// Set (in memory only) once a provider write may have been sent under this claim — by this request, or by the
    /// earlier attempt a resumed claim inherits. A held claim that may have reached the provider is parked as
    /// Unknown on failure; one that never did is simply released.
    /// </summary>
    public bool ProviderMayHaveActed { get; private set; }

    public void MarkProviderMayHaveActed() => ProviderMayHaveActed = true;

    public bool IsStale(DateTimeOffset now, TimeSpan inProgressTimeout) =>
        State == PaymentClaimState.Unknown ||
        (State == PaymentClaimState.InProgress && now - UpdatedAt > inProgressTimeout);

    public void Resume(DateTimeOffset now)
    {
        Transition(PaymentClaimState.InProgress, ResultReference, now);
        ProviderMayHaveActed = true;
    }

    public void Succeed(string? resultReference, DateTimeOffset now) => Transition(PaymentClaimState.Succeeded, resultReference, now);

    public void MarkUnknown(DateTimeOffset now) => Transition(PaymentClaimState.Unknown, ResultReference, now);

    private void Transition(PaymentClaimState state, string? resultReference, DateTimeOffset now)
    {
        State = state;
        ResultReference = resultReference;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }
}
