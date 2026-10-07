
# Report Order Ex Ante Cost Create Request Regular

*This model accepts additional fields of type object.*

## Structure

`ReportOrderExAnteCostCreateRequestRegular`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | The type of report must be “ORDER_EX_ANTE_COST”.<br><br>**Default**: `"ORDER_EX_ANTE_COST"` |
| `Order` | [`ReportOrderExAnteCostCreateRequestRegularOrder`](../../doc/models/containers/report-order-ex-ante-cost-create-request-regular-order.md) | Required | This is a container for one-of cases. |
| `Fees` | [`List<ReportOrderExAnteCostCreateRequestRegularFees>`](../../doc/models/containers/report-order-ex-ante-cost-create-request-regular-fees.md) | Optional | This is List of a container for one-of cases. |
| `Inducements` | [`List<Inducement>`](../../doc/models/inducement.md) | Optional | Client inducements. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

ReportOrderExAnteCostCreateRequestRegular reportOrderExAnteCostCreateRequestRegular = new ReportOrderExAnteCostCreateRequestRegular
{
    Type = "ORDER_EX_ANTE_COST",
    Order = ReportOrderExAnteCostCreateRequestRegularOrder.FromExAnteCostUserOrder(
        new ExAnteCostUserOrder
        {
            UserId = new Guid("000002de-0000-0000-0000-000000000000"),
            AccountId = new Guid("00001c82-0000-0000-0000-000000000000"),
            Currency = Currency.Eur,
            Side = Side8.Buy,
            InstrumentId = "instrument_id0",
            InstrumentIdType = "ISIN",
            OrderType = OrderType4.Limit,
            CashAmount = "cash_amount4",
            Quantity = "quantity2",
            LimitPrice = "limit_price0",
            StopPrice = "stop_price0",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        }
    ),
    Fees = new List<ReportOrderExAnteCostCreateRequestRegularFees>
    {
        ReportOrderExAnteCostCreateRequestRegularFees.FromAbsoluteFee4(
            new AbsoluteFee4
            {
                Type = FeeType8.AnnualAumBasedFee,
                ValueType = "value_type0",
                CashAmount = "cash_amount8",
                Currency = Currency.Eur,
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
    },
    Inducements = new List<Inducement>
    {
        new Inducement
        {
            ValueType = "value_type8",
            CashAmount = "cash_amount6",
            Currency = Currency.Eur,
        },
        new Inducement
        {
            ValueType = "value_type8",
            CashAmount = "cash_amount6",
            Currency = Currency.Eur,
        },
        new Inducement
        {
            ValueType = "value_type8",
            CashAmount = "cash_amount6",
            Currency = Currency.Eur,
        },
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

