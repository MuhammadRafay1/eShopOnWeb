
# User Tol Data Change Request Name Change

*This model accepts additional fields of type object.*

## Structure

`UserTolDataChangeRequestNameChange`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `FirstName` | `string` | Required | First name(s) of the user. Please include all first and middle names of the user.<br><br>**Constraints**: *Minimum Length*: `2`, *Maximum Length*: `100` |
| `LastName` | `string` | Required | Last name of the user.<br><br>**Constraints**: *Minimum Length*: `1`, *Maximum Length*: `100` |
| `IssuanceDate` | `DateTime` | Required | Issuance date in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) |
| `DataDownloadLink` | `string` | Required | Download URL for the document proving the data change. The file size must not exceed 250 MB.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `DocumentType` | [`DocumentType`](../../doc/models/document-type.md) | Required | The type of document used to prove the data change<br><br>* PASSPORT - Passport<br>* ID_CARD - National Identification document |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserTolDataChangeRequestNameChange userTolDataChangeRequestNameChange = new UserTolDataChangeRequestNameChange
{
    FirstName = "first_name0",
    LastName = "last_name8",
    IssuanceDate = DateTime.Parse("2016-03-13"),
    DataDownloadLink = "data_download_link2",
    DocumentType = DocumentType.Passport,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

