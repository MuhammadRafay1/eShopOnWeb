
# Absolute Transaction Fee Tier

A single tier of a transaction fee model with a fixed, absolute cash amount.

*This model accepts additional fields of type object.*

## Structure

`AbsoluteTransactionFeeTier`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `TierId` | `string` | Required | Unique identifier of the fee tier within the transaction fee model. A numeric string of up to 63 digits.<br><br>**Constraints**: *Pattern*: `^[0-9]{1,63}$` |
| `BaseAmountFrom` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `FeeAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AbsoluteTransactionFeeTier absoluteTransactionFeeTier = new AbsoluteTransactionFeeTier
{
    TierId = "tier_id8",
    BaseAmountFrom = "base_amount_from0",
    FeeAmount = "fee_amount8",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

