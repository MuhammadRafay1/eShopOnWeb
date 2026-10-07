using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// Writes euro money as a JSON number with exactly two decimal places (e.g. <c>0.70</c>, <c>10.00</c>),
/// so money fields have a stable shape regardless of a decimal's internal scale.
/// </summary>
public class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        var rounded = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        writer.WriteRawValue(rounded.ToString("0.00", CultureInfo.InvariantCulture));
    }
}
