using System.Globalization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Converts between decimal amounts and PayPal's string <c>value</c> wire shape. Always invariant-culture with
/// two decimal places — a culture with a comma decimal separator would silently corrupt the wire value, which
/// the SDK's (non-enforced) validation attributes would not catch. Assumes a 2-decimal currency (the task's
/// sandbox verification currency); zero/three-decimal currencies are out of scope.
/// </summary>
public static class MoneyFormatter
{
    public static string ToWire(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static decimal? ParseOrNull(string? wire) =>
        decimal.TryParse(wire, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
