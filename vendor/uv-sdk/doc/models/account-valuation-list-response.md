
# Account Valuation List Response

## Structure

`AccountValuationListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<AccountValuation>`](../../doc/models/account-valuation.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AccountValuationListResponse accountValuationListResponse = new AccountValuationListResponse
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
    Data = new List<AccountValuation>
    {
        new AccountValuation
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            AccountId = new Guid("000015ce-0000-0000-0000-000000000000"),
            TotalSecurityValue = new TotalSecurityValue
            {
                Amount = "amount2",
                Currency = Currency.Eur,
            },
            PriceQuality = PriceQuality4.Eod,
            SecurityPositions = new List<AccountValuationSecurityPosition>
            {
                new AccountValuationSecurityPosition
                {
                    Instrument = new Instrument6
                    {
                        Uuid = new Guid("00001fd8-0000-0000-0000-000000000000"),
                        Isin = "isin4",
                    },
                    Quantity = "quantity4",
                    MValue = new MValue
                    {
                        Amount = "amount4",
                        Currency = Currency.Eur,
                        PriceTime = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                            provider: CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind),
                    },
                    Weight = "weight4",
                    PriceQuality = PriceQuality5.Eod,
                },
            },
            ValuationTime = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
    },
};
```

