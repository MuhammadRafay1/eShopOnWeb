
# User Check List Response Data

## Class Name

`UserCheckListResponseData`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`UserCheckKnowYourCustomer`](../../../doc/models/user-check-know-your-customer.md) | UserCheckListResponseData.FromUserCheckKnowYourCustomer(UserCheckKnowYourCustomer userCheckKnowYourCustomer) |
| [`UserCheckProofOfResidency`](../../../doc/models/user-check-proof-of-residency.md) | UserCheckListResponseData.FromUserCheckProofOfResidency(UserCheckProofOfResidency userCheckProofOfResidency) |
| [`UserCheckInstrumentFit`](../../../doc/models/user-check-instrument-fit.md) | UserCheckListResponseData.FromUserCheckInstrumentFit(UserCheckInstrumentFit userCheckInstrumentFit) |
| [`UserCheckCompliance`](../../../doc/models/user-check-compliance.md) | UserCheckListResponseData.FromUserCheckCompliance(UserCheckCompliance userCheckCompliance) |
| [`UserCheckGuardian`](../../../doc/models/user-check-guardian.md) | UserCheckListResponseData.FromUserCheckGuardian(UserCheckGuardian userCheckGuardian) |
| [`UserCheckUsWithholdingTaxStatus`](../../../doc/models/user-check-us-withholding-tax-status.md) | UserCheckListResponseData.FromUserCheckUSWithholdingTaxStatus(UserCheckUsWithholdingTaxStatus userCheckUsWithholdingTaxStatus) |

## UserCheckKnowYourCustomer

### Initialization Code

#### Example

```csharp
UserCheckListResponseData value = UserCheckListResponseData.FromUserCheckKnowYourCustomer(
    new UserCheckKnowYourCustomer
    {
        Id = new Guid("00001800-0000-0000-0000-000000000000"),
        UserId = new Guid("00001f10-0000-0000-0000-000000000000"),
        Type = "KYC",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        DataDownloadLink = "data_download_link6",
        DocumentType = DocumentType3.ResidencePermit,
        Status = Status6.Passed,
        Provider = "provider4",
        Method = Method.TwoPlusTwoVerification,
    }
);
```

## UserCheckProofOfResidency

### Initialization Code

#### Example

```csharp
UserCheckListResponseData value = UserCheckListResponseData.FromUserCheckProofOfResidency(
    new UserCheckProofOfResidency
    {
        Id = new Guid("0000000e-0000-0000-0000-000000000000"),
        UserId = new Guid("0000071e-0000-0000-0000-000000000000"),
        Type = "POR",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        IssuanceDate = DateTime.Parse("2016-03-13"),
        DataDownloadLink = "data_download_link6",
        DocumentType = DocumentType4.InternetBill,
        Status = Status7.Passed,
        ConfirmedAddress = new Address
        {
            AddressLine1 = "address_line16",
            Postcode = "postcode6",
            Country = Country.Tv,
            City = "city2",
        },
    }
);
```

## UserCheckInstrumentFit

### Initialization Code

#### Example

```csharp
UserCheckListResponseData value = UserCheckListResponseData.FromUserCheckInstrumentFit(
    new UserCheckInstrumentFit
    {
        Id = new Guid("00001e38-0000-0000-0000-000000000000"),
        UserId = new Guid("00002548-0000-0000-0000-000000000000"),
        Type = "INSTRUMENT_FIT",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        Status = Status8.Passed,
        InstrumentSuitability = new InstrumentSuitability
        {
            Suitability = false,
        },
    }
);
```

## UserCheckCompliance

### Initialization Code

#### Example

```csharp
UserCheckListResponseData value = UserCheckListResponseData.FromUserCheckCompliance(
    new UserCheckCompliance
    {
        Id = new Guid("00000418-0000-0000-0000-000000000000"),
        UserId = new Guid("00000b28-0000-0000-0000-000000000000"),
        Type = "COMPLIANCE",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
    }
);
```

## UserCheckGuardian

### Initialization Code

#### Example

```csharp
UserCheckListResponseData value = UserCheckListResponseData.FromUserCheckGuardian(
    new UserCheckGuardian
    {
        Id = new Guid("0000169c-0000-0000-0000-000000000000"),
        UserId = new Guid("00001dac-0000-0000-0000-000000000000"),
        Type = "GUARDIAN",
        RoleId = new Guid("00000ff4-0000-0000-0000-000000000000"),
        DocumentType = "BIRTH_CERTIFICATE",
        DataDownloadLink = "data_download_link0",
        Status = Status10.Passed,
    }
);
```

## UserCheckUsWithholdingTaxStatus

### Initialization Code

#### Example

```csharp
UserCheckListResponseData value = UserCheckListResponseData.FromUserCheckUSWithholdingTaxStatus(
    new UserCheckUsWithholdingTaxStatus
    {
        Id = new Guid("00001aa6-0000-0000-0000-000000000000"),
        UserId = new Guid("000021b6-0000-0000-0000-000000000000"),
        Type = "US_WITHHOLDING_TAX_STATUS",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        PersonType = UserPersonType.UsPerson,
        DocumentationType = UserDocumentationType.Kyc,
        ValidFrom = DateTime.Parse("2016-03-13"),
        Status = Status11.InProgress,
    }
);
```

