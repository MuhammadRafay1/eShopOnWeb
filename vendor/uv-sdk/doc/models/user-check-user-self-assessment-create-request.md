
# User Check User Self Assessment Create Request

The User Self-Assessment check is completed by the client providing self-assessment information about the user for AML risk monitoring.

*This model accepts additional fields of type object.*

## Structure

`UserCheckUserSelfAssessmentCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | The type of check must be "USER_SELF_ASSESSMENT".<br><br>**Default**: `"USER_SELF_ASSESSMENT"` |
| `CheckConfirmedAt` | `DateTime` | Required | The date and time when the self assessment information was provided by the end user. |
| `UserOccupation` | [`UserOccupation`](../../doc/models/user-occupation.md) | Required | The user's employment/occupation status. |
| `UserIndustry` | [`UserIndustry`](../../doc/models/user-industry.md) | Required | The industry the user is professionally active in. |
| `YearlyGrossIncome` | [`MonetaryRange`](../../doc/models/monetary-range.md) | Required | A monetary range defined by a lower bound and an optional upper bound. |
| `IncomeSources` | [`List<UserIncomeSource>`](../../doc/models/user-income-source.md) | Required | The user's sources of income. More than one may apply.<br><br>**Constraints**: *Minimum Items*: `1` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserCheckUserSelfAssessmentCreateRequest userCheckUserSelfAssessmentCreateRequest = new UserCheckUserSelfAssessmentCreateRequest
{
    Type = "USER_SELF_ASSESSMENT",
    CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserOccupation = UserOccupation.Homemaker,
    UserIndustry = UserIndustry.PublicAdministration,
    YearlyGrossIncome = new MonetaryRange
    {
        Currency = Currency.Eur,
        LowerBound = 202.12,
        UpperBound = 160.18,
    },
    IncomeSources = new List<UserIncomeSource>
    {
        UserIncomeSource.EmploymentIncome,
        UserIncomeSource.CapitalGainsSavingsAndInvestments,
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

