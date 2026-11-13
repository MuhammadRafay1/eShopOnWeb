using System;
using System.Globalization;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Converts the app's decimal money/weights to the regex-constrained decimal <b>strings</b> Upvest expects,
/// and parses amounts back. All formatting is invariant-culture (never a localized decimal separator).
/// </summary>
internal static class UpvestValueFormatting
{
    /// <summary>A euro cash amount as a decimal string with exactly two decimal places (e.g. "100.00").</summary>
    public static string CashAmount(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// A percentage weight as a decimal string matching Upvest's allocation-weight pattern
    /// (<c>^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$</c>): "100" for 100, otherwise up to five trimmed decimals.
    /// </summary>
    public static string Weight(decimal weight)
    {
        if (weight == 100m) return "100";
        var rounded = Math.Round(weight, 5, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    /// <summary>Parse a Upvest decimal string (amount/quantity/value) back to a decimal; 0 when absent or unparseable.</summary>
    public static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
