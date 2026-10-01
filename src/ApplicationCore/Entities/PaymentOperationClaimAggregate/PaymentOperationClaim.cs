using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentOperationClaimAggregate;

/// <summary>
/// Duplicate-prevention claim for a payment write (authorize/capture/void/refund). A caller takes the
/// claim (insert) before making the PayPal call; a second caller for the same logical operation is
/// refused by the primary-key conflict on insert - enforced even by the EF InMemory provider - rather
/// than by a read-then-write race. The key is deterministic, e.g. "{orderId}:authorize" or
/// "{orderId}:refund:{idempotencyKey}".
/// </summary>
public class PaymentOperationClaim : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PaymentOperationClaim() { }

    public PaymentOperationClaim(string id)
    {
        Guard.Against.NullOrEmpty(id, nameof(id));
        Id = id;
        CreatedDate = DateTimeOffset.UtcNow;
    }

    public string Id { get; private set; }
    public DateTimeOffset CreatedDate { get; private set; }

    /// <summary>Set once the operation has settled, e.g. "completed", so a duplicate caller can be told
    /// to fetch the result rather than being silently refused.</summary>
    public string? Outcome { get; private set; }

    public void Complete(string? outcome = "completed")
    {
        Outcome = outcome;
    }
}
