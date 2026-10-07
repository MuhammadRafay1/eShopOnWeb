
# Portfolios Order

Represents a portfolio order — a BUY or SELL instruction that invests or withdraws a cash amount distributed across all instruments in the account's current allocation.

## Structure

`PortfoliosOrder`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Universally Unique Identifier (UUID) of a portfolio order. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid?` | Optional | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `AllocationId` | `Guid?` | Optional | Universally Unique Identifier (UUID) of a portfolio allocation. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Status` | [`Status65`](../../doc/models/status-65.md) | Required | Execution status of the Portfolio Order.<br><br>* NEW -<br>* PROCESSING -<br>* FILLED -<br>* SETTLED -<br>* CANCELLED - |
| `Type` | [`Type46?`](../../doc/models/type-46.md) | Optional | Type of the Portfolio Order.<br><br>* BUY -<br>* SELL -<br>* REBALANCING - |
| `PostTax` | `bool` | Required | Cash amount is post-tax value<br><br>**Default**: `false` |
| `Orders` | [`List<PortfoliosOrder1>`](../../doc/models/portfolios-order-1.md) | Required | Orders associated with this portfolio order |
| `ClientReference` | `string` | Required | A reference string provided by the client to correlate the portfolio order with a record in the client's own system. |
| `InitiationFlow` | [`InitiationFlowUsedDuringOrderCreation`](../../doc/models/initiation-flow-used-during-order-creation.md) | Required | What triggered the order creation .<br><br>* API -<br>* SAVINGS_PLAN - |
| `CancellationReason` | [`CancellationReasonCode?`](../../doc/models/cancellation-reason-code.md) | Optional | Explains the reason why the order was cancelled .<br><br>* ACCOUNT_IS_EMPTY -<br>* CANCELLED_BY_CLIENT -<br>* CANCELLED_BY_UPVEST -<br>* PORTFOLIO_IS_BALANCED -<br>* SELL_LIMIT_EXCEEDED - |
| `CancellationDetails` | `string` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

PortfoliosOrder portfoliosOrder = new PortfoliosOrder
{
    Id = new Guid("000021c4-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountId = new Guid("00001b68-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount2",
    Currency = Currency.Eur,
    Status = Status65.Processing,
    PostTax = false,
    Orders = new List<PortfoliosOrder1>
    {
        new PortfoliosOrder1
        {
            Id = new Guid("0000181c-0000-0000-0000-000000000000"),
            Side = Side15.Buy,
            Status = Status51.New,
        },
    },
    ClientReference = "client_reference6",
    InitiationFlow = InitiationFlowUsedDuringOrderCreation.Api,
    UserId = new Guid("000001c4-0000-0000-0000-000000000000"),
    AllocationId = new Guid("00001084-0000-0000-0000-000000000000"),
    Type = Type46.Buy,
    CancellationReason = CancellationReasonCode.CancelledByUpvest,
    CancellationDetails = "cancellation_details6",
};
```

