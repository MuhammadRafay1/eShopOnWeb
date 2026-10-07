
# Relative Transaction Fee Tier

A single tier of a transaction fee model based on a percentage (defined in basis points).

*This model accepts additional fields of type object.*

## Structure

`RelativeTransactionFeeTier`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `TierId` | `string` | Required | Unique identifier of the fee tier within the transaction fee model. A numeric string of up to 63 digits.<br><br>**Constraints**: *Pattern*: `^[0-9]{1,63}$` |
| `BaseAmountFrom` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `FeeBps` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `MinFeeAmount` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `MaxFeeAmount` | `string` | Optional | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

RelativeTransactionFeeTier relativeTransactionFeeTier = new RelativeTransactionFeeTier
{
    TierId = "tier_id8",
    BaseAmountFrom = "base_amount_from0",
    FeeBps = "fee_bps6",
    MinFeeAmount = "min_fee_amount8",
    MaxFeeAmount = "max_fee_amount6",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

