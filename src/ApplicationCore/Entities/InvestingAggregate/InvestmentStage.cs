namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>Internal investment progression stage, driven by the background reconciler.</summary>
public enum InvestmentStage
{
    /// <summary>Needs the account group funded with the set-aside amount (top-up not yet created).</summary>
    Funding,

    /// <summary>Top-up created; waiting for the cash to be confirmed at Upvest.</summary>
    AwaitingFunds,

    /// <summary>Funds confirmed; the buy order is about to be placed.</summary>
    Placing,

    /// <summary>Order placed; waiting for it to fill/settle at Upvest.</summary>
    AwaitingFill,

    /// <summary>Order filled and settled.</summary>
    Settled,

    /// <summary>Order did not complete.</summary>
    Failed
}
