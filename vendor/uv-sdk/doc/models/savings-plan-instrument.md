
# Savings Plan Instrument

Represents an `INSTRUMENT`-type savings plan — a recurring investment that automatically buys a fixed cash amount of a specified instrument on a defined schedule.

## Structure

`SavingsPlanInstrument`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Universally Unique Identifier (UUID) of a savings plan. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UserId` | `Guid` | Required | Unique identifier of the user, as a UUID. |
| `AccountId` | `Guid` | Required | Account unique identifier. |
| `Name` | `string` | Optional | Savings plan name |
| `Type` | [`Type67`](../../doc/models/type-67.md) | Required | Type of the Savings plan.<br><br>* PORTFOLIO -<br>* INSTRUMENT - |
| `InstrumentId` | [`SavingsPlanInstrumentInstrumentId`](../../doc/models/containers/savings-plan-instrument-instrument-id.md) | Optional | This is a container for one-of cases. |
| `InstrumentIdType` | [`InstrumentIdType10?`](../../doc/models/instrument-id-type-10.md) | Optional | The type of the ID used in the request.<br><br>* ISIN - International Securities Identification Number<br>* WKN - German securities identification code<br><br>**Default**: `InstrumentIdType10.ISIN` |
| `CashAmount` | `string` | Required | **Constraints**: *Pattern*: `^[0-9]{0,63}(\.[0-9]{1,27})?$` |
| `StartDate` | `string` | Required | First date of the savings plan execution in YYYY-MM-DD format.<br><br>**Constraints**: *Pattern*: `^[12]\d{3}-(0[1-9]\|1[0-2])-(0[1-9]\|[12]\d\|3[01])$` |
| `Currency` | [`Currency`](../../doc/models/currency.md) | Required | Alphabetic three-letter [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) currency code.<br><br>* EUR - Euro<br>* GBP - Pound Sterling |
| `Period` | [`Period1`](../../doc/models/period-1.md) | Required | Unit of time.<br><br>* WEEK -<br>* MONTH -<br>* YEAR - |
| `Interval` | `int` | Required | Number of periods between executions<br><br>**Default**: `1`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `Status` | [`Status88?`](../../doc/models/status-88.md) | Optional | Status of a Savings Plan.<br><br>* ACTIVE -<br>* CANCELLED - |
| `FeeConfiguration` | [`List<SavingsPlanFeeConfigurationOnlyForInstrument>`](../../doc/models/savings-plan-fee-configuration-only-for-instrument.md) | Optional | Fee configuration for instrument-type savings plan executions. Specifies the transaction fee model to apply to each buy order placed on execution. |
| `CancellationReason` | [`CancellationReasonCodeForSavingsPlan?`](../../doc/models/cancellation-reason-code-for-savings-plan.md) | Optional | Explains the reason why the savings plan was cancelled .<br><br>* CANCELLED_BY_CLIENT: The savings plan was cancelled by the client.<br>* CANCELLED_BY_UPVEST: The savings plan was cancelled by Upvest. |
| `CancellationDetails` | `string` | Optional | Additional details about the cancellation |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;

SavingsPlanInstrument savingsPlanInstrument = new SavingsPlanInstrument
{
    Id = new Guid("000018e0-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UserId = new Guid("00001ff0-0000-0000-0000-000000000000"),
    AccountId = new Guid("00001284-0000-0000-0000-000000000000"),
    Type = Type67.Portfolio,
    CashAmount = "cash_amount6",
    StartDate = "start_date2",
    Currency = Currency.Eur,
    Period = Period1.Month,
    Interval = 1,
    Name = "name8",
    InstrumentId = SavingsPlanInstrumentInstrumentId.FromString("String3"),
    InstrumentIdType = InstrumentIdType10.Isin,
    Status = Status88.Active,
    FeeConfiguration = new List<SavingsPlanFeeConfigurationOnlyForInstrument>
    {
        new SavingsPlanFeeConfigurationOnlyForInstrument
        {
            Type = "type2",
            TransactionFeeModelId = new Guid("0000158e-0000-0000-0000-000000000000"),
        },
    },
};
```

