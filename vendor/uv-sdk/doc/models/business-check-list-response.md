
# Business Check List Response

List of business checks.

## Structure

`BusinessCheckListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Data` | [`List<BusinessCheckListResponseData>`](../../doc/models/containers/business-check-list-response-data.md) | Required | This is List of a container for one-of cases. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

BusinessCheckListResponse businessCheckListResponse = new BusinessCheckListResponse
{
    Data = new List<BusinessCheckListResponseData>
    {
        BusinessCheckListResponseData.FromBusinessCheckKYB(
            new BusinessCheckKyb
            {
                Id = new Guid("00000d00-0000-0000-0000-000000000000"),
                BusinessId = new Guid("00001cbe-0000-0000-0000-000000000000"),
                Type = "type2",
                OriginallyConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                Status = Status109.Failed,
                DataDownloadLink = "data_download_link0",
                DocumentType = "document_type6",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
    },
};
```

