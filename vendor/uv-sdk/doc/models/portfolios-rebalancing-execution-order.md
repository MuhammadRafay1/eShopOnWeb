
# Portfolios Rebalancing Execution Order

An individual account-level order within a rebalancing execution. Each entry corresponds to a single account being rebalanced and has its own status lifecycle.

## Structure

`PortfoliosRebalancingExecutionOrder`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Universally Unique Identifier (UUID) of a rebalancing execution order. |
| `ExecutionId` | `Guid` | Required | Universally Unique Identifier (UUID) of a portfolio rebalancing execution. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `PortfolioOrderId` | `Guid?` | Required | - |
| `Status` | [`Status71`](../../doc/models/status-71.md) | Required | Status of the Rebalancing Execution Order.<br><br>* NEW -<br>* PROCESSING -<br>* FILLED -<br>* CANCELLED - |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `CancellationReason` | [`CancellationReason3?`](../../doc/models/cancellation-reason-3.md) | Optional | Reason for Rebalancing Execution Order Cancellation. The field is present in case the Order has a status of CANCELLED.<br><br>* ACCOUNT_IS_EMPTY -<br>* ACCOUNT_NOT_FOUND -<br>* CANCELLED_BY_CLIENT -<br>* CANCELLED_BY_UPVEST -<br>* CONFIGURATION_IS_MISSING -<br>* INVALID_ACCOUNT_TYPE -<br>* PORTFOLIO_IS_BALANCED -<br>* UNKNOWN - |
| `CancellationDetails` | `string` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

PortfoliosRebalancingExecutionOrder portfoliosRebalancingExecutionOrder = new PortfoliosRebalancingExecutionOrder
{
    Id = new Guid("00000cba-0000-0000-0000-000000000000"),
    ExecutionId = new Guid("0000129c-0000-0000-0000-000000000000"),
    AccountId = new Guid("0000065e-0000-0000-0000-000000000000"),
    PortfolioOrderId = new Guid("00001e9e-0000-0000-0000-000000000000"),
    Status = Status71.Filled,
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    CancellationReason = CancellationReason3.PortfolioIsBalanced,
    CancellationDetails = "cancellation_details0",
};
```

