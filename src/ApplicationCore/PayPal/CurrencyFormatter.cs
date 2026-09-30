using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

/// <summary>Formats amounts the way PayPal expects: a decimal string to the currency's minor-unit precision.</summary>
public static class CurrencyFormatter
{
    // PayPal's zero-decimal currencies (ISO 4217 currencies with no minor unit). Everything else uses 2.
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "JPY", "KRW", "HUF", "TWD"
    };

    public static int MinorUnits(string currency) => ZeroDecimalCurrencies.Contains(currency) ? 0 : 2;

    public static string Format(decimal amount, string currency)
    {
        var decimals = MinorUnits(currency);
        return Math.Round(amount, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.InvariantCulture);
    }
}
