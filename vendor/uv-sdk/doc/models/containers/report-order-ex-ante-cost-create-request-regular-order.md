
# Report Order Ex Ante Cost Create Request Regular Order

## Class Name

`ReportOrderExAnteCostCreateRequestRegularOrder`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`ExAnteCostUserOrder`](../../../doc/models/ex-ante-cost-user-order.md) | ReportOrderExAnteCostCreateRequestRegularOrder.FromExAnteCostUserOrder(ExAnteCostUserOrder exAnteCostUserOrder) |
| [`ExAnteCostBusinessOrder`](../../../doc/models/ex-ante-cost-business-order.md) | ReportOrderExAnteCostCreateRequestRegularOrder.FromExAnteCostBusinessOrder(ExAnteCostBusinessOrder exAnteCostBusinessOrder) |

## ExAnteCostUserOrder

### Initialization Code

#### Example

```csharp
ReportOrderExAnteCostCreateRequestRegularOrder value = ReportOrderExAnteCostCreateRequestRegularOrder.FromExAnteCostUserOrder(
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
);
```

## ExAnteCostBusinessOrder

### Initialization Code

#### Example

```csharp
ReportOrderExAnteCostCreateRequestRegularOrder value = ReportOrderExAnteCostCreateRequestRegularOrder.FromExAnteCostBusinessOrder(
    new ExAnteCostBusinessOrder
    {
        BusinessId = new Guid("00000b6c-0000-0000-0000-000000000000"),
        AccountId = new Guid("00001c62-0000-0000-0000-000000000000"),
        Currency = Currency.Eur,
        Side = Side8.Buy,
        InstrumentId = "instrument_id8",
        InstrumentIdType = "ISIN",
        OrderType = OrderType4.Stop,
    }
);
```

