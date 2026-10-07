
# Common Reporting Standards

Common reporting standards information.

*This model accepts additional fields of type object.*

## Structure

`CommonReportingStandards`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `IsPassiveNonFinancialEntity` | `bool` | Required | Indicates if the company is a passive non-financial entity. |
| `IsFinancialInstitution` | `bool` | Required | Indicates if the company is a financial institution. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

CommonReportingStandards commonReportingStandards = new CommonReportingStandards
{
    IsPassiveNonFinancialEntity = false,
    IsFinancialInstitution = false,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

