
# Order Type

Type of the order.

* MARKET — executes immediately at the best available market price.
* LIMIT — executes only at or better than the specified `limit_price`.
* STOP — triggers when the market price reaches `stop_price`, then executes at the prevailing market price.

## Enumeration

`OrderType`

## Fields

| Name |
|  --- |
| `Market` |
| `Limit` |
| `Stop` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

OrderType orderType = OrderType.Limit;
```

