using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// The rules of the "invest your change" scheme that do not belong to any one entity.
/// </summary>
public static class SpareChange
{
    /// <summary>Once a shopper's set-aside balance reaches this many euros, the whole balance is invested.</summary>
    public const decimal InvestmentThresholdEuros = 10m;

    /// <summary>
    /// The amount a paid order sets aside: the difference between its total and the next whole euro.
    /// An order whose total is already a whole number of euros sets aside nothing.
    /// </summary>
    public static decimal RoundUp(decimal orderTotal)
    {
        if (orderTotal <= 0m)
        {
            return 0m;
        }

        var roundedUp = Math.Ceiling(orderTotal);
        return decimal.Round(roundedUp - orderTotal, 2, MidpointRounding.AwayFromZero);
    }
}
