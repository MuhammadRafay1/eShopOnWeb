
# Fatca

The user's FATCA status and when it was confirmed

*This model accepts additional fields of type object.*

## Structure

`Fatca`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Status` | `bool` | Required | The user’s FATCA status is true if the user is subject to paying taxes in the US, otherwise it can be set to false. |
| `ConfirmedAt` | `DateTime` | Required | Timestamp at which the user confirmed their FATCA status. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Fatca fatca = new Fatca
{
    Status = false,
    ConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

