
# Instruments Response

## Structure

`InstrumentsResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Instrument unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Isin` | `string` | Optional | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |
| `Wkn` | `string` | Optional | German securities identification code known as [Wertpapierkennnummer](https://en.wikipedia.org/wiki/Wertpapierkennnummer).<br><br>**Constraints**: *Pattern*: `^[A-HJ-NP-Z0-9]{6}$` |
| `Name` | `string` | Required | Instrument name<br><br>**Constraints**: *Maximum Length*: `100` |
| `FractionalTrading` | `bool` | Required | Determines whether the platform can handle fractional investments within this instrument. |
| `TradingStatus` | [`InstrumentTradingStatus`](../../doc/models/instrument-trading-status.md) | Required | Instrument trading status<br><br>* ACTIVE - The instrument can currently be traded on the Upvest platform.<br>* INACTIVE - The instrument cannot currently be traded on the Upvest platform. |
| `Details` | [`Details`](../../doc/models/details.md) | Optional | Details |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

InstrumentsResponse instrumentsResponse = new InstrumentsResponse
{
    Id = new Guid("0000035a-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Name = "name8",
    FractionalTrading = false,
    TradingStatus = InstrumentTradingStatus.Active,
    Isin = "isin2",
    Wkn = "wkn6",
    Details = new Details
    {
        DistributionCountries = new List<string>
        {
            "distribution_countries3",
            "distribution_countries4",
        },
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
};
```

