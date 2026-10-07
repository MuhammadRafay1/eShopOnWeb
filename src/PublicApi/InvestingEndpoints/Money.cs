using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Serializes a euro amount as a JSON number with exactly two decimal places (e.g. <c>0.70</c>,
/// <c>10.40</c>, <c>0.00</c>), as the investing API's money fields require.
/// </summary>
public class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteRawValue(value.ToString("F2", CultureInfo.InvariantCulture));
}
