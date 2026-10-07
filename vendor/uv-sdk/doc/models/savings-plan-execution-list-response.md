
# Savings Plan Execution List Response

Paginated list of savings plan executions for a savings plan, including cursor-based pagination metadata.

## Structure

`SavingsPlanExecutionListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Meta` | [`Meta`](../../doc/models/meta.md) | Required | - |
| `Data` | [`List<SavingsPlanExecutionListResponseData>`](../../doc/models/containers/savings-plan-execution-list-response-data.md) | Required | This is List of a container for any-of cases. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

SavingsPlanExecutionListResponse savingsPlanExecutionListResponse = new SavingsPlanExecutionListResponse
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
    Data = new List<SavingsPlanExecutionListResponseData>
    {
        SavingsPlanExecutionListResponseData.FromSavingsPlanExecutionInstrument(
            new SavingsPlanExecutionInstrument
            {
                Id = new Guid("000019fc-0000-0000-0000-000000000000"),
                CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UserId = new Guid("0000210c-0000-0000-0000-000000000000"),
                AccountId = new Guid("000013a0-0000-0000-0000-000000000000"),
                SavingsPlanId = new Guid("000019f4-0000-0000-0000-000000000000"),
                OrderId = SavingsPlanExecutionInstrumentOrderId.FromUUID(new Guid("00001dcc-0000-0000-0000-000000000000")),
                CashAmount = "cash_amount0",
                Currency = Currency.Eur,
                Status = Status93.Processing,
                Type = "type8",
                ExecutionDate = "execution_date0",
                InstrumentId = SavingsPlanExecutionInstrumentInstrumentId.FromString("String5"),
                InstrumentIdType = InstrumentIdType10.Isin,
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
                    new SavingsPlanFeeConfigurationOnlyForInstrument
                    {
                        Type = "type2",
                        TransactionFeeModelId = new Guid("0000158e-0000-0000-0000-000000000000"),
                    },
                },
                CancellationReason = CancellationReasonCodeForSavingsPlanExecution.CancelledByClient,
                CancellationDetails = "cancellation_details4",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
        SavingsPlanExecutionListResponseData.FromSavingsPlanExecutionInstrument(
            new SavingsPlanExecutionInstrument
            {
                Id = new Guid("000019fc-0000-0000-0000-000000000000"),
                CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                UserId = new Guid("0000210c-0000-0000-0000-000000000000"),
                AccountId = new Guid("000013a0-0000-0000-0000-000000000000"),
                SavingsPlanId = new Guid("000019f4-0000-0000-0000-000000000000"),
                OrderId = SavingsPlanExecutionInstrumentOrderId.FromUUID(new Guid("00001dcc-0000-0000-0000-000000000000")),
                CashAmount = "cash_amount0",
                Currency = Currency.Eur,
                Status = Status93.Processing,
                Type = "type8",
                ExecutionDate = "execution_date0",
                InstrumentId = SavingsPlanExecutionInstrumentInstrumentId.FromString("String5"),
                InstrumentIdType = InstrumentIdType10.Isin,
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
                    new SavingsPlanFeeConfigurationOnlyForInstrument
                    {
                        Type = "type2",
                        TransactionFeeModelId = new Guid("0000158e-0000-0000-0000-000000000000"),
                    },
                },
                CancellationReason = CancellationReasonCodeForSavingsPlanExecution.CancelledByClient,
                CancellationDetails = "cancellation_details4",
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
    },
};
```

