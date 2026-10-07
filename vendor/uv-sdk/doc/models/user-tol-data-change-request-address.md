
# User Tol Data Change Request Address

*This model accepts additional fields of type object.*

## Structure

`UserTolDataChangeRequestAddress`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Address` | [`Address`](../../doc/models/address.md) | Required | Address. Must not be a P.O. box or c/o address. |
| `IssuanceDate` | `DateTime` | Required | Issuance date in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) |
| `DataDownloadLink` | `string` | Required | Download URL for the document proving the data change. The file size must not exceed 250 MB.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `DocumentType` | [`DocumentType2`](../../doc/models/document-type-2.md) | Required | The type of document used to proof data change<br><br>* UTILITY_BILL - Utility bill<br>* TELEPHONE_BILL - Telephone bill<br>* INTERNET_BILL - Internet bill<br>* BANK_STATEMENT - Bank statement<br>* REGISTRATION_CERT - Registration certificate<br>* RESIDENCE_PERMIT - Residence permit<br>* ID_CARD - National Identification document |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserTolDataChangeRequestAddress userTolDataChangeRequestAddress = new UserTolDataChangeRequestAddress
{
    Address = new Address
    {
        AddressLine1 = "address_line10",
        Postcode = "postcode0",
        Country = Country.Bf,
        City = "city6",
        AddressLine2 = "address_line28",
        State = "state2",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    IssuanceDate = DateTime.Parse("2016-03-13"),
    DataDownloadLink = "data_download_link0",
    DocumentType = DocumentType2.UtilityBill,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

