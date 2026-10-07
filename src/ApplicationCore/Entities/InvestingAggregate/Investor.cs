using System;
using System.Collections.Generic;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Aggregate root owning the shopper's
/// set-aside ledger and their investments. One per shopper (keyed by <see cref="BuyerId"/>).
///
/// The ledger is kept in whole euro cents. <see cref="PendingAmountCents"/> is the spare change set
/// aside but not yet invested; <see cref="InvestedAmountCents"/> is the total that has actually
/// settled into investments. No personal detail from the sign-up form is stored here — only the
/// Upvest references needed to act on the shopper's behalf.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The set-aside balance at which the whole balance is invested.</summary>
    public const long InvestmentThresholdCents = 1000; // €10

    #pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }

    public Investor(string buyerId, Guid upvestUserId, EnrolmentStatus status)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        BuyerId = buyerId;
        UpvestUserId = upvestUserId;
        Status = status;
    }

    /// <summary>The shopper's identity (their username / the JWT name claim).</summary>
    public string BuyerId { get; private set; }

    /// <summary>The shopper's Upvest user id.</summary>
    public Guid UpvestUserId { get; private set; }

    /// <summary>The Upvest trading account that holds investments, once created.</summary>
    public Guid? UpvestAccountId { get; private set; }

    /// <summary>The Upvest account group that holds cash, once created.</summary>
    public Guid? UpvestAccountGroupId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    /// <summary>Spare change set aside but not yet invested, in euro cents.</summary>
    public long PendingAmountCents { get; private set; }

    /// <summary>Total amount that has settled into investments, in euro cents.</summary>
    public long InvestedAmountCents { get; private set; }

    private readonly List<Investment> _investments = new();
    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    public decimal PendingAmount => PendingAmountCents / 100m;
    public decimal InvestedAmount => InvestedAmountCents / 100m;

    public void UpdateEnrolmentStatus(EnrolmentStatus status) => Status = status;

    public void SetUpvestAccount(Guid accountId, Guid accountGroupId)
    {
        UpvestAccountId = accountId;
        UpvestAccountGroupId = accountGroupId;
    }

    public bool HasUpvestAccount => UpvestAccountId.HasValue && UpvestAccountGroupId.HasValue;

    /// <summary>Set aside the round-up from a paid order. No-op for a non-positive amount.</summary>
    public void SetAside(long cents)
    {
        if (cents <= 0)
        {
            return;
        }
        PendingAmountCents += cents;
    }

    /// <summary>True when there is enough set aside to invest and the shopper is an accepted investor.</summary>
    public bool CanInvest => Status == EnrolmentStatus.Active && PendingAmountCents >= InvestmentThresholdCents;

    /// <summary>
    /// Move the whole set-aside balance into a freshly placed investment. The pending balance drops
    /// to zero; if the investment is already settled the invested total grows immediately.
    /// </summary>
    public Investment StartInvestment(Guid upvestOrderId, InvestmentStatus status)
    {
        Guard.Against.OutOfRange(PendingAmountCents, nameof(PendingAmountCents), 1, long.MaxValue);
        var amountCents = PendingAmountCents;
        PendingAmountCents = 0;

        var investment = new Investment(upvestOrderId, amountCents, status);
        _investments.Add(investment);

        if (status == InvestmentStatus.Settled)
        {
            InvestedAmountCents += amountCents;
        }
        return investment;
    }

    /// <summary>
    /// Reflect the outcome Upvest reported for an investment. Only a pending investment transitions:
    /// settling grows the invested total; failing returns its amount to the set-aside balance so it
    /// accrues towards the next investment. Idempotent.
    /// </summary>
    public void ApplyInvestmentOutcome(Investment investment, InvestmentStatus status)
    {
        Guard.Against.Null(investment, nameof(investment));
        if (investment.Status != InvestmentStatus.Pending || status == InvestmentStatus.Pending)
        {
            return;
        }

        if (status == InvestmentStatus.Settled)
        {
            InvestedAmountCents += investment.AmountCents;
        }
        else if (status == InvestmentStatus.Failed)
        {
            PendingAmountCents += investment.AmountCents;
        }
        investment.SetStatus(status);
    }
}
