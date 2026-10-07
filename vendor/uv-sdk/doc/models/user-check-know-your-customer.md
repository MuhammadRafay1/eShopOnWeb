
# User Check Know Your Customer

The KYC (Know your customer) check is completed by the client sharing the valid and relevant KYC data for the user.

*This model accepts additional fields of type object.*

## Structure

`UserCheckKnowYourCustomer`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | User Check unique identifier. |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `Type` | `string` | Required | The type of check must be “KYC”.<br><br>**Default**: `"KYC"` |
| `CheckConfirmedAt` | `DateTime` | Required | Completion date and time of the KYC check. Must not be older than 24 months. |
| `DataDownloadLink` | `string` | Required | Download URL for the KYC evidence file. The file size must not exceed 250 MB.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `DocumentType` | [`DocumentType3`](../../doc/models/document-type-3.md) | Required | The type of document used in the KYC process.<br><br>* PASSPORT - Passport<br>* ID_CARD - National Identification document<br>* RESIDENCE_PERMIT - Residence Permit<br>* DRIVING_LICENSE - Driving License<br>* TWO_PLUS_TWO_VERIFICATION_PROOF - Two plus two verification proof (for GB residence users only) |
| `DocumentExpirationDate` | `DateTime?` | Optional | Expiration date of the document used in KYC process in YYYY-MM-DD format.<br><br>**The field is required for the following document types:**<br><br>* PASSPORT<br>* ID_CARD<br>* RESIDENCE_PERMIT<br>* DRIVING_LICENSE |
| `Nationality` | `string` | Optional | Issuing country of the document used in the KYC process. [ISO 3166 alpha-2 codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2).<br><br>**The field is required for the following document types:**<br><br>* PASSPORT<br>* ID_CARD<br>* RESIDENCE_PERMIT<br>* DRIVING_LICENSE<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}$` |
| `Status` | [`Status6`](../../doc/models/status-6.md) | Required | Final status of the KYC check.<br><br>* IN_PROGRESS - KYC check is in progress<br>* PASSED - KYC check passed<br>* FAILED - KYC check failed |
| `Provider` | `string` | Required | Provider that was used to perform the KYC check.<br><br>**Constraints**: *Maximum Length*: `100` |
| `Method` | [`Method`](../../doc/models/method.md) | Required | Method used for AML-compliant KYC process<br><br>* VIDEO_ID - Video identification<br>* IN_PERSON_ID - In-person identification at the post office or the client's outlet<br>* ELECTRONIC_ID - Advanced electronic identification methods (namely German eID)<br>* LIVENESS_PHOTO_ID - Photos and security features of the identification document in combination with a video-based liveness check (residence country must not be Germany in this case)<br>* QUALIFIED_ELECTRONIC_SIGNATURE_WITH_TX - Qualified electronic signature accompanied by a bank transaction for verification<br>* TWO_PLUS_TWO_VERIFICATION - A method of verifying identity by matching at least two personal details from two separate sources (for GB residence users only)<br>* ID_COLLECTION - The identification document of the user is collected but has not undergone verification |
| `ConfirmedAddress` | [`Address`](../../doc/models/address.md) | Optional | Address. Must not be a P.O. box or c/o address. |
| `KycUpdate` | `bool?` | Optional | Marks this check as a KYC refresh/update as opposed to an initial record. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserCheckKnowYourCustomer userCheckKnowYourCustomer = new UserCheckKnowYourCustomer
{
    Id = new Guid("0000183c-0000-0000-0000-000000000000"),
    UserId = new Guid("00001f4c-0000-0000-0000-000000000000"),
    Type = "KYC",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    DataDownloadLink = "data_download_link4",
    DocumentType = DocumentType3.ResidencePermit,
    Status = Status6.Failed,
    Provider = "provider4",
    Method = Method.IdCollection,
    DocumentExpirationDate = DateTime.Parse("2016-03-13"),
    Nationality = "nationality2",
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
    KycUpdate = false,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

