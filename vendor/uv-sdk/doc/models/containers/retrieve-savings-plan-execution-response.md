
# Retrieve Savings Plan Execution Response

## Class Name

`RetrieveSavingsPlanExecutionResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`SavingsPlanExecutionInstrument`](../../../doc/models/savings-plan-execution-instrument.md) | RetrieveSavingsPlanExecutionResponse.FromSavingsPlanExecutionInstrument(SavingsPlanExecutionInstrument savingsPlanExecutionInstrument) |
| [`SavingsPlanExecutionPortfolio`](../../../doc/models/savings-plan-execution-portfolio.md) | RetrieveSavingsPlanExecutionResponse.FromSavingsPlanExecutionPortfolio(SavingsPlanExecutionPortfolio savingsPlanExecutionPortfolio) |

## SavingsPlanExecutionInstrument

### Initialization Code

#### Example

```csharp
RetrieveSavingsPlanExecutionResponse value = RetrieveSavingsPlanExecutionResponse.FromSavingsPlanExecutionInstrument(
    new SavingsPlanExecutionInstrument
    {
        Id = new Guid("000019fc-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UserId = new Guid("0000210c-0000-0000-0000-000000000000"),
        AccountId = new Guid("000013a0-0000-0000-0000-000000000000"),
        SavingsPlanId = new Guid("000019f4-0000-0000-0000-000000000000"),
        OrderId = SavingsPlanExecutionInstrumentOrderId.FromUUID(new Guid("00001dcc-0000-0000-0000-000000000000")),
        CashAmount = "cash_amount0",
        Currency = Currency.Eur,
        Status = Status93.Processing,
        Type = "INSTRUMENT",
        ExecutionDate = "execution_date0",
        InstrumentIdType = InstrumentIdType10.Isin,
    }
);
```

## SavingsPlanExecutionPortfolio

### Initialization Code

#### Example

```csharp
RetrieveSavingsPlanExecutionResponse value = RetrieveSavingsPlanExecutionResponse.FromSavingsPlanExecutionPortfolio(
    new SavingsPlanExecutionPortfolio
    {
        Id = new Guid("0000246a-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UserId = new Guid("0000046a-0000-0000-0000-000000000000"),
        AccountId = new Guid("00001e0e-0000-0000-0000-000000000000"),
        SavingsPlanId = new Guid("00000f86-0000-0000-0000-000000000000"),
        OrderId = SavingsPlanExecutionPortfolioOrderId.FromUUID(new Guid("0000135e-0000-0000-0000-000000000000")),
        CashAmount = "cash_amount0",
        Currency = Currency.Eur,
        Status = Status93.Processing,
        Type = "PORTFOLIO",
        ExecutionDate = "execution_date0",
        InstrumentIdType = InstrumentIdType10.Isin,
    }
);
```

