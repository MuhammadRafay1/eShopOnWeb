
# Absolute Fee

A fixed cash fee applied to an order, specified as an absolute amount in a given currency.

*This model accepts additional fields of type object.*

## Structure

`AbsoluteFee`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | [`FeeType`](../../doc/models/fee-type.md) | Required | Fee type<br><br>* TRANSACTION_FEE_BUY -<br>* TRANSACTION_FEE_SELL - |
| `ValueType` | `string` | Required | The value type must be “ABSOLUTE”.<br><br>**Default**: `"ABSOLUTE"` |
| `ChargeMethod` | [`FeeChargeMethod`](../../doc/models/fee-charge-method.md) | Required | Indicates whether the fee will be charged by client or by other methods.<br><br>* CHARGED_BY_CLIENT -<br>* COLLECTED_BY_UPVEST - |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{1,9}(\.[0-9]{2})?$` |
| `Currency` | [`Currency29`](../../doc/models/currency-29.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - British Pound<br>* USD - US Dollar |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AbsoluteFee absoluteFee = new AbsoluteFee
{
    Type = FeeType.TransactionFeeBuy,
    ValueType = "ABSOLUTE",
    ChargeMethod = FeeChargeMethod.ChargedByClient,
    CashAmount = "cash_amount0",
    Currency = Currency29.Usd,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

