
# Third Party Payments 1

Third party payments associated with the investment service.

## Structure

`ThirdPartyPayments1`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Total` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `ReceivedByClient` | [`Cost`](../../doc/models/cost.md) | Optional | - |
| `ReceivedByUpvest` | [`ReceivedByUpvest`](../../doc/models/received-by-upvest.md) | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

ThirdPartyPayments1 thirdPartyPayments1 = new ThirdPartyPayments1
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

