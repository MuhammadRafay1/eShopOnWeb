
# Savings Plan Execution Portfolio

Represents a single execution of a `PORTFOLIO`-type savings plan, recording the portfolio order placed, the execution date, and the resulting status.

*This model accepts additional fields of type object.*

## Structure

`SavingsPlanExecutionPortfolio`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Universally Unique Identifier (UUID) of a savings plan execution. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `SavingsPlanId` | `Guid` | Required | Universally Unique Identifier (UUID) of a savings plan. |
| `OrderId` | [`SavingsPlanExecutionPortfolioOrderId`](../../doc/models/containers/savings-plan-execution-portfolio-order-id.md) | Required | This is a container for one-of cases. |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Status` | [`Status93`](../../doc/models/status-93.md) | Required | Status of a Savings Plan Execution.<br><br>* NEW -<br>* PROCESSING -<br>* FILLED -<br>* SETTLED -<br>* CANCELLED - |
| `Type` | `string` | Required | The type of savings plan must be "PORTFOLIO".<br><br>**Default**: `"PORTFOLIO"` |
| `ExecutionDate` | `string` | Required | Date of a savings plan execution in YYYY-MM-DD format.<br><br>**Constraints**: *Pattern*: `^[12]\d{3}-(0[1-9]\|1[0-2])-(0[1-9]\|[12]\d\|3[01])$` |
| `InstrumentId` | [`SavingsPlanExecutionPortfolioInstrumentId`](../../doc/models/containers/savings-plan-execution-portfolio-instrument-id.md) | Optional | This is a container for one-of cases. |
| `InstrumentIdType` | [`InstrumentIdType10?`](../../doc/models/instrument-id-type-10.md) | Optional | The type of the ID used in the request.<br><br>* ISIN - International Securities Identification Number<br>* WKN - German securities identification code<br><br>**Default**: `InstrumentIdType10.ISIN` |
| `CancellationReason` | [`CancellationReasonCodeForSavingsPlanExecution?`](../../doc/models/cancellation-reason-code-for-savings-plan-execution.md) | Optional | Explains the reason why the savings plan execution was cancelled .<br><br>* CANCELLED_BY_CLIENT: The savings plan execution was cancelled by the client.<br>* CANCELLED_BY_UPVEST: The savings plan execution was cancelled by Upvest. |
| `CancellationDetails` | `string` | Optional | Human-readable description providing additional context about why the savings plan execution was cancelled. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

SavingsPlanExecutionPortfolio savingsPlanExecutionPortfolio = new SavingsPlanExecutionPortfolio
{
    Id = new Guid("000021e6-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("000001e6-0000-0000-0000-000000000000"),
    AccountId = new Guid("00001b8a-0000-0000-0000-000000000000"),
    SavingsPlanId = new Guid("00001506-0000-0000-0000-000000000000"),
    OrderId = SavingsPlanExecutionPortfolioOrderId.FromUUID(new Guid("00001da2-0000-0000-0000-000000000000")),
    CashAmount = "cash_amount6",
    Currency = Currency.Eur,
    Status = Status93.New,
    Type = "PORTFOLIO",
    ExecutionDate = "execution_date6",
    InstrumentId = SavingsPlanExecutionPortfolioInstrumentId.FromString("String3"),
    InstrumentIdType = InstrumentIdType10.Isin,
    CancellationReason = CancellationReasonCodeForSavingsPlanExecution.CancelledByClient,
    CancellationDetails = "cancellation_details0",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

