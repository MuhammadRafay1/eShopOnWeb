
# Monetary Range

A monetary range defined by a lower bound and an optional upper bound.

## Structure

`MonetaryRange`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `LowerBound` | `double` | Required | The lower bound of the monetary range.<br><br>**Constraints**: `>= 0` |
| `UpperBound` | `double?` | Optional | The upper bound of the monetary range.<br><br>**Constraints**: `>= 0` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

MonetaryRange monetaryRange = new MonetaryRange
{
    Currency = Currency.Eur,
    LowerBound = 217.76,
    UpperBound = 175.82,
};
```

