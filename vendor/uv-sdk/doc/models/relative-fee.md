
# Relative Fee

*This model accepts additional fields of type object.*

## Structure

`RelativeFee`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | [`FeeType8`](../../doc/models/fee-type-8.md) | Required | Fee type<br><br>* TRANSACTION_FEE_BUY -<br>* TRANSACTION_FEE_SELL -<br>* ANNUAL_AUM_BASED_FEE - |
| `ValueType` | `string` | Required | The type of fee value must be “RELATIVE”.<br><br>**Default**: `"RELATIVE"` |
| `Bps` | `string` | Required | **Constraints**: *Pattern*: `^-?[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

RelativeFee relativeFee = new RelativeFee
{
    Type = FeeType8.AnnualAumBasedFee,
    ValueType = "RELATIVE",
    Bps = "bps2",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

