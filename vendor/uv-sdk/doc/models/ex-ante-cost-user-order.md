
# Ex Ante Cost User Order

Ex-ante order details for a user.

*This model accepts additional fields of type object.*

## Structure

`ExAnteCostUserOrder`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid` | Required | The user's ID. |
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

ExAnteCostUserOrder exAnteCostUserOrder = new ExAnteCostUserOrder
{
    UserId = new Guid("00000286-0000-0000-0000-000000000000"),
    AccountId = new Guid("00001c2a-0000-0000-0000-000000000000"),
    Currency = Currency.Eur,
    Side = Side8.Buy,
    InstrumentId = "instrument_id2",
    InstrumentIdType = "ISIN",
    OrderType = OrderType4.Stop,
    CashAmount = "cash_amount6",
    Quantity = "quantity4",
    LimitPrice = "limit_price8",
    StopPrice = "stop_price2",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

