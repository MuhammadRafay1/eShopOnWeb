using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Conversions across the boundary between the shop's domain (decimal euros, decimal weights) and Upvest's
/// wire contract (culture-invariant decimal strings with specific precision). Keeping these in one place
/// prevents a culture-dependent comma or a wrong precision slipping into a request.
/// </summary>
internal static class UpvestConversions
{
    /// <summary>Format a euro amount as a 2-decimal invariant string (matches Upvest's amount regex).</summary>
    public static string Amount(decimal euros) =>
        Math.Round(euros, 2, MidpointRounding.ToEven).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Format a model weight (percentage) for Upvest's allocation weight regex
    /// <c>^(100|[0-9]{1,2}(\.[0-9]{1,5})?)$</c>: up to 5 decimals, no trailing zeros, "100" for 100.
    /// </summary>
    public static string Weight(decimal weight)
    {
        var rounded = Math.Round(weight, 5, MidpointRounding.ToEven);
        if (rounded == 100m) return "100";
        // "0.#####" drops trailing zeros and the decimal point when whole.
        return rounded.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    /// <summary>Parse an Upvest decimal string (amount/quantity/value) back to a decimal, invariant culture.</summary>
    public static decimal ParseDecimal(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? 0m
            : decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    /// <summary>
    /// A stable idempotency key derived from the parts of a logical request, so a retry of the same request
    /// reuses the same key and Upvest treats it as one write. Never <c>Guid.NewGuid()</c>.
    /// </summary>
    public static Guid DeterministicKey(params string[] parts)
    {
        var input = string.Join("|", parts);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }
}
