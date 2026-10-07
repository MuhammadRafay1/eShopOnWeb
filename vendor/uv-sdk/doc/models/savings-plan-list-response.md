
# Savings Plan List Response

Paginated list of savings plans, including cursor-based pagination metadata.

## Structure

`SavingsPlanListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<SavingsPlanInstrument>`](../../doc/models/savings-plan-instrument.md) | Required | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

SavingsPlanListResponse savingsPlanListResponse = new SavingsPlanListResponse
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
    Data = new List<SavingsPlanInstrument>
    {
        new SavingsPlanInstrument
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UserId = new Guid("0000233a-0000-0000-0000-000000000000"),
            AccountId = new Guid("000015ce-0000-0000-0000-000000000000"),
            Type = Type67.Portfolio,
            CashAmount = "cash_amount8",
            StartDate = "start_date4",
            Currency = Currency.Eur,
            Period = Period1.Week,
            Interval = 1,
            Name = "name0",
            InstrumentId = SavingsPlanInstrumentInstrumentId.FromString("String5"),
            InstrumentIdType = InstrumentIdType10.Isin,
            Status = Status88.Active,
            FeeConfiguration = new List<SavingsPlanFeeConfigurationOnlyForInstrument>
            {
                new SavingsPlanFeeConfigurationOnlyForInstrument
                {
                    Type = "type2",
                    TransactionFeeModelId = new Guid("0000158e-0000-0000-0000-000000000000"),
                },
                new SavingsPlanFeeConfigurationOnlyForInstrument
                {
                    Type = "type2",
                    TransactionFeeModelId = new Guid("0000158e-0000-0000-0000-000000000000"),
                },
            },
        },
    },
};
```

