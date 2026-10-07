
# User Tol Data Change Request Nationality

*This model accepts additional fields of type object.*

## Structure

`UserTolDataChangeRequestNationality`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Nationalities` | [`List<Nationality>`](../../doc/models/nationality.md) | Required | Nationalities of the user. [ISO 3166 alpha-2 Codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2).<br><br>**Constraints**: *Minimum Items*: `1` |
| `IssuanceDate` | `DateTime` | Required | Issuance date in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) |
| `DataDownloadLink` | `string` | Required | Download URL for the document proving the data change. The file size must not exceed 250 MB.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `DocumentType` | [`DocumentType`](../../doc/models/document-type.md) | Required | The type of document used to prove the data change<br><br>* PASSPORT - Passport<br>* ID_CARD - National Identification document |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserTolDataChangeRequestNationality userTolDataChangeRequestNationality = new UserTolDataChangeRequestNationality
{
    Nationalities = new List<Nationality>
    {
        Nationality.Nc,
        Nationality.Na,
    },
    IssuanceDate = DateTime.Parse("2016-03-13"),
    DataDownloadLink = "data_download_link4",
    DocumentType = DocumentType.Passport,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

