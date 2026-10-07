
# Trigger Portfolio Rebalancing Response

Response returned after triggering a portfolio rebalancing, containing the `id` of the rebalancing execution created.

## Structure

`TriggerPortfolioRebalancingResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Universally Unique Identifier (UUID) of a portfolio rebalancing execution. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TriggerPortfolioRebalancingResponse triggerPortfolioRebalancingResponse = new TriggerPortfolioRebalancingResponse
{
    Id = new Guid("00000b90-0000-0000-0000-000000000000"),
};
```

