
# Cancellation Reason Code for Savings Plan Execution

Explains the reason why the savings plan execution was cancelled .

* CANCELLED_BY_CLIENT: The savings plan execution was cancelled by the client.
* CANCELLED_BY_UPVEST: The savings plan execution was cancelled by Upvest.

## Enumeration

`CancellationReasonCodeForSavingsPlanExecution`

## Fields

| Name |
|  --- |
| `CancelledByClient` |
| `CancelledByUpvest` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CancellationReasonCodeForSavingsPlanExecution cancellationReasonCodeForSavingsPlanExecution = CancellationReasonCodeForSavingsPlanExecution.CancelledByClient;
```

