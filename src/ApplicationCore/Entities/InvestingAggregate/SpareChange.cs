using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>The spare-change rule: how much a paid order sets aside.</summary>
public static class SpareChange
{
    /// <summary>
    /// The difference between an order total and the next whole euro. An order of €12.30
    /// sets aside €0.70; an order that is already a whole number of euros sets aside nothing.
    /// </summary>
    public static decimal RoundUp(decimal orderTotal)
    {
        if (orderTotal <= 0m)
        {
            return 0m;
        }

        return decimal.Round(Math.Ceiling(orderTotal) - orderTotal, 2, MidpointRounding.AwayFromZero);
    }
}
