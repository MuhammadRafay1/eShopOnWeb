
# Inducement

## Structure

`Inducement`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `ValueType` | `string` | Required | The type of inducement value must be “ABSOLUTE”.<br><br>**Default**: `"ABSOLUTE"` |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Inducement inducement = new Inducement
{
    ValueType = "ABSOLUTE",
    CashAmount = "cash_amount8",
    Currency = Currency.Eur,
};
```

