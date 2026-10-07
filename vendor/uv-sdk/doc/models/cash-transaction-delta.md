
# Cash Transaction Delta

Entity representing cash transaction delta.

## Structure

`CashTransactionDelta`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency1`](../../doc/models/currency-1.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling<br>* USD - The United States dollar |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

CashTransactionDelta cashTransactionDelta = new CashTransactionDelta
{
    Amount = "amount4",
    Currency = Currency1.Usd,
};
```

