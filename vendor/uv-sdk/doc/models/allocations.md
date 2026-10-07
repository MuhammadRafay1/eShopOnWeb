
# Allocations

*This model accepts additional fields of type object.*

## Structure

`Allocations`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Allocations` | `List<Guid>` | Required | List of allocations |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Allocations allocations = new Allocations
{
    Allocations = new List<Guid>
    {
        new Guid("00001ca2-0000-0000-0000-000000000000"),
        new Guid("00001ca3-0000-0000-0000-000000000000"),
        new Guid("00001ca4-0000-0000-0000-000000000000"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

