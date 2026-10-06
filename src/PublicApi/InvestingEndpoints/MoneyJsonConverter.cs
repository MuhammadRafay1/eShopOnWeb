using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Writes money as a JSON number in euros with exactly two decimal places (e.g. <c>0.70</c>,
/// <c>10.00</c>), as required by the investing API responses.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String
            ? decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture)
            : reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        var rounded = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        writer.WriteRawValue(rounded.ToString("0.00", CultureInfo.InvariantCulture));
    }
}
