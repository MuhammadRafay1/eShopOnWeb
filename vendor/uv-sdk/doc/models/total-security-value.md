
# Total Security Value

Entity representing the monetary value by amount and currency.

## Structure

`TotalSecurityValue`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TotalSecurityValue totalSecurityValue = new TotalSecurityValue
{
    Amount = "amount8",
    Currency = Currency.Eur,
};
```

