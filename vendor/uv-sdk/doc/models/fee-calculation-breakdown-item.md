
# Fee Calculation Breakdown Item

## Structure

`FeeCalculationBreakdownItem`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `FeeModelId` | `Guid` | Required | Fee model unique identifier. |
| `SubperiodStart` | `DateTime` | Required | Start date of the fee subperiod in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) RFC 3339 |
| `SubperiodEnd` | `DateTime` | Required | End date of the fee subperiod in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) RFC 3339 |
| `SubtotalAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Components` | [`List<FeeBreakdownComponent>`](../../doc/models/fee-breakdown-component.md) | Required | Individual fee components contributing to the subtotal. |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;

FeeCalculationBreakdownItem feeCalculationBreakdownItem = new FeeCalculationBreakdownItem
{
    FeeModelId = new Guid("0000157c-0000-0000-0000-000000000000"),
    SubperiodStart = DateTime.Parse("2016-03-13"),
    SubperiodEnd = DateTime.Parse("2016-03-13"),
    SubtotalAmount = "subtotal_amount6",
    Components = new List<FeeBreakdownComponent>
    {
        new FeeBreakdownComponent
        {
            Type = Type38.TransactionLumpSum,
            Amount = "amount8",
        },
    },
};
```

