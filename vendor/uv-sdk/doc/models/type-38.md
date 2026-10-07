
# Type 38

Type of the fee component

* TRANSACTION_LUMP_SUM - Lump sum transaction fee
* PLATFORM - Platform fee
* SERVICE - Service fee (client portfolio)
* VAT - Value-added tax

## Enumeration

`Type38`

## Fields

| Name |
|  --- |
| `TransactionLumpSum` |
| `Platform` |
| `Service` |
| `Vat` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Type38 type38 = Type38.Service;
```

