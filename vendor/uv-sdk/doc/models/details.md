
# Details

Details

*This model accepts additional fields of type object.*

## Structure

`Details`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `DistributionCountries` | `List<string>` | Optional | Distribution Countries<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Details details = new Details
{
    DistributionCountries = new List<string>
    {
        "distribution_countries3",
        "distribution_countries4",
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

