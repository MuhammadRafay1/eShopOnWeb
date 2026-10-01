using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Payments;

/// <summary>
/// Formats/parses the decimal order total the way PayPal expects its wire-format Money.Value string
/// (ISO 4217 minor-unit count varies by currency - PayPal rejects e.g. "10.00" for JPY).
/// </summary>
public static class CurrencyFormat
{
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "JPY", "HUF", "TWD", "KRW", "CLP", "VND", "XAF", "XOF", "XPF", "ISK"
    };

    private static readonly HashSet<string> ThreeDecimalCurrencies = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "KWD", "OMR", "JOD", "TND"
    };

    public static int DecimalsFor(string currencyCode)
    {
        if (ZeroDecimalCurrencies.Contains(currencyCode)) return 0;
        if (ThreeDecimalCurrencies.Contains(currencyCode)) return 3;
        return 2;
    }

    public static string Format(decimal amount, string currencyCode)
    {
        var decimals = DecimalsFor(currencyCode);
        return amount.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }

    public static decimal Parse(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
