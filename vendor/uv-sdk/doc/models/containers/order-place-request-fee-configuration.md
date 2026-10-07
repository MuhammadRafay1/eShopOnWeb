
# Order Place Request Fee Configuration

## Class Name

`OrderPlaceRequestFeeConfiguration`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AbsoluteFee`](../../../doc/models/absolute-fee.md) | OrderPlaceRequestFeeConfiguration.FromAbsoluteFee(AbsoluteFee absoluteFee) |
| [`TransactionFeeModel`](../../../doc/models/transaction-fee-model.md) | OrderPlaceRequestFeeConfiguration.FromTransactionFeeModel(TransactionFeeModel transactionFeeModel) |

## AbsoluteFee

### Initialization Code

#### Example

```csharp
OrderPlaceRequestFeeConfiguration value = OrderPlaceRequestFeeConfiguration.FromAbsoluteFee(
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
OrderPlaceRequestFeeConfiguration value = OrderPlaceRequestFeeConfiguration.FromTransactionFeeModel(
    new TransactionFeeModel
    {
        Type = FeeType.TransactionFeeBuy,
        TransactionFeeModelId = new Guid("00001afc-0000-0000-0000-000000000000"),
    }
);
```

