
# User Check Us Withholding Tax Status

The US Withholding Tax Status check captures the user's US tax person classification and the supporting documentation used to determine US withholding obligations.

*This model accepts additional fields of type object.*

## Structure

`UserCheckUsWithholdingTaxStatus`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | User Check unique identifier. |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `Type` | `string` | Required | The type of check must be "US_WITHHOLDING_TAX_STATUS".<br><br>**Default**: `"US_WITHHOLDING_TAX_STATUS"` |
| `CheckConfirmedAt` | `DateTime` | Required | The date and time when the US withholding tax status was confirmed. |
| `DataDownloadLink` | `string` | Optional | Download URL for the withholding status evidence file. Only required when `documentation_type` is "W8-BEN" or "W9", and only when your operating model requires supporting documents to be supplied to Upvest. The file size must not exceed 250 MB.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `PersonType` | [`UserPersonType`](../../doc/models/user-person-type.md) | Required | US tax person classification of the user.<br><br>* US_PERSON - The user is a US person for tax purposes.<br>* NON_RESIDENT_ALIEN - The user is a non-resident alien for US tax purposes. |
| `DocumentationType` | [`UserDocumentationType`](../../doc/models/user-documentation-type.md) | Required | Type of documentation supporting the US withholding tax status.<br><br>* KYC - Withholding status derived from the existing KYC data (non-resident aliens only).<br>* W8-BEN - IRS Form W-8BEN provided by the user (non-resident aliens only).<br>* W9 - IRS Form W-9 provided by the user (US persons only).<br>* UNDOCUMENTED - No documentation is available for the user.<br><br>For a non-resident alien, "KYC" is accepted for every operating model; whether "W8-BEN" and "UNDOCUMENTED" are also accepted depends on your operating model. |
| `ValidFrom` | `DateTime` | Required | The date from which the US withholding tax status is valid, in YYYY-MM-DD format. |
| `ValidTo` | `DateTime?` | Optional | The date until which the US withholding tax status is valid, in YYYY-MM-DD format. Null if the status has no defined end date. |
| `Status` | [`Status11`](../../doc/models/status-11.md) | Required | Final status of the US Withholding Tax Status check.<br><br>* IN_PROGRESS - US Withholding Tax Status check is in progress<br>* PASSED - US Withholding Tax Status check passed<br>* FAILED - US Withholding Tax Status check failed<br><br>A documented status ("KYC", "W8-BEN" or "W9") is corroborated against the user's tax residency: it fails when the declared `person_type` contradicts the tax residency Upvest holds for the user, or when the user has no active tax residency on record. An "UNDOCUMENTED" status is recorded as declared and is not corroborated against tax residency. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserCheckUsWithholdingTaxStatus userCheckUsWithholdingTaxStatus = new UserCheckUsWithholdingTaxStatus
{
    Id = new Guid("000006c4-0000-0000-0000-000000000000"),
    UserId = new Guid("00000dd4-0000-0000-0000-000000000000"),
    Type = "US_WITHHOLDING_TAX_STATUS",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    PersonType = UserPersonType.UsPerson,
    DocumentationType = UserDocumentationType.W9,
    ValidFrom = DateTime.Parse("2016-03-13"),
    Status = Status11.Passed,
    DataDownloadLink = "data_download_link4",
    ValidTo = DateTime.Parse("2016-03-13"),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

