
# Payments Top Ups List Response

Paginated list of top-ups for an account group, including cursor-based pagination metadata.

## Structure

`PaymentsTopUpsListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<Datum3>`](../../doc/models/datum-3.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PaymentsTopUpsListResponse paymentsTopUpsListResponse = new PaymentsTopUpsListResponse
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
    Data = new List<Datum3>
    {
        new Datum3
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            AccountGroupId = new Guid("00000794-0000-0000-0000-000000000000"),
            CashAmount = "cash_amount8",
            Currency = Currency.Eur,
            SettlementReference = "settlement_reference6",
            Status = Status29.Cancelled,
        },
    },
};
```

