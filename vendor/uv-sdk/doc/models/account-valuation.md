
# Account Valuation

## Structure

`AccountValuation`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Account valuation unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `TotalSecurityValue` | [`TotalSecurityValue`](../../doc/models/total-security-value.md) | Required | Entity representing the monetary value by amount and currency. |
| `PriceQuality` | [`PriceQuality4`](../../doc/models/price-quality-4.md) | Required | The requested price quality.<br><br>* EOD - End of day prices<br>* HIGHEST_AVAILABLE - The most recent available prices |
| `SecurityPositions` | [`List<AccountValuationSecurityPosition>`](../../doc/models/account-valuation-security-position.md) | Required | Positions associated with this account valuation. |
| `ValuationTime` | `DateTime` | Required | Date and time as of which the value was calculated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

AccountValuation accountValuation = new AccountValuation
{
    Id = new Guid("00000618-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountId = new Guid("000026cc-0000-0000-0000-000000000000"),
    TotalSecurityValue = new TotalSecurityValue
    {
        Amount = "amount2",
        Currency = Currency.Eur,
    },
    PriceQuality = PriceQuality4.Eod,
    SecurityPositions = new List<AccountValuationSecurityPosition>
    {
        new AccountValuationSecurityPosition
        {
            Instrument = new Instrument6
            {
                Uuid = new Guid("00001fd8-0000-0000-0000-000000000000"),
                Isin = "isin4",
            },
            Quantity = "quantity4",
            MValue = new MValue
            {
                Amount = "amount4",
                Currency = Currency.Eur,
                PriceTime = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
            },
            Weight = "weight4",
            PriceQuality = PriceQuality5.Eod,
        },
    },
    ValuationTime = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
};
```

