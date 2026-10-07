
# User Check Proof of Residency Create Request

The PoR (Proof of residency) check is completed by the client sharing a valid PoR document, if not fulfilled by the KYC check.

*This model accepts additional fields of type object.*

## Structure

`UserCheckProofOfResidencyCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | The type of check must be POR.<br><br>**Default**: `"POR"` |
| `CheckConfirmedAt` | `DateTime` | Required | Completion date and time of the PoR check. |
| `IssuanceDate` | `DateTime` | Required | Issuance date in YYYY-MM-DD format. |
| `DataDownloadLink` | `string` | Required | Download URL for the PoR evidence file. The file size must not exceed 250 MB.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `DocumentType` | [`DocumentType6`](../../doc/models/document-type-6.md) | Required | The type of document used in the PoR process. Maximum age of the document is 12 months (stated on the document) applicable for: Utility bills (water, gas, electricity), Telephone bills (only landline), Internet bills, Bank account statements. Documents that need to be still valid - Registration certificate (must be valid and issued within the past 5 years), Residence permit e.g. Blue Card (as long as valid and contains the registration address), ID Card that contains the registration address.<br><br>* UTILITY_BILL -<br>* TELEPHONE_BILL -<br>* INTERNET_BILL -<br>* BANK_STATEMENT -<br>* REGISTRATION_CERT -<br>* RESIDENCE_PERMIT -<br>* ID_CARD - |
| `ConfirmedAddress` | [`Address`](../../doc/models/address.md) | Required | Address. Must not be a P.O. box or c/o address. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserCheckProofOfResidencyCreateRequest userCheckProofOfResidencyCreateRequest = new UserCheckProofOfResidencyCreateRequest
{
    Type = "POR",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    IssuanceDate = DateTime.Parse("2016-03-13"),
    DataDownloadLink = "data_download_link2",
    DocumentType = DocumentType6.RegistrationCert,
    ConfirmedAddress = new Address
    {
        AddressLine1 = "address_line16",
        Postcode = "postcode6",
        Country = Country.Tv,
        City = "city2",
        AddressLine2 = "address_line24",
        State = "state8",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

