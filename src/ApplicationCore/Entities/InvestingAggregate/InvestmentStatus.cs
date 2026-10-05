namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Where a single investment has got to. Surfaced to callers as <c>pending</c>, <c>settled</c>, <c>failed</c>.
/// </summary>
public enum InvestmentStatus
{
    /// <summary>Placed with the provider; its final outcome is not yet known.</summary>
    Pending = 0,

    /// <summary>The provider executed the investment.</summary>
    Settled = 1,

    /// <summary>The provider did not execute the investment; the set-aside money was returned to the balance.</summary>
    Failed = 2
}
