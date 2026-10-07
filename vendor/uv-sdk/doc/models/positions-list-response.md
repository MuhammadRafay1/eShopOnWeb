
# Positions List Response

## Structure

`PositionsListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Data` | [`List<Datum1>`](../../doc/models/datum-1.md) | Required | - |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

PositionsListResponse positionsListResponse = new PositionsListResponse
{
    Data = new List<Datum1>
    {
        new Datum1
        {
            AccountId = new Guid("000015ce-0000-0000-0000-000000000000"),
            Instrument = new Instrument
            {
                Uuid = new Guid("00001fd8-0000-0000-0000-000000000000"),
                Isin = "isin4",
            },
            Quantity = "quantity6",
            LockedForTrading = "locked_for_trading4",
            PendingSettlement = "pending_settlement4",
            AvailableForTrading = "available_for_trading8",
            AvailableForInstruction = "available_for_instruction2",
            SettledQuantity = "settled_quantity2",
        },
    },
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
};
```

