
# Portfolios Orders List Response

Paginated list of portfolio orders, including cursor-based pagination metadata.

## Structure

`PortfoliosOrdersListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<PortfoliosOrder>`](../../doc/models/portfolios-order.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PortfoliosOrdersListResponse portfoliosOrdersListResponse = new PortfoliosOrdersListResponse
{
    Meta = new Meta
    {
        Offset = 222,
        Limit = 126,
        Count = 14,
        TotalCount = 150,
        Sort = "sort4",
        Order = Order1.Asc,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Data = new List<PortfoliosOrder>
    {
        new PortfoliosOrder
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            AccountId = new Guid("000015ce-0000-0000-0000-000000000000"),
            CashAmount = "cash_amount8",
            Currency = Currency.Eur,
            Status = Status65.Filled,
            PostTax = false,
            Orders = new List<PortfoliosOrder1>
            {
                new PortfoliosOrder1
                {
                    Id = new Guid("0000181c-0000-0000-0000-000000000000"),
                    Side = Side15.Buy,
                    Status = Status51.New,
                },
            },
            ClientReference = "client_reference2",
            InitiationFlow = InitiationFlowUsedDuringOrderCreation.Api,
            UserId = new Guid("0000233a-0000-0000-0000-000000000000"),
            AllocationId = new Guid("000010f2-0000-0000-0000-000000000000"),
            Type = Type46.Rebalancing,
            CancellationReason = CancellationReasonCode.PortfolioIsBalanced,
            CancellationDetails = "cancellation_details2",
        },
    },
};
```

