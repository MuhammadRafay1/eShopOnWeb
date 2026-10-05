using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>Pure helpers for the spare-change round-up calculation.</summary>
public static class ChangeMath
{
    /// <summary>
    /// The change set aside for an order: the difference between its total and the next whole euro, in
    /// cents. An order whose total is already a whole number of euros sets aside nothing (0).
    /// </summary>
    public static long RoundUpCents(decimal orderTotal)
    {
        if (orderTotal <= 0m)
            return 0;

        var totalCents = (long)Math.Round(orderTotal * 100m, MidpointRounding.AwayFromZero);
        return (100 - (totalCents % 100)) % 100;
    }
}
