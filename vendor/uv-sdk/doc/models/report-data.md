
# Report Data

Contents of the report.

*This model accepts additional fields of type object.*

## Structure

`ReportData`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Account` | [`Account7`](../../doc/models/account-7.md) | Optional | Account information. |
| `References` | [`List<ReportReferenceData>`](../../doc/models/report-reference-data.md) | Optional | - |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

ReportData reportData = new ReportData
{
    Account = new Account7
    {
        Id = new Guid("000025e4-0000-0000-0000-000000000000"),
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    References = new List<ReportReferenceData>
    {
        new ReportReferenceData
        {
            Id = new Guid("00000f98-0000-0000-0000-000000000000"),
            Type = ReportReferenceType.AccountId,
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        new ReportReferenceData
        {
            Id = new Guid("00000f98-0000-0000-0000-000000000000"),
            Type = ReportReferenceType.AccountId,
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        new ReportReferenceData
        {
            Id = new Guid("00000f98-0000-0000-0000-000000000000"),
            Type = ReportReferenceType.AccountId,
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

