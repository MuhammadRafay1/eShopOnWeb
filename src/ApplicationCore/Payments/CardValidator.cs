using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>
/// Cheap local checks on card input before it is sent to the provider. Error messages never echo the input.
/// </summary>
public static class CardValidator
{
    private static readonly Regex ExpiryPattern = new(@"^(\d{4})-(\d{2})$", RegexOptions.Compiled);
    private static readonly Regex SecurityCodePattern = new(@"^\d{3,4}$", RegexOptions.Compiled);
    private static readonly Regex CountryPattern = new("^[A-Z]{2}$", RegexOptions.Compiled);

    /// <summary>Normalises the number (spaces/dashes removed) and validates the card. Returns the normalised card.</summary>
    public static CardDetails Validate(CardDetails card, DateTimeOffset now)
    {
        if (card is null) throw new PaymentValidationException("Card details are required.");

        var number = new string((card.Number ?? string.Empty).Where(c => c is not (' ' or '-')).ToArray());
        if (number.Length is < 12 or > 19 || !number.All(char.IsAsciiDigit) || !PassesLuhn(number))
            throw new PaymentValidationException("The card number is not valid.");

        var expiry = NormaliseExpiry(card.Expiry);
        var match = ExpiryPattern.Match(expiry);
        if (!match.Success)
            throw new PaymentValidationException("The card expiry must be given as YYYY-MM (or MM/YY).");
        var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        if (month is < 1 or > 12)
            throw new PaymentValidationException("The card expiry month is not valid.");
        var endOfMonth = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        if (endOfMonth <= now)
            throw new PaymentValidationException("The card has expired.");

        if (card.SecurityCode is not null && !SecurityCodePattern.IsMatch(card.SecurityCode))
            throw new PaymentValidationException("The card security code must be 3 or 4 digits.");

        var address = card.BillingAddress;
        if (address is not null)
        {
            var country = (address.CountryCode ?? string.Empty).Trim().ToUpperInvariant();
            if (!CountryPattern.IsMatch(country))
                throw new PaymentValidationException("The billing address country code must be a two-letter ISO code.");
            address = address with { CountryCode = country };
        }

        return card with { Number = number, Expiry = expiry, BillingAddress = address };
    }

    private static string NormaliseExpiry(string? expiry)
    {
        var value = (expiry ?? string.Empty).Trim();
        // Accept the common MM/YY and MM/YYYY spellings as well as the provider's YYYY-MM.
        var slash = Regex.Match(value, @"^(\d{1,2})/(\d{2}|\d{4})$");
        if (!slash.Success) return value;
        var month = int.Parse(slash.Groups[1].Value, CultureInfo.InvariantCulture);
        var year = slash.Groups[2].Value.Length == 2
            ? 2000 + int.Parse(slash.Groups[2].Value, CultureInfo.InvariantCulture)
            : int.Parse(slash.Groups[2].Value, CultureInfo.InvariantCulture);
        return $"{year:0000}-{month:00}";
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }
}
