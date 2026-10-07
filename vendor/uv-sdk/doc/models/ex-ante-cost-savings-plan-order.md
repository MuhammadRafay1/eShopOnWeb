
# Ex Ante Cost Savings Plan Order

Savings Plan Order details.

*This model accepts additional fields of type object.*

## Structure

`ExAnteCostSavingsPlanOrder`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | The ID of the user. |
| `AccountId` | `Guid` | Required | Account ID owning the order. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | - |
| `Side` | [`Side8`](../../doc/models/side-8.md) | Required | Side of the order.<br><br>* BUY -<br>* SELL - |
| `InstrumentId` | `string` | Required | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |
| `InstrumentIdType` | `string` | Required, Constant | The type of the ID used in the request.<br><br>* ISIN -<br><br>**Value**: `"ISIN"` |
| `OrderType` | [`OrderType4`](../../doc/models/order-type-4.md) | Required | Order type.<br><br>* MARKET -<br>* LIMIT -<br>* STOP - |
| `Period` | [`Period`](../../doc/models/period.md) | Required | - |
| `Interval` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,63}$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

ExAnteCostSavingsPlanOrder exAnteCostSavingsPlanOrder = new ExAnteCostSavingsPlanOrder
{
    UserId = new Guid("00000e6c-0000-0000-0000-000000000000"),
    AccountId = new Guid("00000100-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount2",
    Currency = Currency.Eur,
    Side = Side8.Buy,
    InstrumentId = "instrument_id8",
    InstrumentIdType = "ISIN",
    OrderType = OrderType4.Stop,
    Period = Period.Year,
    Interval = "interval2",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

