
# Portfolios Configuration Create Request

Request body for creating a portfolio configuration for an account. Links the account to a target allocation and optional rebalancing strategies.

## Structure

`PortfoliosConfigurationCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `AllocationId` | `Guid` | Required | Universally Unique Identifier (UUID) of a portfolio allocation. |
| `RebalancingStrategyIds` | `List<Guid>` | Optional | List of rebalancing strategy ids |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;

PortfoliosConfigurationCreateRequest portfoliosConfigurationCreateRequest = new PortfoliosConfigurationCreateRequest
{
    AccountId = new Guid("000018b0-0000-0000-0000-000000000000"),
    AllocationId = new Guid("0000133c-0000-0000-0000-000000000000"),
    RebalancingStrategyIds = new List<Guid>
    {
        new Guid("00000763-0000-0000-0000-000000000000"),
    },
};
```

