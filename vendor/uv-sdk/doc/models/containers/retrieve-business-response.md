
# Retrieve Business Response

## Class Name

`RetrieveBusinessResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`BusinessesResponse`](../../../doc/models/businesses-response.md) | RetrieveBusinessResponse.FromBusinessesResponse(BusinessesResponse businessesResponse) |
| [`BusinessesResponse1`](../../../doc/models/businesses-response-1.md) | RetrieveBusinessResponse.FromBusinessesResponse1(BusinessesResponse1 businessesResponse1) |

## BusinessesResponse

### Initialization Code

#### Example

```csharp
RetrieveBusinessResponse value = RetrieveBusinessResponse.FromBusinessesResponse(
    new BusinessesResponse
    {
        Id = new Guid("00000344-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        ContactEmail = "contact_email4",
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
        Status = Status103.Active,
    }
);
```

## BusinessesResponse1

### Initialization Code

#### Example

```csharp
RetrieveBusinessResponse value = RetrieveBusinessResponse.FromBusinessesResponse1(
    new BusinessesResponse1
    {
        Id = new Guid("00000370-0000-0000-0000-000000000000"),
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

