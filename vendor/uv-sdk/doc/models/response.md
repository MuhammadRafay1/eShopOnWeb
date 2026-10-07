
# Response

*This model accepts additional fields of type object.*

## Structure

`Response`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Status` | `int` | Required | - |
| `Headers` | `Dictionary<string, string>` | Required | - |
| `Body` | `string` | Required | - |
| `Error` | `string` | Optional | - |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Response response = new Response
{
    Status = 110,
    Headers = new Dictionary<string, string>
    {
        ["key0"] = "headers3",
        ["key1"] = "headers4",
        ["key2"] = "headers5",
    },
    Body = "body6",
    Error = "error4",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

