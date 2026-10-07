
# Cancellation Reason Code for Direct Debit

Reason the direct debit was cancelled. The field is present in case the direct debit has a status of CANCELLED.

* CANCELLED_BY_BANK - The payment was not completed by your bank. Please check your account details or contact support.
* CANCELLED_BY_UPVEST - The payment was cancelled. Contact support for details.
* CANCELLED_BY_CLIENT - The payment was cancelled at your request.
* OTHER - Reason not available (applies to historical data only).

## Enumeration

`CancellationReasonCodeForDirectDebit`

## Fields

| Name |
|  --- |
| `CancelledByBank` |
| `CancelledByUpvest` |
| `CancelledByClient` |
| `Other` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CancellationReasonCodeForDirectDebit cancellationReasonCodeForDirectDebit = CancellationReasonCodeForDirectDebit.CancelledByClient;
```

