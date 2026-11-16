using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Links a signed-in shop user to their Upvest investor identity (user, account group and account).
/// Deliberately stores no personal KYC data — only the Upvest resource identifiers and their
/// last-known statuses. Personal details are forwarded to Upvest and never persisted or logged here.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The owning shop user (the JWT subject / user name). An investor belongs to exactly one shopper.</summary>
    public string ShopUserName { get; private set; }

    public string? UpvestUserId { get; private set; }
    public string? UpvestUserStatus { get; private set; }

    public string? AccountGroupId { get; private set; }
    public string? AccountGroupStatus { get; private set; }

    public string? AccountId { get; private set; }
    public string? AccountStatus { get; private set; }

    /// <summary>The model portfolio (by its operator-facing id) the investor has chosen to follow, if any.</summary>
    public string? ModelId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Investor()
    {
        ShopUserName = default!;
    }

    public Investor(string shopUserName)
    {
        ShopUserName = Guard.Against.NullOrEmpty(shopUserName, nameof(shopUserName));
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void SetUpvestUser(string userId, string? status)
    {
        UpvestUserId = Guard.Against.NullOrEmpty(userId, nameof(userId));
        UpvestUserStatus = status;
        Touch();
    }

    public void SetAccountGroup(string accountGroupId, string? status)
    {
        AccountGroupId = Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        AccountGroupStatus = status;
        Touch();
    }

    public void SetAccount(string accountId, string? status)
    {
        AccountId = Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        AccountStatus = status;
        Touch();
    }

    public void UpdateStatuses(string? userStatus, string? accountGroupStatus, string? accountStatus)
    {
        if (userStatus is not null) UpvestUserStatus = userStatus;
        if (accountGroupStatus is not null) AccountGroupStatus = accountGroupStatus;
        if (accountStatus is not null) AccountStatus = accountStatus;
        Touch();
    }

    public void ChooseModel(string modelId)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    /// <summary>
    /// Maps the live Upvest resource statuses onto the capability status the API exposes:
    /// <c>pending</c> until the user is accepted and the account is ready, then <c>active</c>,
    /// or <c>rejected</c> if Upvest will not take the shopper on.
    /// </summary>
    public InvestorStatus MapStatus()
    {
        // Upvest will not onboard the shopper -> rejected.
        if (IsOffboarded(UpvestUserStatus) || IsClosedOrLocked(AccountGroupStatus) || IsClosedOrLocked(AccountStatus))
            return InvestorStatus.Rejected;

        // Fully onboarded and ready to hold a portfolio: user active and account active.
        if (string.Equals(UpvestUserStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            && string.Equals(AccountStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            return InvestorStatus.Active;

        return InvestorStatus.Pending;
    }

    private static bool IsOffboarded(string? s) =>
        string.Equals(s, "OFFBOARDED", StringComparison.OrdinalIgnoreCase)
        || string.Equals(s, "OFFBOARDING", StringComparison.OrdinalIgnoreCase);

    private static bool IsClosedOrLocked(string? s) =>
        string.Equals(s, "CLOSED", StringComparison.OrdinalIgnoreCase)
        || string.Equals(s, "LOCKED", StringComparison.OrdinalIgnoreCase)
        || string.Equals(s, "CLOSING", StringComparison.OrdinalIgnoreCase);
}
