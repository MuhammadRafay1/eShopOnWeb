
# Order 39

Represents an investment order placed by an end user or on their behalf. An order transitions through states (`NEW` → `PROCESSING` → `FILLED` or `CANCELLED`) as it is routed and executed.

## Structure

`Order39`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Unique identifier for an order. Universally Unique Identifier (UUID). |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid?` | Optional | The ID of the user. Either user ID or business ID must be specified. |
| `BusinessId` | `Guid?` | Optional | The ID of the business. Either user ID or business ID must be specified. |
| `AccountId` | `Guid` | Required | The ID of the account that owns the order |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency29`](../../doc/models/currency-29.md) | Required | - |
| `Side` | [`Side`](../../doc/models/side.md) | Required | Side of the order.<br><br>* BUY — purchases the specified instrument.<br>* SELL — disposes of the specified instrument. |
| `InstrumentId` | `string` | Required | International securities identification number defined by [ISO 6166](https://en.wikipedia.org/wiki/International_Securities_Identification_Number).<br><br>**Constraints**: *Pattern*: `^[A-Z]{2}[A-Z0-9]{9}[0-9]$` |
| `InstrumentIdType` | `string` | Required, Constant | The type of the ID used in the request.<br><br>* ISIN -<br><br>**Value**: `"ISIN"` |
| `OrderType` | [`OrderType`](../../doc/models/order-type.md) | Required | Type of the order.<br><br>* MARKET — executes immediately at the best available market price.<br>* LIMIT — executes only at or better than the specified `limit_price`.<br>* STOP — triggers when the market price reaches `stop_price`, then executes at the prevailing market price. |
| `Quantity` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `UserInstrumentFitAcknowledgement` | `bool?` | Optional | Only applicable if the user has failed the instrument fit check for the instrument type being ordered. True if the user has acknowledged their willingness to trade. |
| `LimitPrice` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `StopPrice` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `ExpiryDate` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{4}-[0-9]{2}-[0-9]{2}$` |
| `Status` | [`Status51`](../../doc/models/status-51.md) | Required | The execution status of the order.<br><br>* NEW — the order has been received and validated, awaiting routing.<br>* PROCESSING — the order is being routed for execution.<br>* FILLED — the order has been fully executed.<br>* CANCELLED — the order was cancelled before being fully executed. |
| `Fee` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `CancellationReason` | [`CancellationReason?`](../../doc/models/cancellation-reason.md) | Optional | Reason for order cancellation. Present only when `status` is `CANCELLED`.<br><br>* CANCELLED_BY_CLIENT — cancelled at the end user's or client's request via the API.<br>* CANCELLED_BY_UPVEST_OPERATIONS — cancelled by Upvest operations.<br>* CANCELLED_BY_TRADING_PARTNER — cancelled by the executing partner.<br>* CANCELLED_BY_UPVEST_PLATFORM — cancelled automatically by the Upvest platform. |
| `InitiationFlow` | [`InitiationFlow`](../../doc/models/initiation-flow.md) | Required | Identifies what triggered the order.<br><br>* API — initiated directly via the client API.<br>* PORTFOLIO — initiated by a portfolio rebalancing flow.<br>* CASH_DIVIDEND_REINVESTMENT — initiated as part of dividend reinvestment.<br>* PORTFOLIO_REBALANCING — initiated by an automated rebalancing.<br>* SELL_TO_COVER_FEES — initiated automatically to cover outstanding fees.<br>* SELL_TO_COVER_TAXES — initiated automatically to cover tax obligations.<br>* ACCOUNT_LIQUIDATION — initiated as part of an account liquidation.<br>* UPVEST_OPERATIONS — initiated by Upvest operations.<br>* SAVINGS_PLAN — initiated by a savings plan execution.<br>* CLIENT_OPERATIONS — initiated by client operations. |
| `ExecutionFlow` | [`ExecutionFlow?`](../../doc/models/execution-flow.md) | Optional | Execution flow for order processing. Defaults to `STRAIGHT_THROUGH` if not specified.<br><br>* STRAIGHT_THROUGH — the order is routed and executed directly without manual intervention.<br>* BLOCK — the order is bundled with other orders for block execution. |
| `Executions` | [`List<OrderExecution>`](../../doc/models/order-execution.md) | Required | Order executions associated with this order |
| `ClientReference` | `string` | Optional | Immutable reference to the API flow that initiated the order. For client initiated API flows, this is a client provided ID. For internal initiations, it is set to the ID of the related object.<br><br>**Constraints**: *Maximum Length*: `100` |
| `FeeConfiguration` | [`List<Order39FeeConfiguration>`](../../doc/models/containers/order-39-fee-configuration.md) | Optional | This is List of a container for one-of cases. |
| `DecisionMakerId` | `string` | Optional | ID of the user behind the decision to place an order. Required only if different from the user_id. (e.g. child account order placed by a guardian) |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

Order39 order39 = new Order39
{
    Id = new Guid("00000640-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    AccountId = new Guid("000026f4-0000-0000-0000-000000000000"),
    CashAmount = "cash_amount8",
    Currency = Currency29.Eur,
    Side = Side.Buy,
    InstrumentId = "instrument_id4",
    InstrumentIdType = "ISIN",
    OrderType = OrderType.Market,
    Quantity = "quantity6",
    Status = Status51.New,
    Fee = "fee2",
    InitiationFlow = InitiationFlow.SellToCoverFees,
    Executions = new List<OrderExecution>
    {
        new OrderExecution
        {
            Id = new Guid("00002632-0000-0000-0000-000000000000"),
            CashAmount = "cash_amount6",
            ShareQuantity = "share_quantity0",
            Price = "price4",
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
            OrderId = new Guid("00001bb4-0000-0000-0000-000000000000"),
            Status = Status52.Cancelled,
            Side = Side1.Buy,
            Currency = Currency29.Eur,
            VenueId = new Guid("00000b18-0000-0000-0000-000000000000"),
            SettlementDate = "settlement_date4",
        },
    },
    UserId = new Guid("00000d50-0000-0000-0000-000000000000"),
    BusinessId = new Guid("000015fe-0000-0000-0000-000000000000"),
    UserInstrumentFitAcknowledgement = false,
    LimitPrice = "limit_price6",
    StopPrice = "stop_price4",
};
```

