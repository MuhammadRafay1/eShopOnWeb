
# Portfolios Configuration

Links an investment account to a portfolio allocation and optional rebalancing strategies. Determines which allocation the account follows and which strategies govern automatic rebalancing.

## Structure

`PortfoliosConfiguration`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `AllocationId` | `Guid` | Required | Universally Unique Identifier (UUID) of a portfolio allocation. |
| `RebalancingStrategyIds` | `List<Guid>` | Optional | List of rebalancing strategy ids |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

PortfoliosConfiguration portfoliosConfiguration = new PortfoliosConfiguration
{
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountId = new Guid("00000326-0000-0000-0000-000000000000"),
    AllocationId = new Guid("000001b6-0000-0000-0000-000000000000"),
    RebalancingStrategyIds = new List<Guid>
    {
        new Guid("0000190f-0000-0000-0000-000000000000"),
        new Guid("0000190e-0000-0000-0000-000000000000"),
    },
};
```

