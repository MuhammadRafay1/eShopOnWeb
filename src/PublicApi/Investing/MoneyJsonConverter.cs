using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.Investing;

/// <summary>
/// Serialises euro amounts as JSON numbers with exactly two decimal places (e.g. 0.70, 10.00),
/// as required by the investing API contract, while still reading plain JSON numbers.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture)
            : reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        var text = Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
        writer.WriteRawValue(text);
    }
}
