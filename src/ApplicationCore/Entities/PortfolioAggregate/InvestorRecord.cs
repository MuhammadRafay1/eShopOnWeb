using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The shop's record that a given shopper has signed up as a managed-portfolio investor with Upvest.
/// Keyed by the shopper's own identity (<see cref="BuyerId"/>) so one shopper maps to exactly one investor,
/// and so a concurrent duplicate sign-up is refused by the primary key. Holds only the Upvest identifiers
/// the shop needs to act on the investor's behalf — never the personal details submitted at sign-up.
/// </summary>
public class InvestorRecord : IAggregateRoot
{
    /// <summary>The authenticated shopper's identity (JWT name). Primary key.</summary>
    public string BuyerId { get; private set; }

    /// <summary>Upvest user id, once the user has been created. This is the investorId the API exposes.</summary>
    public Guid? UpvestUserId { get; private set; }

    /// <summary>Upvest account-group id (PERSONAL), once created.</summary>
    public Guid? UpvestAccountGroupId { get; private set; }

    /// <summary>Upvest account id (PORTFOLIO), once created — the account able to hold a managed portfolio.</summary>
    public Guid? UpvestAccountId { get; private set; }

    /// <summary>
    /// Last known provisioning outcome: Pending / Active / Rejected. The authoritative status is re-derived
    /// from Upvest on read; this is the cached value and the floor when a re-read is not possible.
    /// </summary>
    public string SignUpStatus { get; private set; } = InvestorStatus.Pending.ToString();

    /// <summary>The shop model id the investor's portfolio currently follows, if any.</summary>
    public string? SelectedModelId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

#pragma warning disable CS8618 // Required by Entity Framework
    private InvestorRecord() { }
#pragma warning restore CS8618

    public InvestorRecord(string buyerId)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
    }

    public void SetUser(Guid userId) => UpvestUserId = userId;

    public void SetAccountGroup(Guid accountGroupId) => UpvestAccountGroupId = accountGroupId;

    public void SetAccount(Guid accountId) => UpvestAccountId = accountId;

    public void SetStatus(InvestorStatus status) => SignUpStatus = status.ToString();

    public void SelectModel(string modelId) => SelectedModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));

    /// <summary>True once the investor is accepted and holds a portfolio-capable account.</summary>
    public bool IsReadyToInvest =>
        UpvestUserId.HasValue && UpvestAccountId.HasValue &&
        string.Equals(SignUpStatus, InvestorStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase);
}
