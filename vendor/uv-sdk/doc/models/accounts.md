
# Accounts

*This model accepts additional fields of type object.*

## Structure

`Accounts`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Accounts` | `List<Guid>` | Required | List of accounts |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Accounts accounts = new Accounts
{
    Accounts = new List<Guid>
    {
        new Guid("00002166-0000-0000-0000-000000000000"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

