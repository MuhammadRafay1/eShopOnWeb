
# Return Impact

Return impact.

*This model accepts additional fields of type object.*

## Structure

`ReturnImpact`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `YearOne` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `YearTwo` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `YearThree` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

ReturnImpact returnImpact = new ReturnImpact
{
    YearOne = new Cost
    {
        CashAmount = "cash_amount8",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage2",
    },
    YearTwo = new Cost
    {
        CashAmount = "cash_amount0",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage4",
    },
    YearThree = new Cost
    {
        CashAmount = "cash_amount2",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage6",
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

