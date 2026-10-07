
# Third Party Payments

Third-party payments associated with the investment service.

## Structure

`ThirdPartyPayments`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Total` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `ReceivedByClient` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `ReceivedByUpvest` | [`ReceivedByUpvest`](../../doc/models/received-by-upvest.md) | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

ThirdPartyPayments thirdPartyPayments = new ThirdPartyPayments
{
    Total = new Cost
    {
        CashAmount = "cash_amount8",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage2",
    },
    ReceivedByClient = new Cost
    {
        CashAmount = "cash_amount4",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage8",
    },
    ReceivedByUpvest = new ReceivedByUpvest
    {
        CashAmount = "cash_amount0",
        Currency = Currency.Eur,
        AsPercentage = "as_percentage4",
    },
};
```

