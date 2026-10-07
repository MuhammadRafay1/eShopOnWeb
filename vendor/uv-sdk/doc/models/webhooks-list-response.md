
# Webhooks List Response

## Structure

`WebhooksListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<Webhook>`](../../doc/models/webhook.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

WebhooksListResponse webhooksListResponse = new WebhooksListResponse
{
    Meta = new Meta
    {
        Offset = 222,
        Limit = 126,
        Count = 14,
        TotalCount = 150,
        Sort = "sort4",
        Order = Order1.Asc,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Data = new List<Webhook>
    {
        new Webhook
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Title = "title6",
            Url = "url4",
            Enabled = false,
            Type = new List<UpvestInvestmentApi.Standard.Models.Type>
            {
                UpvestInvestmentApi.Standard.Models.Type.AccountValuation,
                UpvestInvestmentApi.Standard.Models.Type.CorporateAction,
                UpvestInvestmentApi.Standard.Models.Type.PortfolioOrder,
            },
            Config = new Config
            {
                Delay = "delay8",
                MaxPackageSize = 152,
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            },
            ExcludeType = new List<ExcludeType>
            {
                ExcludeType.IsaWrapper,
                ExcludeType.IsaWrapperAllowance,
            },
        },
    },
};
```

