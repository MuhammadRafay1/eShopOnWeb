
# Config

Configuration of webhook packages collection.

*This model accepts additional fields of type object.*

## Structure

`Config`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Delay` | `string` | Optional | Maximum time of package collection (1s-30s).<br><br>**Constraints**: *Pattern*: `^([1-9]\|[12][0-9]\|30)s$` |
| `MaxPackageSize` | `int?` | Optional | Maximum package size (bytes)<br><br>**Constraints**: `>= 100`, `<= 1048576` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Config config = new Config
{
    Delay = "delay8",
    MaxPackageSize = 152,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

