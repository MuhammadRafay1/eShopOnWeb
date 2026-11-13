using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The application's own record of a shopper who has signed up to hold a managed investment
/// portfolio at Upvest. Keyed by <see cref="ShopperId"/> (the signed-in shopper's identity from
/// the JWT), so one shopper can have at most one investor record and a concurrent second sign-up
/// is refused by the store's primary key. Holds only the Upvest resource identifiers and the
/// idempotency keys needed to reconcile each onboarding step — never the shopper's personal
/// details, which are sent to Upvest and never persisted or logged here.
/// </summary>
public class Investor : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string shopperId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        ShopperId = shopperId;
        InvestorId = Guid.NewGuid();
        Status = InvestorStatus.Pending;
        CreatedDate = DateTimeOffset.UtcNow;

        // Real, caller-stable idempotency keys persisted at claim time and replayed on retry,
        // so a resend of any onboarding step replays rather than duplicates at Upvest.
        UserIdempotencyKey = Guid.NewGuid();
        TaxIdempotencyKey = Guid.NewGuid();
        AccountGroupIdempotencyKey = Guid.NewGuid();
        AccountIdempotencyKey = Guid.NewGuid();
        ConfigurationIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>The signed-in shopper's identity (JWT name claim). Primary key.</summary>
    public string ShopperId { get; private set; }

    /// <summary>The application-facing investor id returned to the caller.</summary>
    public Guid InvestorId { get; private set; }

    public InvestorStatus Status { get; private set; }

    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }

    /// <summary>The model portfolio the investor currently follows, if chosen.</summary>
    public string? ChosenModelId { get; private set; }

    public Guid UserIdempotencyKey { get; private set; }
    public Guid TaxIdempotencyKey { get; private set; }
    public Guid AccountGroupIdempotencyKey { get; private set; }
    public Guid AccountIdempotencyKey { get; private set; }
    public Guid ConfigurationIdempotencyKey { get; private set; }

    public DateTimeOffset CreatedDate { get; private set; }

    /// <summary>A short, non-personal note about the last onboarding outcome (e.g. a provider status).</summary>
    public string? Note { get; private set; }

    public void SetUpvestUserId(Guid userId) => UpvestUserId = userId;
    public void SetAccountGroupId(Guid accountGroupId) => UpvestAccountGroupId = accountGroupId;
    public void SetAccountId(Guid accountId) => UpvestAccountId = accountId;

    public void SetStatus(InvestorStatus status) => Status = status;

    public void SetNote(string? note) => Note = note;

    public void ChooseModel(string modelId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        ChosenModelId = modelId;
    }

    /// <summary>True once Upvest has accepted the shopper and their account is ready to hold a portfolio.</summary>
    public bool IsReadyToInvest => Status == InvestorStatus.Active && UpvestAccountId.HasValue;
}
