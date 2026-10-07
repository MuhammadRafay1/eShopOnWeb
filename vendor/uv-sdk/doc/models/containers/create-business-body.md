
# Create Business Body

## Class Name

`CreateBusinessBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`BusinessCompanyCreateRequest`](../../../doc/models/business-company-create-request.md) | CreateBusinessBody.FromBusinessCompanyCreateRequest(BusinessCompanyCreateRequest businessCompanyCreateRequest) |
| [`BusinessSoleTraderCreateRequest`](../../../doc/models/business-sole-trader-create-request.md) | CreateBusinessBody.FromBusinessSoleTraderCreateRequest(BusinessSoleTraderCreateRequest businessSoleTraderCreateRequest) |

## BusinessCompanyCreateRequest

### Initialization Code

#### Example

```csharp
CreateBusinessBody value = CreateBusinessBody.FromBusinessCompanyCreateRequest(
    new BusinessCompanyCreateRequest
    {
        ContactEmail = "contact_email6",
        RegisteredAddress = new Address
        {
            AddressLine1 = "address_line10",
            Postcode = "postcode0",
            Country = Country.Nc,
            City = "city4",
        },
        TaxInformation = new TaxInformation
        {
            TaxCountryCode = TaxCountryCode.Kp,
            TaxIdentificationType = TaxIdentificationType.Steuernummer,
            TaxIdentificationNumber = "tax_identification_number4",
            IsResidentInMultipleTaxJurisdictions = false,
            IsSubjectToFatca = false,
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
            },
        },
        TermsAndConditions = new TermsAndConditions4
        {
            ConsentDocumentId = new Guid("00001ed2-0000-0000-0000-000000000000"),
            ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
        DataPrivacyAndSharingAgreement = new DataPrivacyAndSharingAgreement4
        {
            ConsentDocumentId = new Guid("00000842-0000-0000-0000-000000000000"),
            ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
    }
);
```

## BusinessSoleTraderCreateRequest

### Initialization Code

#### Example

```csharp
CreateBusinessBody value = CreateBusinessBody.FromBusinessSoleTraderCreateRequest(
    new BusinessSoleTraderCreateRequest
    {
        ContactEmail = "contact_email8",
        RegisteredAddress = new Address
        {
            AddressLine1 = "address_line10",
            Postcode = "postcode0",
            Country = Country.Nc,
            City = "city4",
        },
        TaxInformation = new TaxInformation
        {
            TaxCountryCode = TaxCountryCode.Kp,
            TaxIdentificationType = TaxIdentificationType.Steuernummer,
            TaxIdentificationNumber = "tax_identification_number4",
            IsResidentInMultipleTaxJurisdictions = false,
            IsSubjectToFatca = false,
        },
        BusinessType = "SOLE_TRADER",
        Identification = new Identification9
        {
            CompanyName = "company_name8",
            LegalDesignation = LegalDesignation1.Einzelunternehmer,
        },
        TermsAndConditions = new TermsAndConditions4
        {
            ConsentDocumentId = new Guid("00001ed2-0000-0000-0000-000000000000"),
            ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
        DataPrivacyAndSharingAgreement = new DataPrivacyAndSharingAgreement4
        {
            ConsentDocumentId = new Guid("00000842-0000-0000-0000-000000000000"),
            ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
    }
);
```

