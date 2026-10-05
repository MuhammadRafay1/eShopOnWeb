using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper's enrolment into "Invest your change". Owns the shopper's link to their Upvest investor
/// (user, account group and account) and the spare change set aside but not yet invested.
///
/// One enrolment per shopper (<see cref="BuyerId"/> is unique). Personal details are never stored here
/// beyond the minimum needed to operate and reconcile the integration (email, and the tax identifiers the
/// sign-up form collects); the rest of the sign-up form is sent to Upvest and not persisted.
/// </summary>
public class InvestorEnrolment : BaseEntity, IAggregateRoot
{
    private InvestorEnrolment()
    {
        // Required by EF and set via the mapped properties.
        BuyerId = string.Empty;
        Email = string.Empty;
    }

    public InvestorEnrolment(string buyerId, string email, string? taxId, string? taxCountry, DateTimeOffset now)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Email = Guard.Against.NullOrEmpty(email, nameof(email));
        TaxId = taxId;
        TaxCountry = taxCountry;
        Status = EnrolmentStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Stable public identifier returned to callers as <c>enrolmentId</c>.</summary>
    public Guid EnrolmentId { get; private set; } = Guid.NewGuid();

    /// <summary>The owning shopper (the JWT subject / username). Unique across enrolments.</summary>
    public string BuyerId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }

    /// <summary>The shopper's email — retained to reconcile a user creation whose outcome was unknown.</summary>
    public string Email { get; private set; }

    public string? TaxId { get; private set; }
    public string? TaxCountry { get; private set; }

    /// <summary>Spare change set aside and not yet invested (euros). Returned to callers as <c>pendingAmount</c>.</summary>
    public decimal SetAsideBalance { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>True once Upvest has accepted the shopper and an account exists to hold investments.</summary>
    public bool CanInvest => Status == EnrolmentStatus.Active
        && UpvestUserId is not null
        && UpvestAccountId is not null;

    public void RecordUpvestUser(Guid userId, DateTimeOffset now)
    {
        UpvestUserId = userId;
        Touch(now);
    }

    public void RecordUpvestAccountGroup(Guid accountGroupId, DateTimeOffset now)
    {
        UpvestAccountGroupId = accountGroupId;
        Touch(now);
    }

    public void RecordUpvestAccount(Guid accountId, DateTimeOffset now)
    {
        UpvestAccountId = accountId;
        Touch(now);
    }

    public void SetStatus(EnrolmentStatus status, DateTimeOffset now)
    {
        Status = status;
        Touch(now);
    }

    /// <summary>Set aside the spare change from a paid order.</summary>
    public void AddSetAside(decimal amount, DateTimeOffset now)
    {
        Guard.Against.Negative(amount, nameof(amount));
        if (amount == 0m) return;
        SetAsideBalance = decimal.Round(SetAsideBalance + amount, 2, MidpointRounding.AwayFromZero);
        Touch(now);
    }

    /// <summary>
    /// Take the whole set-aside balance to invest it, resetting the balance to zero. Returns the amount
    /// taken, so afterwards the balance accrues towards the next investment from zero.
    /// </summary>
    public decimal TakeBalanceForInvestment(DateTimeOffset now)
    {
        var amount = SetAsideBalance;
        SetAsideBalance = 0m;
        Touch(now);
        return amount;
    }

    /// <summary>Return money to the set-aside balance when its investment failed at Upvest.</summary>
    public void ReturnFailedInvestment(decimal amount, DateTimeOffset now)
    {
        Guard.Against.Negative(amount, nameof(amount));
        SetAsideBalance = decimal.Round(SetAsideBalance + amount, 2, MidpointRounding.AwayFromZero);
        Touch(now);
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;
}
