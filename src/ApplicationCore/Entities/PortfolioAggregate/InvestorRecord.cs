using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The application's own record of a shopper who has signed up as an investor with Upvest.
/// Keyed by the shopper's identity (the JWT name claim) so one shopper can never reach another's data.
/// Holds only the Upvest identifiers the application needs to drive later flows — never the shopper's
/// personal details, which are sent to Upvest and never persisted here.
/// </summary>
public class InvestorRecord
{
    /// <summary>The signed-in shopper (JWT name claim). Primary key.</summary>
    public string Shopper { get; set; } = default!;

    /// <summary>The Upvest user id returned by sign-up. Reported to the caller as <c>investorId</c>.</summary>
    public Guid UpvestUserId { get; set; }

    /// <summary>The Upvest account group that holds the investor's accounts.</summary>
    public Guid? UpvestAccountGroupId { get; set; }

    /// <summary>The Upvest PORTFOLIO account that holds the managed portfolio.</summary>
    public Guid? UpvestAccountId { get; set; }

    /// <summary>The last known sign-up status. GET always re-reads Upvest and refreshes this.</summary>
    public InvestorSignupStatus Status { get; set; }

    /// <summary>The model the investor's portfolio currently follows, or null if none chosen yet.</summary>
    public string? ChosenModelId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
