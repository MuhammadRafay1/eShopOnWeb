
# Portfolios Rebalancing Execution

Represents a rebalancing execution — a batch event that rebalances one or more accounts to match their target allocations. Contains references to the affected accounts and allocations.

## Structure

`PortfoliosRebalancingExecution`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Universally Unique Identifier (UUID) of a portfolio order. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `Accounts` | `List<Guid>` | Optional | Accounts associated with this rebalancing execution |
| `Allocations` | `List<Guid>` | Optional | Allocations associated with this rebalancing execution |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

PortfoliosRebalancingExecution portfoliosRebalancingExecution = new PortfoliosRebalancingExecution
{
    Id = new Guid("00001c6e-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Accounts = new List<Guid>
    {
        new Guid("00000aa8-0000-0000-0000-000000000000"),
        new Guid("00000aa7-0000-0000-0000-000000000000"),
    },
    Allocations = new List<Guid>
    {
        new Guid("000025cc-0000-0000-0000-000000000000"),
        new Guid("000025cd-0000-0000-0000-000000000000"),
    },
};
```

