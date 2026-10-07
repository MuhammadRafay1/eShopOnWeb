
# Portfolios Rebalancing Execution Order List Response

Paginated list of rebalancing execution orders for a given rebalancing execution, including cursor-based pagination metadata.

## Structure

`PortfoliosRebalancingExecutionOrderListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<PortfoliosRebalancingExecutionOrder>`](../../doc/models/portfolios-rebalancing-execution-order.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PortfoliosRebalancingExecutionOrderListResponse portfoliosRebalancingExecutionOrderListResponse = new PortfoliosRebalancingExecutionOrderListResponse
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
    Data = new List<PortfoliosRebalancingExecutionOrder>
    {
        new PortfoliosRebalancingExecutionOrder
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            ExecutionId = new Guid("0000220c-0000-0000-0000-000000000000"),
            AccountId = new Guid("000015ce-0000-0000-0000-000000000000"),
            PortfolioOrderId = new Guid("000017e2-0000-0000-0000-000000000000"),
            Status = Status71.Filled,
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            CancellationReason = CancellationReason3.PortfolioIsBalanced,
            CancellationDetails = "cancellation_details2",
        },
    },
};
```

