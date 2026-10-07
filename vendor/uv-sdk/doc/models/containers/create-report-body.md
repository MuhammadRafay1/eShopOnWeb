
# Create Report Body

## Class Name

`CreateReportBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`ReportOrderExAnteCostCreateRequestRegular`](../../../doc/models/report-order-ex-ante-cost-create-request-regular.md) | CreateReportBody.FromReportOrderExAnteCostCreateRequestRegular(ReportOrderExAnteCostCreateRequestRegular reportOrderExAnteCostCreateRequestRegular) |
| [`ReportOrderExAnteCostCreateRequestSavingsPlan`](../../../doc/models/report-order-ex-ante-cost-create-request-savings-plan.md) | CreateReportBody.FromReportOrderExAnteCostCreateRequestSavingsPlan(ReportOrderExAnteCostCreateRequestSavingsPlan reportOrderExAnteCostCreateRequestSavingsPlan) |

## ReportOrderExAnteCostCreateRequestRegular

### Initialization Code

#### Example

```csharp
CreateReportBody value = CreateReportBody.FromReportOrderExAnteCostCreateRequestRegular(
    new ReportOrderExAnteCostCreateRequestRegular
    {
        Type = "ORDER_EX_ANTE_COST",
        Order = ReportOrderExAnteCostCreateRequestRegularOrder.FromExAnteCostUserOrder(
            new ExAnteCostUserOrder
            {
                UserId = new Guid("000002de-0000-0000-0000-000000000000"),
                AccountId = new Guid("00001c82-0000-0000-0000-000000000000"),
                Currency = Currency.Eur,
                Side = Side8.Buy,
                InstrumentId = "instrument_id0",
                InstrumentIdType = "ISIN",
                OrderType = OrderType4.Limit,
            }
        ),
    }
);
```

## ReportOrderExAnteCostCreateRequestSavingsPlan

### Initialization Code

#### Example

```csharp
CreateReportBody value = CreateReportBody.FromReportOrderExAnteCostCreateRequestSavingsPlan(
    new ReportOrderExAnteCostCreateRequestSavingsPlan
    {
        Type = "ORDER_EX_ANTE_COST_SAVINGS_PLAN",
        Order = new ExAnteCostSavingsPlanOrder
        {
            UserId = new Guid("00001850-0000-0000-0000-000000000000"),
            AccountId = new Guid("00000ae4-0000-0000-0000-000000000000"),
            CashAmount = "cash_amount4",
            Currency = Currency.Eur,
            Side = Side8.Buy,
            InstrumentId = "instrument_id0",
            InstrumentIdType = "ISIN",
            OrderType = OrderType4.Limit,
            Period = Period.Month,
            Interval = "interval4",
        },
    }
);
```

