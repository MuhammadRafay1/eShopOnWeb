
# Details 2

## Structure

`Details2`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `CurrentYearSubscription` | [`CurrentYearSubscription`](../../doc/models/current-year-subscription.md) | Required | Details related to the current year’s subscription being transferred.<br><br>Required if transfer_type is ISA_INTERNAL. |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

Details2 details2 = new Details2
{
    CurrentYearSubscription = new CurrentYearSubscription
    {
        TransferAmount = "transfer_amount6",
        FirstSubscriptionAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
    },
};
```

