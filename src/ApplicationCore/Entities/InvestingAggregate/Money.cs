using System;
using System.Globalization;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Euro money helpers. Amounts are kept as integer euro cents internally to avoid rounding drift, and
/// rendered as fixed two-decimal strings for Upvest and as decimals for the API surface.
/// </summary>
public static class Money
{
    /// <summary>Convert a euro decimal amount to whole cents.</summary>
    public static long ToCents(decimal euros) => (long)Math.Round(euros * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Convert whole cents to a euro decimal amount.</summary>
    public static decimal ToEuros(long cents) => cents / 100m;

    /// <summary>
    /// The spare change an order sets aside: the difference between its total and the next whole euro.
    /// €12.30 → €0.70; a whole-euro total → €0.00.
    /// </summary>
    public static long RoundUpCents(decimal orderTotal)
    {
        var totalCents = ToCents(orderTotal);
        var remainder = totalCents % 100;
        return remainder == 0 ? 0 : 100 - remainder;
    }

    /// <summary>Render cents as a fixed two-decimal euro amount string (e.g. 1070 → "10.70"), for Upvest requests.</summary>
    public static string CentsToAmountString(long cents) =>
        (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
