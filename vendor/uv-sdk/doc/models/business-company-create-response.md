
# Business Company Create Response

Response schema for the create business endpoint.

*This model accepts additional fields of type object.*

## Structure

`BusinessCompanyCreateResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid?` | Optional | Unique identifier for the business. |
| `CreatedAt` | `DateTime?` | Optional | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime?` | Optional | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `ContactEmail` | `string` | Optional | Contact email address of the business. |
| `RegisteredAddress` | [`Address`](../../doc/models/address.md) | Optional | Address. Must not be a P.O. box or c/o address. |
| `RegistrationNumber` | `string` | Optional | Registration number of the business. |
| `TaxInformation` | [`TaxInformation`](../../doc/models/tax-information.md) | Optional | Tax information of the business. |
| `BusinessType` | [`BusinessType?`](../../doc/models/business-type.md) | Optional | Type of the business. |
| `Identification` | [`Identification8`](../../doc/models/identification-8.md) | Optional | Identification information of the business. |
| `TermsAndConditions` | [`TermsAndConditions4`](../../doc/models/terms-and-conditions-4.md) | Optional | Terms and conditions agreement. |
| `DataPrivacyAndSharingAgreement` | [`DataPrivacyAndSharingAgreement4`](../../doc/models/data-privacy-and-sharing-agreement-4.md) | Optional | Data privacy and sharing agreement. |
| `Status` | [`Status103?`](../../doc/models/status-103.md) | Optional | Status of the business.<br><br>* ACTIVE -<br>* INACTIVE -<br>* OFFBOARDING -<br>* OFFBOARDED - |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessCompanyCreateResponse businessCompanyCreateResponse = new BusinessCompanyCreateResponse
{
    Id = new Guid("00001fc4-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ContactEmail = "contact_email8",
    RegisteredAddress = new Address
    {
        AddressLine1 = "address_line10",
        Postcode = "postcode0",
        Country = Country.Nc,
        City = "city4",
        AddressLine2 = "address_line28",
        State = "state8",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

