
# Create Business Check Body

## Class Name

`CreateBusinessCheckBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`BusinessCheckKnowYourBusinessCreateRequest`](../../../doc/models/business-check-know-your-business-create-request.md) | CreateBusinessCheckBody.FromBusinessCheckKnowYourBusinessCreateRequest(BusinessCheckKnowYourBusinessCreateRequest businessCheckKnowYourBusinessCreateRequest) |
| [`BusinessCheckBusinessSelfAssessmentCreateRequest`](../../../doc/models/business-check-business-self-assessment-create-request.md) | CreateBusinessCheckBody.FromBusinessCheckBusinessSelfAssessmentCreateRequest(BusinessCheckBusinessSelfAssessmentCreateRequest businessCheckBusinessSelfAssessmentCreateRequest) |

## BusinessCheckKnowYourBusinessCreateRequest

### Initialization Code

#### Example

```csharp
CreateBusinessCheckBody value = CreateBusinessCheckBody.FromBusinessCheckKnowYourBusinessCreateRequest(
    new BusinessCheckKnowYourBusinessCreateRequest
    {
        Type = "KYB",
        OriginallyConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        DataDownloadLink = "data_download_link2",
    }
);
```

## BusinessCheckBusinessSelfAssessmentCreateRequest

### Initialization Code

#### Example

```csharp
CreateBusinessCheckBody value = CreateBusinessCheckBody.FromBusinessCheckBusinessSelfAssessmentCreateRequest(
    new BusinessCheckBusinessSelfAssessmentCreateRequest
    {
        Type = "BUSINESS_SELF_ASSESSMENT",
        CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        BusinessIndustry = BusinessIndustry.WholesaleTradeLuxuryAndCollectibles,
        PurposeOfBusinessRelationship = PurposeOfBusinessRelationship.EmployeeBenefitFunding,
        PrimaryCountriesOfActivity = new List<PrimaryCountriesOfActivity>
        {
            PrimaryCountriesOfActivity.Na,
            PrimaryCountriesOfActivity.Nc,
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

