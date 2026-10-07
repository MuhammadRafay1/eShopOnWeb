
# Webhook

## Structure

`Webhook`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Webhook unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Title` | `string` | Required | Title of the webhook for use on tenant side.<br><br>**Constraints**: *Pattern*: `^[a-zA-Z0-9 ()\[\]{}.-]{1,32}$` |
| `Url` | `string` | Required | The callback URL to be called by the webhook.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `Enabled` | `bool` | Required | Enable/disable webhook. |
| `Type` | [`List<Type>`](../../doc/models/type.md) | Required | What kind of events to be sent by the webhook. |
| `ExcludeType` | [`List<ExcludeType>`](../../doc/models/exclude-type.md) | Optional | What kind of events to be excluded if subscription type is: ALL. |
| `Config` | [`Config`](../../doc/models/config.md) | Required | Configuration of webhook packages collection. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Webhook webhook = new Webhook
{
    Id = new Guid("00000002-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Title = "title2",
    Url = "url6",
    Enabled = false,
    Type = new List<UpvestInvestmentApi.Standard.Models.Type>
    {
        UpvestInvestmentApi.Standard.Models.Type.AccountGroup,
        UpvestInvestmentApi.Standard.Models.Type.Instrument,
        UpvestInvestmentApi.Standard.Models.Type.Report,
    },
    Config = new Config
    {
        Delay = "delay8",
        MaxPackageSize = 152,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    ExcludeType = new List<ExcludeType>
    {
        ExcludeType.AccountLiquidation,
    },
};
```

