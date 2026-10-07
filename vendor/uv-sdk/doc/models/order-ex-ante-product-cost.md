
# Order Ex Ante Product Cost

All costs and associated charges related to the financial instrument.

## Structure

`OrderExAnteProductCost`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `OneOff` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `Ongoing` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `Transaction` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `Incidental` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `Total` | [`Cost`](../../doc/models/cost.md) | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

OrderExAnteProductCost orderExAnteProductCost = new OrderExAnteProductCost
{
    OneOff = new Cost
    {
        CashAmount = "cash_amount4",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage8",
    },
    Ongoing = new Cost
    {
        CashAmount = "cash_amount6",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage0",
    },
    Transaction = new Cost
    {
        CashAmount = "cash_amount6",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage0",
    },
    Incidental = new Cost
    {
        CashAmount = "cash_amount8",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage2",
    },
    Total = new Cost
    {
        CashAmount = "cash_amount8",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage2",
    },
};
```

