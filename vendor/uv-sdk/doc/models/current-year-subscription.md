
# Current Year Subscription

Details related to the current year’s subscription being transferred.

Required if transfer_type is ISA_INTERNAL.

## Structure

`CurrentYearSubscription`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `TransferAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `FirstSubscriptionAt` | `DateTime?` | Optional | The date of the first subscription made to the ISA in the current tax year. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html)<br><br>Required if transfer_type is ISA_INTERNAL. |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

CurrentYearSubscription currentYearSubscription = new CurrentYearSubscription
{
    TransferAmount = "transfer_amount0",
    FirstSubscriptionAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
};
```

