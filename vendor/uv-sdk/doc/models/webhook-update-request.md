
# Webhook Update Request

## Structure

`WebhookUpdateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Title` | `string` | Optional | Title of the webhook for use on tenant side.<br><br>**Constraints**: *Pattern*: `^[a-zA-Z0-9 ()\[\]{}.-]{1,32}$` |
| `Url` | `string` | Optional | The callback URL to be called by the webhook.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `Enabled` | `bool?` | Optional | Enable/disable webhook. |
| `Type` | [`List<Type>`](../../doc/models/type.md) | Optional | What kind of events to be sent by the webhook. |
| `ExcludeType` | [`List<ExcludeType>`](../../doc/models/exclude-type.md) | Optional | What kind of events to be excluded if subscription type is: ALL. |
| `Config` | [`Config`](../../doc/models/config.md) | Optional | Configuration of webhook packages collection. |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;

WebhookUpdateRequest webhookUpdateRequest = new WebhookUpdateRequest
{
    Title = "title8",
    Url = "url6",
    Enabled = false,
    Type = new List<UpvestInvestmentApi.Standard.Models.Type>
    {
        UpvestInvestmentApi.Standard.Models.Type.VirtualCashDecrease,
    },
    ExcludeType = new List<ExcludeType>
    {
        ExcludeType.TaxExemption,
        ExcludeType.TaxCollection,
        ExcludeType.IsaWrapper,
    },
};
```

