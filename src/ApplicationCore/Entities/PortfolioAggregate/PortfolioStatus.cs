namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The externally-exposed status strings for sign-ups, payments and realignments, together with the
/// mapping from the raw Upvest statuses onto them. These values are part of the public API contract.
/// </summary>
public static class PortfolioStatus
{
    // Investor sign-up
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Rejected = "rejected";

    // Payment
    public const string Invested = "invested";
    public const string Failed = "failed";

    // Realignment
    public const string Completed = "completed";

    /// <summary>Maps an Upvest user status + account status onto the investor sign-up status.</summary>
    public static string FromUpvest(string? userStatus, string? accountStatus)
    {
        var user = (userStatus ?? string.Empty).ToUpperInvariant();
        var account = (accountStatus ?? string.Empty).ToUpperInvariant();

        if (user is "OFFBOARDING" or "OFFBOARDED")
        {
            return Rejected;
        }

        if (user == "ACTIVE" && account == "ACTIVE")
        {
            return Active;
        }

        return Pending;
    }

    /// <summary>Maps an Upvest portfolio-order status onto the payment status.</summary>
    public static string FromPortfolioOrder(string? orderStatus)
    {
        return (orderStatus ?? string.Empty).ToUpperInvariant() switch
        {
            "FILLED" or "SETTLED" => Invested,
            "CANCELLED" => Failed,
            _ => Pending,
        };
    }

    /// <summary>Derives the realignment status from the statuses of a rebalancing execution's orders.</summary>
    public static string FromRebalancingOrders(System.Collections.Generic.IReadOnlyList<string> orderStatuses)
    {
        if (orderStatuses == null || orderStatuses.Count == 0)
        {
            return Pending;
        }

        bool anyPending = false;
        bool anyCancelled = false;
        bool allTerminalSuccess = true;

        foreach (var raw in orderStatuses)
        {
            switch ((raw ?? string.Empty).ToUpperInvariant())
            {
                case "FILLED":
                case "SETTLED":
                    break;
                case "CANCELLED":
                    anyCancelled = true;
                    allTerminalSuccess = false;
                    break;
                default: // NEW, PROCESSING, unknown
                    anyPending = true;
                    allTerminalSuccess = false;
                    break;
            }
        }

        if (anyPending)
        {
            return Pending;
        }

        if (allTerminalSuccess)
        {
            return Completed;
        }

        return anyCancelled ? Failed : Pending;
    }
}
