
# Portfolios Allocation Update Request

Request body for updating a portfolio allocation. Updates the name and/or instrument weights. Updated weights must still sum to 100.

## Structure

`PortfoliosAllocationUpdateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Name` | `string` | Optional | Allocation name |
| `Allocation` | [`List<Allocation>`](../../doc/models/allocation.md) | Required | List of portfolios allocations |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;

PortfoliosAllocationUpdateRequest portfoliosAllocationUpdateRequest = new PortfoliosAllocationUpdateRequest
{
    Allocation = new List<Allocation>
    {
        new Allocation
        {
            InstrumentId = AllocationInstrumentId.FromString("String3"),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = "weight6",
        },
    },
    Name = "name2",
};
```

