namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle of a shopper's sign-up to become a managed-portfolio investor with Upvest.
/// The value always reflects the latest known state at Upvest (see <c>GET /api/portfolio/investor</c>).
/// </summary>
public enum InvestorStatus
{
    /// <summary>Upvest has accepted the request but the investor's account is not yet ready.</summary>
    Pending = 0,

    /// <summary>Upvest has accepted the shopper and their portfolio account is ready.</summary>
    Active = 1,

    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected = 2
}
