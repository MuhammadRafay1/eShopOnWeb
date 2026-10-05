using System;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Works out the spare change an order sets aside: the difference between the
/// order total and the next whole euro. An order of €12.30 sets aside €0.70;
/// an order that is already a whole number of euros sets aside nothing.
/// </summary>
public static class SpareChangeCalculator
{
    public static decimal RoundUpToNextEuro(decimal orderTotal)
    {
        if (orderTotal <= 0m)
        {
            return 0m;
        }

        var roundUp = Math.Ceiling(orderTotal) - orderTotal;
        // Normalise to cents to avoid a non-zero scale artifact (e.g. 0.70).
        return Math.Round(roundUp, 2, MidpointRounding.AwayFromZero);
    }
}
