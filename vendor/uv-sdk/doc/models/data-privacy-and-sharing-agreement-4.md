
# Data Privacy and Sharing Agreement 4

Data privacy and sharing agreement.

*This model accepts additional fields of type object.*

## Structure

`DataPrivacyAndSharingAgreement4`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `ConsentDocumentId` | `Guid` | Required | UUID of the data privacy and sharing agreement document. |
| `ConfirmedAt` | `DateTime` | Required | Timestamp when the data privacy and sharing agreement was confirmed. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

DataPrivacyAndSharingAgreement4 dataPrivacyAndSharingAgreement4 = new DataPrivacyAndSharingAgreement4
{
    ConsentDocumentId = new Guid("00002394-0000-0000-0000-000000000000"),
    ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

