using System;
using System.Globalization;
using System.Net;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using UpvestInvestmentApi.Models.Enums;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Conversions between the app's domain values (euros as <see cref="decimal"/>, ISO country codes as
/// strings) and the Upvest wire forms (decimal strings with per-field regexes, open string enums).
/// All numeric formatting/parsing is invariant-culture so a comma locale never corrupts an amount.
/// </summary>
internal static class UpvestWire
{
    /// <summary>A money amount in euros, two decimals (matches the top-up / bank-transaction regex).</summary>
    public static string Money2Dp(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A model fund weight as a percentage string (<c>100</c> or up to 5 decimals).</summary>
    public static string Weight(decimal weight) =>
        weight == 100m ? "100" : weight.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>Parse a decimal-string money/quantity value coming back from Upvest.</summary>
    public static decimal ParseDecimal(string? value) =>
        string.IsNullOrWhiteSpace(value) ? 0m : decimal.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);

    public static Nationality Nationality(string code) =>
        ResolveNationality(code);

    private static Nationality ResolveNationality(string code)
    {
        if (UpvestInvestmentApi.Models.Enums.Nationality.TryGetKnownValue(code?.ToUpperInvariant(), out var value))
        {
            return value;
        }
        throw Invalid($"nationality '{code}'");
    }

    public static Country Country(string code)
    {
        if (UpvestInvestmentApi.Models.Enums.Country.TryGetKnownValue(code?.ToUpperInvariant(), out var value))
        {
            return value;
        }
        throw Invalid($"country '{code}'");
    }

    public static BirthCountry? BirthCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }
        if (UpvestInvestmentApi.Models.Enums.BirthCountry.TryGetKnownValue(code.ToUpperInvariant(), out var value))
        {
            return value;
        }
        throw Invalid($"birth country '{code}'");
    }

    public static IssuingCountry IssuingCountry(string code)
    {
        if (UpvestInvestmentApi.Models.Enums.IssuingCountry.TryGetKnownValue(code?.ToUpperInvariant(), out var value))
        {
            return value;
        }
        throw Invalid($"issuing country '{code}'");
    }

    private static UpvestGatewayException Invalid(string what) =>
        new($"Unknown or unsupported {what}.", HttpStatusCode.BadRequest);
}
