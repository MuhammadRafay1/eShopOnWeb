
# Retrieve Report Response

## Class Name

`RetrieveReportResponse`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`UserReport`](../../../doc/models/user-report.md) | RetrieveReportResponse.FromUserReport(UserReport userReport) |
| [`BusinessReport`](../../../doc/models/business-report.md) | RetrieveReportResponse.FromBusinessReport(BusinessReport businessReport) |
| [`UserReportOrderExAnteCost`](../../../doc/models/user-report-order-ex-ante-cost.md) | RetrieveReportResponse.FromUserReportOrderExAnteCost(UserReportOrderExAnteCost userReportOrderExAnteCost) |
| [`BusinessReportOrderExAnteCost`](../../../doc/models/business-report-order-ex-ante-cost.md) | RetrieveReportResponse.FromBusinessReportOrderExAnteCost(BusinessReportOrderExAnteCost businessReportOrderExAnteCost) |

## UserReport

### Initialization Code

#### Example

```csharp
RetrieveReportResponse value = RetrieveReportResponse.FromUserReport(
    new UserReport
    {
        Id = new Guid("000015b4-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        UserId = new Guid("00001cc4-0000-0000-0000-000000000000"),
        Type = ReportType.AnnualTaxStatement,
        SubstitutedReportId = new Guid("0000255c-0000-0000-0000-000000000000"),
    }
);
```

## BusinessReport

### Initialization Code

#### Example

```csharp
RetrieveReportResponse value = RetrieveReportResponse.FromBusinessReport(
    new BusinessReport
    {
        Id = new Guid("0000175a-0000-0000-0000-000000000000"),
        CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        BusinessId = new Guid("00000008-0000-0000-0000-000000000000"),
        Type = ReportType.FeeCollection,
        SubstitutedReportId = new Guid("00002702-0000-0000-0000-000000000000"),
    }
);
```

## UserReportOrderExAnteCost

### Initialization Code

#### Example

```csharp
RetrieveReportResponse value = RetrieveReportResponse.FromUserReportOrderExAnteCost(
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
RetrieveReportResponse value = RetrieveReportResponse.FromBusinessReportOrderExAnteCost(
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

