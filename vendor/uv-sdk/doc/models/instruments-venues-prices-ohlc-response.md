
# Instruments Venues Prices Ohlc Response

## Structure

`InstrumentsVenuesPricesOhlcResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Data` | [`List<Datum8>`](../../doc/models/datum-8.md) | Required | - |
| `Meta` | [`Meta26`](../../doc/models/meta-26.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

InstrumentsVenuesPricesOhlcResponse instrumentsVenuesPricesOhlcResponse = new InstrumentsVenuesPricesOhlcResponse
{
    Data = new List<Datum8>
    {
        new Datum8
        {
            Currency = Currency.Eur,
            Time = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Open = "open4",
            Close = "close8",
            High = "high0",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
    Meta = new Meta26
    {
        Count = 14,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
};
```

