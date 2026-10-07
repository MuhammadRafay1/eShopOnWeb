
# Instrument 2

Instrument details.

*This model accepts additional fields of type object.*

## Structure

`Instrument2`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Isin` | `string` | Optional | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |
| `ShortName` | `string` | Optional | Instrument short name.<br><br>**Constraints**: *Maximum Length*: `100` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Instrument2 instrument2 = new Instrument2
{
    Isin = "isin4",
    ShortName = "short_name2",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

