
# Webhook Create Request

## Structure

`WebhookCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Title` | `string` | Required | Title of the webhook for use on tenant side.<br><br>**Constraints**: *Pattern*: `^[a-zA-Z0-9 ()\[\]{}.-]{1,32}$` |
| `Url` | `string` | Required | The callback URL to be called by the webhook.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `Type` | [`List<Type>`](../../doc/models/type.md) | Optional | What kind of events to be sent by the webhook. |
| `ExcludeType` | [`List<ExcludeType>`](../../doc/models/exclude-type.md) | Optional | What kind of events to be excluded if subscription type is: ALL. |
| `Config` | [`Config`](../../doc/models/config.md) | Optional | Configuration of webhook packages collection. |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

WebhookCreateRequest webhookCreateRequest = new WebhookCreateRequest
{
    Title = "title2",
    Url = "url0",
    Type = new List<UpvestInvestmentApi.Standard.Models.Type>
    {
        UpvestInvestmentApi.Standard.Models.Type.SavingsPlan,
    },
    ExcludeType = new List<ExcludeType>
    {
        ExcludeType.User,
        ExcludeType.UserCheck,
        ExcludeType.Order,
    },
    Config = new Config
    {
        Delay = "delay8",
        MaxPackageSize = 152,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
};
```

