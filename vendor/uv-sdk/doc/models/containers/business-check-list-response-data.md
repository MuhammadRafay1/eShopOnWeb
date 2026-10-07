
# Business Check List Response Data

## Class Name

`BusinessCheckListResponseData`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`BusinessCheckKyb`](../../../doc/models/business-check-kyb.md) | BusinessCheckListResponseData.FromBusinessCheckKYB(BusinessCheckKyb businessCheckKyb) |
| [`BusinessCheckBusinessSelfAssessment`](../../../doc/models/business-check-business-self-assessment.md) | BusinessCheckListResponseData.FromBusinessCheckBusinessSelfAssessment(BusinessCheckBusinessSelfAssessment businessCheckBusinessSelfAssessment) |

## BusinessCheckKyb

### Initialization Code

#### Example

```csharp
BusinessCheckListResponseData value = BusinessCheckListResponseData.FromBusinessCheckKYB(
    new BusinessCheckKyb
    {
        Id = new Guid("00000d00-0000-0000-0000-000000000000"),
        BusinessId = new Guid("00001cbe-0000-0000-0000-000000000000"),
        Type = "KYB",
        OriginallyConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        Status = Status109.Failed,
        DataDownloadLink = "data_download_link0",
        DocumentType = "KYB_DOCUMENTS",
    }
);
```

## BusinessCheckBusinessSelfAssessment

### Initialization Code

#### Example

```csharp
BusinessCheckListResponseData value = BusinessCheckListResponseData.FromBusinessCheckBusinessSelfAssessment(
    new BusinessCheckBusinessSelfAssessment
    {
        Id = new Guid("00002422-0000-0000-0000-000000000000"),
        BusinessId = new Guid("00000cd0-0000-0000-0000-000000000000"),
        Type = "BUSINESS_SELF_ASSESSMENT",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        Status = Status109.Failed,
        BusinessIndustry = BusinessIndustry.EntertainmentArtsAndRecreation,
        PurposeOfBusinessRelationship = PurposeOfBusinessRelationship.WealthGrowth,
        PrimaryCountriesOfActivity = new List<PrimaryCountriesOfActivity>
        {
            PrimaryCountriesOfActivity.Sg,
            PrimaryCountriesOfActivity.Se,
            PrimaryCountriesOfActivity.Sd,
        },
        AnnualRevenue = new MonetaryRange
        {
            Currency = Currency.Eur,
            LowerBound = 18.4,
        },
        ExpectedVolumeOfInvestment = new MonetaryRange
        {
            Currency = Currency.Eur,
            LowerBound = 170.26,
        },
        TotalAssetValue = new MonetaryRange
        {
            Currency = Currency.Eur,
            LowerBound = 49.86,
        },
    }
);
```

