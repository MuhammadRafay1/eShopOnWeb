
# Portfolios Allocation Accounts List Response

Paginated list of accounts associated with a portfolio allocation, including cursor-based pagination metadata.

## Structure

`PortfoliosAllocationAccountsListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<PortfoliosAllocationAccount>`](../../doc/models/portfolios-allocation-account.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PortfoliosAllocationAccountsListResponse portfoliosAllocationAccountsListResponse = new PortfoliosAllocationAccountsListResponse
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
    Data = new List<PortfoliosAllocationAccount>
    {
        new PortfoliosAllocationAccount
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
        },
    },
};
```

