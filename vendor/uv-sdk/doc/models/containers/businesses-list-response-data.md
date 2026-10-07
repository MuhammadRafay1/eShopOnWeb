
# Businesses List Response Data

## Class Name

`BusinessesListResponseData`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`Business2`](../../../doc/models/business-2.md) | BusinessesListResponseData.FromBusiness2(Business2 business2) |
| [`Business21`](../../../doc/models/business-21.md) | BusinessesListResponseData.FromBusiness21(Business21 business21) |

## Business2

### Initialization Code

#### Example

```csharp
BusinessesListResponseData value = BusinessesListResponseData.FromBusiness2(
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
        Status = Status103.Offboarding,
    }
);
```

## Business21

### Initialization Code

#### Example

```csharp
BusinessesListResponseData value = BusinessesListResponseData.FromBusiness21(
    new Business21
    {
        Id = new Guid("00000280-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        ContactEmail = "contact_email0",
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
        Status = Status103.Active,
    }
);
```

