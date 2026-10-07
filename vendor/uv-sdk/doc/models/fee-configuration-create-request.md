
# Fee Configuration Create Request

Request to create a transaction fee model. Defines the currency, charge method, value type, application type, base amount scope, and the fee tiers. Transaction fee models are immutable once created.

## Structure

`FeeConfigurationCreateRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Label` | `string` | Required | A human-readable label for the transaction fee model. |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `ChargeMethod` | `string` | Required, Constant | Indicates how the transaction fee is charged.<br><br>* `CHARGED_BY_CLIENT` — The fee is charged by the client as part of post-trade settlement; the fee movement occurs outside Upvest cash balances.<br><br>**Value**: `"CHARGED_BY_CLIENT"` |
| `ValueType` | [`ValueType`](../../doc/models/value-type.md) | Required | The value type of the transaction fee model.<br><br>* `ABSOLUTE` — Tier fees are fixed cash amounts.<br>* `RELATIVE` — Tier fees are percentages of the order value, expressed in basis points. |
| `ApplicationType` | `string` | Required, Constant | The application type of the transaction fee model.<br><br>* `VOLUME` — The full order value is assessed against the tier thresholds, and the matching tier's fee applies to the total order volume.<br><br>**Value**: `"VOLUME"` |
| `BaseAmountScope` | `string` | Required, Constant | The scope of the base amount that fee tiers are evaluated against.<br><br>* `ORDER` — Tiers are evaluated against the total cash value of each order.<br><br>**Value**: `"ORDER"` |
| `Tiers` | [`List<FeeConfigurationCreateRequestTiers>`](../../doc/models/containers/fee-configuration-create-request-tiers.md) | Required | This is List of a container for one-of cases. |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

FeeConfigurationCreateRequest feeConfigurationCreateRequest = new FeeConfigurationCreateRequest
{
    Label = "label2",
    Currency = Currency.Eur,
    ChargeMethod = "CHARGED_BY_CLIENT",
    ValueType = ValueType.Absolute,
    ApplicationType = "VOLUME",
    BaseAmountScope = "ORDER",
    Tiers = new List<FeeConfigurationCreateRequestTiers>
    {
        FeeConfigurationCreateRequestTiers.FromAbsoluteTransactionFeeTier(
            new AbsoluteTransactionFeeTier
            {
                TierId = "tier_id8",
                BaseAmountFrom = "base_amount_from0",
                FeeAmount = "fee_amount8",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
        FeeConfigurationCreateRequestTiers.FromAbsoluteTransactionFeeTier(
            new AbsoluteTransactionFeeTier
            {
                TierId = "tier_id8",
                BaseAmountFrom = "base_amount_from0",
                FeeAmount = "fee_amount8",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
    },
};
```

