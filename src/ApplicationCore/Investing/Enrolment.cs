using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// A shopper's enrolment as an Upvest investor. Belongs to exactly one shopper
/// (<see cref="BuyerId"/>) and carries only the identifiers Upvest handed back —
/// never the shopper's personal details, which are passed straight through to
/// Upvest and never persisted or logged here.
/// </summary>
public class Enrolment : BaseEntity, IAggregateRoot
{
    public string BuyerId { get; private set; } = default!;
    public EnrolmentStatus Status { get; private set; }
    public Guid UpvestUserId { get; private set; }
    public Guid? AccountGroupId { get; private set; }
    public Guid? AccountId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Enrolment() { }

    public Enrolment(string buyerId, Guid upvestUserId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        BuyerId = buyerId;
        UpvestUserId = upvestUserId;
        Status = EnrolmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void AttachAccountGroup(Guid accountGroupId) => AccountGroupId = accountGroupId;
    public void AttachAccount(Guid accountId) => AccountId = accountId;
    public void MarkActive() => Status = EnrolmentStatus.Active;
    public void MarkRejected() => Status = EnrolmentStatus.Rejected;

    /// <summary>A shopper can only invest once Upvest has accepted them.</summary>
    public bool CanInvest => Status == EnrolmentStatus.Active && AccountId.HasValue;
}
