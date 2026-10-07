
# Order 55

Order details.

*This model accepts additional fields of type object.*

## Structure

`Order55`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `BusinessId` | `Guid?` | Optional | The ID of the business. |
| `AccountId` | `Guid?` | Optional | The ID of the account that owns the order. |
| `CashAmount` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency?`](../../doc/models/currency.md) | Optional | - |
| `Side` | [`Side8?`](../../doc/models/side-8.md) | Optional | Side of the order.<br><br>* BUY -<br>* SELL - |
| `InstrumentId` | `string` | Optional | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |
| `InstrumentIdType` | [`InstrumentIdType?`](../../doc/models/instrument-id-type.md) | Optional | The type of the ID used in the request.<br><br>* ISIN -<br><br>**Default**: `InstrumentIdType.ISIN` |
| `OrderType` | [`OrderType4?`](../../doc/models/order-type-4.md) | Optional | Order type.<br><br>* MARKET -<br>* LIMIT -<br>* STOP - |
| `Quantity` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `LimitPrice` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `StopPrice` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Price` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Order55 order55 = new Order55
{
    BusinessId = new Guid("000002d8-0000-0000-0000-000000000000"),
    AccountId = new Guid("000013ce-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount6",
    Currency = Currency.Eur,
    Side = Side8.Buy,
    InstrumentIdType = InstrumentIdType.Isin,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

