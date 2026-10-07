
# Ex Ante Cost Business Order

Ex Ante Order details for a business.

*This model accepts additional fields of type object.*

## Structure

`ExAnteCostBusinessOrder`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `BusinessId` | `Guid` | Required | The business's ID |
| `AccountId` | `Guid` | Required | Account ID owning the order. |
| `CashAmount` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | - |
| `Side` | [`Side8`](../../doc/models/side-8.md) | Required | Side of the order.<br><br>* BUY -<br>* SELL - |
| `InstrumentId` | `string` | Required | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |
| `InstrumentIdType` | `string` | Required, Constant | The type of the ID used in the request.<br><br>* ISIN -<br><br>**Value**: `"ISIN"` |
| `OrderType` | [`OrderType4`](../../doc/models/order-type-4.md) | Required | Order type.<br><br>* MARKET -<br>* LIMIT -<br>* STOP - |
| `Quantity` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `LimitPrice` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `StopPrice` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

ExAnteCostBusinessOrder exAnteCostBusinessOrder = new ExAnteCostBusinessOrder
{
    BusinessId = new Guid("000013e2-0000-0000-0000-000000000000"),
    AccountId = new Guid("000024d8-0000-0000-0000-000000000000"),
    Currency = Currency.Eur,
    Side = Side8.Buy,
    InstrumentId = "instrument_id4",
    InstrumentIdType = "ISIN",
    OrderType = OrderType4.Stop,
    CashAmount = "cash_amount8",
    Quantity = "quantity6",
    LimitPrice = "limit_price6",
    StopPrice = "stop_price4",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

