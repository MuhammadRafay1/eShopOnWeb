
# Portfolios Orders List Response 1

Paginated list of account liquidations, including cursor-based pagination metadata.

## Structure

`PortfoliosOrdersListResponse1`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<AccountLiquidation>`](../../doc/models/account-liquidation.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PortfoliosOrdersListResponse1 portfoliosOrdersListResponse1 = new PortfoliosOrdersListResponse1
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
    Data = new List<AccountLiquidation>
    {
        new AccountLiquidation
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
            Currency = Currency167.Eur,
            Status = Status72.Filled,
            Orders = new List<AccountLiquidation1>
            {
                new AccountLiquidation1
                {
                    Id = new Guid("0000181c-0000-0000-0000-000000000000"),
                    Side = "SELL",
                    Status = Status73.New,
                },
            },
            FeeCollectionId = new Guid("0000134c-0000-0000-0000-000000000000"),
            UserId = new Guid("0000233a-0000-0000-0000-000000000000"),
            BusinessId = new Guid("000004d8-0000-0000-0000-000000000000"),
        },
    },
};
```

