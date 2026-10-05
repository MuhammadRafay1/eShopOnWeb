using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to "invest your change". The key is the shopper's own identity
/// (never another shopper's), so one row exists per shopper and a shopper only ever sees their own.
/// Personal details supplied at enrolment are sent to Upvest and are deliberately NOT persisted here.
/// </summary>
public class Enrolment : IAggregateRoot
{
    public const long InvestThresholdCents = 1000; // €10.00

    // Key = the shopper's identity from the JWT. In-memory and relational providers both enforce the
    // primary key, so a duplicate enrolment insert is refused by the store.
    public string ShopperId { get; private set; }

    /// <summary>Opaque public id returned to the caller as <c>enrolmentId</c> (never the shopper's identity).</summary>
    public Guid PublicId { get; private set; }

    public EnrolmentStage Stage { get; private set; }

    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }

    /// <summary>Spare change set aside and not yet invested, in euro cents.</summary>
    public long PendingCents { get; private set; }

    // Stable idempotency keys, generated once so a worker retry replays at Upvest rather than duplicating.
    public Guid CreateUserIdempotencyKey { get; private set; }
    public Guid SetTaxIdempotencyKey { get; private set; }
    public Guid CreateAccountGroupIdempotencyKey { get; private set; }
    public Guid CreateAccountIdempotencyKey { get; private set; }

    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Enrolment() { }
#pragma warning restore CS8618

    public Enrolment(string shopperId)
    {
        ShopperId = shopperId;
        PublicId = Guid.NewGuid();
        Stage = EnrolmentStage.AwaitingUserActivation;
        // One idempotency key per Upvest write, stable on this persisted row so that a retry of THIS
        // enrolment replays the same write. Paired with a stable request body (see ConsentTimestamp), a
        // retry is a true replay; a brand-new enrolment is a new attempt with new keys.
        CreateUserIdempotencyKey = Guid.NewGuid();
        SetTaxIdempotencyKey = Guid.NewGuid();
        CreateAccountGroupIdempotencyKey = Guid.NewGuid();
        CreateAccountIdempotencyKey = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>
    /// A stable timestamp for consent/confirmation fields sent to Upvest. Using the row's creation time
    /// (not "now") keeps the create-user request body byte-identical across retries, so the idempotency
    /// key replays rather than tripping a 422 on a changed body.
    /// </summary>
    public DateTimeOffset ConsentTimestamp => CreatedAt;

    public EnrolmentStatus Status => Stage switch
    {
        EnrolmentStage.Active => EnrolmentStatus.Active,
        EnrolmentStage.Rejected => EnrolmentStatus.Rejected,
        _ => EnrolmentStatus.Pending
    };

    /// <summary>A shopper may only invest once Upvest has accepted them (user + trading account active).</summary>
    public bool IsAccepted => Stage == EnrolmentStage.Active;

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void RecordUser(Guid userId) { UpvestUserId = userId; Touch(); }

    public void RecordAccounts(Guid accountGroupId, Guid accountId)
    {
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
        Stage = EnrolmentStage.AwaitingAccountActivation;
        Touch();
    }

    public void MarkActive()
    {
        Stage = EnrolmentStage.Active;
        LastError = null;
        Touch();
    }

    public void MarkRejected(string reason)
    {
        Stage = EnrolmentStage.Rejected;
        LastError = Truncate(reason);
        Touch();
    }

    public void RecordTransientError(string reason)
    {
        LastError = Truncate(reason);
        Touch();
    }

    /// <summary>Set aside the given round-up. Returns the whole balance to invest when it reaches €10, else 0.</summary>
    public long AddRoundUpAndMaybeInvest(long roundUpCents)
    {
        if (roundUpCents <= 0) return 0;
        PendingCents += roundUpCents;
        if (PendingCents < InvestThresholdCents) { Touch(); return 0; }
        var toInvest = PendingCents;
        PendingCents = 0; // the balance starts again from zero
        Touch();
        return toInvest;
    }

    /// <summary>A failed investment returns its amount to the set-aside balance so no change is lost.</summary>
    public void ReturnToPending(long cents)
    {
        if (cents > 0) { PendingCents += cents; Touch(); }
    }

    private static string Truncate(string s) => s.Length > 500 ? s[..500] : s;
}
