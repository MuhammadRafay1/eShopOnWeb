using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Aggregate root owning the
/// shopper's enrolment, their set-aside ("pending") balance, and the investments made for them.
/// Everything here belongs to exactly one shopper, identified by <see cref="ShopperId"/>.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }

    public Investor(string shopperId, string upvestUserId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));

        PublicId = Guid.NewGuid();
        ShopperId = shopperId;
        UpvestUserId = upvestUserId;
        Status = EnrolmentStatus.Pending;
        PendingAmount = 0m;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable, externally shared identifier for this enrolment.</summary>
    public Guid PublicId { get; private set; }

    /// <summary>Identity of the owning shopper (the authenticated user name from the token).</summary>
    public string ShopperId { get; private set; }

    public string UpvestUserId { get; private set; }
    public string? UpvestAccountGroupId { get; private set; }
    public string? UpvestAccountId { get; private set; }
    public string? UpvestKycCheckId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    /// <summary>Euros set aside from paid orders but not yet invested.</summary>
    public decimal PendingAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<Investment> _investments = new();
    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    /// <summary>True once Upvest has provisioned the account that can hold investments.</summary>
    public bool IsProvisioned => !string.IsNullOrEmpty(UpvestAccountGroupId) && !string.IsNullOrEmpty(UpvestAccountId);

    /// <summary>Total invested so far: the sum of every investment that has not failed.</summary>
    public decimal InvestedAmount =>
        decimal.Round(_investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.Amount), 2);

    public void SetAccounts(string accountGroupId, string accountId)
    {
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
    }

    public void SetKycCheck(string checkId) => UpvestKycCheckId = checkId;

    public void Activate()
    {
        if (Status == EnrolmentStatus.Pending) Status = EnrolmentStatus.Active;
    }

    public void Reject()
    {
        if (Status == EnrolmentStatus.Pending) Status = EnrolmentStatus.Rejected;
    }

    /// <summary>
    /// Sets aside the round-up for a paid order: the difference between the order total and the
    /// next whole euro. A shopper who is not an accepted (active) investor sets aside nothing.
    /// Returns the amount actually set aside (0 when none).
    /// </summary>
    public decimal SetAsideChange(decimal orderTotal)
    {
        if (Status != EnrolmentStatus.Active) return 0m;

        var roundUp = decimal.Round(Math.Ceiling(orderTotal) - orderTotal, 2);
        if (roundUp <= 0m) return 0m;

        PendingAmount = decimal.Round(PendingAmount + roundUp, 2);
        return roundUp;
    }

    /// <summary>
    /// Whether the set-aside balance has reached the threshold, the account is ready, and there is
    /// no investment already in flight (a balance is only ever invested once at a time).
    /// </summary>
    public bool CanBeginInvestment(decimal threshold) =>
        Status == EnrolmentStatus.Active &&
        IsProvisioned &&
        PendingAmount >= threshold &&
        !_investments.Any(i => i.Status == InvestmentStatus.Pending);

    /// <summary>
    /// Creates a new investment for the whole set-aside balance. This does NOT yet reset the
    /// balance; the caller persists this, then calls <see cref="ResetPendingForInvestment"/> and
    /// persists again. (The two-step persistence avoids an in-memory-provider quirk where a root
    /// scalar change is dropped when saved together with a newly added child.)
    /// </summary>
    public Investment StartInvestment()
    {
        var investment = new Investment(PendingAmount);
        _investments.Add(investment);
        return investment;
    }

    /// <summary>Removes the invested amount from the set-aside balance.</summary>
    public void ResetPendingForInvestment(decimal amount)
    {
        PendingAmount = decimal.Round(PendingAmount - amount, 2);
        if (PendingAmount < 0m) PendingAmount = 0m;
    }

    public void ConfirmInvestmentPlaced(Investment investment, string upvestOrderId, InvestmentStatus status)
        => investment.MarkOrderPlaced(upvestOrderId, status);

    /// <summary>The investment could not be placed; return its amount to the set-aside balance.</summary>
    public void FailInvestment(Investment investment)
    {
        if (investment.Status == InvestmentStatus.Failed) return;
        investment.ApplyStatus(InvestmentStatus.Failed);
        PendingAmount = decimal.Round(PendingAmount + investment.Amount, 2);
    }

    /// <summary>
    /// Reflects the latest known outcome of an investment's Upvest order. A transition to
    /// <see cref="InvestmentStatus.Failed"/> returns the amount to the set-aside balance so it
    /// accrues towards the next investment.
    /// </summary>
    public void SettleInvestment(Investment investment, InvestmentStatus status)
    {
        if (investment.Status == status || investment.Status != InvestmentStatus.Pending) return;

        if (status == InvestmentStatus.Failed)
        {
            FailInvestment(investment);
        }
        else
        {
            investment.ApplyStatus(status);
        }
    }
}
