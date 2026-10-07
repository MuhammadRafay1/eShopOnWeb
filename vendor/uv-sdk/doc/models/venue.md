
# Venue

Venue

*This model accepts additional fields of type object.*

## Structure

`Venue`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Name` | `string` | Optional | The name of venue. |
| `Id` | `Guid` | Required | Venue unique identifier. |
| `PriceQualities` | [`List<PriceQuality>`](../../doc/models/price-quality.md) | Optional | The available price qualities. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Venue venue = new Venue
{
    Id = new Guid("00000ac6-0000-0000-0000-000000000000"),
    Name = "name8",
    PriceQualities = new List<PriceQuality>
    {
        PriceQuality.Realtime,
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

