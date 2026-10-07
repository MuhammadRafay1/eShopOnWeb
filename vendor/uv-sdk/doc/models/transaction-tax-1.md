
# Transaction Tax 1

Entity representing the transaction tax.

## Structure

`TransactionTax1`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency1`](../../doc/models/currency-1.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling<br>* USD - The United States dollar |
| `Type` | [`Type49`](../../doc/models/type-49.md) | Required | Type of the tax. |
| `TaxingJurisdiction` | `string` | Required | Country code. [ISO 3166 alpha-2 Codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TransactionTax1 transactionTax1 = new TransactionTax1
{
    Amount = "amount6",
    Currency = Currency1.Eur,
    Type = Type49.StampDuty,
    TaxingJurisdiction = "taxing_jurisdiction2",
};
```

