using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Formats decimal amounts into PayPal's money.value string using the correct number of minor
/// units for the currency. PayPal rejects "10.00" for JPY (0 decimals) and expects 3 decimals for
/// currencies like BHD/KWD/OMR — so the decimal count is derived from the currency, never hardcoded.
/// This is what makes "the amount PayPal holds must equal the order total to the cent" hold for
/// whatever currency the account is configured with.
/// </summary>
public static class PayPalMoney
{
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG",
        "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF", "HUF", "TWD"
    };

    private static readonly HashSet<string> ThreeDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "KWD", "OMR", "TND", "IQD", "JOD", "LYD"
    };

    public static int DecimalPlaces(string currencyCode)
    {
        if (ZeroDecimalCurrencies.Contains(currencyCode)) return 0;
        if (ThreeDecimalCurrencies.Contains(currencyCode)) return 3;
        return 2;
    }

    /// <summary>Renders an amount as PayPal's money.value string (e.g. "10.00", "1000", "1.234").</summary>
    public static string Format(decimal amount, string currencyCode)
    {
        var decimals = DecimalPlaces(currencyCode);
        var rounded = Math.Round(amount, decimals, MidpointRounding.AwayFromZero);
        return rounded.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>Parses a PayPal money.value string into a decimal (invariant culture).</summary>
    public static decimal Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        return decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }
}
