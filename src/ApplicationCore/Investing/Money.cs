using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>Helpers for working with euro amounts as integer cents.</summary>
public static class Money
{
    /// <summary>Convert a euro amount to whole cents, rounding to the nearest cent.</summary>
    public static long ToCents(decimal euros) =>
        (long)Math.Round(euros * 100m, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The change needed to round a euro amount up to the next whole euro, in cents.
    /// €12.30 → 70 cents; a whole-euro amount → 0.
    /// </summary>
    public static long RoundUpCents(decimal euros)
    {
        var cents = ToCents(euros);
        var remainder = ((cents % 100) + 100) % 100;
        return remainder == 0 ? 0 : 100 - remainder;
    }
}
