
# Create Report Response

## Class Name

`CreateReportResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`UserReportOrderExAnteCost`](../../../doc/models/user-report-order-ex-ante-cost.md) | CreateReportResponse.FromUserReportOrderExAnteCost(UserReportOrderExAnteCost userReportOrderExAnteCost) |
| [`BusinessReportOrderExAnteCost`](../../../doc/models/business-report-order-ex-ante-cost.md) | CreateReportResponse.FromBusinessReportOrderExAnteCost(BusinessReportOrderExAnteCost businessReportOrderExAnteCost) |

## UserReportOrderExAnteCost

### Initialization Code

#### Example

```csharp
CreateReportResponse value = CreateReportResponse.FromUserReportOrderExAnteCost(
    new UserReportOrderExAnteCost
    {
        Id = new Guid("0000219e-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UserId = new Guid("0000019e-0000-0000-0000-000000000000"),
        Type = Type44.OrderExAnteCost,
        SubstitutedReportId = new Guid("00000a36-0000-0000-0000-000000000000"),
    }
);
```

## BusinessReportOrderExAnteCost

### Initialization Code

#### Example

```csharp
CreateReportResponse value = CreateReportResponse.FromBusinessReportOrderExAnteCost(
    new BusinessReportOrderExAnteCost
    {
        Id = new Guid("00002216-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        BusinessId = new Guid("00000ac4-0000-0000-0000-000000000000"),
        Type = "ORDER_EX_ANTE_COST",
        SubstitutedReportId = new Guid("00000aae-0000-0000-0000-000000000000"),
    }
);
```

