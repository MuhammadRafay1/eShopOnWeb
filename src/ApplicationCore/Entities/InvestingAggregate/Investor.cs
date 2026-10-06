using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Aggregate root that owns the shopper's
/// enrolment, set-aside ledger and investments. Everything here belongs to a single shopper
/// (<see cref="ShopperId"/>) and is never shared with another.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The balance must reach this many euros before it is invested.</summary>
    public const decimal InvestmentThreshold = 10m;

    private readonly List<Investment> _investments = new();

    #pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
    #pragma warning restore CS8618

    public Investor(string shopperId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        ShopperId = shopperId;
        EnrolmentId = Guid.NewGuid();
        Status = EnrolmentStatus.Pending;
        SetAsideAmount = 0m;
    }

    /// <summary>The signed-in shopper this investor belongs to (the JWT identity name).</summary>
    public string ShopperId { get; private set; }

    /// <summary>The identifier surfaced to callers as <c>enrolmentId</c>.</summary>
    public Guid EnrolmentId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    /// <summary>Change set aside so far but not yet invested, in euros (surfaced as <c>pendingAmount</c>).</summary>
    public decimal SetAsideAmount { get; private set; }

    // Upvest identities captured during enrolment.
    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }
    public Guid? UpvestWebhookId { get; private set; }

    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    /// <summary>Total amount invested so far (surfaced as <c>investedAmount</c>): every investment that
    /// has not failed. A failed investment's money is returned to the set-aside balance.</summary>
    public decimal InvestedAmount =>
        _investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.Amount);

    /// <summary>Records the Upvest user (and webhook) created in phase one of enrolment.</summary>
    public void RecordUpvestUser(Guid userId, Guid? webhookId)
    {
        UpvestUserId = userId;
        UpvestWebhookId = webhookId;
    }

    /// <summary>Records the account group and trading account provisioned once the user is active.</summary>
    public void RecordUpvestAccount(Guid accountGroupId, Guid accountId)
    {
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
    }

    /// <summary>True once the Upvest account has been provisioned (user is active).</summary>
    public bool IsAccountProvisioned => UpvestAccountId.HasValue;

    public void MarkActive() => Status = EnrolmentStatus.Active;

    public void MarkRejected() => Status = EnrolmentStatus.Rejected;

    public void MarkPending() => Status = EnrolmentStatus.Pending;

    /// <summary>
    /// Sets aside the rounding of a paid order: the difference between its total and the next whole euro.
    /// A whole-euro total sets aside nothing. Returns the amount set aside (0 when nothing).
    /// </summary>
    public decimal SetAsideRoundUp(decimal orderTotal)
    {
        if (orderTotal <= 0m)
        {
            return 0m;
        }

        var roundUp = Math.Ceiling(orderTotal) - orderTotal;
        if (roundUp <= 0m)
        {
            return 0m;
        }

        SetAsideAmount += roundUp;
        return roundUp;
    }

    /// <summary>True once the set-aside balance has reached the investment threshold.</summary>
    public bool IsReadyToInvest => SetAsideAmount >= InvestmentThreshold;

    /// <summary>
    /// Moves the whole set-aside balance into a new pending <see cref="Investment"/> and resets the balance
    /// to zero. The caller then places the order at Upvest and records its id, or (on failure)
    /// calls <see cref="FailInvestment"/> to return the money to the balance.
    /// </summary>
    public Investment BeginInvestment()
    {
        if (!IsReadyToInvest)
        {
            throw new InvalidOperationException("Set-aside balance has not reached the investment threshold.");
        }

        var amount = SetAsideAmount;
        var idempotencyKey = Guid.NewGuid();
        var clientReference = $"eshop-{EnrolmentId:N}-{_investments.Count}";
        var investment = new Investment(amount, clientReference, idempotencyKey);
        _investments.Add(investment);
        SetAsideAmount = 0m;
        return investment;
    }

    /// <summary>Marks an investment failed and returns its money to the set-aside balance to accrue again.</summary>
    public void FailInvestment(Investment investment)
    {
        Guard.Against.Null(investment, nameof(investment));
        if (investment.Status == InvestmentStatus.Failed)
        {
            return;
        }

        investment.MarkFailed();
        SetAsideAmount += investment.Amount;
    }
}
