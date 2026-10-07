
# Cost

## Structure

`Cost`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `CashAmount` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency?`](../../doc/models/currency.md) | Optional | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `AsPercentage` | `string` | Optional | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Cost cost = new Cost
{
    CashAmount = "cash_amount6",
    Currency = Currency.Eur,
    AsPercentage = "as_percentage0",
};
```

