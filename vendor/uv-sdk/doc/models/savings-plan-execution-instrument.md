
# Savings Plan Execution Instrument

Represents a single execution of an `INSTRUMENT`-type savings plan, recording the order placed, the execution date, and the resulting status.

*This model accepts additional fields of type object.*

## Structure

`SavingsPlanExecutionInstrument`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Universally Unique Identifier (UUID) of a savings plan execution. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `SavingsPlanId` | `Guid` | Required | Universally Unique Identifier (UUID) of a savings plan. |
| `OrderId` | [`SavingsPlanExecutionInstrumentOrderId`](../../doc/models/containers/savings-plan-execution-instrument-order-id.md) | Required | This is a container for one-of cases. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Status` | [`Status93`](../../doc/models/status-93.md) | Required | Status of a Savings Plan Execution.<br><br>* NEW -<br>* PROCESSING -<br>* FILLED -<br>* SETTLED -<br>* CANCELLED - |
| `Type` | `string` | Required | The type of savings plan must be "INSTRUMENT".<br><br>**Default**: `"INSTRUMENT"` |
| `ExecutionDate` | `string` | Required | Date of a savings plan execution in YYYY-MM-DD format.<br><br>**Constraints**: *Pattern*: `^[12]\d{3}-(0[1-9]\|1[0-2])-(0[1-9]\|[12]\d\|3[01])$` |
| `InstrumentId` | [`SavingsPlanExecutionInstrumentInstrumentId`](../../doc/models/containers/savings-plan-execution-instrument-instrument-id.md) | Optional | This is a container for one-of cases. |
| `InstrumentIdType` | [`InstrumentIdType10?`](../../doc/models/instrument-id-type-10.md) | Optional | The type of the ID used in the request.<br><br>* ISIN - International Securities Identification Number<br>* WKN - German securities identification code<br><br>**Default**: `InstrumentIdType10.ISIN` |
| `FeeConfiguration` | [`List<SavingsPlanFeeConfigurationOnlyForInstrument>`](../../doc/models/savings-plan-fee-configuration-only-for-instrument.md) | Optional | Fee configuration for instrument-type savings plan executions. Specifies the transaction fee model to apply to each buy order placed on execution. |
| `CancellationReason` | [`CancellationReasonCodeForSavingsPlanExecution?`](../../doc/models/cancellation-reason-code-for-savings-plan-execution.md) | Optional | Explains the reason why the savings plan execution was cancelled .<br><br>* CANCELLED_BY_CLIENT: The savings plan execution was cancelled by the client.<br>* CANCELLED_BY_UPVEST: The savings plan execution was cancelled by Upvest. |
| `CancellationDetails` | `string` | Optional | Human-readable description providing additional context about why the savings plan execution was cancelled. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

SavingsPlanExecutionInstrument savingsPlanExecutionInstrument = new SavingsPlanExecutionInstrument
{
    Id = new Guid("0000215e-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("0000015e-0000-0000-0000-000000000000"),
    AccountId = new Guid("00001b02-0000-0000-0000-000000000000"),
    SavingsPlanId = new Guid("0000147e-0000-0000-0000-000000000000"),
    OrderId = SavingsPlanExecutionInstrumentOrderId.FromUUID(new Guid("00001d1a-0000-0000-0000-000000000000")),
    CashAmount = "cash_amount0",
    Currency = Currency.Eur,
    Status = Status93.Cancelled,
    Type = "INSTRUMENT",
    ExecutionDate = "execution_date0",
    InstrumentId = SavingsPlanExecutionInstrumentInstrumentId.FromString("String7"),
    InstrumentIdType = InstrumentIdType10.Isin,
    FeeConfiguration = new List<SavingsPlanFeeConfigurationOnlyForInstrument>
    {
        new SavingsPlanFeeConfigurationOnlyForInstrument
        {
            Type = "type2",
            TransactionFeeModelId = new Guid("0000158e-0000-0000-0000-000000000000"),
        },
    },
    CancellationReason = CancellationReasonCodeForSavingsPlanExecution.CancelledByClient,
    CancellationDetails = "cancellation_details4",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

