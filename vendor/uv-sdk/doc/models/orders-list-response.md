
# Orders List Response

Paginated list of orders for the specified account, including cursor-based pagination metadata.

## Structure

`OrdersListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<Order39>`](../../doc/models/order-39.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

OrdersListResponse ordersListResponse = new OrdersListResponse
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
    Data = new List<Order39>
    {
        new Order39
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
            Currency = Currency29.Eur,
            Side = Side.Buy,
            InstrumentId = "instrument_id4",
            InstrumentIdType = "ISIN",
            OrderType = OrderType.Market,
            Quantity = "quantity6",
            Status = Status51.Filled,
            Fee = "fee2",
            InitiationFlow = InitiationFlow.SellToCoverFees,
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
                            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                        },
                    },
                    OrderId = new Guid("00001bb4-0000-0000-0000-000000000000"),
                    Status = Status52.Cancelled,
                    Side = Side1.Buy,
                    Currency = Currency29.Eur,
                    VenueId = new Guid("00000b18-0000-0000-0000-000000000000"),
                    SettlementDate = "settlement_date4",
                },
            },
            UserId = new Guid("0000233a-0000-0000-0000-000000000000"),
            BusinessId = new Guid("000004d8-0000-0000-0000-000000000000"),
            UserInstrumentFitAcknowledgement = false,
            LimitPrice = "limit_price4",
            StopPrice = "stop_price4",
        },
    },
};
```

