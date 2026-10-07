
# Cancellation Reason

Reason for order cancellation. Present only when `status` is `CANCELLED`.

* CANCELLED_BY_CLIENT — cancelled at the end user's or client's request via the API.
* CANCELLED_BY_UPVEST_OPERATIONS — cancelled by Upvest operations.
* CANCELLED_BY_TRADING_PARTNER — cancelled by the executing partner.
* CANCELLED_BY_UPVEST_PLATFORM — cancelled automatically by the Upvest platform.

## Enumeration

`CancellationReason`

## Fields

| Name |
|  --- |
| `CancelledByClient` |
| `CancelledByUpvestOperations` |
| `CancelledByTradingPartner` |
| `CancelledByUpvestPlatform` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CancellationReason cancellationReason = CancellationReason.CancelledByTradingPartner;
```

