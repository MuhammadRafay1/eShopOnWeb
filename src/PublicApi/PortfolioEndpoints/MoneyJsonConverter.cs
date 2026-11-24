using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Serialises a monetary <see cref="decimal"/> as a JSON number with exactly two decimal
/// places (euros). Reads standard JSON numbers back.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? decimal.Parse(reader.GetString()!, NumberStyles.Any, CultureInfo.InvariantCulture)
            : reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.ToString("0.00", CultureInfo.InvariantCulture));
}
