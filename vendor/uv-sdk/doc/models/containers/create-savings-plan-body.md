
# Create Savings Plan Body

## Class Name

`CreateSavingsPlanBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`SavingsPlanPortfolio`](../../../doc/models/savings-plan-portfolio.md) | CreateSavingsPlanBody.FromSavingsPlanPortfolio(SavingsPlanPortfolio savingsPlanPortfolio) |
| [`SavingsPlanInstrument2`](../../../doc/models/savings-plan-instrument-2.md) | CreateSavingsPlanBody.FromSavingsPlanInstrument2(SavingsPlanInstrument2 savingsPlanInstrument2) |

## SavingsPlanPortfolio

### Initialization Code

#### Example

```csharp
CreateSavingsPlanBody value = CreateSavingsPlanBody.FromSavingsPlanPortfolio(
    new SavingsPlanPortfolio
    {
        UserId = new Guid("0000099c-0000-0000-0000-000000000000"),
        AccountId = new Guid("00002340-0000-0000-0000-000000000000"),
        Type = "PORTFOLIO",
        CashAmount = "cash_amount0",
        Currency = Currency.Eur,
        StartDate = "start_date6",
        Period = Period1.Week,
        Interval = 1,
    }
);
```

## SavingsPlanInstrument2

### Initialization Code

#### Example

```csharp
CreateSavingsPlanBody value = CreateSavingsPlanBody.FromSavingsPlanInstrument2(
    new SavingsPlanInstrument2
    {
        UserId = new Guid("00001de4-0000-0000-0000-000000000000"),
        AccountId = new Guid("00001078-0000-0000-0000-000000000000"),
        Type = "INSTRUMENT",
        CashAmount = "cash_amount2",
        Currency = Currency.Eur,
        StartDate = "start_date8",
        Period = Period1.Year,
        Interval = 1,
        InstrumentIdType = InstrumentIdType10.Isin,
    }
);
```

