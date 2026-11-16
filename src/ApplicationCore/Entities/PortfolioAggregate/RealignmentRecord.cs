using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The application's record of a realignment the operator triggered for a model. The realignment id is the
/// Upvest rebalancing-execution id; status is derived by re-reading that execution's orders.
/// </summary>
public class RealignmentRecord
{
    /// <summary>The Upvest rebalancing-execution id. Primary key, reported as <c>realignmentId</c>.</summary>
    public Guid RealignmentId { get; set; }

    /// <summary>The model that was realigned.</summary>
    public string ModelId { get; set; } = default!;

    /// <summary>The Upvest allocation the realignment targeted.</summary>
    public Guid AllocationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
