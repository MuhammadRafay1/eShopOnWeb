using System.Globalization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// The single place that formats a <see cref="decimal"/> as PayPal's money <c>value</c> string, so
/// every call site is consistent (invariant culture, 2 decimal places).
/// </summary>
public static class PayPalMoneyFormatter
{
    public static string Format(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    public static decimal Parse(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;
}
