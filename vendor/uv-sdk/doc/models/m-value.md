
# M Value

## Structure

`MValue`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Amount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `PriceTime` | `DateTime` | Required | The date and time of the price used for the calculation. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

MValue mValue = new MValue
{
    Amount = "amount8",
    Currency = Currency.Eur,
    PriceTime = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
};
```

