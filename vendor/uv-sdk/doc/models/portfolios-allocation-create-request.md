
# Portfolios Allocation Create Request

Request body for creating a portfolio allocation. Specifies the allocation name, instrument weights, and optional rebalancing strategy IDs.

## Structure

`PortfoliosAllocationCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Name` | `string` | Optional | Allocation name |
| `Allocation` | [`List<Allocation>`](../../doc/models/allocation.md) | Required | List of portfolios allocations |
| `RebalancingStrategyIds` | `List<Guid>` | Optional | List of rebalancing strategy ids |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;

PortfoliosAllocationCreateRequest portfoliosAllocationCreateRequest = new PortfoliosAllocationCreateRequest
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
    Name = "name8",
    RebalancingStrategyIds = new List<Guid>
    {
        new Guid("000002ef-0000-0000-0000-000000000000"),
    },
};
```

