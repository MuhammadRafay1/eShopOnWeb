
# Order Ex Ante Aggregated Cost

Aggregated totals of product costs, service costs and third party payments.

## Structure

`OrderExAnteAggregatedCost`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Product` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `Service` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `ThirdParty` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `Total` | [`Cost`](../../doc/models/cost.md) | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

OrderExAnteAggregatedCost orderExAnteAggregatedCost = new OrderExAnteAggregatedCost
{
    Product = new Cost
    {
        CashAmount = "cash_amount8",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage2",
    },
    Service = new Cost
    {
        CashAmount = "cash_amount8",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage2",
    },
    ThirdParty = new Cost
    {
        CashAmount = "cash_amount0",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage4",
    },
    Total = new Cost
    {
        CashAmount = "cash_amount8",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage2",
    },
};
```

