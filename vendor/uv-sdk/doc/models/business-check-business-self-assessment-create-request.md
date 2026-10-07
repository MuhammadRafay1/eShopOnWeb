
# Business Check Business Self Assessment Create Request

The Business Self Assessment check is completed by the client providing self-assessment information about the business for risk scoring purposes.

*This model accepts additional fields of type object.*

## Structure

`BusinessCheckBusinessSelfAssessmentCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | The type of check must be "BUSINESS_SELF_ASSESSMENT".<br><br>**Default**: `"BUSINESS_SELF_ASSESSMENT"` |
| `CheckConfirmedAt` | `DateTime` | Required | The date and time when the self assessment check was confirmed. |
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

BusinessCheckBusinessSelfAssessmentCreateRequest businessCheckBusinessSelfAssessmentCreateRequest = new BusinessCheckBusinessSelfAssessmentCreateRequest
{
    Type = "BUSINESS_SELF_ASSESSMENT",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    BusinessIndustry = BusinessIndustry.ProfessionalAndTechnicalServicesUnregulated,
    PurposeOfBusinessRelationship = PurposeOfBusinessRelationship.StrategicInvestment,
    PrimaryCountriesOfActivity = new List<PrimaryCountriesOfActivity>
    {
        PrimaryCountriesOfActivity.Om,
        PrimaryCountriesOfActivity.Nz,
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

