
# Business Check Kyb

A Know Your Business (KYB) check for a business, recording confirmation timestamps and a link to the submitted documents.

*This model accepts additional fields of type object.*

## Structure

`BusinessCheckKyb`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Business Check unique identifier. |
| `BusinessId` | `Guid` | Required | Unique identifier for the business. |
| `Type` | `string` | Required | The type of check must be “KYB”.<br><br>**Default**: `"KYB"` |
| `OriginallyConfirmedAt` | `DateTime` | Required | The date and time when the check was first confirmed. When the check has only been confirmed once, this value is the same as check_confirmed_at. When renewing KYB check, this value remains the same as when the check was first confirmed. |
| `CheckConfirmedAt` | `DateTime` | Required | The date and time when the check was latest confirmed |
| `Status` | [`Status109`](../../doc/models/status-109.md) | Required | Final status of the business check.<br><br>* IN_PROGRESS - Check is in progress<br>* PASSED - Check passed<br>* FAILED - Check failed |
| `DataDownloadLink` | `string` | Required | Link to download the document bundle submitted for the KYB check |
| `DocumentType` | `string` | Required | Type of the document bundle. Always `KYB_DOCUMENTS`<br><br>**Default**: `"KYB_DOCUMENTS"` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessCheckKyb businessCheckKyb = new BusinessCheckKyb
{
    Id = new Guid("000001c4-0000-0000-0000-000000000000"),
    BusinessId = new Guid("00001182-0000-0000-0000-000000000000"),
    Type = "KYB",
    OriginallyConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Status = Status109.InProgress,
    DataDownloadLink = "data_download_link4",
    DocumentType = "KYB_DOCUMENTS",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

