
# Cash Transaction List Response

## Structure

`CashTransactionListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<CashTransaction>`](../../doc/models/cash-transaction.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

CashTransactionListResponse cashTransactionListResponse = new CashTransactionListResponse
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
    Data = new List<CashTransaction>
    {
        new CashTransaction
        {
            AccountGroupId = new Guid("00000794-0000-0000-0000-000000000000"),
            BookingDate = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Delta = new CashTransactionDelta
            {
                Amount = "amount0",
                Currency = Currency1.Eur,
            },
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            Taxes = new List<TransactionTax>
            {
                new TransactionTax
                {
                    Amount = "amount2",
                    Currency = Currency1.Eur,
                    Type = "TOTAL",
                },
            },
            TaxesDetails = new TransactionTaxesDetails
            {
                TotalAmount = new TotalAmount
                {
                    Amount = "amount8",
                    Currency = Currency1.Usd,
                    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                },
                TaxBreakdown = new List<TransactionTax1>
                {
                    new TransactionTax1
                    {
                        Amount = "amount6",
                        Currency = Currency1.Usd,
                        Type = Type49.SolidaritySurcharge,
                        TaxingJurisdiction = "taxing_jurisdiction2",
                    },
                },
            },
            References = new List<CashTransactionReference>
            {
                new CashTransactionReference
                {
                    Id = new Guid("00000f98-0000-0000-0000-000000000000"),
                    Type = Type50.CorporateAction,
                },
            },
            Type = TransactionType.CashDistributionFromNonEligibleSecuritiesSalesCancellation,
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            ValueDate = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            Instrument = new Instrument8
            {
                Uuid = new Guid("00001fd8-0000-0000-0000-000000000000"),
                Isin = "isin4",
            },
            AccountId = new Guid("000015ce-0000-0000-0000-000000000000"),
            Fees = new List<TransactionFee2>
            {
                new TransactionFee2
                {
                    Amount = "amount8",
                    Currency = Currency1.Eur,
                    Type = "type0",
                    ChargeMethod = ChargeMethod.ChargedByClient,
                },
            },
            Fx = new Fx
            {
                BaseCurrency = BaseCurrency.Gbp,
                QuoteCurrency = QuoteCurrency.Eur,
                Rate = new FxRate
                {
                    AllInRate = "all_in_rate8",
                    BaseRate = "base_rate8",
                    MarkupRate = "markup_rate8",
                },
            },
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
};
```

