
# Portfolios Allocation Account

An account associated with a portfolio allocation, identified by its account UUID.

## Structure

`PortfoliosAllocationAccount`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Account unique identifier. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

PortfoliosAllocationAccount portfoliosAllocationAccount = new PortfoliosAllocationAccount
{
    Id = new Guid("00001576-0000-0000-0000-000000000000"),
};
```

