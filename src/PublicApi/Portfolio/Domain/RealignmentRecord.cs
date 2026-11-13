using System;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Domain;

/// <summary>
/// An operator-triggered realignment of every portfolio following a model back to the model's current weights,
/// backed by an Upvest portfolio-rebalancing execution.
/// </summary>
public class RealignmentRecord
{
    /// <summary>Client-generated realignment id surfaced as <c>realignmentId</c>. Primary key.</summary>
    public Guid RealignmentId { get; set; }

    /// <summary>The model whose followers are realigned.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The Upvest allocation id realigned.</summary>
    public Guid AllocationId { get; set; }

    /// <summary>Upvest rebalancing-execution id, once triggered.</summary>
    public Guid? UpvestExecutionId { get; set; }

    /// <summary>Reconciled status at Upvest.</summary>
    public RealignmentStatus Status { get; set; } = RealignmentStatus.Pending;

    /// <summary>Persisted idempotency key for the trigger write (reused on retry).</summary>
    public Guid IdempotencyKey { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
