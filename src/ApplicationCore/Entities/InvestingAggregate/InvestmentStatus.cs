namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to at Upvest.
/// Surfaced to callers (lower-cased) as the <c>status</c> field.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed with Upvest; its outcome is not yet known.</summary>
    Pending = 0,

    /// <summary>Upvest filled the order; the money is invested.</summary>
    Settled = 1,

    /// <summary>Upvest could not invest the money; it is returned to the set-aside balance.</summary>
    Failed = 2
}
