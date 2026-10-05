using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Everything the shop holds on a single shopper's behalf for the "invest your change"
/// capability: their enrolment with Upvest, the change set aside but not yet invested,
/// and the investments made for them. This is the aggregate root — all state transitions
/// go through its methods so the invariants (money is never lost, one enrolment per
/// shopper, set-aside is idempotent per order) hold.
///
/// It belongs to exactly one shopper, identified by <see cref="BuyerId"/>, and is only
/// ever loaded and returned for that shopper — one shopper never sees another's.
/// No personal detail from the sign-up form is stored here; those are sent to Upvest and
/// only the resulting Upvest identifiers are kept.
/// </summary>
public class InvestingAccount : BaseEntity, IAggregateRoot
{
    /// <summary>The set-aside balance at (or above) which the whole balance is invested.</summary>
    public const decimal InvestmentThreshold = 10m;

    private readonly List<Investment> _investments = new();
    private readonly List<SpareChangeEntry> _spareChangeEntries = new();

#pragma warning disable CS8618 // Required by Entity Framework
    private InvestingAccount() { }
#pragma warning restore CS8618

    public InvestingAccount(string buyerId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        BuyerId = buyerId;
        EnrolmentId = Guid.NewGuid();
        Status = EnrolmentStatus.Pending;
        PendingAmount = 0m;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>The shopper this account belongs to (their user name / email).</summary>
    public string BuyerId { get; private set; }

    /// <summary>Stable identifier exposed to API callers as <c>enrolmentId</c>.</summary>
    public Guid EnrolmentId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    public string? UpvestUserId { get; private set; }

    public string? UpvestAccountGroupId { get; private set; }

    public string? UpvestAccountId { get; private set; }

    /// <summary>Change set aside from paid orders but not yet invested, in euros.</summary>
    public decimal PendingAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    public IReadOnlyCollection<SpareChangeEntry> SpareChangeEntries => _spareChangeEntries.AsReadOnly();

    /// <summary>Total invested on the shopper's behalf so far (excludes failed investments).</summary>
    public decimal InvestedAmount =>
        _investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.Amount);

    /// <summary>True once Upvest has accepted the shopper and an account exists to hold investments.</summary>
    public bool IsAcceptedInvestor =>
        Status == EnrolmentStatus.Active && !string.IsNullOrEmpty(UpvestAccountId);

    /// <summary>True when there is enough set aside to make an investment.</summary>
    public bool IsReadyToInvest => IsAcceptedInvestor && PendingAmount >= InvestmentThreshold;

    public void LinkUpvestUser(string upvestUserId)
    {
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        UpvestUserId = upvestUserId;
        Touch();
    }

    public void LinkUpvestAccounts(string accountGroupId, string accountId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
        Touch();
    }

    /// <summary>Record that Upvest has accepted the shopper as an investor.</summary>
    public void MarkActive()
    {
        // Rejection is terminal; never flip a rejected enrolment back to active.
        if (Status == EnrolmentStatus.Rejected)
        {
            return;
        }

        if (Status != EnrolmentStatus.Active)
        {
            Status = EnrolmentStatus.Active;
            Touch();
        }
    }

    /// <summary>Record that Upvest will not take the shopper on.</summary>
    public void MarkRejected()
    {
        if (Status != EnrolmentStatus.Rejected)
        {
            Status = EnrolmentStatus.Rejected;
            Touch();
        }
    }

    /// <summary>
    /// Set aside the change from a paid order: the difference between its total and the next
    /// whole euro. Does nothing (returns 0) unless the shopper is an accepted investor, and is
    /// idempotent per order so the same order never sets aside twice.
    /// </summary>
    /// <returns>The amount set aside, in euros (0 when nothing was set aside).</returns>
    public decimal SetAsideForOrder(int orderId, decimal orderTotal)
    {
        if (!IsAcceptedInvestor)
        {
            return 0m;
        }

        if (_spareChangeEntries.Any(e => e.OrderId == orderId))
        {
            return 0m;
        }

        var roundUp = RoundUpToWholeEuro(orderTotal);
        _spareChangeEntries.Add(new SpareChangeEntry(orderId, roundUp, orderTotal));
        PendingAmount += roundUp;
        Touch();
        return roundUp;
    }

    /// <summary>
    /// The difference between an order total and the next whole euro. Zero when the total is
    /// already a whole number of euros. e.g. 12.30 -> 0.70, 15.00 -> 0.00.
    /// </summary>
    public static decimal RoundUpToWholeEuro(decimal orderTotal)
    {
        var total = Math.Round(orderTotal, 2, MidpointRounding.AwayFromZero);
        var roundUp = Math.Ceiling(total) - total;
        return Math.Round(roundUp, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Move the whole set-aside balance into a new pending investment. The balance resets to
    /// zero and future set-aside accrues towards the next investment.
    /// </summary>
    public Investment BeginInvestment()
    {
        if (!IsReadyToInvest)
        {
            throw new InvalidOperationException(
                "Cannot begin an investment: the shopper is not an accepted investor or has not reached the threshold.");
        }

        var amount = Math.Round(PendingAmount, 2, MidpointRounding.AwayFromZero);
        var investment = new Investment(amount);
        _investments.Add(investment);
        PendingAmount = 0m;
        Touch();
        return investment;
    }

    public void AttachOrderToInvestment(Guid investmentPublicId, string upvestOrderId)
    {
        var investment = FindInvestment(investmentPublicId);
        investment.AttachOrder(upvestOrderId);
        Touch();
    }

    public void SettleInvestment(Guid investmentPublicId)
    {
        FindInvestment(investmentPublicId).Settle();
        Touch();
    }

    /// <summary>
    /// Mark an investment as failed. Its amount returns to the set-aside balance so it can
    /// accrue towards a later investment — money is never lost.
    /// </summary>
    public void FailInvestment(Guid investmentPublicId)
    {
        var investment = FindInvestment(investmentPublicId);
        if (investment.Fail())
        {
            PendingAmount += investment.Amount;
            Touch();
        }
    }

    private Investment FindInvestment(Guid investmentPublicId)
    {
        var investment = _investments.FirstOrDefault(i => i.PublicId == investmentPublicId);
        Guard.Against.Null(investment, nameof(investmentPublicId));
        return investment;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
