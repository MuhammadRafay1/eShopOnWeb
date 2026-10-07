
# Twr

*This model accepts additional fields of type object.*

## Structure

`Twr`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Daily` | `string` | Optional | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Cumulative` | `string` | Optional | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `CumulativeStartDate` | `DateTime?` | Optional | Date when the cumulative time-weighted returns calculation starts. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Twr twr = new Twr
{
    Daily = "daily2",
    Cumulative = "cumulative2",
    CumulativeStartDate = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

