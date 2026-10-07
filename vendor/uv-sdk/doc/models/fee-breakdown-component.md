
# Fee Breakdown Component

## Structure

`FeeBreakdownComponent`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | [`Type38`](../../doc/models/type-38.md) | Required | Type of the fee component<br><br>* TRANSACTION_LUMP_SUM - Lump sum transaction fee<br>* PLATFORM - Platform fee<br>* SERVICE - Service fee (client portfolio)<br>* VAT - Value-added tax |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

FeeBreakdownComponent feeBreakdownComponent = new FeeBreakdownComponent
{
    Type = Type38.Service,
    Amount = "amount8",
};
```

