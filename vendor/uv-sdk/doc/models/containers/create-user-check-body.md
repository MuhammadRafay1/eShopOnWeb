
# Create User Check Body

## Class Name

`CreateUserCheckBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`UserCheckKnowYourCustomerCreateRequest`](../../../doc/models/user-check-know-your-customer-create-request.md) | CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(UserCheckKnowYourCustomerCreateRequest userCheckKnowYourCustomerCreateRequest) |
| [`UserCheckProofOfResidencyCreateRequest`](../../../doc/models/user-check-proof-of-residency-create-request.md) | CreateUserCheckBody.FromUserCheckProofOfResidencyCreateRequest(UserCheckProofOfResidencyCreateRequest userCheckProofOfResidencyCreateRequest) |
| [`UserCheckInstrumentFitCreateRequest`](../../../doc/models/user-check-instrument-fit-create-request.md) | CreateUserCheckBody.FromUserCheckInstrumentFitCreateRequest(UserCheckInstrumentFitCreateRequest userCheckInstrumentFitCreateRequest) |
| [`UserCheckGuardianCreateRequest`](../../../doc/models/user-check-guardian-create-request.md) | CreateUserCheckBody.FromUserCheckGuardianCreateRequest(UserCheckGuardianCreateRequest userCheckGuardianCreateRequest) |
| [`UserCheckUserSelfAssessmentCreateRequest`](../../../doc/models/user-check-user-self-assessment-create-request.md) | CreateUserCheckBody.FromUserCheckUserSelfAssessmentCreateRequest(UserCheckUserSelfAssessmentCreateRequest userCheckUserSelfAssessmentCreateRequest) |
| [`UserCheckUsWithholdingTaxStatusCreateRequest`](../../../doc/models/user-check-us-withholding-tax-status-create-request.md) | CreateUserCheckBody.FromUserCheckUSWithholdingTaxStatusCreateRequest(UserCheckUsWithholdingTaxStatusCreateRequest userCheckUsWithholdingTaxStatusCreateRequest) |

## UserCheckKnowYourCustomerCreateRequest

### Initialization Code

#### Example

```csharp
CreateUserCheckBody value = CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(
    new UserCheckKnowYourCustomerCreateRequest
    {
        Type = "KYC",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        DataDownloadLink = "data_download_link0",
        DocumentType = DocumentType3.IdCard,
        Provider = "provider0",
        Method = Method.ElectronicId,
    }
);
```

## UserCheckProofOfResidencyCreateRequest

### Initialization Code

#### Example

```csharp
CreateUserCheckBody value = CreateUserCheckBody.FromUserCheckProofOfResidencyCreateRequest(
    new UserCheckProofOfResidencyCreateRequest
    {
        Type = "POR",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        IssuanceDate = DateTime.Parse("2016-03-13"),
        DataDownloadLink = "data_download_link8",
        DocumentType = DocumentType6.BankStatement,
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

## UserCheckInstrumentFitCreateRequest

### Initialization Code

#### Example

```csharp
CreateUserCheckBody value = CreateUserCheckBody.FromUserCheckInstrumentFitCreateRequest(
    new UserCheckInstrumentFitCreateRequest
    {
        Type = "INSTRUMENT_FIT",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        InstrumentSuitability = new InstrumentSuitability
        {
            Suitability = false,
        },
    }
);
```

## UserCheckGuardianCreateRequest

### Initialization Code

#### Example

```csharp
CreateUserCheckBody value = CreateUserCheckBody.FromUserCheckGuardianCreateRequest(
    new UserCheckGuardianCreateRequest
    {
        Type = "GUARDIAN",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        RoleId = new Guid("00002540-0000-0000-0000-000000000000"),
        DocumentType = "BIRTH_CERTIFICATE",
        DataDownloadLink = "data_download_link2",
    }
);
```

## UserCheckUserSelfAssessmentCreateRequest

### Initialization Code

#### Example

```csharp
CreateUserCheckBody value = CreateUserCheckBody.FromUserCheckUserSelfAssessmentCreateRequest(
    new UserCheckUserSelfAssessmentCreateRequest
    {
        Type = "USER_SELF_ASSESSMENT",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UserOccupation = UserOccupation.Other,
        UserIndustry = UserIndustry.InformationTechnologyAndMedia,
        YearlyGrossIncome = new MonetaryRange
        {
            Currency = Currency.Eur,
            LowerBound = 202.12,
        },
        IncomeSources = new List<UserIncomeSource>
        {
            UserIncomeSource.PensionRetirementFunds,
        },
    }
);
```

## UserCheckUsWithholdingTaxStatusCreateRequest

### Initialization Code

#### Example

```csharp
CreateUserCheckBody value = CreateUserCheckBody.FromUserCheckUSWithholdingTaxStatusCreateRequest(
    new UserCheckUsWithholdingTaxStatusCreateRequest
    {
        Type = "US_WITHHOLDING_TAX_STATUS",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        PersonType = UserPersonType.UsPerson,
        DocumentationType = UserDocumentationType.Kyc,
        ValidFrom = DateTime.Parse("2016-03-13"),
    }
);
```

