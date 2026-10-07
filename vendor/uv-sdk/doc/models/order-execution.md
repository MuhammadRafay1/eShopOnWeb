
# Order Execution

Represents a single execution (trade fill) within an order. An order may have multiple executions if it is partially filled across several trades.

## Structure

`OrderExecution`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | - |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `ShareQuantity` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Price` | `string` | Required | Price of an instrument for a trade execution provided as a decimal string.<br><br>**Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,4})?$` |
| `TransactionTime` | `DateTime` | Required | Timestamp of when the trade was executed at the market. [RFC 3339](https://datatracker.ietf.org/doc/html/rfc3339) date-time format. |
| `Taxes` | [`List<Tax3>`](../../doc/models/tax-3.md) | Required | Taxes deducted as part of this execution. |
| `OrderId` | `Guid` | Required | Unique identifier for an order. Universally Unique Identifier (UUID). |
| `Status` | [`Status52`](../../doc/models/status-52.md) | Required | Status of the execution.<br><br>* FILLED — the execution has been filled.<br>* SETTLED — the execution has settled and securities and cash have been exchanged.<br>* CANCELLED — the execution was cancelled before settlement. |
| `Side` | [`Side1`](../../doc/models/side-1.md) | Required | Side of the execution.<br><br>* BUY — a buy execution.<br>* SELL — a sell execution. |
| `Currency` | [`Currency29`](../../doc/models/currency-29.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - British Pound<br>* USD - US Dollar |
| `SettlementDate` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{4}-[0-9]{2}-[0-9]{2}$` |
| `VenueId` | `Guid` | Required | The ID of the venue |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

OrderExecution orderExecution = new OrderExecution
{
    Id = new Guid("00000a16-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount0",
    ShareQuantity = "share_quantity4",
    Price = "price0",
    TransactionTime = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    Taxes = new List<Tax3>
    {
        new Tax3
        {
            Type = "TOTAL",
            Amount = "amount2",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
    OrderId = new Guid("000026a8-0000-0000-0000-000000000000"),
    Status = Status52.Filled,
    Side = Side1.Buy,
    Currency = Currency29.Gbp,
    VenueId = new Guid("00000024-0000-0000-0000-000000000000"),
    SettlementDate = "settlement_date8",
};
```

