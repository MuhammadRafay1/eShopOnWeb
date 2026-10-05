using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. The aggregate
/// owns the shopper's set-aside balance, their spare-change ledger and their
/// investments, and tracks their enrolment as an investor with Upvest.
///
/// A shopper's enrolment, ledger and investments belong to that shopper alone:
/// every query is scoped by <see cref="BuyerId"/>.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>Once the set-aside balance reaches this, the whole balance is invested.</summary>
    public const decimal InvestmentThresholdEuros = 10m;

    #pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }

    public Investor(string buyerId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        BuyerId = buyerId;
        Status = EnrolmentStatus.Pending;
        PendingAmount = 0m;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The owning shopper (the PublicApi caller's identity / username).</summary>
    public string BuyerId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    /// <summary>The shopper's investor id at Upvest.</summary>
    public string? UpvestUserId { get; private set; }

    /// <summary>The shopper's account group id at Upvest (holds the cash balance).</summary>
    public string? UpvestAccountGroupId { get; private set; }

    /// <summary>The shopper's trading account id at Upvest (holds the investments).</summary>
    public string? UpvestAccountId { get; private set; }

    /// <summary>Spare change set aside but not yet invested, in euros.</summary>
    public decimal PendingAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<Investment> _investments = new();
    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    private readonly List<SpareChangeLedgerEntry> _ledger = new();
    public IReadOnlyCollection<SpareChangeLedgerEntry> Ledger => _ledger.AsReadOnly();

    public void LinkUpvestUser(string upvestUserId)
    {
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        UpvestUserId = upvestUserId;
    }

    public void LinkAccountGroup(string accountGroupId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        UpvestAccountGroupId = accountGroupId;
    }

    public void LinkAccount(string accountId)
    {
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        UpvestAccountId = accountId;
    }

    public void Activate()
    {
        if (Status == EnrolmentStatus.Rejected)
        {
            return;
        }

        Status = EnrolmentStatus.Active;
    }

    public void Reject() => Status = EnrolmentStatus.Rejected;

    /// <summary>
    /// Set aside the spare change from a paid order. Only an accepted investor
    /// sets anything aside. Returns the amount actually set aside (0 if none).
    /// </summary>
    public decimal SetAside(int orderId, decimal amount)
    {
        if (Status != EnrolmentStatus.Active || amount <= 0m)
        {
            return 0m;
        }

        _ledger.Add(new SpareChangeLedgerEntry(orderId, amount));
        PendingAmount += amount;
        return amount;
    }

    public bool IsReadyToInvest() =>
        Status == EnrolmentStatus.Active && PendingAmount >= InvestmentThresholdEuros;

    /// <summary>
    /// Move the whole set-aside balance into a new investment backed by the
    /// given Upvest order, resetting the balance to zero. Later spare change
    /// accrues towards the next investment.
    /// </summary>
    public Investment BeginInvestment(string upvestOrderId)
    {
        Guard.Against.NullOrEmpty(upvestOrderId, nameof(upvestOrderId));

        var amount = PendingAmount;
        Guard.Against.NegativeOrZero(amount, nameof(amount));

        var investment = new Investment(amount, upvestOrderId);
        _investments.Add(investment);
        PendingAmount = 0m;
        return investment;
    }

    /// <summary>Total invested so far (pending + settled; failed investments do not count).</summary>
    public decimal TotalInvested() =>
        _investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.Amount);
}
