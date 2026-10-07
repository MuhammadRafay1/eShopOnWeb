
# Order Place Request

Request body for placing a new order. Either `cash_amount` or `quantity` must be provided, not both. `account_id`, `side`, `instrument_id`, and `instrument_id_type` are always required.

## Structure

`OrderPlaceRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `UserId` | `Guid?` | Optional | The ID of the user. Either user ID or business ID must be specified. |
| `BusinessId` | `Guid?` | Optional | The ID of the business. Either user ID or business ID must be specified. |
| `AccountId` | `Guid` | Required | The ID of the account that owns the order |
| `CashAmount` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency29?`](../../doc/models/currency-29.md) | Optional | - |
| `Side` | [`Side`](../../doc/models/side.md) | Required | Side of the order.<br><br>* BUY — purchases the specified instrument.<br>* SELL — disposes of the specified instrument. |
| `InstrumentId` | `string` | Required | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |
| `InstrumentIdType` | `string` | Required, Constant | The type of the ID used in the request.<br><br>* ISIN -<br><br>**Value**: `"ISIN"` |
| `OrderType` | [`OrderType?`](../../doc/models/order-type.md) | Optional | Type of the order.<br><br>* MARKET — executes immediately at the best available market price.<br>* LIMIT — executes only at or better than the specified `limit_price`.<br>* STOP — triggers when the market price reaches `stop_price`, then executes at the prevailing market price. |
| `Quantity` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `UserInstrumentFitAcknowledgement` | `bool?` | Optional | Only applicable if the user has failed the instrument fit check for the instrument type being ordered. True if the user has acknowledged their willingness to trade. |
| `LimitPrice` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `StopPrice` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `ExpiryDate` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{4}-[0-9]{2}-[0-9]{2}$` |
| `ClientReference` | `string` | Optional | Immutable reference to the API flow that initiated the order. For client initiated API flows, this is a client provided ID. For internal initiations, it is set to the ID of the related object.<br><br>**Constraints**: *Maximum Length*: `100` |
| `ExecutionFlow` | [`ExecutionFlow?`](../../doc/models/execution-flow.md) | Optional | Execution flow for order processing. Defaults to `STRAIGHT_THROUGH` if not specified.<br><br>* STRAIGHT_THROUGH — the order is routed and executed directly without manual intervention.<br>* BLOCK — the order is bundled with other orders for block execution. |
| `FeeConfiguration` | [`List<OrderPlaceRequestFeeConfiguration>`](../../doc/models/containers/order-place-request-fee-configuration.md) | Optional | This is List of a container for one-of cases. |
| `DecisionMakerId` | `string` | Optional | ID of the user behind the decision to place an order. Required only if different from the user_id. (e.g. child account order placed by a guardian) |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

OrderPlaceRequest orderPlaceRequest = new OrderPlaceRequest
{
    AccountId = new Guid("00000cde-0000-0000-0000-000000000000"),
    Side = Side.Buy,
    InstrumentId = "instrument_id6",
    InstrumentIdType = "ISIN",
    UserId = new Guid("00001a4a-0000-0000-0000-000000000000"),
    BusinessId = new Guid("000022f8-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount0",
    Currency = Currency29.Usd,
    OrderType = OrderType.Stop,
};
```

