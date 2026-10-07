
# Cancellation Reason Code

Explains the reason why the order was cancelled .

* ACCOUNT_IS_EMPTY -
* CANCELLED_BY_CLIENT -
* CANCELLED_BY_UPVEST -
* PORTFOLIO_IS_BALANCED -
* SELL_LIMIT_EXCEEDED -

## Enumeration

`CancellationReasonCode`

## Fields

| Name |
|  --- |
| `AccountIsEmpty` |
| `CancelledByClient` |
| `CancelledByUpvest` |
| `PortfolioIsBalanced` |
| `SellLimitExceeded` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CancellationReasonCode cancellationReasonCode = CancellationReasonCode.PortfolioIsBalanced;
```

