using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Serialises a money amount as a JSON number in euros with exactly two decimal places
/// (e.g. <c>100.00</c>), regardless of the underlying decimal's scale.
/// </summary>
public class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteRawValue(value.ToString("0.00", CultureInfo.InvariantCulture));
}
