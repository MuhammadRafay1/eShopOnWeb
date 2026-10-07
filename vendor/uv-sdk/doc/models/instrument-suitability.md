
# Instrument Suitability

Outcome of the user's instrument suitability assessment.

*This model accepts additional fields of type object.*

## Structure

`InstrumentSuitability`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Suitability` | `bool` | Required | Did the user go through a suitability assessment and the outcome indicated suitability. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

InstrumentSuitability instrumentSuitability = new InstrumentSuitability
{
    Suitability = false,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

