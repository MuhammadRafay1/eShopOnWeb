
# Order 39 Fee Configuration

## Class Name

`Order39FeeConfiguration`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AbsoluteFee`](../../../doc/models/absolute-fee.md) | Order39FeeConfiguration.FromAbsoluteFee(AbsoluteFee absoluteFee) |
| [`TransactionFeeModel`](../../../doc/models/transaction-fee-model.md) | Order39FeeConfiguration.FromTransactionFeeModel(TransactionFeeModel transactionFeeModel) |

## AbsoluteFee

### Initialization Code

#### Example

```csharp
Order39FeeConfiguration value = Order39FeeConfiguration.FromAbsoluteFee(
    new AbsoluteFee
    {
        Type = FeeType.TransactionFeeBuy,
        ValueType = "ABSOLUTE",
        ChargeMethod = FeeChargeMethod.ChargedByClient,
        CashAmount = "cash_amount2",
        Currency = Currency29.Eur,
    }
);
```

## TransactionFeeModel

### Initialization Code

#### Example

```csharp
Order39FeeConfiguration value = Order39FeeConfiguration.FromTransactionFeeModel(
    new TransactionFeeModel
    {
        Type = FeeType.TransactionFeeBuy,
        TransactionFeeModelId = new Guid("00001afc-0000-0000-0000-000000000000"),
    }
);
```

