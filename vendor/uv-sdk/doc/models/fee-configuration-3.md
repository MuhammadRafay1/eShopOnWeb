
# Fee Configuration 3

## Structure

`FeeConfiguration3`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `FeeModelId` | `Guid` | Required | Fee model unique identifier. |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

FeeConfiguration3 feeConfiguration3 = new FeeConfiguration3
{
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountId = new Guid("000002c0-0000-0000-0000-000000000000"),
    FeeModelId = new Guid("00000048-0000-0000-0000-000000000000"),
};
```

