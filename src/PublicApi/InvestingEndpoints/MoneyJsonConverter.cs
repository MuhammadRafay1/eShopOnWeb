using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Writes a money amount as a JSON number with exactly two decimal places (euros), and reads either a number
/// or a numeric string back to <see cref="decimal"/>.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            return decimal.Parse(s!, NumberStyles.Number, CultureInfo.InvariantCulture);
        }
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        // Emit as a bare JSON number with two decimals, e.g. 0.70, 12.00.
        writer.WriteRawValue(value.ToString("0.00", CultureInfo.InvariantCulture));
    }
}
