
# Fee Configuration Tiers

## Class Name

`FeeConfigurationTiers`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AbsoluteTransactionFeeTier`](../../../doc/models/absolute-transaction-fee-tier.md) | FeeConfigurationTiers.FromAbsoluteTransactionFeeTier(AbsoluteTransactionFeeTier absoluteTransactionFeeTier) |
| [`RelativeTransactionFeeTier`](../../../doc/models/relative-transaction-fee-tier.md) | FeeConfigurationTiers.FromRelativeTransactionFeeTier(RelativeTransactionFeeTier relativeTransactionFeeTier) |

## AbsoluteTransactionFeeTier

### Initialization Code

#### Example

```csharp
FeeConfigurationTiers value = FeeConfigurationTiers.FromAbsoluteTransactionFeeTier(
    new AbsoluteTransactionFeeTier
    {
        TierId = "tier_id8",
        BaseAmountFrom = "base_amount_from0",
        FeeAmount = "fee_amount8",
    }
);
```

## RelativeTransactionFeeTier

### Initialization Code

#### Example

```csharp
FeeConfigurationTiers value = FeeConfigurationTiers.FromRelativeTransactionFeeTier(
    new RelativeTransactionFeeTier
    {
        TierId = "tier_id4",
        BaseAmountFrom = "base_amount_from6",
        FeeBps = "fee_bps0",
    }
);
```

