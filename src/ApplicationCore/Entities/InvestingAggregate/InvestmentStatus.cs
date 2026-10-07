namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// The lifecycle of a single investment made on a shopper's behalf at Upvest.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed at Upvest; the outcome is not yet known.</summary>
    Pending = 0,

    /// <summary>The order executed and settled at Upvest.</summary>
    Settled = 1,

    /// <summary>The order did not complete at Upvest (rejected, cancelled or failed).</summary>
    Failed = 2
}
