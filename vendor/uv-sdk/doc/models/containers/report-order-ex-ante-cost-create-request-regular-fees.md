
# Report Order Ex Ante Cost Create Request Regular Fees

## Class Name

`ReportOrderExAnteCostCreateRequestRegularFees`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`AbsoluteFee4`](../../../doc/models/absolute-fee-4.md) | ReportOrderExAnteCostCreateRequestRegularFees.FromAbsoluteFee4(AbsoluteFee4 absoluteFee4) |
| [`RelativeFee`](../../../doc/models/relative-fee.md) | ReportOrderExAnteCostCreateRequestRegularFees.FromRelativeFee(RelativeFee relativeFee) |
| [`TransactionFee`](../../../doc/models/transaction-fee.md) | ReportOrderExAnteCostCreateRequestRegularFees.FromTransactionFee(TransactionFee transactionFee) |

## AbsoluteFee4

### Initialization Code

#### Example

```csharp
ReportOrderExAnteCostCreateRequestRegularFees value = ReportOrderExAnteCostCreateRequestRegularFees.FromAbsoluteFee4(
    new AbsoluteFee4
    {
        Type = FeeType8.AnnualAumBasedFee,
        ValueType = "ABSOLUTE",
        CashAmount = "cash_amount8",
        Currency = Currency.Eur,
    }
);
```

## RelativeFee

### Initialization Code

#### Example

```csharp
ReportOrderExAnteCostCreateRequestRegularFees value = ReportOrderExAnteCostCreateRequestRegularFees.FromRelativeFee(
    new RelativeFee
    {
        Type = FeeType8.TransactionFeeBuy,
        ValueType = "RELATIVE",
        Bps = "bps8",
    }
);
```

## TransactionFee

### Initialization Code

#### Example

```csharp
ReportOrderExAnteCostCreateRequestRegularFees value = ReportOrderExAnteCostCreateRequestRegularFees.FromTransactionFee(
    new TransactionFee
    {
        Type = FeeType.TransactionFeeBuy,
        TransactionFeeModelId = new Guid("00001bb2-0000-0000-0000-000000000000"),
    }
);
```

