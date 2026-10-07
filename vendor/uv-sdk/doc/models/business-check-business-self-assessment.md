
# Business Check Business Self Assessment

The Business Self Assessment check captures information about the business's activities, financials, and purpose of the business relationship for risk scoring.

*This model accepts additional fields of type object.*

## Structure

`BusinessCheckBusinessSelfAssessment`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Business Check unique identifier. |
| `BusinessId` | `Guid` | Required | Unique identifier for the business. |
| `Type` | `string` | Required | The type of check must be "BUSINESS_SELF_ASSESSMENT".<br><br>**Default**: `"BUSINESS_SELF_ASSESSMENT"` |
| `CheckConfirmedAt` | `DateTime` | Required | The date and time when the self assessment check was confirmed. |
| `Status` | [`Status109`](../../doc/models/status-109.md) | Required | Final status of the business check.<br><br>* IN_PROGRESS - Check is in progress<br>* PASSED - Check passed<br>* FAILED - Check failed |
| `BusinessIndustry` | [`BusinessIndustry`](../../doc/models/business-industry.md) | Required | The business industry classification. |
| `PurposeOfBusinessRelationship` | [`PurposeOfBusinessRelationship`](../../doc/models/purpose-of-business-relationship.md) | Required | The purpose of the business relationship. |
| `PrimaryCountriesOfActivity` | [`List<PrimaryCountriesOfActivity>`](../../doc/models/primary-countries-of-activity.md) | Required | List of primary countries where the business operates. ISO 3166-1 alpha-2 country codes.<br><br>**Constraints**: *Minimum Items*: `1` |
| `AnnualRevenue` | [`MonetaryRange`](../../doc/models/monetary-range.md) | Required | A monetary range defined by a lower bound and an optional upper bound. |
| `ExpectedVolumeOfInvestment` | [`MonetaryRange`](../../doc/models/monetary-range.md) | Required | A monetary range defined by a lower bound and an optional upper bound. |
| `TotalAssetValue` | [`MonetaryRange`](../../doc/models/monetary-range.md) | Required | A monetary range defined by a lower bound and an optional upper bound. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

BusinessCheckBusinessSelfAssessment businessCheckBusinessSelfAssessment = new BusinessCheckBusinessSelfAssessment
{
    Id = new Guid("0000076c-0000-0000-0000-000000000000"),
    BusinessId = new Guid("0000172a-0000-0000-0000-000000000000"),
    Type = "BUSINESS_SELF_ASSESSMENT",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Status = Status109.InProgress,
    BusinessIndustry = BusinessIndustry.RealEstateAgencyAndBrokerage,
    PurposeOfBusinessRelationship = PurposeOfBusinessRelationship.WealthGrowth,
    PrimaryCountriesOfActivity = new List<PrimaryCountriesOfActivity>
    {
        PrimaryCountriesOfActivity.Ar,
    },
    AnnualRevenue = new MonetaryRange
    {
        Currency = Currency.Eur,
        LowerBound = 18.4,
        UpperBound = 232.46,
    },
    ExpectedVolumeOfInvestment = new MonetaryRange
    {
        Currency = Currency.Eur,
        LowerBound = 170.26,
        UpperBound = 128.32,
    },
    TotalAssetValue = new MonetaryRange
    {
        Currency = Currency.Eur,
        LowerBound = 49.86,
        UpperBound = 7.92,
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

