
# Transaction Tax

Entity representing the transaction tax.

## Structure

`TransactionTax`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency1`](../../doc/models/currency-1.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling<br>* USD - The United States dollar |
| `Type` | `string` | Required, Constant | Type of the tax.<br><br>* TOTAL - Total taxes<br><br>**Value**: `"TOTAL"` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TransactionTax transactionTax = new TransactionTax
{
    Amount = "amount2",
    Currency = Currency1.Eur,
    Type = "TOTAL",
};
```

