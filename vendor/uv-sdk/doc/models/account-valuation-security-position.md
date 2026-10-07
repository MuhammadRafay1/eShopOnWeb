
# Account Valuation Security Position

## Structure

`AccountValuationSecurityPosition`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Instrument` | [`Instrument6`](../../doc/models/instrument-6.md) | Required | Entity representing the financial instrument. |
| `Quantity` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,10})?$` |
| `MValue` | [`MValue`](../../doc/models/m-value.md) | Required | - |
| `Weight` | `string` | Required | Total weight of the instrument (10 decimal places).<br><br>**Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,10})?$` |
| `PriceQuality` | [`PriceQuality5?`](../../doc/models/price-quality-5.md) | Optional | The price quality used for the calculation of the value of the position.<br><br>* EOD - end of day price<br>* REALTIME - realtime price<br>* DELAYED - delayed price<br>* NA - no available price |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

AccountValuationSecurityPosition accountValuationSecurityPosition = new AccountValuationSecurityPosition
{
    Instrument = new Instrument6
    {
        Uuid = new Guid("00001fd8-0000-0000-0000-000000000000"),
        Isin = "isin4",
    },
    Quantity = "quantity8",
    MValue = new MValue
    {
        Amount = "amount4",
        Currency = Currency.Eur,
        PriceTime = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
    },
    Weight = "weight8",
    PriceQuality = PriceQuality5.Eod,
};
```

