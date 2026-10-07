
# Cancellation Reason Code for Savings Plan

Explains the reason why the savings plan was cancelled .

* CANCELLED_BY_CLIENT: The savings plan was cancelled by the client.
* CANCELLED_BY_UPVEST: The savings plan was cancelled by Upvest.

## Enumeration

`CancellationReasonCodeForSavingsPlan`

## Fields

| Name |
|  --- |
| `CancelledByClient` |
| `CancelledByUpvest` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CancellationReasonCodeForSavingsPlan cancellationReasonCodeForSavingsPlan = CancellationReasonCodeForSavingsPlan.CancelledByClient;
```

