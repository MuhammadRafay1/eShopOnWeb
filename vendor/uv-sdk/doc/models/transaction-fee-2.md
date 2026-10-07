
# Transaction Fee 2

Entity representing the transaction fee.

## Structure

`TransactionFee2`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency1`](../../doc/models/currency-1.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling<br>* USD - The United States dollar |
| `Type` | `string` | Required, Constant | Type of the fee.<br><br>* TOTAL - Total fees<br><br>**Value**: `"TOTAL"` |
| `ChargeMethod` | [`ChargeMethod`](../../doc/models/charge-method.md) | Required | Indicates whether the fee was charged by client or by other methods.<br><br>* CHARGED_BY_CLIENT - Charged by client<br>* COLLECTED_BY_UPVEST - Charged by client and collected by Upvest |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TransactionFee2 transactionFee2 = new TransactionFee2
{
    Amount = "amount4",
    Currency = Currency1.Usd,
    Type = "TOTAL",
    ChargeMethod = ChargeMethod.ChargedByClient,
};
```

