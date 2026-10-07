
# Webhooks Test Response

*This model accepts additional fields of type object.*

## Structure

`WebhooksTestResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Url` | `string` | Required | - |
| `Response` | [`Response`](../../doc/models/response.md) | Required | - |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

WebhooksTestResponse webhooksTestResponse = new WebhooksTestResponse
{
    Url = "url2",
    Response = new Response
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
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

