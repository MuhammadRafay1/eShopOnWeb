using System;
using System.Globalization;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Enums = UpvestInvestmentApi.Models.Enums;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio.Services;

/// <summary>Maps the shop's plain values onto the SDK's typed enums/dates/strings, validating caller input.</summary>
internal static class UpvestMapping
{
    /// <summary>A date-only value as the SDK's <see cref="DateTimeOffset"/> (its converter writes YYYY-MM-DD).</summary>
    public static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    /// <summary>A euro amount as Upvest's decimal string, two places (matches top-up/order regex).</summary>
    public static string CashAmount(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A percentage weight as Upvest's string (no trailing zeros; "100" exactly for a full weight).</summary>
    public static string Weight(decimal weight) =>
        weight == 100m
            ? "100"
            : weight.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>Upvest phone numbers are 8–15 digits with no punctuation; strip everything else.</summary>
    public static string PhoneDigits(string? phone) =>
        new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>Parse a provider decimal string (amount/quantity) in the invariant culture; 0 if unparseable.</summary>
    public static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    public static Enums.Country Country(string code, string field)
    {
        if (!Enums.Country.TryGetKnownValue(Normalize(code), out var value))
            throw new PortfolioValidationException($"'{field}' is not a recognised ISO 3166-1 alpha-2 country code.");
        return value;
    }

    public static Enums.Nationality Nationality(string code, string field)
    {
        if (!Enums.Nationality.TryGetKnownValue(Normalize(code), out var value))
            throw new PortfolioValidationException($"'{field}' is not a recognised ISO 3166-1 alpha-2 nationality code.");
        return value;
    }

    public static Enums.BirthCountry BirthCountry(string code, string field)
    {
        if (!Enums.BirthCountry.TryGetKnownValue(Normalize(code), out var value))
            throw new PortfolioValidationException($"'{field}' is not a recognised ISO 3166-1 alpha-2 country code.");
        return value;
    }

    public static Enums.DocumentType3 DocumentType(string type)
    {
        // The shop's identity-document type onto Upvest's document type; default to PASSPORT if unrecognised.
        return Enums.DocumentType3.TryGetKnownValue(Normalize(type), out var value)
            ? value
            : Enums.DocumentType3.Passport;
    }

    private static string Normalize(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();
}
