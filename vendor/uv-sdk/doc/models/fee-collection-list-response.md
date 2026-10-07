
# Fee Collection List Response

## Structure

`FeeCollectionListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<FeeCollection>`](../../doc/models/fee-collection.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

FeeCollectionListResponse feeCollectionListResponse = new FeeCollectionListResponse
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
    Data = new List<FeeCollection>
    {
        new FeeCollection
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            AccountId = new Guid("000015ce-0000-0000-0000-000000000000"),
            AccountGroupId = new Guid("00000794-0000-0000-0000-000000000000"),
            Type = Type37.ServiceFee,
            CollectionAmount = "collection_amount6",
            ProcessedAmount = new ProcessedAmount
            {
                CashBalance = "cash_balance4",
                SellToCover = "sell_to_cover4",
                TotalResidualAmount = "total_residual_amount2",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            },
            Currency = Currency.Eur,
            Status = Status53.Cancelled,
            PeriodStart = DateTime.Parse("2016-03-13"),
            PeriodEnd = DateTime.Parse("2016-03-13"),
            SellToCoverOrders = new List<SellToCoverOrderDetails>
            {
                new SellToCoverOrderDetails
                {
                    Id = new Guid("00000dc6-0000-0000-0000-000000000000"),
                    ResidualAmount = "residual_amount8",
                },
            },
            CalculationBreakdown = new List<FeeCalculationBreakdownItem>
            {
                new FeeCalculationBreakdownItem
                {
                    FeeModelId = new Guid("000021aa-0000-0000-0000-000000000000"),
                    SubperiodStart = DateTime.Parse("2016-03-13"),
                    SubperiodEnd = DateTime.Parse("2016-03-13"),
                    SubtotalAmount = "subtotal_amount8",
                    Components = new List<FeeBreakdownComponent>
                    {
                        new FeeBreakdownComponent
                        {
                            Type = Type38.TransactionLumpSum,
                            Amount = "amount8",
                        },
                    },
                },
                new FeeCalculationBreakdownItem
                {
                    FeeModelId = new Guid("000021aa-0000-0000-0000-000000000000"),
                    SubperiodStart = DateTime.Parse("2016-03-13"),
                    SubperiodEnd = DateTime.Parse("2016-03-13"),
                    SubtotalAmount = "subtotal_amount8",
                    Components = new List<FeeBreakdownComponent>
                    {
                        new FeeBreakdownComponent
                        {
                            Type = Type38.TransactionLumpSum,
                            Amount = "amount8",
                        },
                    },
                },
            },
            ReplacedCollectionId = new Guid("0000214a-0000-0000-0000-000000000000"),
        },
    },
};
```

