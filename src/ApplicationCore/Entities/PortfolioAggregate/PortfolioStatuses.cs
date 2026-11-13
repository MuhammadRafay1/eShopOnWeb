namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The caller-facing status strings reported by the managed-portfolio endpoints. Each value
/// reflects the state of the corresponding resource at Upvest (see upvest-investment-api-plan.md).
/// </summary>
public static class PortfolioStatuses
{
    /// <summary>Investor sign-up status values.</summary>
    public static class Investor
    {
        public const string Pending = "pending";
        public const string Active = "active";
        public const string Rejected = "rejected";
    }

    /// <summary>Pay-in status values.</summary>
    public static class Payment
    {
        public const string Pending = "pending";
        public const string Invested = "invested";
        public const string Failed = "failed";
    }

    /// <summary>Realignment status values.</summary>
    public static class Realignment
    {
        public const string Pending = "pending";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }
}
