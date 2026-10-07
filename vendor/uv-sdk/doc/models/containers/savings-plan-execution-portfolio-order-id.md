
# Savings Plan Execution Portfolio Order Id

## Class Name

`SavingsPlanExecutionPortfolioOrderId`

## Cases

| Type | Factory Method |
|  --- | --- |
| `Guid` | SavingsPlanExecutionPortfolioOrderId.FromUUID(Guid uuid) |
| [`Order39`](../../../doc/models/order-39.md) | SavingsPlanExecutionPortfolioOrderId.FromOrder39(Order39 order39) |

## Guid

### Initialization Code

#### Example

```csharp
SavingsPlanExecutionPortfolioOrderId value = SavingsPlanExecutionPortfolioOrderId.FromUUID(new Guid("00000000-0000-0000-0000-000000000000"));
```

## Order39

### Initialization Code

#### Example

```csharp
SavingsPlanExecutionPortfolioOrderId value = SavingsPlanExecutionPortfolioOrderId.FromOrder39(
    new Order39
    {
        Id = new Guid("0000130c-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        AccountId = new Guid("00000cb0-0000-0000-0000-000000000000"),
        CashAmount = "cash_amount4",
        Currency = Currency29.Eur,
        Side = Side.Buy,
        InstrumentId = "instrument_id0",
        InstrumentIdType = "ISIN",
        OrderType = OrderType.Market,
        Quantity = "quantity2",
        Status = Status51.New,
        Fee = "fee8",
        InitiationFlow = InitiationFlow.Api,
        Executions = new List<OrderExecution>
        {
            new OrderExecution
            {
                Id = new Guid("00002632-0000-0000-0000-000000000000"),
                CashAmount = "cash_amount6",
                ShareQuantity = "share_quantity0",
                Price = "price4",
                TransactionTime = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                Taxes = new List<Tax3>
                {
                    new Tax3
                    {
                        Type = "TOTAL",
                        Amount = "amount2",
                    },
                },
                OrderId = new Guid("00001bb4-0000-0000-0000-000000000000"),
                Status = Status52.Cancelled,
                Side = Side1.Buy,
                Currency = Currency29.Eur,
                VenueId = new Guid("00000b18-0000-0000-0000-000000000000"),
            },
        },
    }
);
```

