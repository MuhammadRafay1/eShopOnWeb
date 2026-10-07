
# Transaction Taxes Details

Entity representing the transaction taxes details.

## Structure

`TransactionTaxesDetails`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `TotalAmount` | [`TotalAmount`](../../doc/models/total-amount.md) | Required | - |
| `TaxBreakdown` | [`List<TransactionTax1>`](../../doc/models/transaction-tax-1.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

TransactionTaxesDetails transactionTaxesDetails = new TransactionTaxesDetails
{
    TotalAmount = new TotalAmount
    {
        Amount = "amount8",
        Currency = Currency1.Usd,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    TaxBreakdown = new List<TransactionTax1>
    {
        new TransactionTax1
        {
            Amount = "amount6",
            Currency = Currency1.Usd,
            Type = Type49.SolidaritySurcharge,
            TaxingJurisdiction = "taxing_jurisdiction2",
        },
    },
};
```

