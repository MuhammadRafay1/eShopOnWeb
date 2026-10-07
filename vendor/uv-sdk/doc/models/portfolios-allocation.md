
# Portfolios Allocation

Defines a named set of instrument weights for a portfolio. The `allocation` array specifies each instrument (by ISIN or Upvest UUID) and its target weight percentage. Weights must sum to 100.

## Structure

`PortfoliosAllocation`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Universally Unique Identifier (UUID) of a portfolio allocation. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Name` | `string` | Required | Allocation name |
| `Allocation` | [`List<Allocation>`](../../doc/models/allocation.md) | Required | List of portfolios allocations |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;

PortfoliosAllocation portfoliosAllocation = new PortfoliosAllocation
{
    Id = new Guid("00001bc8-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Name = "name2",
    Allocation = new List<Allocation>
    {
        new Allocation
        {
            InstrumentId = AllocationInstrumentId.FromString("String3"),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = "weight6",
        },
    },
};
```

