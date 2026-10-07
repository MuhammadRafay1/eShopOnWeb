
# Portfolios Configuration Update Request

Request body for updating a portfolio configuration. Updates the target allocation and/or associated rebalancing strategies.

## Structure

`PortfoliosConfigurationUpdateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AllocationId` | `Guid?` | Optional | Universally Unique Identifier (UUID) of a portfolio allocation. |
| `RebalancingStrategyIds` | `List<Guid>` | Optional | List of rebalancing strategy ids |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;

PortfoliosConfigurationUpdateRequest portfoliosConfigurationUpdateRequest = new PortfoliosConfigurationUpdateRequest
{
    AllocationId = new Guid("00000e38-0000-0000-0000-000000000000"),
    RebalancingStrategyIds = new List<Guid>
    {
        new Guid("00000c67-0000-0000-0000-000000000000"),
    },
};
```

