
# Transaction Fee Models List Response

Paginated list of transaction fee models. Contains a `data` array of transaction fee model objects and a `meta` object with offset/limit pagination metadata.

## Structure

`TransactionFeeModelsListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<FeeConfiguration>`](../../doc/models/fee-configuration.md) | Required | List of transaction fee models matching the query. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

TransactionFeeModelsListResponse transactionFeeModelsListResponse = new TransactionFeeModelsListResponse
{
    Meta = new Meta
    {
        Offset = 222,
        Limit = 126,
        Count = 14,
        TotalCount = 150,
        Sort = "sort4",
        Order = Order1.Asc,
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    Data = new List<FeeConfiguration>
    {
        new FeeConfiguration
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Label = "label0",
            Currency = Currency.Eur,
            ChargeMethod = "CHARGED_BY_CLIENT",
            ValueType = ValueType.Absolute,
            ApplicationType = "VOLUME",
            BaseAmountScope = "ORDER",
            Tiers = new List<FeeConfigurationTiers>
            {
                FeeConfigurationTiers.FromAbsoluteTransactionFeeTier(
                    new AbsoluteTransactionFeeTier
                    {
                        TierId = "tier_id8",
                        BaseAmountFrom = "base_amount_from0",
                        FeeAmount = "fee_amount8",
                        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                    }
                ),
            },
        },
    },
};
```

