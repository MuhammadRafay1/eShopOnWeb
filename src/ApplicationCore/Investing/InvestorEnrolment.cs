using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// A shopper's enrolment as an investor with Upvest. Holds only the identifiers Upvest returns —
/// never the shopper's personal details, which are forwarded to Upvest at enrolment and not retained.
/// </summary>
public class InvestorEnrolment : BaseEntity, IAggregateRoot
{
    public Guid EnrolmentId { get; private set; }

    /// <summary>The owning shopper (the authenticated user name from the bearer token).</summary>
    public string ShopperId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    public string? UpvestUserId { get; private set; }
    public string? UpvestAccountGroupId { get; private set; }
    public string? UpvestAccountId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Diagnostic detail for a rejection. Never contains personal data and is not exposed.</summary>
    public string? FailureReason { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private InvestorEnrolment() { }
#pragma warning restore CS8618

    public InvestorEnrolment(string shopperId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        ShopperId = shopperId;
        EnrolmentId = Guid.NewGuid();
        Status = EnrolmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void SetUpvestUser(string upvestUserId)
    {
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        UpvestUserId = upvestUserId;
        Touch();
    }

    public void SetUpvestAccount(string accountGroupId, string accountId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
        Touch();
    }

    public void Activate()
    {
        Status = EnrolmentStatus.Active;
        FailureReason = null;
        Touch();
    }

    public void Reject(string reason)
    {
        Status = EnrolmentStatus.Rejected;
        FailureReason = reason;
        Touch();
    }

    public bool IsAcceptedInvestor =>
        Status == EnrolmentStatus.Active &&
        !string.IsNullOrEmpty(UpvestUserId) &&
        !string.IsNullOrEmpty(UpvestAccountId) &&
        !string.IsNullOrEmpty(UpvestAccountGroupId);

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
