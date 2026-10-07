
# Status 34

Status of the withdrawal

* NEW - Withdrawal is created but not started processing.
* PROCESSING - Withdrawal is in processing.
* CONFIRMED - Withdrawal was successfully sent to the bank for processing.
* CANCELLED - Withdrawal was cancelled. **Note**: In rare instances, a bank may reject a withdrawal after it has been confirmed.
  For more information, see the [Potential cancellation after confirmation](/documentation/guides/payments/cash_balances/cash_withdrawal#potential-cancellation-after-confirmation) section of the guide.

## Enumeration

`Status34`

## Fields

| Name |
|  --- |
| `New` |
| `Processing` |
| `Confirmed` |
| `Cancelled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status34 status34 = Status34.New;
```

