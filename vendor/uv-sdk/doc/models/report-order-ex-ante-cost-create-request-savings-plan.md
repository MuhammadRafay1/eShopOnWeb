
# Report Order Ex Ante Cost Create Request Savings Plan

*This model accepts additional fields of type object.*

## Structure

`ReportOrderExAnteCostCreateRequestSavingsPlan`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | The type of report must be “ORDER_EX_ANTE_COST_SAVINGS_PLAN”. Savings plan ex-ante reports are only supported for individual users, not for business entities.<br><br>**Default**: `"ORDER_EX_ANTE_COST_SAVINGS_PLAN"` |
| `Order` | [`ExAnteCostSavingsPlanOrder`](../../doc/models/ex-ante-cost-savings-plan-order.md) | Required | Savings Plan Order details. |
| `Fees` | [`List<ReportOrderExAnteCostCreateRequestSavingsPlanFees>`](../../doc/models/containers/report-order-ex-ante-cost-create-request-savings-plan-fees.md) | Optional | This is List of a container for one-of cases. |
| `Inducements` | [`List<Inducement>`](../../doc/models/inducement.md) | Optional | Client inducements. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

ReportOrderExAnteCostCreateRequestSavingsPlan reportOrderExAnteCostCreateRequestSavingsPlan = new ReportOrderExAnteCostCreateRequestSavingsPlan
{
    Type = "ORDER_EX_ANTE_COST_SAVINGS_PLAN",
    Order = new ExAnteCostSavingsPlanOrder
    {
        UserId = new Guid("00001850-0000-0000-0000-000000000000"),
        AccountId = new Guid("00000ae4-0000-0000-0000-000000000000"),
        CashAmount = "cash_amount4",
        Currency = Currency.Eur,
        Side = Side8.Buy,
        InstrumentId = "instrument_id0",
        InstrumentIdType = "ISIN",
        OrderType = OrderType4.Limit,
        Period = Period.Month,
        Interval = "interval4",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Fees = new List<ReportOrderExAnteCostCreateRequestSavingsPlanFees>
    {
        ReportOrderExAnteCostCreateRequestSavingsPlanFees.FromAbsoluteFee4(
            new AbsoluteFee4
            {
                Type = FeeType8.AnnualAumBasedFee,
                ValueType = "value_type0",
                CashAmount = "cash_amount8",
                Currency = Currency.Eur,
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
        ReportOrderExAnteCostCreateRequestSavingsPlanFees.FromAbsoluteFee4(
            new AbsoluteFee4
            {
                Type = FeeType8.AnnualAumBasedFee,
                ValueType = "value_type0",
                CashAmount = "cash_amount8",
                Currency = Currency.Eur,
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
        ReportOrderExAnteCostCreateRequestSavingsPlanFees.FromAbsoluteFee4(
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
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

