
# User Check Guardian Create Request

The Guardian check is completed by the client providing documentation proving the guardian relationship with the child user.

*This model accepts additional fields of type object.*

## Structure

`UserCheckGuardianCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | The type of check must be “GUARDIAN“.<br><br>**Default**: `"GUARDIAN"` |
| `CheckConfirmedAt` | `DateTime` | Required | Completion date and time of the PoR check. |
| `RoleId` | `Guid` | Required | Unique identifier for the role that this guardian check is associated with. |
| `DocumentType` | `string` | Required, Constant | The type of document used in the Guardian check.<br><br>* BIRTH_CERTIFICATE - Birth certificate proving guardian relationship<br><br>**Value**: `"BIRTH_CERTIFICATE"` |
| `DataDownloadLink` | `string` | Required | Download URL for the guardian evidence file. The file size must not exceed 250 MB.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserCheckGuardianCreateRequest userCheckGuardianCreateRequest = new UserCheckGuardianCreateRequest
{
    Type = "GUARDIAN",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    RoleId = new Guid("000006d6-0000-0000-0000-000000000000"),
    DocumentType = "BIRTH_CERTIFICATE",
    DataDownloadLink = "data_download_link6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

