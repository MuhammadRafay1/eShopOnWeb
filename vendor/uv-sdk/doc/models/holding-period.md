
# Holding Period

Holding period.

*This model accepts additional fields of type object.*

## Structure

`HoldingPeriod`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Unit` | [`Unit?`](../../doc/models/unit.md) | Optional | Unit of time.<br><br>* YEAR -<br><br>**Default**: `Unit.YEAR` |
| `Quantity` | `int?` | Optional | Quantity of time units. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

HoldingPeriod holdingPeriod = new HoldingPeriod
{
    Unit = Unit.Year,
    Quantity = 106,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

