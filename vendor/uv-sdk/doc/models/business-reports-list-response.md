
# Business Reports List Response

Paginated response containing the reports generated for a business.

## Structure

`BusinessReportsListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta27`](../../doc/models/meta-27.md) | Required | - |
| `Data` | [`List<BusinessReport>`](../../doc/models/business-report.md) | Required | List of reports generated for the business |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessReportsListResponse businessReportsListResponse = new BusinessReportsListResponse
{
    Meta = new Meta27
    {
        Offset = 222,
        Limit = 126,
        Count = 14,
        TotalCount = 150,
        Sort = "sort4",
        Order = Order1.Asc,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Data = new List<BusinessReport>
    {
        new BusinessReport
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            BusinessId = new Guid("000004d8-0000-0000-0000-000000000000"),
            Type = ReportType.QuarterlyAccountStatement,
            SubstitutedReportId = new Guid("000004c2-0000-0000-0000-000000000000"),
            Data = new ReportData
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
                },
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            },
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
};
```

