using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.Configuration;

/// <summary>
/// Writes a euro amount as a JSON number with exactly two decimal places
/// (e.g. 0.50, 10.00), as required by the investing API contract.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        writer.WriteRawValue(rounded.ToString("F2", CultureInfo.InvariantCulture));
    }
}
