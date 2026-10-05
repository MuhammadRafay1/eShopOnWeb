namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>Public investment status returned by the API.</summary>
public enum InvestmentStatus
{
    /// <summary>The investment's outcome at Upvest is not yet known.</summary>
    Pending,

    /// <summary>The order filled and settled at Upvest.</summary>
    Settled,

    /// <summary>The order did not complete at Upvest.</summary>
    Failed
}
