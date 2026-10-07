
# Businesses List Response

Response for listing businesses.

## Structure

`BusinessesListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<BusinessesListResponseData>`](../../doc/models/containers/businesses-list-response-data.md) | Required | This is List of a container for one-of cases. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

BusinessesListResponse businessesListResponse = new BusinessesListResponse
{
    Meta = new Meta
    {
        Offset = 222,
        Limit = 126,
        Count = 14,
        TotalCount = 150,
        Sort = "sort4",
        Order = Order1.Asc,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Data = new List<BusinessesListResponseData>
    {
        BusinessesListResponseData.FromBusiness2(
            new Business2
            {
                Id = new Guid("00000a52-0000-0000-0000-000000000000"),
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
                BusinessType = "business_type0",
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
                Status = Status103.Offboarding,
                RegistrationNumber = "registration_number4",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
        BusinessesListResponseData.FromBusiness2(
            new Business2
            {
                Id = new Guid("00000a52-0000-0000-0000-000000000000"),
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
                BusinessType = "business_type0",
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
                Status = Status103.Offboarding,
                RegistrationNumber = "registration_number4",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
    },
};
```

