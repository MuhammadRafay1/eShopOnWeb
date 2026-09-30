using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.eShopWeb.Infrastructure.Payments;

public static class AmountFormatter
{
    // Currencies PayPal treats as having zero or three minor-unit digits; everything else defaults to two.
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "JPY", "KRW", "VND", "HUF", "CLP", "ISK", "GNF", "PYG", "UGX", "XAF", "XOF", "XPF"
    };

    private static readonly HashSet<string> ThreeDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "KWD", "OMR", "TND", "JOD"
    };

    public static int MinorUnitDigits(string currencyCode)
    {
        if (ZeroDecimalCurrencies.Contains(currencyCode))
        {
            return 0;
        }

        if (ThreeDecimalCurrencies.Contains(currencyCode))
        {
            return 3;
        }

        return 2;
    }

    public static string ToPayPalValue(decimal amount, string currencyCode)
    {
        var digits = MinorUnitDigits(currencyCode);
        return amount.ToString("F" + digits, CultureInfo.InvariantCulture);
    }

    public static decimal FromPayPalValue(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
