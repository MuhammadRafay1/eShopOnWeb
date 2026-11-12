using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolios;

/// <summary>
/// A shopper who has signed up to hold a managed investment portfolio with Upvest.
/// A single shopper maps to exactly one investor record; the record is private to that shopper.
/// No personal detail is stored here — only the identifiers Upvest returned and the local state we track.
/// </summary>
public class Investor
{
    /// <summary>Stable local identifier for the investor, surfaced to callers as <c>investorId</c>.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owning shopper (the authenticated caller's name/identity). Scopes all reads to this shopper.</summary>
    public string ShopperId { get; set; } = string.Empty;

    /// <summary>The Upvest user id created for this shopper, if the user was created.</summary>
    public string? UpvestUserId { get; set; }

    /// <summary>The Upvest account group id, if created.</summary>
    public string? UpvestAccountGroupId { get; set; }

    /// <summary>The Upvest (portfolio) account id able to hold the managed portfolio, if created.</summary>
    public string? UpvestAccountId { get; set; }

    /// <summary>The model portfolio this investor currently follows, if chosen.</summary>
    public string? ModelId { get; set; }

    /// <summary>Last status observed from Upvest.</summary>
    public InvestorStatus Status { get; set; } = InvestorStatus.Pending;

    /// <summary>Human-readable note about the most recent Upvest interaction (never contains secrets or PII).</summary>
    public string? StatusDetail { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>One fund in a model portfolio: an ISIN and its target weight (a percentage).</summary>
public class ModelFund
{
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Target weight as a percentage. The weights of a model add up to exactly 100.</summary>
    public decimal Weight { get; set; }
}

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund.
/// Backed by an Upvest portfolio allocation.
/// </summary>
public class ModelPortfolio
{
    /// <summary>Operator-chosen identifier (from the route), surfaced as <c>modelId</c>.</summary>
    public string ModelId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<ModelFund> Funds { get; set; } = new();

    /// <summary>The Upvest allocation id backing this model, once created.</summary>
    public string? UpvestAllocationId { get; set; }

    /// <summary>Note about the most recent Upvest allocation sync (never contains secrets).</summary>
    public string? SyncDetail { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A pay-in: money the investor paid in, credited to their Upvest account and invested across
/// their model's funds. Status is derived from the backing Upvest portfolio order.
/// </summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ShopperId { get; set; } = string.Empty;

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EUR";

    /// <summary>Upvest top-up id that credits cash to the account, if created.</summary>
    public string? UpvestTopupId { get; set; }

    /// <summary>Upvest portfolio order id that invests the cash across the funds, if created.</summary>
    public string? UpvestPortfolioOrderId { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string? StatusDetail { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A realignment run: brings every portfolio following a model back to the model's current weights.
/// Backed by an Upvest portfolio rebalancing execution.
/// </summary>
public class Realignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ModelId { get; set; } = string.Empty;

    /// <summary>Upvest rebalancing execution id, if created.</summary>
    public string? UpvestExecutionId { get; set; }

    public RealignmentStatus Status { get; set; } = RealignmentStatus.Pending;

    public string? StatusDetail { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
