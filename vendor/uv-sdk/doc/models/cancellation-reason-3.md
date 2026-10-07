
# Cancellation Reason 3

Reason for Rebalancing Execution Order Cancellation. The field is present in case the Order has a status of CANCELLED.

* ACCOUNT_IS_EMPTY -
* ACCOUNT_NOT_FOUND -
* CANCELLED_BY_CLIENT -
* CANCELLED_BY_UPVEST -
* CONFIGURATION_IS_MISSING -
* INVALID_ACCOUNT_TYPE -
* PORTFOLIO_IS_BALANCED -
* UNKNOWN -

## Enumeration

`CancellationReason3`

## Fields

| Name |
|  --- |
| `AccountIsEmpty` |
| `AccountNotFound` |
| `CancelledByClient` |
| `CancelledByUpvest` |
| `ConfigurationIsMissing` |
| `InvalidAccountType` |
| `PortfolioIsBalanced` |
| `Unknown` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CancellationReason3 cancellationReason3 = CancellationReason3.ConfigurationIsMissing;
```

