
# Fee Collection

## Structure

`FeeCollection`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Fee collection unique identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `AccountGroupId` | `Guid` | Required | Account group unique identifier. |
| `Type` | [`Type37`](../../doc/models/type-37.md) | Required | Type of the fee collection<br><br>* SERVICE_FEE - Service fee intake in a pre-defined cadence (e.g. monthly)<br>* SERVICE_FEE_LIQUIDATION - Service fee intake as a result of a Portfolio liquidation |
| `CollectionAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `ProcessedAmount` | [`ProcessedAmount`](../../doc/models/processed-amount.md) | Required | - |
| `SellToCoverOrders` | [`List<SellToCoverOrderDetails>`](../../doc/models/sell-to-cover-order-details.md) | Optional | - |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Status` | [`Status53`](../../doc/models/status-53.md) | Required | Status of the fee collection<br><br>* PROCESSING - Fee collection is in progress.<br>* FINALISED - Fees have been collected from the account and the funds has been transferred to the client.<br>* CANCELLED - Fee collection has been cancelled. |
| `PeriodStart` | `DateTime` | Required | Start date of the fee collection period in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) RFC 3339 |
| `PeriodEnd` | `DateTime` | Required | End date of the fee collection period in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) RFC 3339 |
| `CalculationBreakdown` | [`List<FeeCalculationBreakdownItem>`](../../doc/models/fee-calculation-breakdown-item.md) | Optional | Breakdown of the fee collection by fee model and subperiod. Populated only for fee collections created from daily fee model calculations. |
| `ReplacedCollectionId` | `Guid?` | Optional | Fee collection unique identifier. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

FeeCollection feeCollection = new FeeCollection
{
    Id = new Guid("00000eac-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountId = new Guid("00000850-0000-0000-0000-000000000000"),
    AccountGroupId = new Guid("00002126-0000-0000-0000-000000000000"),
    Type = Type37.ServiceFee,
    CollectionAmount = "collection_amount2",
    ProcessedAmount = new ProcessedAmount
    {
        CashBalance = "cash_balance4",
        SellToCover = "sell_to_cover4",
        TotalResidualAmount = "total_residual_amount2",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Currency = Currency.Eur,
    Status = Status53.Finalised,
    PeriodStart = DateTime.Parse("2016-03-13"),
    PeriodEnd = DateTime.Parse("2016-03-13"),
    SellToCoverOrders = new List<SellToCoverOrderDetails>
    {
        new SellToCoverOrderDetails
        {
            Id = new Guid("00000dc6-0000-0000-0000-000000000000"),
            ResidualAmount = "residual_amount8",
        },
        new SellToCoverOrderDetails
        {
            Id = new Guid("00000dc6-0000-0000-0000-000000000000"),
            ResidualAmount = "residual_amount8",
        },
        new SellToCoverOrderDetails
        {
            Id = new Guid("00000dc6-0000-0000-0000-000000000000"),
            ResidualAmount = "residual_amount8",
        },
    },
    CalculationBreakdown = new List<FeeCalculationBreakdownItem>
    {
        new FeeCalculationBreakdownItem
        {
            FeeModelId = new Guid("000021aa-0000-0000-0000-000000000000"),
            SubperiodStart = DateTime.Parse("2016-03-13"),
            SubperiodEnd = DateTime.Parse("2016-03-13"),
            SubtotalAmount = "subtotal_amount8",
            Components = new List<FeeBreakdownComponent>
            {
                new FeeBreakdownComponent
                {
                    Type = Type38.TransactionLumpSum,
                    Amount = "amount8",
                },
            },
        },
    },
    ReplacedCollectionId = new Guid("000013cc-0000-0000-0000-000000000000"),
};
```

