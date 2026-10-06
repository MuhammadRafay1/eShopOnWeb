using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Serialises a money amount as a JSON number with exactly two decimal places (euros), e.g.
/// <c>0.50</c>, <c>10.00</c>, <c>12.30</c>.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteRawValue(value.ToString("F2", CultureInfo.InvariantCulture));
}
