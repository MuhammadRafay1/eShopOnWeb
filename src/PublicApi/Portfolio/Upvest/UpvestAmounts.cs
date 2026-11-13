using System;
using System.Globalization;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;

/// <summary>
/// Conversions between our API's JSON-number money/weights (euros, two decimals) and the decimal <b>strings</b>
/// Upvest uses on the wire. All conversions go through <see cref="decimal"/> (never <see cref="double"/>) so no
/// precision is lost, and formatting matches Upvest's documented regexes.
/// </summary>
public static class UpvestAmounts
{
    /// <summary>
    /// Formats a euro amount as Upvest's cash-amount string (regex <c>^[0-9]{1,9}(\.[0-9]{2})?$</c>): exactly two
    /// decimals. Throws <see cref="ArgumentOutOfRangeException"/> if the amount is not strictly positive or exceeds
    /// nine integer digits.
    /// </summary>
    public static string ToCashAmount(decimal amount)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");

        var rounded = Math.Round(amount, 2, MidpointRounding.ToEven);
        if (rounded >= 1_000_000_000m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount exceeds the maximum of 999,999,999.99.");

        return rounded.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a percentage weight as Upvest's weight string (regex <c>^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$</c>):
    /// <c>"100"</c> for exactly 100, otherwise 0–99 with up to five decimals and no trailing zeros.
    /// </summary>
    public static string ToWeight(decimal weight)
    {
        if (weight < 0m || weight > 100m)
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be between 0 and 100.");

        if (weight == 100m)
            return "100";

        var rounded = Math.Round(weight, 5, MidpointRounding.ToEven);
        return rounded.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    /// <summary>Parses an Upvest decimal string (amount/quantity/value) into a <see cref="decimal"/>; returns 0 for null/blank.</summary>
    public static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;
        return decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    /// <summary>Rounds a value read back from Upvest to two decimals for our JSON-number money responses.</summary>
    public static decimal ToMoney(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);
}
