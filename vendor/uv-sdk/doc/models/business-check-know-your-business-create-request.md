
# Business Check Know Your Business Create Request

The KYB (Know Your Business) check is completed by the client sharing the valid and relevant KYB data for the business.

*This model accepts additional fields of type object.*

## Structure

`BusinessCheckKnowYourBusinessCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | The type of check must be “KYB”.<br><br>**Default**: `"KYB"` |
| `OriginallyConfirmedAt` | `DateTime` | Required | The date and time when the check was first confirmed. When the check has only been confirmed once, this value is the same as check_confirmed_at. When renewing KYB check, this value remains the same as when the check was first confirmed. |
| `CheckConfirmedAt` | `DateTime` | Required | The date and time when the check was latest confirmed |
| `DataDownloadLink` | `string` | Required | Link to download the document bundle submitted for the KYB check |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessCheckKnowYourBusinessCreateRequest businessCheckKnowYourBusinessCreateRequest = new BusinessCheckKnowYourBusinessCreateRequest
{
    Type = "KYB",
    OriginallyConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    DataDownloadLink = "data_download_link0",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

