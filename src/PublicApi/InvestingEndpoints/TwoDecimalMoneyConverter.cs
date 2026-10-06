using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Writes a money amount as a JSON number with exactly two decimal places (e.g. <c>0.00</c>, <c>10.30</c>),
/// as the API contract requires. Reads a JSON number back as a decimal.
/// </summary>
public sealed class TwoDecimalMoneyConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.ToString("F2", CultureInfo.InvariantCulture));
}
