using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The result of phase one of registration: the Upvest user is created and its checks/tax submitted, but the
/// user activates asynchronously. The account group and trading account are provisioned later, once the user
/// is active (see <see cref="UpvestAccountProvision"/>).
/// </summary>
public sealed record UpvestInvestorRegistration
{
    public required Guid UpvestUserId { get; init; }
    public Guid? WebhookId { get; init; }
}

/// <summary>The account group and trading account provisioned for an active user.</summary>
public sealed record UpvestAccountProvision
{
    public required Guid AccountGroupId { get; init; }
    public required Guid AccountId { get; init; }

    /// <summary>Enrolment status implied by the account's status (Active once the account is active).</summary>
    public required EnrolmentStatus Status { get; init; }
}

/// <summary>Everything the gateway needs to invest one tranche of set-aside change.</summary>
public sealed record UpvestInvestmentInstruction
{
    public required Guid UpvestUserId { get; init; }
    public required Guid AccountGroupId { get; init; }
    public required Guid AccountId { get; init; }

    /// <summary>Amount to invest, in euros.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Stable client reference sent as the order's <c>client_reference</c> for reconciliation.</summary>
    public required string ClientReference { get; init; }

    /// <summary>Stable idempotency key reused across retries of this tranche's provider writes.</summary>
    public required Guid IdempotencyKey { get; init; }
}

/// <summary>The outcome of placing an investment order at Upvest.</summary>
public sealed record UpvestInvestmentPlacement
{
    public required Guid UpvestOrderId { get; init; }
    public required InvestmentStatus InitialStatus { get; init; }
}
