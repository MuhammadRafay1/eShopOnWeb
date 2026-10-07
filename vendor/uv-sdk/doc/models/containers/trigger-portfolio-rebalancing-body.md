
# Trigger Portfolio Rebalancing Body

## Class Name

`TriggerPortfolioRebalancingBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`Accounts`](../../../doc/models/accounts.md) | TriggerPortfolioRebalancingBody.FromAccounts(Accounts accounts) |
| [`Allocations`](../../../doc/models/allocations.md) | TriggerPortfolioRebalancingBody.FromAllocations(Allocations allocations) |

## Accounts

### Initialization Code

#### Example

```csharp
TriggerPortfolioRebalancingBody value = TriggerPortfolioRebalancingBody.FromAccounts(
    new Accounts
    {
        Accounts = new List<Guid>
        {
            new Guid("00001924-0000-0000-0000-000000000000"),
            new Guid("00001923-0000-0000-0000-000000000000"),
            new Guid("00001922-0000-0000-0000-000000000000"),
        },
    }
);
```

## Allocations

### Initialization Code

#### Example

```csharp
TriggerPortfolioRebalancingBody value = TriggerPortfolioRebalancingBody.FromAllocations(
    new Allocations
    {
        Allocations = new List<Guid>
        {
            new Guid("000015fe-0000-0000-0000-000000000000"),
            new Guid("000015ff-0000-0000-0000-000000000000"),
            new Guid("00001600-0000-0000-0000-000000000000"),
        },
    }
);
```

