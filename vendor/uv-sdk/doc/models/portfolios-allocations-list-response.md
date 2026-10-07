
# Portfolios Allocations List Response

Paginated list of portfolio allocations, including cursor-based pagination metadata.

## Structure

`PortfoliosAllocationsListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<PortfoliosAllocation>`](../../doc/models/portfolios-allocation.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

PortfoliosAllocationsListResponse portfoliosAllocationsListResponse = new PortfoliosAllocationsListResponse
{
    Meta = new Meta
    {
        Offset = 222,
        Limit = 126,
        Count = 14,
        TotalCount = 150,
        Sort = "sort4",
        Order = Order1.Asc,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Data = new List<PortfoliosAllocation>
    {
        new PortfoliosAllocation
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Name = "name0",
            Allocation = new List<Allocation>
            {
                new Allocation
                {
                    InstrumentId = AllocationInstrumentId.FromString("String3"),
                    InstrumentIdType = InstrumentIdType4.Isin,
                    Weight = "weight6",
                },
            },
        },
    },
};
```

