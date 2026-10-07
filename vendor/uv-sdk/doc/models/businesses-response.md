
# Businesses Response

Schema for business company information.

*This model accepts additional fields of type object.*

## Structure

`BusinessesResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Unique identifier for the business. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `ContactEmail` | `string` | Required | Contact email address of the business. |
| `RegisteredAddress` | [`Address`](../../doc/models/address.md) | Required | Address. Must not be a P.O. box or c/o address. |
| `RegistrationNumber` | `string` | Optional | Registration number of the business. |
| `TaxInformation` | [`TaxInformation`](../../doc/models/tax-information.md) | Required | Tax information of the business. |
| `BusinessType` | `string` | Required, Constant | Type of the business.<br><br>**Value**: `"LIMITED_LIABILITY_COMPANY"` |
| `Identification` | [`Identification8`](../../doc/models/identification-8.md) | Required | Identification information of the business. |
| `TermsAndConditions` | [`TermsAndConditions4`](../../doc/models/terms-and-conditions-4.md) | Required | Terms and conditions agreement. |
| `DataPrivacyAndSharingAgreement` | [`DataPrivacyAndSharingAgreement4`](../../doc/models/data-privacy-and-sharing-agreement-4.md) | Required | Data privacy and sharing agreement. |
| `Status` | [`Status103`](../../doc/models/status-103.md) | Required | Status of the business.<br><br>* ACTIVE -<br>* INACTIVE -<br>* OFFBOARDING -<br>* OFFBOARDED - |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessesResponse businessesResponse = new BusinessesResponse
{
    Id = new Guid("00001584-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ContactEmail = "contact_email2",
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
    TaxInformation = new TaxInformation
    {
        TaxCountryCode = TaxCountryCode.Kp,
        TaxIdentificationType = TaxIdentificationType.Steuernummer,
        TaxIdentificationNumber = "tax_identification_number4",
        IsResidentInMultipleTaxJurisdictions = false,
        IsSubjectToFatca = false,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    BusinessType = "LIMITED_LIABILITY_COMPANY",
    Identification = new Identification8
    {
        CompanyName = "company_name8",
        IncorporationDate = "incorporation_date8",
        LegalDesignation = LegalDesignation.Aktiengesellschaft,
        CommonReportingStandards = new CommonReportingStandards
        {
            IsPassiveNonFinancialEntity = false,
            IsFinancialInstitution = false,
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
        DistrictCourt = "district_court2",
        LegalEntityIdentifier = "legal_entity_identifier4",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    TermsAndConditions = new TermsAndConditions4
    {
        ConsentDocumentId = new Guid("00001ed2-0000-0000-0000-000000000000"),
        ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    DataPrivacyAndSharingAgreement = new DataPrivacyAndSharingAgreement4
    {
        ConsentDocumentId = new Guid("00000842-0000-0000-0000-000000000000"),
        ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Status = Status103.Active,
    RegistrationNumber = "registration_number0",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

