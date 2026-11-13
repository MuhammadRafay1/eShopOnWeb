using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>
/// A shopper who has signed up to hold a managed investment portfolio with Upvest.
/// One row per shopper, keyed by the JWT subject so one shopper can never read another's.
/// Personal details are NOT stored here — they are sent to Upvest and never persisted/logged.
/// </summary>
public class Investor
{
    /// <summary>The signed-in shopper's stable identity (JWT subject / user name). Primary key.</summary>
    public string ShopperId { get; set; } = default!;

    /// <summary>The Upvest user id created for this shopper (null until the create call succeeds).</summary>
    public Guid? UpvestUserId { get; set; }

    public Guid? AccountGroupId { get; set; }

    /// <summary>The PORTFOLIO account at Upvest that holds the managed portfolio.</summary>
    public Guid? AccountId { get; set; }

    // Persisted idempotency keys so an ambiguous create can be safely re-read / replayed.
    public Guid CreateUserIdempotencyKey { get; set; }
    public Guid AccountGroupIdempotencyKey { get; set; }
    public Guid AccountIdempotencyKey { get; set; }

    public Guid? KycCheckId { get; set; }
    public Guid? InstrumentFitCheckId { get; set; }

    /// <summary>The shop's model this investor follows (null until they choose one). Maps to a Upvest allocation.</summary>
    public string? ChosenModelId { get; set; }

    /// <summary>Last-known sign-up status; refreshed from Upvest on read.</summary>
    public InvestorSignupStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight each (summing to 100).
/// Backed at Upvest by a portfolio "allocation".
/// </summary>
public class ModelPortfolio
{
    /// <summary>The operator-chosen model id from the route. Primary key.</summary>
    public string ModelId { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>The Upvest allocation id this model is backed by (null until the allocation write succeeds).</summary>
    public Guid? AllocationId { get; set; }

    /// <summary>Stable idempotency key for creating this model's Upvest allocation (replay-safe).</summary>
    public Guid AllocationIdempotencyKey { get; set; }

    public List<ModelFund> Funds { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>One fund in a model portfolio: an instrument (ISIN) and its target weight (a percentage).</summary>
public class ModelFund
{
    public int Id { get; set; }
    public string ModelId { get; set; } = default!;
    public string InstrumentId { get; set; } = default!;
    public decimal Weight { get; set; }
}

/// <summary>A pay-in by an investor: cash credited at Upvest and invested across their model's funds.</summary>
public class Payment
{
    /// <summary>App-generated payment id. Primary key, and the duplicate-claim for a pay-in.</summary>
    public Guid PaymentId { get; set; }

    public string ShopperId { get; set; } = default!;
    public decimal Amount { get; set; }

    public Guid? TopUpId { get; set; }
    public Guid? OrderId { get; set; }

    // Persisted before the provider calls so an ambiguous write can be reconciled/replayed.
    public Guid TopUpIdempotencyKey { get; set; }
    public Guid OrderIdempotencyKey { get; set; }

    public PaymentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>An operator realignment of every portfolio following a model, back to the model's weights.</summary>
public class Realignment
{
    /// <summary>App-generated realignment id. Primary key.</summary>
    public Guid RealignmentId { get; set; }

    public string ModelId { get; set; } = default!;
    public Guid AllocationId { get; set; }
    public Guid? ExecutionId { get; set; }
    public Guid TriggerIdempotencyKey { get; set; }

    public RealignmentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
